using System.Collections;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 엘리베이터 동작 총괄.
    ///  · 평상시(Act 1): 호출 → 도착 → 문 열림 → 탑승 연출 (정상 작동). 아무것도 막지 않는다.
    ///  · 화재 후(정전 플래그 또는 불이 붙은 뒤): 호출할 때마다 ElevatorHazardConfig 의 가중치로 결과를 굴린다
    ///    (화상 / 무반응 / 도착 후 화염·유독가스 / 극히 드문 안전 도착). 탑승하면 승강로 연기로 질식한다.
    /// 모든 결과는 [Risk] 로그로 남기고, 시도 횟수는 ScoreSystem 에 기록만 한다.
    /// 일시정지/사망/엔딩 중에는 진행하지 않으며(타이머는 scaled time), 재시작하면 씬과 함께 새로 만들어진다.
    /// </summary>
    public class ElevatorHazardSystem : MonoBehaviour
    {
        /// <summary>Act 2 폭발 때 true 로 설정되는 "정전/화재 중" 플래그</summary>
        public const string FireModeFlag = "blackout";
        public const string FireStartTimeKey = "blackout_time";

        [SerializeField] private ElevatorHazardConfig config;

        public static ElevatorHazardSystem Instance { get; private set; }

        private ElevatorInteractable door;
        private int pressCount;
        private bool arriving, doorOpen, riding;
        private float firstFireSeenAt = -1f;

        /// <summary>씬에 시스템이 없어도(빌더 미실행 등) 기본 설정으로 동작하도록 필요할 때 만든다</summary>
        public static ElevatorHazardSystem GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("ElevatorHazardSystem");
            return go.AddComponent<ElevatorHazardSystem>();   // Awake 에서 Instance 설정
        }

        public static void ResetRunState() { Instance = null; }

        private ElevatorHazardConfig Cfg
        {
            get
            {
                if (config == null) { config = ScriptableObject.CreateInstance<ElevatorHazardConfig>(); config.hideFlags = HideFlags.HideAndDontSave; }
                return config;
            }
        }

        private void Awake()
        {
            Instance = this;
            foreach (ElevatorInteractable e in FindObjectsByType<ElevatorInteractable>(FindObjectsInactive.Include))
                if (e.Role == ElevatorInteractable.PartRole.Door) door = e;
            if (door != null) door.SetOpen(false);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ───────── 상태 판정 ─────────
        public static bool FireMode
        {
            get
            {
                if (ScenarioFlags.GetBool(FireModeFlag)) return true;
                foreach (FireSource f in FireSource.All) if (f != null && f.IsLit) return true;
                return false;
            }
        }

        private bool Blocked => GameFlow.IsEnded || GameFlow.IsPaused;

        // ───────── 호출 ─────────
        public void CallElevator(Transform at)
        {
            if (Blocked || arriving || doorOpen || riding) return;
            ElevatorHazardConfig c = Cfg;
            pressCount++;
            Count("risk_elevator_attempts");

            if (!FireMode)
            {
                pressCount = 0;   // 평상시 호출은 화재 중 위험도(누른 횟수)에 반영하지 않는다
                StartCoroutine(NormalArrival());
                return;
            }

            // 위험도 입력값: 복도 연기, 불 근접, 화재 후 경과 시간, 같은 버튼을 누른 횟수
            float smoke = HazardManager.Instance != null ? HazardManager.Instance.PlayerSmoke : 0f;
            float fire01 = FireProximity(at != null ? at.position : transform.position, c.fireNearRadius);
            float now = GameClock.Instance != null ? GameClock.Instance.RealSeconds : 0f;
            float since = ScenarioFlags.Has(FireStartTimeKey) ? ScenarioFlags.GetNumber(FireStartTimeKey) : (firstFireSeenAt < 0f ? (firstFireSeenAt = now) : firstFireSeenAt);
            float minutes = Mathf.Max(0f, (now - since) / 60f);

            ElevatorOutcome outcome = c.Roll(smoke, fire01, minutes, pressCount, out string details);
            Debug.Log($"[Risk] elevator call #{pressCount} → {outcome}  ({details})");
            Record("risk_elevator_last_outcome", (float)outcome);

            switch (outcome)
            {
                case ElevatorOutcome.ButtonBurn:
                    ApplyButtonBurn(c);
                    break;
                case ElevatorOutcome.NoResponse:
                    EventBus.ShowMessage(c.noResponseMessage);
                    break;
                default:
                    StartCoroutine(FireArrival(outcome));
                    break;
            }
        }

        private void ApplyButtonBurn(ElevatorHazardConfig c)
        {
            EventBus.ShowMessage(c.burnMessage);
            if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(new Color(1f, 0.3f, 0.1f), 0.5f, 0.8f);
            PlayerVitals v = FindFirstObjectByType<PlayerVitals>();
            if (v != null) v.AddBurn(c.burnAmount);
        }

        private IEnumerator NormalArrival()
        {
            ElevatorHazardConfig c = Cfg;
            arriving = true;
            yield return new WaitForSeconds(c.normalArriveSeconds);
            arriving = false;
            if (GameFlow.IsEnded) yield break;
            SetDoorOpen(true);
            EventBus.ShowMessage(c.normalArriveMessage);
            yield return new WaitForSeconds(c.normalDoorOpenSeconds);
            if (doorOpen && !riding) SetDoorOpen(false);
        }

        private IEnumerator FireArrival(ElevatorOutcome outcome)
        {
            ElevatorHazardConfig c = Cfg;
            arriving = true;
            EventBus.ShowScent(c.arriveHintMessage);
            yield return new WaitForSeconds(c.arriveSeconds);
            arriving = false;
            if (GameFlow.IsEnded) yield break;
            SetDoorOpen(true);

            if (outcome == ElevatorOutcome.ArriveFireball)
            {
                EventBus.ShowMessage(c.fireballMessage);
                if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(new Color(1f, 0.55f, 0.15f), 1f, 1.5f);
                yield return new WaitForSeconds(c.fireballDeathDelay);
                Die(DeathReason.ElevatorFireball);
            }
            else if (outcome == ElevatorOutcome.ArriveSmoke)
            {
                EventBus.ShowMessage(c.smokeMessage);
                FindFirstObjectByType<PlayerController>()?.Shake(0.06f, c.smokeDeathDelay);
                yield return new WaitForSeconds(c.smokeDeathDelay);
                Die(DeathReason.ElevatorSmoke);
            }
            else
            {
                EventBus.ShowMessage(c.safeMessage);
                yield return new WaitForSeconds(c.safeDoorOpenSeconds);
                if (doorOpen && !riding) SetDoorOpen(false);
            }
        }

        // ───────── 탑승 ─────────
        public void Board()
        {
            if (Blocked || !doorOpen || riding) return;
            ElevatorHazardConfig c = Cfg;
            Count("risk_elevator_boarded");

            if (!FireMode)
            {
                if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(Color.black, 1f, 1.8f);
                EventBus.ShowMessage(c.normalRideMessage);
                SetDoorOpen(false);
                return;
            }
            Debug.Log("[Risk] elevator boarded during fire → trapped, shaft smoke");
            StartCoroutine(RideToDeath());
        }

        private IEnumerator RideToDeath()
        {
            ElevatorHazardConfig c = Cfg;
            riding = true;
            SetDoorOpen(false);
            LightGroup.SetAllOn(false);   // 문이 닫히며 정전
            EventBus.ShowMessage(c.rideMessage);
            if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(Color.black, 0.85f, c.rideDeathSeconds);
            PlayerController p = FindFirstObjectByType<PlayerController>();
            if (p != null) p.Shake(0.05f, c.rideDeathSeconds);
            yield return new WaitForSeconds(c.rideDeathSeconds);
            Die(DeathReason.ElevatorSmoke);
        }

        // ───────── 헬퍼 ─────────
        private void SetDoorOpen(bool open)
        {
            doorOpen = open;
            if (door != null) door.SetOpen(open);
        }

        private static float FireProximity(Vector3 from, float radius)
        {
            float best = 0f;
            foreach (FireSource f in FireSource.All)
            {
                if (f == null || !f.IsLit) continue;
                float d = Vector3.Distance(from, f.transform.position);
                best = Mathf.Max(best, 1f - Mathf.Clamp01(d / Mathf.Max(0.1f, radius)));
            }
            return best;
        }

        private static void Die(DeathReason reason)
        {
            if (DeathAndEnding.Instance != null) DeathAndEnding.Instance.Die(reason);
            else EventBus.RaisePlayerDied(reason);
        }

        private static void Count(string key) { if (ScoreSystem.Instance != null) { ScoreSystem.Instance.Increment(key); ScoreSystem.Instance.Increment("risk_attempts_total"); } }
        private static void Record(string key, float v) { if (ScoreSystem.Instance != null) ScoreSystem.Instance.Record(key, v); }
    }
}
