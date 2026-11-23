using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(NPCMover))]
public sealed class NPCChaser : MonoBehaviour
{
    public enum State { Patrol, Chase, Search, InvestigateAlarm, Return, RushToGrate }

    [SerializeField, Min(0f)] private float grateInspectionSeconds = 10f;
    [SerializeField, Min(0.1f)] private float searchSeconds = 4f;
    private GraphManager graph;
    private NPCMover mover;
    private CharacterController body, playerBody;
    private RifleNpc guard;
    private IvanStealth stealth;
    private Transform player;
    private Node home, patrolEnd, goal, grate;
    private M0GateMechanism mechanism;
    private Vector3 post, destination, lastSeen, progressPosition;
    private Quaternion postFacing;
    private float nextCheck, nextShout, inspectUntil, progressTime;
    private bool patrolsEntrance, hasSight, blocked, direct, inspecting, trackingFootsteps;
    public State CurrentState { get; private set; }

    public void Initialize(GraphManager manager, Transform target, M0GateMechanism gate, Node gateApproach, Node entranceReturnPoint = null)
    {
        graph = manager;
        player = target;
        playerBody = target.GetComponent<CharacterController>();
        stealth = target.GetComponent<IvanStealth>();
        body = GetComponent<CharacterController>();
        guard = GetComponent<RifleNpc>();
        mover = GetComponent<NPCMover>();
        post = mover.Feet;
        postFacing = transform.rotation;
        patrolsEntrance = entranceReturnPoint != null;
        home = patrolsEntrance ? entranceReturnPoint : Anchor(post);
        if (patrolsEntrance) patrolEnd = gateApproach;
        else if (home != null && !guard.HoldsPosition)
            foreach (var edge in home.edges)
                if (edge.GetCost() < float.MaxValue) { patrolEnd = edge.target; break; }
        mechanism = gate;
        grate = gateApproach;
        if (mechanism != null) mechanism.PassabilityChanged += OnGrate;
        graph.PassageChanged += OnPassageChanged;
        graph.Alarm += OnAlarm;
        Patrol(patrolsEntrance ? patrolEnd : home);
    }

    private void OnDestroy()
    {
        if (mechanism != null) mechanism.PassabilityChanged -= OnGrate;
        if (graph == null) return;
        graph.PassageChanged -= OnPassageChanged;
        graph.Alarm -= OnAlarm;
    }

    private Node Anchor(Vector3 feet) => graph.FindAnchor(feet, body, playerBody);

    private void OnAlarm(NPCChaser caller, Node origin)
    {
        if (caller == this || hasSight || !isActiveAndEnabled || !guard.AvailableForTakedown || origin == null) return;
        Travel(State.InvestigateAlarm, origin, origin.transform.position);
    }

    private void OnGrate()
    {
        if (mechanism.IsOpen && grate != null && !guard.HoldsPosition && CurrentState == State.Patrol)
            Travel(State.RushToGrate, grate, grate.transform.position);
    }

    private void Patrol(Node stop) => Travel(State.Patrol, stop,
        stop == home || stop == null ? post : stop.transform.position);

    private void Travel(State state, Node node, Vector3 point)
    {
        CurrentState = state;
        goal = node;
        destination = point;
        inspecting = trackingFootsteps = false;
        Replan();
    }

    private void OnPassageChanged()
    {
        if (patrolsEntrance && CurrentState == State.Patrol && mover.HasRoute) return;
        if (!body.enabled || (inspecting && !blocked)) return;
        inspecting = false;
        Replan();
    }

    private bool CanWalkDirectly(Vector3 point) =>
        Mathf.Abs(mover.Feet.y - point.y) < body.stepOffset &&
        GraphManager.ClearApproach(mover.Feet, point, body, playerBody);

    private void Replan()
    {
        direct = CanWalkDirectly(destination);
        blocked = false;
        // Mid-flight, neither landing may be reachable by a straight capsule cast.
        // Continue toward the current waypoint when an alarm changes into a chase.
        mover.SetRoute(direct ? null : graph.ApproachRoute(Anchor(mover.Feet) ?? mover.Next, goal, out blocked));
        progressTime = Time.time;
        progressPosition = mover.Feet;
    }

    private Vector3 PlayerFeet => playerBody != null ? NPCMover.FootPosition(playerBody) : player.position;

    internal void RecognizePlayer()
    {
        lastSeen = PlayerFeet;
        destination = lastSeen;
        if (hasSight) return;
        hasSight = true;
        Travel(State.Chase, Anchor(lastSeen), lastSeen);
        if (Time.time >= nextShout)
        {
            nextShout = Time.time + 3f;
            guard.Shout();
            graph.RaiseAlarm(this, graph.SoundAnchor(mover.Feet, body, playerBody) ?? mover.LastReached);
        }
    }

    public Vector3 GetDirection(bool visible)
    {
        if (visible) RecognizePlayer();
        else if (hasSight)
        {
            hasSight = false;
            Travel(State.Search, Anchor(lastSeen) ?? graph.SoundAnchor(lastSeen, body, playerBody), lastSeen);
        }

        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + 0.3f;
            if (visible)
            {
                Node node = Anchor(lastSeen);
                if (node != goal || direct != CanWalkDirectly(lastSeen))
                {
                    goal = node;
                    Replan();
                }
            }
            else if (CurrentState == State.Patrol || CurrentState == State.Return || trackingFootsteps)
                Listen(PlayerFeet);
        }

        if (inspecting) return Inspect();
        Vector3 heading = direct ? ToPoint(destination) : mover.Direction();
        if (!direct && mover.AtGoal && !blocked && CanWalkDirectly(destination))
        {
            direct = true;
            heading = ToPoint(destination);
        }
        if (heading.sqrMagnitude > 0f)
        {
            if (Vector3.Distance(mover.Feet, progressPosition) > 0.15f)
            {
                progressTime = Time.time;
                progressPosition = mover.Feet;
            }
            if (Time.time - progressTime < 2f || visible) return heading;
            blocked = true;
        }
        if (visible) return Vector3.zero;
        if (CurrentState == State.Return && !blocked)
        {
            Patrol(home);
            return Vector3.zero;
        }
        if (CurrentState == State.Patrol && guard.HoldsPosition)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, postFacing, 150f * Time.deltaTime);
            return Vector3.zero;
        }
        inspecting = true;
        inspectUntil = Time.time + (CurrentState != State.Patrol ? searchSeconds :
            patrolsEntrance && goal == grate ? grateInspectionSeconds : 2f);
        return Vector3.zero;
    }

    private Vector3 ToPoint(Vector3 point)
    {
        Vector3 flat = Vector3.ProjectOnPlane(point - mover.Feet, Vector3.up);
        return flat.sqrMagnitude > 0.15f * 0.15f ? flat.normalized : Vector3.zero;
    }

    private void Listen(Vector3 feet)
    {
        if (playerBody == null || (stealth != null && stealth.IsCrouching) ||
            Vector3.ProjectOnPlane(playerBody.velocity, Vector3.up).sqrMagnitude < 0.12f) return;
        Node source = graph.FootstepSource(feet, body, playerBody);
        if (source == null) return;
        if (graph.FootstepLevel(source, graph.SoundAnchor(mover.Feet, body, playerBody)) < 0.25f) return;
        if (!trackingFootsteps || goal != source)
        {
            Travel(State.Search, source, source.transform.position);
            trackingFootsteps = true;
        }
    }

    private Vector3 Inspect()
    {
        if (CurrentState != State.Patrol || !patrolsEntrance)
            transform.Rotate(0f, (360f / searchSeconds) * Time.deltaTime, 0f);
        if (Time.time < inspectUntil) return Vector3.zero;
        if (CurrentState == State.Patrol) Patrol(goal == home ? patrolEnd : home);
        else Travel(State.Return, home, post);
        return Vector3.zero;
    }
}
