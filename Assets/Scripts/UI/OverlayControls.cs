using System;
using UnityEngine.InputSystem;

// The archive and the comic remain interactive while their overlay pauses gameplay.
internal sealed class OverlayControls
{
    public readonly InputAction Left = Find("Overlay/Left");
    public readonly InputAction Right = Find("Overlay/Right");
    public readonly InputAction Up = Find("Overlay/Up");
    public readonly InputAction Down = Find("Overlay/Down");
    public readonly InputAction Accept = Find("Overlay/Accept");
    public readonly InputAction Back = Find("Overlay/Back");

    private readonly Action refreshHints;
    public bool GamepadHints { get; private set; }

    public OverlayControls(Action refreshHints)
    {
        this.refreshHints = refreshHints;
        GamepadHints = Gamepad.current != null && Keyboard.current == null;
    }

    public static InputAction Find(string path) => InputSystem.actions.FindAction(path, true);

    public bool Pressed(InputAction action)
    {
        if (!action.WasPressedThisFrame())
            return false;

        UseDevice(action.activeControl?.device);
        return true;
    }

    public void UseDevice(InputDevice device)
    {
        if (device == null || GamepadHints == (device is Gamepad))
            return;

        GamepadHints = device is Gamepad;
        refreshHints();
    }

    public string Label(InputAction action) =>
        GameBindings.Label(action.actionMap.name + "/" + action.name, GamepadHints);
}
