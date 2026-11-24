using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
public sealed class NimbEncounter : MonoBehaviour
{
    internal const string ArenaScene = "Temple_Final_Room";
    private const int AltarCount = 6;
    private const float RuneLitSeconds = 30f;
    private const float RuneDarkSeconds = 8f;
    private const float VulnerabilitySeconds = 15f;

    private enum Phase { HuntingAltar, BetweenRunes, Vulnerable, Complete }

    private readonly System.Random random = new();
    private NimbAltar[] altars;
    internal System.Collections.Generic.IReadOnlyList<NimbAltar> Chambers => altars;
    private bool[] spent;
    private NimbVulnerability vulnerability;
    private NimbAltarLink altarLink;
    private Transform player;
    private CombatHealth playerHealth;
    private Phase phase;
    private float recoversAt;
    private float runeChangesAt;
    private int activeIndex = -1;

    public int ConfirmedAltars { get; private set; }
    public NimbAltar ActiveAltar => activeIndex >= 0 && phase != Phase.BetweenRunes ? altars[activeIndex] : null;
    public bool IsVulnerable => phase == Phase.Vulnerable;
    public bool IsComplete => phase == Phase.Complete;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnArenaLoaded;
        SceneManager.sceneLoaded += OnArenaLoaded;
    }

    private static void OnArenaLoaded(Scene scene, LoadSceneMode _)
    {
        if (scene.name != ArenaScene)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!root.TryGetComponent(out AngelSealAttack boss))
                continue;

            var encounter = new GameObject("Nimb encounter");
            SceneManager.MoveGameObjectToScene(encounter, scene);
            encounter.AddComponent<NimbEncounter>().Initialize(boss);
            return;
        }
    }

    private void Initialize(AngelSealAttack boss)
    {
        altars = NimbAltar.CreateStations(gameObject.scene);
        if (altars == null || altars.Length != AltarCount)
        {
            enabled = false;
            return;
        }

        spent = new bool[altars.Length];
        foreach (NimbAltar altar in altars)
        {
            altar.SetState(NimbAltarState.Waiting);
            altar.Broken += OnAltarBroken;
        }

        boss.BindEncounter(this);
        vulnerability = boss.gameObject.AddComponent<NimbVulnerability>();
        vulnerability.Initialize(boss);
        vulnerability.Hit += OnBossHit;
        altarLink = gameObject.AddComponent<NimbAltarLink>();
        FindPlayer();
        SelectAltar();
    }

    internal bool ContainsChamberPosition(Vector3 feet)
    {
        foreach (var altar in altars)
            if (altar.InsideChamber(feet)) return true;
        return false;
    }

    internal float LimitToChamberEntrance(Vector3 origin, Vector3 direction, float distance)
    {
        foreach (var altar in altars)
            distance = altar.LimitToChamberEntrance(origin, direction, distance);
        return distance;
    }

    private void FindPlayer()
    {
        player = null;
        playerHealth = null;

        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (!root.activeInHierarchy || !root.CompareTag("Player"))
                continue;

            player = root.transform;
            playerHealth = root.GetComponent<CombatHealth>();
            break;
        }
    }

    private void Update()
    {
        if (phase == Phase.Complete)
            return;

        // Story entry replaces the arena's standalone Ivan after the additive load.
        if (player == null || !player.gameObject.activeInHierarchy)
            FindPlayer();

        switch (phase)
        {
            case Phase.HuntingAltar when Time.time >= runeChangesAt:
                ActiveAltar.SetState(NimbAltarState.Waiting);
                altarLink.Clear();
                BeginDarkInterval();
                break;
            case Phase.BetweenRunes when Time.time >= runeChangesAt:
                SelectAltar();
                break;
            case Phase.Vulnerable when Time.time >= recoversAt:
                vulnerability.SetStunned(false);
                ActivateAltar();
                break;
        }
    }

    private void SelectAltar()
    {
        int remaining = altars.Length - ConfirmedAltars;
        // After a missed rune, give another chamber a turn if one remains.
        int exclude = remaining > 1 ? activeIndex : -1;
        int choice = random.Next(remaining - (exclude >= 0 ? 1 : 0));
        for (int index = 0; index < spent.Length; index++)
        {
            if (spent[index] || index == exclude)
                continue;
            if (choice-- != 0)
                continue;

            activeIndex = index;
            ActivateAltar();
            break;
        }
    }

    private void ActivateAltar()
    {
        phase = Phase.HuntingAltar;
        runeChangesAt = Time.time + RuneLitSeconds;
        NimbAltar altar = ActiveAltar;
        altar.SetActiveDeadline(runeChangesAt);
        altar.SetState(NimbAltarState.Active);
        altarLink.Focus(altar, vulnerability);
        RefreshObjective();
    }

    private void BeginDarkInterval()
    {
        phase = Phase.BetweenRunes;
        runeChangesAt = Time.time + RuneDarkSeconds;
        RefreshObjective();
    }

    private void OnAltarBroken(NimbAltar broken)
    {
        if (phase != Phase.HuntingAltar || broken != ActiveAltar || !PlayerCanFight())
            return;

        phase = Phase.Vulnerable;
        recoversAt = Time.time + VulnerabilitySeconds;
        altarLink.Break();
        vulnerability.SetStunned(true);
        RefreshObjective();
        if (ConfirmedAltars == 0)
            StoryGameplayVoice.FromPlayer(broken, player.gameObject, "boss.first_altar", ArenaScene);
        else if (ConfirmedAltars == AltarCount - 1)
            StoryGameplayVoice.FromPlayer(broken, player.gameObject, "boss.last_altar", ArenaScene);
    }

    private void OnBossHit()
    {
        if (phase != Phase.Vulnerable || !PlayerCanFight())
            return;

        spent[activeIndex] = true;
        ConfirmedAltars++;
        ActiveAltar.SetState(NimbAltarState.Spent);
        activeIndex = -1;
        if (ConfirmedAltars == altars.Length)
        {
            phase = Phase.Complete;
            altarLink.Clear();
            vulnerability.Finish();
            RefreshObjective();
            if (!GameCheckpoint.TryFinishRitual(gameObject.scene))
                gameObject.AddComponent<NimbVictoryComic>().Begin();
            return;
        }

        vulnerability.SetStunned(false);
        BeginDarkInterval();
    }

    private bool PlayerCanFight() => player != null && player.gameObject.activeInHierarchy &&
        (playerHealth == null || !playerHealth.IsDead);

    private void RefreshObjective()
    {
        if (gameObject.scene == SceneManager.GetActiveScene())
            GameObjectives.SetNimbProgress(ConfirmedAltars, IsVulnerable);
    }

    private void OnDestroy()
    {
        if (altars != null)
            foreach (NimbAltar altar in altars)
                if (altar != null)
                    altar.Broken -= OnAltarBroken;

        if (vulnerability != null)
            vulnerability.Hit -= OnBossHit;
    }
}
