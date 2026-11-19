using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 이웃 NPC 공통 기반 (CharacterController 로 이동하는 그레이박스 캡슐). 상호작용(말 걸기)·동행(follow)·연기 호흡 게이지를 담당한다.
    /// NPC 는 플레이어가 아니므로 HazardZone/FireManager 의 플레이어 판정에는 걸리지 않고,
    /// 연기는 HazardManager.SmokeAt(위치)로 따로 계산한다. 모든 시간 값은 GameTimingConfig 에서 읽는다.
    /// 비그래픽: 쓰러짐·실패는 자막과 "화면 밖" 연출로만 표현한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public abstract class NpcBase : InteractableObject
    {
        private static readonly List<NpcBase> all = new List<NpcBase>();
        public static IReadOnlyList<NpcBase> All => all;

        [Header("NPC")]
        [SerializeField] protected string displayName = "이웃";
        [SerializeField, Tooltip("몸통/머리 등 시각 루트 (쓰러짐·퇴장 연출용). 비우면 자신")] protected Transform visualRoot;
        [SerializeField, Tooltip("목소리/기침 소리 슬롯 (클립은 나중에 연결, 비어 있어도 동작)")] protected AudioSource voice;
        [SerializeField] protected AudioClip[] coughClips;
        [SerializeField] private float gravity = -15f;

        protected CharacterController motor;
        private PlayerController player;
        private float verticalVelocity;
        private float lastSpeakAt = -99f;
        private float cough01;
        private float farTimer;

        /// <summary>0~1. 0 이 되면 쓰러진다 (연기 + 수건 없음)</summary>
        public float Health01 { get; protected set; } = 1f;
        public bool HasTowel { get; protected set; }
        public bool IsFollowing { get; protected set; }
        public bool IsGone { get; protected set; }

        protected static GameTimingConfig T => GameTimingConfig.Current;
        protected PlayerController Player => player != null ? player : (player = FindFirstObjectByType<PlayerController>());
        protected Vector3 PlayerPos => Player != null ? Player.transform.position : transform.position;

        protected virtual void Awake()
        {
            motor = GetComponent<CharacterController>();
            if (visualRoot == null) visualRoot = transform;
        }

        protected virtual void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        protected virtual void OnDisable() { all.Remove(this); }

        // ───────── 이동 ─────────
        /// <summary>목표 지점으로 수평 이동. 도착하면 true</summary>
        protected bool MoveToward(Vector3 target, float speed, float stopDistance)
        {
            Vector3 to = target - transform.position; to.y = 0f;
            float dist = to.magnitude;
            Vector3 move = Vector3.zero;
            bool arrived = dist <= stopDistance;
            if (!arrived)
            {
                Vector3 dir = to / Mathf.Max(0.001f, dist);
                move = dir * speed;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
            }
            ApplyMove(move);
            return arrived;
        }

        protected void ApplyMove(Vector3 planar)
        {
            if (motor == null || !motor.enabled) return;
            verticalVelocity = motor.isGrounded ? -1f : verticalVelocity + gravity * Time.deltaTime;
            motor.Move((planar + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        protected void FaceToward(Vector3 point)
        {
            Vector3 d = point - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }

        /// <summary>순간이동 (동행 압축 이동, 뒤처짐 보정)</summary>
        public void Warp(Vector3 position, float yaw)
        {
            if (motor == null) { transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f)); return; }
            motor.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            motor.enabled = true;
        }

        // ───────── 동행 ─────────
        /// <summary>플레이어 뒤를 따라간다. 너무 멀면 멈춰서 기다려 달라고 말하고, 한참 뒤처지면 뒤로 순간이동한다</summary>
        protected void UpdateFollow(float speed, string waitLine)
        {
            Vector3 p = PlayerPos;
            float dist = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(transform.position.x, 0f, transform.position.z));

            if (dist > T.npcWaitDistance)
            {
                ApplyMove(Vector3.zero);
                Speak(waitLine, 3f, 6f);
                farTimer += Time.deltaTime;
                if (dist > T.npcWarpDistance && farTimer > 3f)
                {
                    farTimer = 0f;
                    Vector3 back = Player != null ? -Player.transform.forward : Vector3.back;
                    Warp(p + back * T.npcFollowDistance, Player != null ? Player.transform.eulerAngles.y : 0f);
                }
                if (dist > T.npcFollowDistance) MoveToward(p, speed * 0.6f, T.npcFollowDistance);
                return;
            }
            farTimer = 0f;
            if (dist > T.npcFollowDistance) MoveToward(p, speed, T.npcFollowDistance);
            else { ApplyMove(Vector3.zero); FaceToward(p); }
        }

        // ───────── 호흡 / 연기 ─────────
        /// <summary>현재 위치의 연기로 호흡 게이지가 줄어든다 (수건이 있으면 느리게). 0 이 되면 true</summary>
        protected bool UpdateBreathing(float dt)
        {
            float smoke = HazardManager.Instance != null ? HazardManager.Instance.SmokeAt(transform.position + Vector3.up) : 0f;
            if (smoke > 0.05f)
            {
                float drain = smoke * T.npcSmokeDrainPerSecond * (HasTowel ? T.npcTowelDrainMultiplier : 1f);
                Health01 = Mathf.Max(0f, Health01 - drain * dt);
                cough01 -= dt;
                if (cough01 <= 0f && Health01 < 0.8f)
                {
                    cough01 = Random.Range(3f, 6f);
                    if (voice != null && coughClips != null && coughClips.Length > 0) voice.PlayOneShot(coughClips[Random.Range(0, coughClips.Length)]);
                    Speak("(콜록콜록…)", 2f, 4f);
                }
            }
            return Health01 <= 0f;
        }

        // ───────── 대사 / 수건 ─────────
        protected void Speak(string line, float seconds, float cooldown = 0f)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (Time.time - lastSpeakAt < cooldown) return;
            lastSpeakAt = Time.time;
            EventBus.ShowSubtitle($"{displayName}: {line}", seconds);
        }

        /// <summary>플레이어가 젖은 수건을 가지고 있으면 NPC 에게 건넨다 (수건이 인벤토리에서 사라진다)</summary>
        protected bool TryGiveWetTowel()
        {
            InventorySystem inv = InventorySystem.Instance;
            if (inv == null || HasTowel) return false;
            ItemData towel = inv.Find(EvacKeys.ItemWetTowel);
            if (towel == null) return false;
            inv.Remove(towel);
            HasTowel = true;
            if (ScoreSystem.Instance != null) ScoreSystem.Instance.Increment("neighbor_towel_given");
            EventBus.ShowMessage($"{displayName}에게 젖은 수건을 건넸다.");
            return true;
        }

        protected static bool PlayerHasWetTowel => InventorySystem.Instance != null && InventorySystem.Instance.Has(EvacKeys.ItemWetTowel);

        /// <summary>시각 루트를 숨긴다 (화면 밖으로 퇴장) — 충돌도 끈다</summary>
        protected void Vanish()
        {
            IsGone = true;
            IsFollowing = false;
            SetVisible(false);
            if (motor != null) motor.enabled = false;
            Interactable = false;
        }

        /// <summary>시각 루트 표시/숨김. 시각 루트가 자기 자신이면 비활성화하면 Update 가 멈추므로 렌더러만 끈다</summary>
        protected void SetVisible(bool visible)
        {
            if (visualRoot == null) return;
            if (visualRoot == transform)
            {
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            }
            else visualRoot.gameObject.SetActive(visible);
        }

        public override bool CanInteract(PlayerInteractor interactor) => base.CanInteract(interactor) && !IsGone && !GameFlow.IsModalOpen;

        protected override void OnInteract(PlayerInteractor interactor)
        {
            if (ChoicePanel.Instance == null) return;
            OpenDialogue(ChoicePanel.Instance);
            Publish(ScenarioEventType.Interacted);
        }

        protected abstract void OpenDialogue(ChoicePanel panel);

        protected static void MarkNeighborRescued()
        {
            ScenarioFlags.Set(EvacKeys.FlagNeighborRescued, true);
        }
    }
}

