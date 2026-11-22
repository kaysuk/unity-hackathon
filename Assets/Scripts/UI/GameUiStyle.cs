using UnityEngine;

internal static class GameUiStyle
{
    internal static readonly Color Ink = new Color32(238, 230, 216, 255);
    internal static readonly Color MutedInk = new Color32(211, 198, 177, 255);
    internal static readonly Color Brass = new Color32(210, 161, 102, 255);
    internal static readonly Color Panel = new Color32(24, 19, 14, 234);

    internal static Font Font => Resources.Load<Font>("KaramazovyUI/CormorantGaramond-SemiBold");
}
