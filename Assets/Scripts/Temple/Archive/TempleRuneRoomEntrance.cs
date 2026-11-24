using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public sealed class TempleRuneRoomEntrance : MonoBehaviour
{
    [SerializeField] private TempleRunePuzzle puzzle;

    private void OnTriggerEnter(Collider other)
    {
        if (isActiveAndEnabled && puzzle != null && !puzzle.AreDialsAligned &&
            puzzle.CanHearRiddle(other.GetComponentInParent<PlayerInteractor>()))
            StorySpeech.SayPrompt("temple.rune.intro");
    }
}
