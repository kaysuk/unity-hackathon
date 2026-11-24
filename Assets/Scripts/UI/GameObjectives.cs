using System;
using UnityEngine;

public static class GameObjectives
{
    public enum Stage
    {
        None = 0,
        GetReadyForWork = 101,
        MeetPartner = 102,
        FindBag = 103,
        ReturnKeys = 104,
        TalkToPartner = 105,
        SetTable = 106,
        FindFather = 201,
        PrepareMissingPersonDetails = 202,
        CompareAnswers = 203,
        AskDmitry = 204,
        InspectLetter = 20401,
        FollowDmitry = 301,
        InspectChurch = 302,
        CheckMarks = 303,
        InvestigateVoices = 401, // Scene 402 keeps this objective.
        EscapeAndCallForHelp = 404,
        InspectDmitrysDesk = 504,
        FindAnotherEntrance = 601,
        ReachBasementUndetected = 602,
        OpenLowerPassage = 603,
        FindWayToMainHall = 604,
        ReachRitualHall = 605,
        EnterHall = 606,
        DestroyAltars = 703
    }

    [Serializable]
    public struct State
    {
        public Stage Stage;
        public int AltarsDestroyed;
        public bool DocumentsCollected;
        public bool NimbVulnerable;
    }

    private const int AltarCount = 6;
    private static State current;

    public static event Action Changed;

    public static string Primary => current.Stage switch
    {
        Stage.GetReadyForWork => "Собраться на службу",
        Stage.MeetPartner => "Встретиться с напарницей",
        Stage.FindBag => "Найти сумку продавца",
        Stage.ReturnKeys => "Вернуть связку владельцу",
        Stage.TalkToPartner => "Поговорить с напарницей",
        Stage.SetTable => "Помочь отцу накрыть на стол",
        Stage.FindFather => "Найти отца",
        Stage.PrepareMissingPersonDetails => "Подготовить сведения о пропавшем",
        Stage.CompareAnswers => "Сверить полученные ответы",
        Stage.AskDmitry => "Попросить Дмитрия о помощи",
        Stage.InspectLetter => "Осмотреть письмо",
        Stage.FollowDmitry => "Проследить за Дмитрием",
        Stage.InspectChurch => "Осмотреть церковь",
        Stage.CheckMarks => "Найти потайной проход за шкафом",
        Stage.InvestigateVoices => "Выяснить источник голосов",
        Stage.EscapeAndCallForHelp => "Выбраться наружу и вызвать помощь",
        Stage.InspectDmitrysDesk => "Осмотреть стол Дмитрия",
        Stage.FindAnotherEntrance => "Найти другой вход",
        Stage.ReachBasementUndetected => "Добраться до подвала незамеченным",
        Stage.OpenLowerPassage => "Открыть нижний проход",
        Stage.FindWayToMainHall => "Найти путь к главному залу",
        Stage.ReachRitualHall => "Добраться до ритуального зала",
        Stage.EnterHall => "Войти в зал",
        Stage.DestroyAltars when current.AltarsDestroyed >= AltarCount => "Нимб повержен",
        Stage.DestroyAltars when current.NimbVulnerable => "Ранить Нимба",
        Stage.DestroyAltars => $"Уничтожить алтари {current.AltarsDestroyed} из {AltarCount}",
        _ => string.Empty
    };

    public static string Secondary => current.Stage switch
    {
        Stage.ReachRitualHall or Stage.EnterHall when !current.DocumentsCollected => "Собрать тайные документы",
        Stage.DestroyAltars when current.AltarsDestroyed < AltarCount => $"Удар рукоятью · {GameBindings.Label("Player/Melee")} / {GameBindings.Label("Player/Melee", true)}",
        _ => string.Empty
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        current = default;
        Changed = null;
    }

    public static void ConfigureScene(string sceneName) => Apply(new State
    {
        Stage = sceneName switch
        {
            "temple_v2" => Stage.FindWayToMainHall,
            NimbEncounter.ArenaScene => Stage.DestroyAltars,
            _ => Stage.None
        }
    });

    public static void EnterStage(Stage nextStage)
    {
        if (current.Stage == nextStage)
            return;

        State next = current;
        next.Stage = nextStage;
        next.AltarsDestroyed = 0;
        next.NimbVulnerable = false;
        Apply(next);
    }

    public static void ReachArchive()
    {
        if (current.Stage == Stage.FindWayToMainHall)
            EnterStage(Stage.ReachRitualHall);
    }

    public static void ReachRitualEntrance()
    {
        if (current.Stage is Stage.FindWayToMainHall or Stage.ReachRitualHall)
            EnterStage(Stage.EnterHall);
    }

    public static void SetArchiveProgress(int documentCount)
    {
        if (current.Stage is not (Stage.FindWayToMainHall or Stage.ReachRitualHall or Stage.EnterHall))
            return;

        State next = current;
        if (next.Stage == Stage.FindWayToMainHall)
            next.Stage = Stage.ReachRitualHall;
        next.DocumentsCollected = documentCount >= TempleArchiveJournal.DocumentTotal;
        Apply(next);
    }

    internal static void SetNimbProgress(int confirmedAltars, bool vulnerable)
    {
        if (current.Stage != Stage.DestroyAltars)
            return;

        State next = current;
        next.AltarsDestroyed = Mathf.Clamp(confirmedAltars, 0, AltarCount);
        next.NimbVulnerable = vulnerable && next.AltarsDestroyed < AltarCount;
        Apply(next);
    }

    public static State Capture() => current;

    public static void Restore(State state)
    {
        if (!Enum.IsDefined(typeof(Stage), state.Stage))
            state.Stage = Stage.None;
        state.AltarsDestroyed = state.Stage == Stage.DestroyAltars
            ? Mathf.Clamp(state.AltarsDestroyed, 0, AltarCount)
            : 0;
        state.NimbVulnerable &= state.Stage == Stage.DestroyAltars && state.AltarsDestroyed < AltarCount;
        Apply(state);
    }

    private static void Apply(State next)
    {
        if (current.Stage == next.Stage && current.AltarsDestroyed == next.AltarsDestroyed &&
            current.DocumentsCollected == next.DocumentsCollected && current.NimbVulnerable == next.NimbVulnerable)
            return;

        current = next;
        Changed?.Invoke();
    }
}
