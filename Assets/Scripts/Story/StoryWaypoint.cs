using UnityEngine;

// Anchored to the existing door, basement threshold or observation point in each level.
public sealed class StoryWaypoint : MonoBehaviour
{
    public enum Passage { FrontDoor, RearDoor, Basement, FirstAltar }
    [SerializeField] private Passage passage;
    [SerializeField] private float radius = 2.4f;

    private void Update() => GameCheckpoint.Approach(passage, transform.position, radius, gameObject.scene);
}
