using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 3(아파트 화재) 시작 훅. ScenarioRunner 가 집 참조를 복원하고 내용물을 활성화한 직후 호출한다.
    ///   · 집의 중립 구역에 시나리오 규칙을 입힌다 (계단실·대피공간·엘리베이터 내부는 격리 구역 + 사망 사유)
    ///   · 디버그 패널 확장 등록 (에디터/개발 빌드)
    /// 집 에셋은 이 값들을 모른다 — 시나리오가 시작할 때만 덧붙인다.
    /// </summary>
    public class S03Bootstrap : MonoBehaviour, IScenarioBootstrap
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static S03DebugExtension DebugExt { get; private set; }
#endif

        public void OnScenarioLoaded()
        {
            Configure(EvacKeys.ZoneStairwell, true, 1f, DeathReason.StairwellSmoke);
            Configure(EvacKeys.ZoneShelter, true, 1f, DeathReason.ShelterSmoke);
            Configure("zone_elevator_cabin", true, 1f, DeathReason.ElevatorStallSmoke);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugExt = new S03DebugExtension();
            DebugPanel.Register(DebugExt);
#endif
        }

        public void OnScenarioUnloading()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugExt != null) DebugPanel.Unregister(DebugExt);
            DebugExt = null;
#endif
        }

        private static void Configure(string zoneId, bool isolated, float crouchScale, DeathReason reason)
        {
            HazardZone z = HazardManager.Instance != null ? HazardManager.Instance.FindZone(zoneId) : null;
            if (z == null) { Debug.LogWarning($"[S03] 집 구역 '{zoneId}' 를 찾지 못했습니다."); return; }
            z.Isolated = isolated;
            z.SetCrouchSmokeScale(crouchScale);
            z.SetSuffocationReason(reason);
        }
    }
}
