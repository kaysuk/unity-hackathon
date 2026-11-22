using UnityEngine;

// Keep rig inputs early, but read the muzzle only after ProceduralHitReaction (200).
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class PistolShotAfterPose : MonoBehaviour
{
    private PistolCombat combat;
    private void Awake() => combat = GetComponent<PistolCombat>();
    private void LateUpdate()
    {
        if (combat != null && combat.isActiveAndEnabled) combat.EvaluateShotAfterPose();
    }
}
