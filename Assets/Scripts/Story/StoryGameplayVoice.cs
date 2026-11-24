using UnityEngine;
using UnityEngine.SceneManagement;

// Additively loaded locations can already run their scripts while Ivan is still in the previous room.
internal static class StoryGameplayVoice
{
    internal static void FromPlayer(Component trigger, GameObject actor, string key, string location = null)
    {
        if (actor != null && actor.CompareTag("Player") && actor.scene == trigger.gameObject.scene)
            InScene(trigger, key, location);
    }

    internal static void InScene(Component trigger, string key, string location)
    {
        Scene scene = trigger.gameObject.scene;
        if (scene == SceneManager.GetActiveScene() && (location == null || location == scene.name))
            StorySpeech.Say(key);
    }
}
