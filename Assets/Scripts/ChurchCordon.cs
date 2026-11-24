using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class ChurchCordon : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private RifleNpc[] courtyardOfficers;
    [SerializeField] private RifleNpc[] streetOfficers;
    [SerializeField] private Transform returnPoint;
    [SerializeField] private AudioSource alertSource;
    [SerializeField] private AudioClip alertClip;
    [SerializeField, Min(.1f)] private float gateHalfWidth = 1.84f;

    private const float WalkSpeed = 2.75f;
    private const float ContactDistance = 1.5f;
    private const float InterceptionDelay = .82f;
    private const float ReactionDelay = .34f;
    private enum Phase { OnPost, Alert, Returning }

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveZ = Animator.StringToHash("MoveZ");
    private readonly List<Officer> officers = new();
    private readonly List<(Collider first, Collider second)> changedCollisionPairs = new();
    private CharacterController playerBody;
    private CombatHealth playerHealth;
    private Vector3 previous;
    private Phase phase;
    private float alertStarted;
    private float contactTime;
    private bool initialized;

    public bool IsAlert => phase == Phase.Alert;
    public bool IsReturning => phase == Phase.Returning;
    public int AlarmCount { get; private set; }
    public int InterceptionCount { get; private set; }

    private sealed class Officer
    {
        public Transform actor;
        public CharacterController body;
        public Animator animator;
        public CombatHealth health;
        public bool courtyard;
        public Vector3 post;
        public Quaternion facing;
        public Vector3 approachOffset;
        public readonly List<Vector3> footsteps = new();
        public PlayerFootsteps stepSounds;
        public bool walking;
        public bool Available => actor != null && actor.gameObject.activeInHierarchy && body.enabled && !health.IsDead;
    }

    private void Start()
    {
        if (player == null || returnPoint == null)
        {
            Debug.LogError("A church cordon needs a player and a street return point.", this);
            enabled = false;
            return;
        }
        playerBody = player.GetComponent<CharacterController>();
        playerHealth = player.GetComponent<CombatHealth>();
        Register(courtyardOfficers, true);
        Register(streetOfficers, false);
        SetUpCollisionPairs();
        previous = transform.InverseTransformPoint(player.position);
        initialized = true;
    }

    internal void OpenForFirstVisit()
    {
        enabled = false;
        foreach (var officer in courtyardOfficers) if (officer != null) officer.gameObject.SetActive(false);
        foreach (var officer in streetOfficers) if (officer != null) officer.gameObject.SetActive(false);
    }

    private void Register(RifleNpc[] members, bool courtyard)
    {
        if (members == null) return;
        for (int i = 0; i < members.Length; i++)
        {
            var npc = members[i];
            if (npc == null || officers.Exists(o => o.actor == npc.transform)) continue;
            npc.enabled = false;
            float angle = (i + .5f) * 2f * Mathf.PI / members.Length;
            var officer = new Officer {
                actor = npc.transform, body = npc.GetComponent<CharacterController>(),
                animator = npc.GetComponent<Animator>(), health = npc.GetComponent<CombatHealth>(),
                courtyard = courtyard, post = npc.transform.position, facing = npc.transform.rotation,
                approachOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.02f
            };
            officers.Add(officer);
        }
    }

    private void IgnorePair(Collider a, Collider b)
    {
        if (a == null || b == null || !a.enabled || !b.enabled || Physics.GetIgnoreCollision(a, b)) return;
        Physics.IgnoreCollision(a, b);
        changedCollisionPairs.Add((a, b));
    }

    private void SetUpCollisionPairs()
    {
        for (int i = 0; i < officers.Count; i++)
        {
            IgnorePair(officers[i].body, playerBody);
            for (int j = 0; j < i; j++) IgnorePair(officers[i].body, officers[j].body);
        }
    }

    private void OnEnable()
    {
        if (!initialized || player == null) return;
        previous = transform.InverseTransformPoint(player.position);
        SetUpCollisionPairs();
    }

    private void OnDisable()
    {
        foreach (var pair in changedCollisionPairs)
            if (pair.first != null && pair.second != null) Physics.IgnoreCollision(pair.first, pair.second, false);
        changedCollisionPairs.Clear();
        foreach (var officer in officers) StopWalking(officer);
    }

    private void LateUpdate()
    {
        if (!initialized || player == null || Time.deltaTime <= 0f) return;
        Vector3 current = transform.InverseTransformPoint(player.position);
        bool entered = CrossedEntrance(previous, current);
        previous = current;
        if (playerHealth != null && playerHealth.IsDead) { if (IsAlert) BeginReturn(); }
        else if (!IsAlert && entered) BeginAlert();

        if (IsAlert)
        {
            if (current.z < -.35f) BeginReturn();
            else Respond();
        }
        if (IsReturning) ReturnToPosts();
    }

    private bool CrossedEntrance(Vector3 from, Vector3 to)
    {
        if (from.z >= 0f || to.z < 0f) return false;
        Vector3 crossing = Vector3.Lerp(from, to, -from.z / (to.z - from.z));
        return Mathf.Abs(crossing.x) <= gateHalfWidth && crossing.y >= -.6f && crossing.y <= 2.4f;
    }

    private void BeginAlert()
    {
        phase = Phase.Alert;
        alertStarted = Time.time;
        contactTime = 0f;
        AlarmCount++;
        StoryGameplayVoice.FromPlayer(this, player.gameObject, "cordon.challenge", "Church_Exterior");
        GameAudio.Alert(alertSource != null ? alertSource.transform.position : transform.position, alertClip);
    }

    private static Vector3 Flat(Vector3 delta) => new Vector3(delta.x, 0f, delta.z);

    private static void Turn(Officer o, Vector3 direction)
    {
        if (direction.sqrMagnitude < .0001f) return;
        o.actor.rotation = Quaternion.RotateTowards(o.actor.rotation, Quaternion.LookRotation(direction), 250f * Time.deltaTime);
    }

    private bool CanReachPlayer(Officer o)
    {
        Vector3 from = o.actor.position + Vector3.up * 1.1f;
        Vector3 to = playerBody != null ? playerBody.bounds.center : player.position + Vector3.up;
        if (!Physics.Linecast(from, to, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
        return hit.transform.IsChildOf(player);
    }

    private void Respond()
    {
        bool inReach = false;
        foreach (var o in officers)
        {
            if (!o.Available) continue;
            Vector3 delta = Flat(player.position - o.actor.position);
            Turn(o, delta);
            if (!o.courtyard || Time.time - alertStarted < ReactionDelay) { StopWalking(o); continue; }
            if (delta.sqrMagnitude <= ContactDistance * ContactDistance &&
                Mathf.Abs(player.position.y - o.actor.position.y) < 1.6f && CanReachPlayer(o))
            {
                inReach = true;
                StopWalking(o);
                continue;
            }

            Vector3 goal = player.position + o.approachOffset;
            // The escort stays in the yard. An officer never has to push through the gate or its hedge.
            Vector3 local = transform.InverseTransformPoint(goal);
            if (local.z < .65f) { local.z = .65f; goal = transform.TransformPoint(local); }
            Vector3 before = o.actor.position;
            Walk(o, goal);
            Vector3 last = o.footsteps.Count > 0 ? o.footsteps[o.footsteps.Count - 1] : o.post;
            if (Flat(o.actor.position - last).sqrMagnitude > .25f && before != o.actor.position)
                o.footsteps.Add(o.actor.position);
        }
        contactTime = inReach ? contactTime + Time.deltaTime : 0f;
        if (contactTime >= InterceptionDelay) EscortToStreet();
    }

    private void EscortToStreet()
    {
        bool restore = playerBody != null && playerBody.enabled;
        if (restore) playerBody.enabled = false;
        player.SetPositionAndRotation(returnPoint.position, returnPoint.rotation);
        if (restore)
        {
            playerBody.enabled = true;
            foreach (var o in officers)
                if (o.Available) Physics.IgnoreCollision(o.body, playerBody);
        }
        previous = transform.InverseTransformPoint(player.position);
        InterceptionCount++;
        BeginReturn();
    }

    private void BeginReturn()
    {
        if (playerHealth != null && !playerHealth.IsDead)
            StoryGameplayVoice.FromPlayer(this, player.gameObject, "cordon.detour", "Church_Exterior");
        phase = Phase.Returning;
        contactTime = 0f;
    }

    private void ReturnToPosts()
    {
        bool allOnPost = true;
        foreach (var o in officers)
        {
            if (!o.Available) continue;
            int last = o.footsteps.Count - 1;
            while (last >= 0 && Flat(o.footsteps[last] - o.actor.position).sqrMagnitude < .04f)
                o.footsteps.RemoveAt(last--);
            Vector3 waypoint = last >= 0 ? o.footsteps[last] : o.post;
            Vector3 heading = Flat(waypoint - o.actor.position);
            if (heading.sqrMagnitude > .0064f)
            {
                allOnPost = false;
                Turn(o, heading);
                Walk(o, waypoint);
            }
            else
            {
                StopWalking(o);
                o.actor.rotation = Quaternion.RotateTowards(o.actor.rotation, o.facing, 250f * Time.deltaTime);
                if (Quaternion.Angle(o.actor.rotation, o.facing) > .5f) allOnPost = false;
            }
        }
        if (allOnPost) phase = Phase.OnPost;
    }

    private static void Walk(Officer o, Vector3 point)
    {
        Vector3 motion = Vector3.ClampMagnitude(Flat(point - o.actor.position), WalkSpeed * Time.deltaTime);
        if (motion.sqrMagnitude < .000001f) { StopWalking(o); return; }
        Vector3 before = o.actor.position;
        motion.y = -2.5f * Time.deltaTime;
        o.body.Move(motion);
        Vector3 moved = Flat(o.actor.position - before) / (WalkSpeed * Time.deltaTime);
        if (moved.sqrMagnitude < .004f) { StopWalking(o); return; }
        if (o.actor.gameObject.scene == SceneManager.GetActiveScene())
            o.stepSounds.Move(o.body, false, 0.5f);
        Vector3 local = o.actor.InverseTransformDirection(moved);
        o.animator.SetFloat(MoveX, Mathf.Clamp(local.x, -1f, 1f));
        o.animator.SetFloat(MoveZ, Mathf.Clamp(local.z, -1f, 1f));
        o.walking = true;
    }

    private static void StopWalking(Officer o)
    {
        if (!o.walking) return;
        o.walking = false;
        if (o.animator == null) return;
        o.animator.SetFloat(MoveX, 0f);
        o.animator.SetFloat(MoveZ, 0f);
    }
}
