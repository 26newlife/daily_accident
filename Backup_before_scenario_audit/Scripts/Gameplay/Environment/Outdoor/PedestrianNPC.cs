using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 창밖 단지의 보행자(저비용 AI). 캡슐 몸통 + 구 머리 + 팔다리 큐브를 절차적으로 흔들어 걷는 느낌을 낸다.
    /// 경로(Waypoint)를 따라 걷다가 가끔 멈추고, 앉아 있거나(벤치) 서 있는(경비원) 유형도 있다.
    /// 창문 가까이 있지 않을 때(DetailActive=false)는 팔다리 애니메이션을 생략하고 위치만 0.25초 간격으로 갱신한다.
    /// 폭발 후에는 멈춰서 위를 올려다보고 일부는 뒤로 물러나거나 달려간다 (React).
    /// </summary>
    public class PedestrianNPC : MonoBehaviour
    {
        public enum Role { Commuter, Student, DogWalker, Jogger, Stroller, Guard, Courier, Sitter }

        [Header("역할/이동")]
        [SerializeField] private Role role = Role.Commuter;
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.3f;
        [SerializeField, Range(0f, 1f), Tooltip("경로 지점마다 멈출 확률")] private float pauseChance = 0.15f;
        [SerializeField] private Vector2 pauseSeconds = new Vector2(2f, 6f);
        [SerializeField] private bool loop = true;

        [Header("몸 (자동 연결)")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftArm, rightArm, leftLeg, rightLeg;
        [SerializeField] private float swingDegrees = 38f;
        [SerializeField] private float stride = 2.4f;
        [SerializeField] private float bobHeight = 0.03f;

        [Header("폭발 반응")]
        [SerializeField, Range(0f, 1f)] private float fleeChance = 0.35f;
        [SerializeField] private float fleeSpeed = 3.2f;
        [SerializeField] private Vector2 fleeSeconds = new Vector2(3f, 6f);
        [SerializeField] private float lookUpDegrees = 35f;

        /// <summary>플레이어가 창문 근처에 있으면 true — 상세 애니메이션 사용</summary>
        public static bool DetailActive { get; set; }

        private enum State { Walking, Paused, Sitting, Standing, Looking, Fleeing }

        private Vector3[] path = new Vector3[0];
        private int index;
        private State state = State.Walking;
        private float stateTimer, phase, accumulatedDt;
        private float scale = 1f;
        private Vector3 fleeDir, homeBodyPos;
        private float reactAt = -1f;
        private Vector3 reactPoint;
        private bool reacted;

        public Role CurrentRole => role;
        public float Scale => scale;

        public void Configure(Role newRole, Vector3[] worldPath, float speed, bool loopPath, float uniformScale)
        {
            role = newRole; path = worldPath ?? new Vector3[0]; walkSpeed = speed; loop = loopPath; scale = uniformScale;
            transform.localScale = Vector3.one * scale;
            index = path.Length > 1 ? Random.Range(0, path.Length) : 0;
            if (path.Length > 0) transform.position = path[index];
            if (body != null) homeBodyPos = body.localPosition;
            phase = Random.value * 6f;
            switch (role)
            {
                case Role.Guard: state = State.Standing; break;
                case Role.Sitter: state = State.Sitting; transform.rotation = Quaternion.Euler(0f, 180f, 0f); ApplySitPose(); break;
                default: state = State.Walking; break;
            }
            if (role == Role.Jogger) { pauseChance = 0f; swingDegrees = 55f; stride = 3.6f; }
            if (role == Role.Stroller) swingDegrees = 22f;
        }

        private void Awake() { if (body != null) homeBodyPos = body.localPosition; }

        /// <summary>폭발 반응 예약 (delay 초 뒤)</summary>
        public void React(Vector3 worldPoint, float delay)
        {
            if (reacted) return;
            reactPoint = worldPoint; reactAt = Time.time + delay;
        }

        private void Update()
        {
            if (reactAt >= 0f && Time.time >= reactAt && !reacted) BeginReaction();

            float dt = Time.deltaTime;
            if (!DetailActive)
            {
                accumulatedDt += dt;
                if (accumulatedDt < 0.25f) return;     // 멀 때는 0.25초마다 한 번만 갱신
                dt = accumulatedDt; accumulatedDt = 0f;
            }
            else accumulatedDt = 0f;

            switch (state)
            {
                case State.Walking: Walk(dt, role == Role.Jogger ? walkSpeed * 2.4f : walkSpeed); break;
                case State.Fleeing:
                    MoveDir(fleeDir, fleeSpeed, dt);
                    Animate(dt, fleeSpeed, true);
                    stateTimer -= dt;
                    if (stateTimer <= 0f) { state = State.Looking; stateTimer = 999f; }
                    break;
                case State.Paused:
                    stateTimer -= dt;
                    Animate(dt, 0f, false);
                    if (stateTimer <= 0f) state = State.Walking;
                    break;
                case State.Standing: case State.Sitting: case State.Looking:
                    Animate(dt, 0f, false);
                    break;
            }
        }

        // ───────── 이동 ─────────
        private void Walk(float dt, float speed)
        {
            if (path.Length < 2) { Animate(dt, 0f, false); return; }
            Vector3 target = path[index];
            Vector3 to = target - transform.position; to.y = 0f;
            if (to.magnitude < 0.3f)
            {
                index++;
                if (index >= path.Length) { if (loop) index = 0; else { state = State.Standing; return; } }
                if (Random.value < pauseChance) { state = State.Paused; stateTimer = Random.Range(pauseSeconds.x, pauseSeconds.y); }
                return;
            }
            MoveDir(to.normalized, speed, dt);
            Animate(dt, speed, role == Role.Jogger);
        }

        private void MoveDir(Vector3 dir, float speed, float dt)
        {
            transform.position += dir * (speed * dt);
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), Mathf.Clamp01(dt * 6f));
        }

        // ───────── 절차적 애니메이션 ─────────
        private void Animate(float dt, float speed, bool fast)
        {
            if (!DetailActive) return;
            if (state == State.Looking || state == State.Standing || state == State.Paused || state == State.Sitting)
            {
                // 서 있을 때: 팔다리를 천천히 원위치
                SwingTo(0f, dt * 8f);
                return;
            }
            phase += dt * speed * stride;
            float s = Mathf.Sin(phase);
            SwingTo(s * swingDegrees * (fast ? 1.2f : 1f), 1f);
            if (body != null) body.localPosition = homeBodyPos + Vector3.up * (Mathf.Abs(s) * bobHeight);
        }

        private void SwingTo(float angle, float lerp)
        {
            Rot(leftArm, angle, lerp); Rot(rightArm, -angle, lerp);
            Rot(leftLeg, -angle, lerp); Rot(rightLeg, angle, lerp);
            if (body != null && lerp < 1f) body.localPosition = Vector3.Lerp(body.localPosition, homeBodyPos, lerp);
        }

        private static void Rot(Transform t, float angle, float lerp)
        {
            if (t == null) return;
            t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(angle, 0f, 0f), Mathf.Clamp01(lerp));
        }

        private void ApplySitPose()
        {
            // 벤치에 앉은 자세: 다리를 앞으로 뻗고 몸을 낮춘다
            Rot(leftLeg, -85f, 1f); Rot(rightLeg, -85f, 1f);
            if (body != null) body.localPosition = homeBodyPos + Vector3.down * 0.25f;
        }

        // ───────── 폭발 반응 ─────────
        private void BeginReaction()
        {
            reacted = true;
            Vector3 toBlast = reactPoint - transform.position; toBlast.y = 0f;
            if (toBlast.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toBlast.normalized, Vector3.up);
            if (head != null) head.localRotation = Quaternion.Euler(-lookUpDegrees, 0f, 0f);       // 위를 올려다봄
            if (role == Role.Sitter) { state = State.Looking; return; }
            if (Random.value < fleeChance)
            {
                fleeDir = -toBlast.normalized; if (fleeDir.sqrMagnitude < 0.01f) fleeDir = -transform.forward;
                state = State.Fleeing; stateTimer = Random.Range(fleeSeconds.x, fleeSeconds.y);
            }
            else state = State.Looking;
        }
    }
}
