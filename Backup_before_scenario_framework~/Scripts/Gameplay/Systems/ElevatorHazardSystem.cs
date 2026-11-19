using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 엘리베이터 진입점(호환 래퍼). 실제 상태 머신은 ElevatorController(Gameplay/Elevator) 가 담당한다.
    /// 기존 호출부(ElevatorInteractable, 할머니 NPC, 디버그)가 쓰는 API 와 FireMode 판정/플래그 이름은 그대로 유지한다.
    /// 화재 판정: Act 2 폭발 때 켜지는 "blackout" 플래그 또는 불이 붙은 FireSource 가 있으면 화재 후.
    /// </summary>
    public class ElevatorHazardSystem : MonoBehaviour
    {
        /// <summary>Act 2 폭발 때 true 로 설정되는 "정전/화재 중" 플래그</summary>
        public const string FireModeFlag = "blackout";
        public const string FireStartTimeKey = "blackout_time";

        [SerializeField] private ElevatorHazardConfig config;

        public static ElevatorHazardSystem Instance { get; private set; }
        public ElevatorHazardConfig Config => config;

        /// <summary>씬에 시스템이 없어도(빌더 미실행 등) 만들어 둔다 (컨트롤러가 없으면 호출은 아무 일도 하지 않는다)</summary>
        public static ElevatorHazardSystem GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("ElevatorHazardSystem");
            return go.AddComponent<ElevatorHazardSystem>();
        }

        public static void ResetRunState() { Instance = null; ElevatorController.ResetRunState(); }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static bool FireMode
        {
            get
            {
                if (ScenarioFlags.GetBool(FireModeFlag)) return true;
                foreach (FireSource f in FireSource.All) if (f != null && f.IsLit) return true;
                return false;
            }
        }

        public void CallElevator(Transform at)
        {
            ElevatorController c = ElevatorController.Instance != null ? ElevatorController.Instance : FindFirstObjectByType<ElevatorController>();
            if (c != null) c.Call(at);
            else Debug.LogWarning("[Elevator] ElevatorController 가 씬에 없습니다. Tools > GameUI > Build Apartment Scene 을 실행하세요.");
        }

        public void Board()
        {
            ElevatorController c = ElevatorController.Instance != null ? ElevatorController.Instance : FindFirstObjectByType<ElevatorController>();
            if (c != null) c.Board();
        }
    }
}
