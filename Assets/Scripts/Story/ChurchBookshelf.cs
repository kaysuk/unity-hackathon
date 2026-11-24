using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class ChurchBookshelf : Interactable
{
    [SerializeField] private Transform closedPosition;
    [SerializeField] private Transform stairLanding;
    [SerializeField] private Transform wineBarrel;
    private Vector3 openPosition;
    private Transform actor;
    private bool moving;
    private bool opened;
    private bool returnVisit;

    public bool Open => opened;
    protected override string ActionLabel => "Отодвинуть шкаф";

    private void Awake() => openPosition = transform.position;

    internal void Prepare(Transform player, bool returning)
    {
        actor = player;
        returnVisit = returning;
        opened = returning;
        transform.position = returning ? openPosition : closedPosition.position;
        Physics.SyncTransforms();
    }

    private void Update()
    {
        if (actor == null || Time.timeScale <= 0f) return;
        float distance = Vector3.Distance(actor.position, closedPosition.position);
        if (returnVisit && distance < 5f) StorySpeech.Say("nearing_basement");
        if (opened || moving || returnVisit) return;
        if (Vector3.Distance(actor.position, stairLanding.position) < 2.6f)
            StorySpeech.Say("basement_door_checked");
        if (Vector3.Distance(actor.position, wineBarrel.position) < 1.9f)
            StorySpeech.Say("dark_stain_inspected");
        if (distance < 4.5f) StorySpeech.Say("cabinet_scratches_inspected");
        if (distance < 2f) StorySpeech.Say("cabinet_inspected");
    }

    public override bool CanInteract(PlayerInteractor player) =>
        base.CanInteract(player) && actor != null && !opened && !moving;

    public override void Interact(PlayerInteractor player)
    {
        if (player == null || player.transform != actor || !CanInteract(player)) return;
        StartCoroutine(Slide());
    }

    private IEnumerator Slide()
    {
        moving = true;
        GameAudio.Play(SoundCue.Slide, transform.position);
        Vector3 start = transform.position;
        const float duration = 0.85f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(start, openPosition, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        transform.position = openPosition;
        opened = true;
        moving = false;
        StorySpeech.Say("cabinet_moved_passage_revealed");
    }
}
