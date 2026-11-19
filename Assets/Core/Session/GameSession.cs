using System;
using GameData;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 씬이 바뀌어도 유지되는 세션 정보. 메인 메뉴에서 고른 시나리오를 보관하며,
    /// GameScene에서는 GameSession.SelectedScenario 로 읽으면 된다.
    /// </summary>
    public static class GameSession
    {
        public static ScenarioData SelectedScenario { get; private set; }
        public static bool HasSelection => SelectedScenario != null;

        /// <summary>선택된 시나리오가 바뀔 때 호출 (해제 시 null)</summary>
        public static event Action<ScenarioData> ScenarioChanged;

        /// <summary>시나리오 선택. 잠긴 시나리오나 null은 무시한다.</summary>
        public static void SelectScenario(ScenarioData scenario)
        {
            if (scenario == null || !scenario.IsUnlocked) return;
            if (SelectedScenario == scenario) return;
            SelectedScenario = scenario;
            ScenarioChanged?.Invoke(scenario);
        }

        public static void ClearSelection()
        {
            if (SelectedScenario == null) return;
            SelectedScenario = null;
            ScenarioChanged?.Invoke(null);
        }

        // 에디터에서 Domain Reload를 끄고 플레이해도 이전 플레이의 값이 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SelectedScenario = null;
            ScenarioChanged = null;
        }
    }
}
