using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 추락 위험 구역(트리거). 열린 개구부(폭발로 깨진 베란다 유리 등) 앞 난간 높이에서
    /// 플레이어가 바깥 방향으로 계속 밀거나(pushSeconds) 점프하면 추락 사고로 게임 오버가 된다 (비그래픽: 암전 + 교육 문구).
    /// 맵 밖으로 나가는 것을 막는 보이지 않는 콜라이더(WindowBarrier_*)는 그대로 유지한다 — 이 구역은 그 안쪽 가장자리에 놓인다.
    /// "활성" 조건: watchRendererName 의 렌더러가 모두 꺼져 있을 때(= 유리가 깨졌을 때). 이름이 비어 있으면 항상 활성.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FallHazard : MonoBehaviour
    {
        [SerializeField, Tooltip("이 이름의 렌더러가 모두 꺼지면(유리 파손) 활성화. 비우면 항상 활성")] private string watchRendererName = "balcony_glass";
        [SerializeField, Tooltip("바깥(추락) 방향 (월드 기준)")] private Vector3 outward = Vector3.forward;
        [SerializeField, Min(0.1f), Tooltip("바깥으로 이 시간(초) 이상 계속 밀면 추락")] private float pushSeconds = 1.2f;
        [SerializeField, Range(0.1f, 1f), Tooltip("바깥 방향 입력 비율이 이 값 이상이어야 '민다'로 센다")] private float pushThreshold = 0.5f;
        [SerializeField, Tooltip("구역 안에서 점프해도 추락")] private bool jumpCausesFall = true;
        [SerializeField] private DeathReason reason = DeathReason.FallFromHeight;
        [SerializeField, TextArea(1, 2)] private string warning = "난간 밖은 까마득한 높이다. 아래가 아찔하다.";

        private readonly List<Renderer> watched = new List<Renderer>();
        private PlayerController player;
        private GameInput input;
        private float push;
        private bool fallen, warned;

        private void Reset() { var c = GetComponent<Collider>(); if (c != null) c.isTrigger = true; }

        private void Start()
        {
            if (string.IsNullOrEmpty(watchRendererName)) return;
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                if (r.gameObject.name == watchRendererName) watched.Add(r);
        }

        private bool Active
        {
            get
            {
                if (string.IsNullOrEmpty(watchRendererName)) return true;
                if (watched.Count == 0) return false;
                foreach (Renderer r in watched) if (r != null && r.enabled) return false;
                return true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<CharacterController>() == null) return;
            player = other.GetComponent<PlayerController>();
            if (player != null) player.Jumped += OnJumped;
            push = 0f;
            if (Active && !warned) { warned = true; EventBus.ShowScent(warning); }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<CharacterController>() == null) return;
            Detach();
            push = 0f;
        }

        private void OnDisable() => Detach();

        private void Detach()
        {
            if (player != null) player.Jumped -= OnJumped;
            player = null;
        }

        private void OnJumped()
        {
            if (jumpCausesFall && Active) Fall("jump");
        }

        private void OnTriggerStay(Collider other)
        {
            if (fallen || GameFlow.GameplayBlocked || !Active) return;
            if (player == null || other.GetComponent<CharacterController>() == null) return;
            if (input == null) input = FindFirstObjectByType<GameInput>();
            if (input == null) return;

            Vector2 m = input.Move;
            Vector3 wish = player.transform.right * m.x + player.transform.forward * m.y;
            float dot = wish.sqrMagnitude > 0.0001f ? Vector3.Dot(wish.normalized, outward.normalized) * Mathf.Min(1f, wish.magnitude) : 0f;
            if (dot >= pushThreshold) push += Time.deltaTime;
            else push = Mathf.Max(0f, push - Time.deltaTime * 2f);
            if (push >= pushSeconds) Fall("push");
        }

        private void Fall(string how)
        {
            if (fallen) return;
            fallen = true;
            Debug.Log($"[Risk] fall from height ({how})");
            if (ScoreSystem.Instance != null) { ScoreSystem.Instance.Increment("risk_fall_attempts"); ScoreSystem.Instance.Increment("risk_attempts_total"); }
            if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(Color.black, 1f, 3f);   // 비그래픽 암전
            if (DeathAndEnding.Instance != null) DeathAndEnding.Instance.Die(reason);
            else EventBus.RaisePlayerDied(reason);
        }
    }
}
