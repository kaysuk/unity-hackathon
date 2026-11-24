using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using RebindingOperation = UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation;

public static class GameBindings
{
    private const string Preference = "Controls.KeyboardMouse.Overrides.v1";
    private const string Gameplay = "Действия";
    private const string Movement = "Передвижение";
    private const string Overlay = "Журнал и загадки";

    public sealed class Entry
    {
        internal readonly InputAction Action;
        internal readonly int Index;
        public string Label { get; }
        public string Section { get; }
        public string DisplayName => Format(Action.bindings[Index].effectivePath);

        internal Entry(InputAction action, int index, string label, string section)
        {
            Action = action;
            Index = index;
            Label = label;
            Section = section;
        }
    }

    private static InputActionAsset asset;
    private static readonly List<Entry> entries = new();
    private static RebindingOperation capture;

    public static event Action Changed;
    public static bool IsCapturing => capture != null;
    public static IReadOnlyList<Entry> Entries
    {
        get
        {
            EnsureInitialized();
            return entries;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        capture?.Dispose();
        capture = null;
        asset = null;
        entries.Clear();
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInitialized()
    {
        var current = InputSystem.actions;
        if (current == asset) return;
        asset = current;
        entries.Clear();
        if (asset == null) return;

        Add("Player/Aim", "Прицелиться", Gameplay);
        Add("Player/Attack", "Выстрелить", Gameplay);
        Add("Player/Reload", "Перезарядить", Gameplay);
        Add("Player/Melee", "Удар рукоятью", Gameplay);
        Add("Player/Takedown", "Захват со спины", Gameplay);
        Add("Player/Next", "Достать пистолет", Gameplay);
        Add("Player/Previous", "Убрать пистолет", Gameplay);
        Add("Player/Interact", "Взаимодействовать", Gameplay);
        Add("Player/Move", "Вперёд", Movement, "up");
        Add("Player/Move", "Назад", Movement, "down");
        Add("Player/Move", "Влево", Movement, "left");
        Add("Player/Move", "Вправо", Movement, "right");
        Add("Player/Sprint", "Бег", Movement);
        Add("Player/Crouch", "Присесть", Movement);
        Add("Player/Journal", "Открыть журнал", Overlay);
        Add("Overlay/Left", "Листать влево", Overlay);
        Add("Overlay/Right", "Листать вправо", Overlay);
        Add("Overlay/Up", "Прокрутить вверх", Overlay);
        Add("Overlay/Down", "Прокрутить вниз", Overlay);
        Add("Overlay/Accept", "Подтвердить", Overlay);
        if (Application.isPlaying) LoadSaved();
    }

    private static void Add(string path, string label, string section, string part = null)
    {
        var action = asset.FindAction(path, true);
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (!IsDesktop(binding.path) || (part != null && binding.name != part)) continue;
            entries.Add(new Entry(action, i, label, section));
        }
    }

    private static bool IsDesktop(string path) =>
        path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase);

    public static string Label(string actionPath, bool gamepad = false)
    {
        EnsureInitialized();
        var action = asset?.FindAction(actionPath);
        if (action != null)
            foreach (var binding in action.bindings)
            {
                if (binding.isComposite || string.IsNullOrEmpty(binding.effectivePath)) continue;
                if (gamepad ? binding.path.StartsWith("<Gamepad>/", StringComparison.OrdinalIgnoreCase) : IsDesktop(binding.path))
                    return Format(binding.effectivePath);
            }
        return "—";
    }

    private static string Format(string path)
    {
        return path switch
        {
            "<Mouse>/leftButton" => "ЛКМ",
            "<Mouse>/rightButton" => "ПКМ",
            "<Mouse>/middleButton" => "СКМ",
            "<Keyboard>/upArrow" => "↑",
            "<Keyboard>/downArrow" => "↓",
            "<Keyboard>/leftArrow" => "←",
            "<Keyboard>/rightArrow" => "→",
            "<Keyboard>/escape" => "Esc",
            "<Keyboard>/enter" => "Enter",
            "<Keyboard>/numpadEnter" => "Num Enter",
            "<Keyboard>/space" => "Пробел",
            "<Keyboard>/leftShift" => "Левый Shift",
            "<Keyboard>/rightShift" => "Правый Shift",
            "<Keyboard>/leftAlt" => "Левый Option / Alt",
            "<Keyboard>/rightAlt" => "Правый Option / Alt",
            "<Keyboard>/leftMeta" => "Левый Cmd / Win",
            "<Keyboard>/rightMeta" => "Правый Cmd / Win",
            "<Gamepad>/rightStickPress" => "R3",
            "<Gamepad>/leftStickPress" => "L3",
            "<Gamepad>/leftTrigger" => "LT",
            "<Gamepad>/rightShoulder" => "RB",
            "<Gamepad>/buttonSouth" => "A / ×",
            "<Gamepad>/buttonEast" => "B / ○",
            "<Gamepad>/buttonWest" => "X / □",
            "<Gamepad>/buttonNorth" => "Y / △",
            "<Gamepad>/dpad/left" => "←",
            "<Gamepad>/dpad/right" => "→",
            "<Gamepad>/dpad/up" => "↑",
            "<Gamepad>/dpad/down" => "↓",
            _ when path.StartsWith("<Keyboard>/") && path.Length == "<Keyboard>/a".Length =>
                path.Substring("<Keyboard>/".Length).ToUpperInvariant(),
            _ => InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice)
        };
    }

    public static void StartCapture(Entry entry, Action<string> finished)
    {
        CancelCapture();
        EnsureInitialized();
        if (!entries.Contains(entry))
        {
            finished?.Invoke("Действие недоступно.");
            return;
        }

        string result = null;
        capture = new RebindingOperation().WithExpectedControlType<ButtonControl>()
            .WithControlsHavingToMatchPath("<Keyboard>/*")
            .WithControlsHavingToMatchPath("<Mouse>/leftButton")
            .WithControlsHavingToMatchPath("<Mouse>/rightButton")
            .WithControlsHavingToMatchPath("<Mouse>/middleButton")
            .WithControlsHavingToMatchPath("<Mouse>/backButton")
            .WithControlsHavingToMatchPath("<Mouse>/forwardButton")
            .WithControlsExcluding("<Keyboard>/anyKey")
            .WithControlsExcluding("<Keyboard>/imeSelected")
            .WithControlsExcluding("<Keyboard>/shift")
            .WithControlsExcluding("<Keyboard>/ctrl")
            .WithControlsExcluding("<Keyboard>/alt")
            .WithCancelingThrough("<Keyboard>/escape")
            .WithMatchingEventsBeingSuppressed()
            .OnApplyBinding((operation, _) =>
            {
                var control = operation.selectedControl;
                // Record physical keys, including the side of a modifier, across OS keyboard layouts.
                if (control is KeyControl || control.device is Mouse)
                {
                    string device = control.device is Mouse ? "<Mouse>/" : "<Keyboard>/";
                    result = Apply(entry, device + control.name);
                }
                else result = "Выберите клавишу или кнопку мыши.";
            })
            .OnCancel(op => FinishCapture(op, finished, "Назначение отменено."))
            .OnComplete(op => FinishCapture(op, finished, result));
        capture.Start();
    }

    private static void FinishCapture(RebindingOperation op, Action<string> finished, string message)
    {
        capture = null;
        op.Dispose();
        finished?.Invoke(message);
    }

    public static void CancelCapture() => capture?.Cancel();

    private static string Apply(Entry entry, string path)
    {
        foreach (var other in entries)
        {
            if (other == entry || !string.Equals(other.Action.bindings[other.Index].effectivePath, path, StringComparison.OrdinalIgnoreCase)) continue;
            if (Conflicts(entry.Action, other.Action))
                return $"Эта кнопка уже назначена на «{other.Label}».";
        }
        if (string.Equals(entry.Action.bindings[entry.Index].path, path, StringComparison.OrdinalIgnoreCase))
            entry.Action.RemoveBindingOverride(entry.Index);
        else
            entry.Action.ApplyBindingOverride(entry.Index, path);
        PlayerPrefs.SetString(Preference, asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
        Changed?.Invoke();
        return null;
    }

    private static bool Conflicts(InputAction first, InputAction second)
    {
        if (first.actionMap == second.actionMap)
        {
            // These two attacks use mutually exclusive weapon states and share F by design.
            return !(first.name == "Melee" && second.name == "Takedown" ||
                     first.name == "Takedown" && second.name == "Melee");
        }

        var gameplay = first.actionMap.name == "Player" ? first : second;
        var overlay = gameplay == first ? second : first;
        // Journal closes its own overlay. Interact also confirms a selected rune.
        return gameplay.name == "Journal" || gameplay.name == "Interact" && overlay.name != "Accept";
    }

    private static void LoadSaved()
    {
        string saved = PlayerPrefs.GetString(Preference, string.Empty);
        if (string.IsNullOrEmpty(saved)) return;
        try
        {
            asset.LoadBindingOverridesFromJson(saved, removeExisting: false);
        }
        catch (Exception error) when (error is ArgumentException || error is NullReferenceException)
        {
            Debug.LogWarning("Не удалось прочитать сохранённые настройки управления.");
        }
    }

    public static void ResetDefaults()
    {
        CancelCapture();
        EnsureInitialized();
        foreach (var entry in entries)
            entry.Action.RemoveBindingOverride(entry.Index);
        PlayerPrefs.DeleteKey(Preference);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
