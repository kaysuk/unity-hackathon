using UnityEngine;

[DisallowMultipleComponent]
public sealed class VotiveCrystal : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 18f;

    private void Update()
    {
        transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
