using GameData;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// ScenarioDefinition 의 기능 플래그를 시나리오 시작 시 적용한다 (ScenarioRunner 가 시나리오 내용물 활성화 전에 호출).
    ///   · Core 가 아는 것: 호흡기 게이지 HUD, 가스 누출 시스템, 화재 확산 시스템
    ///   · 집(베이스)이 처리하는 것(화재 소품·도어 스코프·창밖 보행자/차량): HouseServices.Environment.ApplyScenarioFeatures
    ///   · 엘리베이터 위험은 시나리오 컴포넌트(ElevatorHazardSystem)가 시나리오 쪽 부트스트랩에서 스스로 확인한다
    /// 베이스 프리팹은 바뀌지 않는다 — 기능은 켜고 끄는 상태일 뿐이다.
    /// </summary>
    public static class ScenarioFeatures
    {
        public static void Apply(ScenarioDefinition def)
        {
            if (def == null) return;

            HUD hud = Object.FindFirstObjectByType<HUD>(FindObjectsInactive.Include);
            if (hud != null) hud.SetVitalsAllowed(def.UseRespiratoryGauge);

            // 시나리오 씬 내용물은 아직 비활성이다 (Awake 전). enabled 를 꺼 두면 활성화 후에도 동작하지 않는다.
            if (!def.UseGasHazard)
                foreach (GasLeakController g in Object.FindObjectsByType<GasLeakController>(FindObjectsInactive.Include)) g.enabled = false;
            if (!def.UseFireProps)
                foreach (FireManager m in Object.FindObjectsByType<FireManager>(FindObjectsInactive.Include)) m.enabled = false;

            if (HouseServices.Environment != null) HouseServices.Environment.ApplyScenarioFeatures(def);
            Debug.Log($"[Scenario] '{def.Id}' 기능 플래그: 호흡기={def.UseRespiratoryGauge} 가스={def.UseGasHazard} 화재소품={def.UseFireProps} 엘리베이터위험={def.UseElevatorHazard} 도어스코프={def.UseDoorPeephole} 창밖교통={def.UseOutdoorTraffic}");
        }
    }
}
