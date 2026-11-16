using System;
using UnityEngine;

namespace Hackathon.Menu
{
    /// <summary>Backdrop state for the current game session, shared across scene loads.</summary>
    public static class MainMenuStoryState
    {
        public static bool RainyNight { get; private set; }
        public static bool PhotoFrameBroken { get; private set; }

        internal static event Action Changed;

        public static void StartRainyNight() => Restore(true, PhotoFrameBroken);

        public static void BreakPhotoFrame() => Restore(RainyNight, true);

        public static void Reset() => Restore(false, false);

        public static void Restore(bool rainyNight, bool photoFrameBroken)
        {
            if (RainyNight == rainyNight && PhotoFrameBroken == photoFrameBroken)
                return;

            RainyNight = rainyNight;
            PhotoFrameBroken = photoFrameBroken;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            RainyNight = false;
            PhotoFrameBroken = false;
            Changed = null;
        }
    }
}
