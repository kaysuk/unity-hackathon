using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal sealed class InteractionPrompt : IDisposable
{
    private Canvas promptCanvas;
    private Text promptLabel;
    private InputAction displayedAction;
    private string buttonPrompt;
    private bool bindingLabelDirty = true;

    internal InteractionPrompt()
    {
        InputSystem.onActionChange += OnActionChange;
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    public void Dispose()
    {
        InputSystem.onActionChange -= OnActionChange;
        InputSystem.onDeviceChange -= OnDeviceChange;
        if (promptCanvas != null)
        {
            promptCanvas.enabled = false;
            UnityEngine.Object.Destroy(promptCanvas.gameObject);
        }
    }

    private void OnActionChange(object changed, InputActionChange change)
    {
        if (change == InputActionChange.BoundControlsChanged)
            bindingLabelDirty = true;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change) => bindingLabelDirty = true;

    internal void Show(PlayerInteractor player, Interactable target, InputAction action)
    {
        if (target == null || action == null)
        {
            Hide();
            return;
        }

        if (bindingLabelDirty || displayedAction != action)
        {
            RefreshPromptText(action);
            displayedAction = action;
        }

        string text = target.GetPrompt(player, buttonPrompt);
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        if (promptLabel == null)
            CreatePrompt(player.gameObject.scene);
        if (promptLabel.text != text)
            promptLabel.text = text;
        if (!promptLabel.enabled)
            promptLabel.enabled = true;
    }

    internal void Hide()
    {
        if (promptLabel != null && promptLabel.enabled)
            promptLabel.enabled = false;
    }

    private void RefreshPromptText(InputAction interact)
    {
        var names = new List<string>();
        var bindings = interact.bindings;
        foreach (InputControl control in interact.controls)
        {
            int index = interact.GetBindingIndexForControl(control);
            if (index < 0)
                continue;

            while (index > 0 && bindings[index].isPartOfComposite)
                index--;

            string name = interact.GetBindingDisplayString(
                index, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                names.Add(name);
        }

        buttonPrompt = names.Count > 0 ? $"Нажмите {string.Join(" / ", names)}" : string.Empty;
        bindingLabelDirty = false;
    }

    private void CreatePrompt(Scene scene)
    {
        var root = new GameObject("Interaction Prompt", typeof(Canvas), typeof(CanvasScaler));
        if (root.scene != scene)
            SceneManager.MoveGameObjectToScene(root, scene);

        promptCanvas = root.GetComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var labelObject = new GameObject("Binding Label", typeof(RectTransform),
            typeof(Text), typeof(Outline));
        labelObject.transform.SetParent(root.transform, false);

        promptLabel = labelObject.GetComponent<Text>();
        promptLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptLabel.fontSize = 28;
        promptLabel.fontStyle = FontStyle.Bold;
        promptLabel.alignment = TextAnchor.MiddleCenter;
        promptLabel.color = Color.white;
        promptLabel.raycastTarget = false;
        promptLabel.supportRichText = false;

        var rect = promptLabel.rectTransform;
        rect.anchorMin = new Vector2(0.1f, 0.38f);
        rect.anchorMax = new Vector2(0.9f, 0.38f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 48f);

        var outline = labelObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }
}
