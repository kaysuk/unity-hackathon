using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum NimbAltarState { Waiting, Active, Broken, Spent }

[DisallowMultipleComponent]
public sealed class NimbAltar : MonoBehaviour, IDamageable, IPistolStrikeTarget
{
    private const float GateLift = 2.5f;
    private const float GateSpeed = 5f;
    private const float WarningSeconds = 5f;
    private const float WarningFadeInSeconds = .55f;
    private const float PulseSpeed = 6f;
    private const float WarningPulseSpeed = 13.5f;
    private const float RuneDissolveSeconds = .62f;
    private const float RuneRestoreSeconds = .46f;
    private const float GrilleWidth = 2.035f;
    internal const float SectorSlope = .57735f;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int RuneMap = Shader.PropertyToID("_RuneMap");
    private static readonly int RuneMapTransform = Shader.PropertyToID("_RuneMap_ST");
    private static readonly int RuneElevation = Shader.PropertyToID("_RuneElevation");
    private static readonly int RuneCut = Shader.PropertyToID("_RuneCut");
    private static readonly int RuneFlash = Shader.PropertyToID("_RuneFlash");
    private static readonly Color RuneGold = new Color(1f, .77f, .34f);
    private static readonly Color RuneGlow = new Color(3f, 1.1f, .24f);
    private static readonly Color CrystalGlow = new Color(2.4f, .16f, .23f);
    private static readonly string[] AltarNames =
        { "Alpha", "Beta", "Delta", "Sigma", "Psi", "Omega" };

    private MeshRenderer[] runes;
    private RuneSurface[] originalRunes;
    private Material runeDissolve;
    private float runeProgress;
    private MeshRenderer crystal;
    private Light crystalLight;
    private Light entranceLight;
    private SphereCollider hitTarget;
    private Transform geometry;
    private Transform gate;
    private BoxCollider gateCollider;
    private Vector3 gateLow;
    private Transform exitingPlayer;
    private float gateHeight;
    private int gateDirection;
    private float chamberFront, chamberBack, chamberCeiling;
    private float activeDeadline = float.PositiveInfinity;
    private float warningPhase;
    private MaterialPropertyBlock properties;
    private Color crystalColor;
    private float crystalLightIntensity;
    private float entranceLightIntensity;

    public NimbAltarState State { get; private set; }
    public event Action<NimbAltar> Broken;

    internal Matrix4x4 ChamberWorldToLocal => geometry.worldToLocalMatrix;
    internal Vector4 ChamberLimits => new(chamberFront, chamberBack, ChamberFloor, chamberCeiling);
    private float ChamberFloor => gateLow.y - .12f;

    internal Vector3 CrystalAnchor => transform.position;
    internal Vector3 EntryAnchor => geometry.TransformPoint(gateLow + Vector3.up * 1.10f);
    internal Vector3 PillarAnchor { get; private set; }
    internal Vector3 PillarChamberAnchor { get; private set; }

    private bool RunesErased => State is NimbAltarState.Broken or NimbAltarState.Spent;

    public static NimbAltar[] CreateStations(Scene scene)
    {
        Transform temple = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Temple Final")
                temple = root.transform;

        Transform chambers = temple != null
            ? temple.Find("Sanctuary of the Six Seals/Six votive chambers") : null;
        Transform grille = temple != null ? temple.Find("Chamber 02/Fixed gallery grille") : null;
        if (chambers == null || grille == null ||
            !grille.TryGetComponent(out MeshFilter grilleMesh) || grilleMesh.sharedMesh == null ||
            !grille.TryGetComponent(out MeshRenderer grilleRenderer) ||
            !grille.TryGetComponent(out BoxCollider grilleCollider))
            return MissingStation("Temple Final, Six votive chambers and a gallery grille");

        var bindings = new StationBinding[AltarNames.Length];
        var existing = new NimbAltar[bindings.Length];
        int existingCount = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            string number = (i + 1).ToString("00");
            bindings[i] = BindStation(temple, chambers, number, AltarNames[i]);
            if (bindings[i] == null)
                return MissingStation($"Chamber {number}, its matching rune meshes, altar and entrance");
            existing[i] = bindings[i].Crystal.GetComponent<NimbAltar>();
            if (existing[i] != null)
                existingCount++;
        }

        if (existingCount == existing.Length)
            return existing;
        if (existingCount != 0)
            return MissingStation("a complete set of altar stations");

        Material dissolve = Resources.Load<Material>("NimbRuneDissolve");
        if (dissolve == null)
            Debug.LogWarning("The Nimb rune dissolve material is missing.");

        var stations = new NimbAltar[bindings.Length];
        for (int i = 0; i < stations.Length; i++)
        {
            StationBinding binding = bindings[i];
            // The existing fixed grilles close every other side passage between chambers.
            if (binding.Geometry.Find("Fixed gallery grille") == null)
            {
                Vector3 position = binding.SideLintel.localPosition;
                position.y = grille.localPosition.y;
                CreateGrille(binding.Geometry, "Closed side passage", position, grille.localRotation,
                    Vector3.one, grilleMesh, grilleRenderer, grilleCollider);
            }

            var doorPosition = binding.EntryLintel.localPosition;
            doorPosition.y = grille.localPosition.y;
            var doorScale = new Vector3((binding.EntryLintel.localScale.x + .04f) / GrilleWidth, 1f, 1f);
            BoxCollider door = CreateGrille(binding.Geometry, "Altar gate", doorPosition,
                Quaternion.identity, doorScale, grilleMesh, grilleRenderer, grilleCollider);

            MeshRenderer entryRune = CreateEntryRune(binding);
            var altar = binding.Crystal.gameObject.AddComponent<NimbAltar>();
            altar.Initialize(binding, entryRune, door, dissolve);
            stations[i] = altar;
        }
        return stations;
    }

    private sealed class StationBinding
    {
        public Transform Geometry;
        public Transform EntryLintel;
        public Transform RearWall;
        public Transform SideLintel;
        public MeshRenderer[] PillarRunes;
        public MeshFilter RuneMesh;
        public MeshRenderer OriginalEntryRune;
        public Light EntranceLight;
        public MeshRenderer Inscription;
        public MeshRenderer Crystal;
        public Light CrystalLight;
    }

    private static StationBinding BindStation(Transform temple, Transform chambers, string number, string name)
    {
        Transform geometry = temple.Find($"Chamber {number}");
        Transform art = chambers.Find($"Votive chamber {number}");
        if (geometry == null || art == null)
            return null;

        Transform pillar = geometry.Find($"Hall column/Pillar runes {number}");
        Transform altar = art.Find($"Votive altar - {name}");
        Transform entry = art.Find($"Entrance seal {number}/Engraved rune");
        if (pillar == null || altar == null || entry == null)
            return null;

        var binding = new StationBinding
        {
            Geometry = geometry,
            EntryLintel = geometry.Find("Entrance lintel"),
            RearWall = geometry.Find("Rear wall infill"),
            SideLintel = geometry.Find("Grate opening lintel"),
            PillarRunes = pillar.GetComponentsInChildren<MeshRenderer>(),
            RuneMesh = pillar.GetComponentInChildren<MeshFilter>(),
            OriginalEntryRune = entry.GetComponent<MeshRenderer>(),
            EntranceLight = art.Find("Light on carved seal")?.GetComponent<Light>(),
            Inscription = altar.Find("Inscription")?.GetComponent<MeshRenderer>(),
            Crystal = altar.Find("Garnet")?.GetComponent<MeshRenderer>(),
            CrystalLight = altar.Find("Reflected garnet light")?.GetComponent<Light>()
        };
        MeshFilter inscriptionMesh = binding.Inscription != null
            ? binding.Inscription.GetComponent<MeshFilter>() : null;
        return binding.EntryLintel != null && binding.RearWall != null && binding.SideLintel != null &&
            binding.PillarRunes.Length == 4 && binding.RuneMesh != null && binding.RuneMesh.sharedMesh != null &&
            inscriptionMesh != null && inscriptionMesh.sharedMesh != null &&
            inscriptionMesh.sharedMesh.name == binding.RuneMesh.sharedMesh.name &&
            binding.OriginalEntryRune != null && binding.EntranceLight != null &&
            binding.Crystal != null && binding.Crystal.sharedMaterial != null && binding.CrystalLight != null
                ? binding : null;
    }

    private static NimbAltar[] MissingStation(string binding)
    {
        Debug.LogError($"Temple_Final_Room cannot start the Nimb encounter. Missing scene binding for {binding}.");
        return Array.Empty<NimbAltar>();
    }

    private static BoxCollider CreateGrille(Transform sector, string name, Vector3 position, Quaternion rotation,
        Vector3 scale, MeshFilter mesh, MeshRenderer renderer, BoxCollider template)
    {
        var grille = new GameObject(name);
        grille.transform.SetParent(sector, false);
        grille.transform.SetLocalPositionAndRotation(position, rotation);
        grille.transform.localScale = scale;
        grille.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
        grille.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        var blocker = grille.AddComponent<BoxCollider>();
        blocker.size = template.size;
        blocker.center = template.center;
        return blocker;
    }

    private static MeshRenderer CreateEntryRune(StationBinding binding)
    {
        var rune = new GameObject("Matching pillar rune");
        Transform original = binding.OriginalEntryRune.transform;
        rune.transform.SetParent(original.parent, false);
        rune.transform.SetLocalPositionAndRotation(original.localPosition + Vector3.back * .075f,
            original.localRotation);
        rune.transform.localScale = Vector3.one * 1.12f;
        rune.AddComponent<MeshFilter>().sharedMesh = binding.RuneMesh.sharedMesh;
        var renderer = rune.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = binding.PillarRunes[0].sharedMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        binding.OriginalEntryRune.enabled = false;
        return renderer;
    }

    private void Initialize(StationBinding binding, MeshRenderer entryRune, BoxCollider door, Material dissolve)
    {
        var glyphs = new List<MeshRenderer>(binding.PillarRunes) { entryRune, binding.Inscription };
        runes = glyphs.ToArray();
        originalRunes = new RuneSurface[runes.Length];
        for (int i = 0; i < runes.Length; i++)
            originalRunes[i] = new RuneSurface(runes[i]);
        runeDissolve = dissolve;
        properties = new MaterialPropertyBlock();
        crystal = binding.Crystal;
        crystalLight = binding.CrystalLight;
        entranceLight = binding.EntranceLight;
        crystalColor = crystal.sharedMaterial.GetColor(BaseColor);
        crystalLightIntensity = crystalLight.intensity;
        entranceLightIntensity = entranceLight.intensity;
        geometry = binding.Geometry;
        gateCollider = door;
        gate = door.transform;
        gateLow = gate.localPosition;
        chamberFront = binding.EntryLintel.localPosition.z + binding.EntryLintel.localScale.z * .5f;
        chamberBack = binding.RearWall.localPosition.z - binding.RearWall.localScale.z * .5f;
        chamberCeiling = binding.EntryLintel.localPosition.y + binding.EntryLintel.localScale.y * .5f;
        PillarAnchor = PillarFaceAnchor(binding.PillarRunes, -geometry.forward);
        PillarChamberAnchor = PillarFaceAnchor(binding.PillarRunes, geometry.forward);

        hitTarget = gameObject.AddComponent<SphereCollider>();
        hitTarget.radius = .29f;
        hitTarget.enabled = false;
        RefreshVisuals();
    }

    private static Vector3 PillarFaceAnchor(MeshRenderer[] faces, Vector3 outward)
    {
        MeshRenderer outerFace = faces[0];
        foreach (MeshRenderer face in faces)
            if (Vector3.Dot(face.bounds.center, outward) > Vector3.Dot(outerFace.bounds.center, outward))
                outerFace = face;

        Transform pose = outerFace.transform;
        Bounds meshBounds = outerFace.GetComponent<MeshFilter>().sharedMesh.bounds;
        Vector3 anchor = meshBounds.center;
        anchor.z = Vector3.Dot(pose.forward, outward) > 0f ? meshBounds.max.z : meshBounds.min.z;
        return pose.TransformPoint(anchor) + .06f * outward;
    }

    internal void SetActiveDeadline(float deadline) => activeDeadline = deadline;

    public void SetState(NimbAltarState state)
    {
        if (State == state)
            return;

        State = state;
        warningPhase = 0f;
        if (state != NimbAltarState.Active)
            activeDeadline = float.PositiveInfinity;
        hitTarget.enabled = state == NimbAltarState.Active;
        if (state is NimbAltarState.Waiting or NimbAltarState.Spent)
            exitingPlayer = FindPlayer();
        RefreshVisuals();
    }

    public void TakeDamage(float amount, Vector3 point, Vector3 direction, GameObject source)
    {
        if (amount > 0f && float.IsFinite(amount) && source != null && source.TryGetComponent<PistolCombat>(out _))
            TryBreak(source, SoundCue.AltarHit);
    }

    public bool TryPistolStrike(GameObject source) => TryBreak(source, SoundCue.Stone);

    private bool TryBreak(GameObject source, SoundCue impact)
    {
        if (State != NimbAltarState.Active || source == null || source.scene != gameObject.scene ||
            !source.CompareTag("Player") || !InsideChamber(source.transform.position))
            return false;

        SetState(NimbAltarState.Broken);
        GameAudio.Play(impact, CrystalAnchor, .36f);
        GameAudio.Play(SoundCue.AltarBreak, CrystalAnchor);
        Broken?.Invoke(this);
        return true;
    }

    internal bool InsideChamber(Vector3 feet)
    {
        // Test Ivan's feet against this wedge of the upper floor. The shoulder camera
        // and muzzle can cross the doorway while Ivan is still on the landing.
        Vector3 local = geometry.InverseTransformPoint(feet);
        return local.z > chamberFront && local.z < chamberBack && WithinChamberSidesAndHeight(local);
    }

    private bool WithinChamberSidesAndHeight(Vector3 local) =>
        Mathf.Abs(local.x) < local.z * SectorSlope && local.y >= ChamberFloor && local.y < chamberCeiling;

    internal float LimitToChamberEntrance(Vector3 origin, Vector3 direction, float distance)
    {
        // direction has unit world length. The ray parameter stays in metres after this transform.
        Vector3 heading = geometry.InverseTransformVector(direction);
        if (heading.z <= 0f) return distance;
        Vector3 start = geometry.InverseTransformPoint(origin);
        float crossing = (chamberFront - start.z) / heading.z;
        if (crossing < 0f || crossing >= distance) return distance;
        return WithinChamberSidesAndHeight(start + heading * crossing) ? crossing : distance;
    }

    private void LateUpdate()
    {
        bool holdingExit = PlayerStillInside();
        bool open = State is NimbAltarState.Active or NimbAltarState.Broken || holdingExit;
        float targetHeight = open ? GateLift : 0f;
        float height = Mathf.MoveTowards(gateHeight, targetHeight, GateSpeed * Time.deltaTime);
        if (height != gateHeight)
        {
            int direction = height > gateHeight ? 1 : -1;
            bool audible = gameObject.scene == SceneManager.GetActiveScene();
            if (audible && direction != gateDirection)
                GameAudio.Play(open ? SoundCue.Gate : SoundCue.GateClose, gate.position, .42f);
            gateDirection = direction;
            gateHeight = height;
            gate.localPosition = gateLow + Vector3.up * height;
            if (height == targetHeight)
            {
                gateDirection = 0;
                if (audible) GameAudio.Play(SoundCue.GateStop, gate.position, .35f);
            }
        }
        bool blocked = height < GateLift && !holdingExit;
        if (gateCollider.enabled != blocked)
            gateCollider.enabled = blocked;

        float runeTarget = RunesErased ? 1f : 0f;
        bool runeChanging = runeProgress != runeTarget;
        if (runeChanging)
        {
            // The victory comic pauses gameplay, but the last burn should still complete.
            float elapsed = State == NimbAltarState.Spent && Time.timeScale == 0f
                ? Time.unscaledDeltaTime : Time.deltaTime;
            float duration = RunesErased ? RuneDissolveSeconds : RuneRestoreSeconds;
            runeProgress = Mathf.MoveTowards(runeProgress, runeTarget, elapsed / duration);
        }

        if (State == NimbAltarState.Active)
        {
            float elapsed = Time.time - (activeDeadline - WarningSeconds);
            float warning = Mathf.SmoothStep(0f, 1f, elapsed / WarningFadeInSeconds);
            warningPhase += (WarningPulseSpeed - PulseSpeed) * warning * Time.deltaTime;
            RefreshVisuals(warning);
        }
        else if (runeChanging)
            RefreshVisuals();
    }

    private Transform FindPlayer()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            if (root.activeInHierarchy && root.CompareTag("Player"))
                return root.transform;
        return null;
    }

    private bool PlayerStillInside()
    {
        if (exitingPlayer == null)
            return false;

        Vector3 position = geometry.InverseTransformPoint(exitingPlayer.position);
        if (position.y > 3.1f && position.z > gateLow.z - .8f &&
            position.z < 28.5f && Mathf.Abs(position.x) < position.z * SectorSlope + .4f)
            return true;

        exitingPlayer = null;
        return false;
    }

    private void RefreshVisuals(float warning = 0f)
    {
        bool active = State == NimbAltarState.Active;
        bool intact = State is NimbAltarState.Active or NimbAltarState.Waiting;
        float phase = Time.time * PulseSpeed;
        float pulse = active ? .63f + .37f * Mathf.Sin(phase) : 0f;
        float runePulse = active ? .63f + .37f * Mathf.Sin(phase + warningPhase) : 0f;
        // Raise the low part of the pulse without brightening beyond its existing crest.
        runePulse += warning * .33f * (1f - runePulse);
        float burnFlash = RunesErased ? 1f - Mathf.SmoothStep(0f, 1f, runeProgress / .20f) : 0f;
        Color glow = active ? RuneGlow * (.09f + .82f * runePulse) : Color.black;
        RefreshRunes(glow, burnFlash);

        crystal.enabled = intact;
        crystalLight.enabled = intact || burnFlash > 0f;
        crystalLight.intensity = crystalLightIntensity *
            ((intact ? .3f + 2f * pulse : 0f) + 3.8f * burnFlash);
        entranceLight.intensity = entranceLightIntensity *
            ((intact ? .3f + 1.7f * runePulse : .10f) + 1.9f * burnFlash);
        if (intact)
            Tint(crystal, crystalColor, CrystalGlow * (.05f + pulse));
    }

    private void RefreshRunes(Color glow, float burnFlash)
    {
        bool changing = runeDissolve != null && (RunesErased ? runeProgress < 1f : runeProgress > 0f);
        float cut = Mathf.Clamp01((runeProgress - .13f) / .87f);
        for (int i = 0; i < runes.Length; i++)
        {
            MeshRenderer rune = runes[i];
            RuneSurface surface = originalRunes[i];
            rune.enabled = changing || !RunesErased;
            Material material = changing ? runeDissolve : surface.Material;
            if (rune.sharedMaterial != material)
                rune.sharedMaterial = material;
            if (changing)
            {
                properties.Clear();
                properties.SetTexture(RuneMap, surface.Texture);
                properties.SetVector(RuneMapTransform, surface.MapTransform);
                properties.SetVector(RuneElevation, surface.Elevation);
                properties.SetFloat(RuneCut, cut);
                properties.SetFloat(RuneFlash, burnFlash);
                rune.SetPropertyBlock(properties);
            }
            else if (!RunesErased)
                Tint(rune, RuneGold, glow);
        }
    }

    private readonly struct RuneSurface
    {
        internal readonly Material Material;
        internal readonly Texture Texture;
        internal readonly Vector4 MapTransform;
        internal readonly Vector4 Elevation;

        internal RuneSurface(MeshRenderer renderer)
        {
            Material = renderer.sharedMaterial;
            Texture = Material.GetTexture(BaseMap) ?? Texture2D.whiteTexture;
            Vector2 tiling = Material.GetTextureScale(BaseMap);
            Vector2 offset = Material.GetTextureOffset(BaseMap);
            MapTransform = new Vector4(tiling.x, tiling.y, offset.x, offset.y);
            Bounds bounds = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
            Elevation = new Vector4(bounds.min.y, 1f / Mathf.Max(.0001f, bounds.size.y),
                bounds.min.x, 1f / Mathf.Max(.0001f, bounds.size.x));
        }
    }

    private void Tint(MeshRenderer renderer, Color tint, Color glow)
    {
        properties.Clear();
        properties.SetColor(BaseColor, tint);
        properties.SetColor(Emission, glow);
        renderer.SetPropertyBlock(properties);
    }
}
