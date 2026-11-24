using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(250)]
[DisallowMultipleComponent]
public sealed class NimbAltarLink : MonoBehaviour
{
    private const float BreakSeconds = .55f;
    private const string MaterialResource = "NimbAltarLink";

    private readonly List<Thread> breaking = new List<Thread>();
    private readonly Stack<Thread> available = new Stack<Thread>();
    private Material material;
    private Thread active;

    private void Awake()
    {
        material = Resources.Load<Material>(MaterialResource);
        if (material == null) Debug.LogError("Nimb's altar thread material is missing.", this);
    }

    public void Focus(NimbAltar altar, NimbVulnerability boss)
    {
        if (material == null || altar == null || boss == null || !isActiveAndEnabled) return;
        if (active != null)
        {
            if (active.Altar == altar && active.Boss == boss) return;
            Release(active);
        }

        active = available.Count != 0 ? available.Pop() : new Thread(transform, material);
        active.Bind(altar, boss);
        active.Draw(-1f);
    }

    public void Break()
    {
        if (active == null) return;

        active.BrokenAt = Time.time;
        active.Draw(0f);
        breaking.Add(active);
        active = null;
    }

    public void Clear()
    {
        if (active != null)
        {
            Release(active);
            active = null;
        }

        foreach (Thread trace in breaking) Release(trace);
        breaking.Clear();
    }

    private void LateUpdate()
    {
        if (active != null && !active.Draw(-1f))
        {
            Release(active);
            active = null;
        }

        for (int i = breaking.Count - 1; i >= 0; i--)
        {
            Thread trace = breaking[i];
            float age = (Time.time - trace.BrokenAt) / BreakSeconds;
            if (age < 1f && trace.Draw(age)) continue;

            Release(trace);
            breaking.RemoveAt(i);
        }
    }

    private void OnDisable() => Clear();

    private void Release(Thread trace)
    {
        trace.Hide();
        available.Push(trace);
    }

    private sealed class Thread
    {
        private const int RoomSteps = 8;
        private const int GallerySteps = 20;
        private const int HallSteps = 24;
        private static readonly int DistanceStart = Shader.PropertyToID("_DistanceStart");
        private static readonly int DistanceLength = Shader.PropertyToID("_DistanceLength");
        private static readonly int TotalDistance = Shader.PropertyToID("_TotalDistance");
        private static readonly int BreakProgress = Shader.PropertyToID("_BreakProgress");

        private readonly LineRenderer chamber, arena;
        private readonly Vector3[] chamberPoints = new Vector3[RoomSteps + GallerySteps + 3];
        private readonly Vector3[] arenaPoints = new Vector3[HallSteps + 1];
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private Vector3 chamberFace, arenaFace, inward;
        private float chamberLength, hiddenLength;

        internal NimbAltar Altar { get; private set; }
        internal NimbVulnerability Boss { get; private set; }
        internal float BrokenAt { get; set; }

        internal Thread(Transform owner, Material material)
        {
            chamber = CreateLine(owner, material, "Nimb altar thread · chamber");
            arena = CreateLine(owner, material, "Nimb altar thread · arena");
        }

        internal void Bind(NimbAltar altar, NimbVulnerability boss)
        {
            Altar = altar;
            Boss = boss;
            chamberFace = altar.PillarChamberAnchor;
            arenaFace = altar.PillarAnchor;
            hiddenLength = Vector3.Distance(chamberFace, arenaFace);
            inward = Vector3.ProjectOnPlane(arenaFace - chamberFace, Vector3.up).normalized;

            Vector3 source = altar.CrystalAnchor;
            Vector3 doorway = altar.EntryAnchor;
            Vector3 insideDoor = doorway - inward * .48f;
            Vector3 outsideDoor = doorway + inward * .48f;

            // Follow the inside of the front wall from the alcove, then turn out
            // through the open doorway. Both controls stay on the chamber side.
            float distance = Vector3.Distance(source, insideDoor);
            Vector3 alongWall = Vector3.ProjectOnPlane(insideDoor - source, inward);
            Curve(chamberPoints, 0, RoomSteps, source, source + alongWall * .55f,
                insideDoor - inward * Mathf.Min(1.25f, distance * .33f), insideDoor);
            chamberPoints[RoomSteps + 1] = doorway;
            distance = Vector3.Distance(outsideDoor, chamberFace);
            Curve(chamberPoints, RoomSteps + 2, GallerySteps, outsideDoor,
                outsideDoor + inward * distance * .31f, chamberFace - inward * distance * .26f, chamberFace);

            chamberLength = Length(chamberPoints);
            chamber.positionCount = chamberPoints.Length;
            chamber.SetPositions(chamberPoints);
        }

        internal bool Draw(float fragment)
        {
            if (Altar == null || Boss == null || hiddenLength < .05f) return false;

            Vector3 target = Boss.TargetPoint;
            BuildArenaPath(target);
            float arenaLength = Length(arenaPoints);
            float totalLength = chamberLength + hiddenLength + arenaLength;
            SetTiming(chamber, 0f, chamberLength, totalLength, fragment);
            if (arenaLength < .02f)
            {
                arena.enabled = false;
                return true;
            }

            arena.positionCount = arenaPoints.Length;
            arena.SetPositions(arenaPoints);
            SetTiming(arena, chamberLength + hiddenLength, arenaLength, totalLength, fragment);
            return true;
        }

        internal void Hide()
        {
            chamber.enabled = false;
            arena.enabled = false;
            Altar = null;
            Boss = null;
        }

        private void BuildArenaPath(Vector3 target)
        {
            // Keep both controls on the open line's horizontal bearing. If Nimb
            // passes behind the pillar, opaque stone hides that part of the thread.
            float distance = Vector3.Distance(arenaFace, target);
            Vector3 sag = Vector3.down * Mathf.Min(distance * .027f, .35f);
            Curve(arenaPoints, 0, HallSteps, arenaFace,
                Vector3.Lerp(arenaFace, target, .29f) + sag,
                Vector3.Lerp(arenaFace, target, .72f) + sag, target);
        }

        private void SetTiming(LineRenderer line, float start, float length, float total, float fragment)
        {
            properties.SetFloat(DistanceStart, start);
            properties.SetFloat(DistanceLength, length);
            properties.SetFloat(TotalDistance, total);
            properties.SetFloat(BreakProgress, fragment);
            line.SetPropertyBlock(properties);
            line.enabled = true;
        }

        private static LineRenderer CreateLine(Transform owner, Material material, string name)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(owner, false);
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = .12f;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.enabled = false;
            return line;
        }

        private static void Curve(Vector3[] points, int start, int steps, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps;
                float rest = 1f - t;
                points[start + step] = rest * rest * rest * a + 3f * rest * rest * t * b +
                    3f * rest * t * t * c + t * t * t * d;
            }
        }

        private static float Length(Vector3[] points)
        {
            float length = 0f;
            for (int i = 1; i < points.Length; i++) length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }
    }
}
