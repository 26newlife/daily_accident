using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 1(전시 상황) 시작 훅. 집은 그대로 두고, 시나리오 시작 시에만:
    ///   · (화재 소품 숨김은 ScenarioDefinition.useFireProps=false 로 ScenarioRunner 가 처리한다 — 여기서는 하지 않는다)
    ///   · 시나리오 전용 HUD 를 켠다 (WarHud)
    ///   · 아이템 사용 이벤트(요오드화 칼륨 복용 / 방독면 착용)를 구독한다
    /// </summary>
    public class S01Bootstrap : MonoBehaviour, IScenarioBootstrap
    {
        [SerializeField] private WarHud hud;

        public void OnScenarioLoaded()
        {
            WarUtilities.Reset();       // 정전·단수 상태 초기화 (재시작 안전)
            if (hud != null) hud.gameObject.SetActive(true);
            EventBus.ScenarioEventRaised -= OnEvent;
            EventBus.ScenarioEventRaised += OnEvent;
        }

        public void OnScenarioUnloading()
        {
            EventBus.ScenarioEventRaised -= OnEvent;
        }

        private void OnDestroy() { EventBus.ScenarioEventRaised -= OnEvent; }

        private static void OnEvent(ScenarioEvent e)
        {
            WarState s = WarState.Instance;
            if (s == null) return;
            if (e.Type == ScenarioEventType.Toggled && e.State && System.Array.IndexOf(WarKeys.Windows, e.SourceId) >= 0) { WarWindows.OnWindowOpened(e.SourceId); return; }
            if (e.Type == ScenarioEventType.ItemUsed && e.SourceId == WarKeys.ItemIodine)
            {
                if (s.IodineTaken) { EventBus.ShowMessage("이미 복용했다."); return; }
                s.TakeIodine();
                InventorySystem.Instance?.ConsumeUse(WarKeys.ItemIodine);
                EventBus.ShowMessage(s.IodineBeforeFallout ? "요오드화 칼륨을 복용했다. 갑상선이 방사성 요오드를 덜 흡수한다." : "요오드화 칼륨을 복용했다. 하지만 이미 피폭이 시작된 뒤라 효과가 적다.");
            }
            else if (e.Type == ScenarioEventType.ItemEquipped && e.SourceId == WarKeys.ItemGasMask)
            {
                ScenarioFlags.Set(WarKeys.FlagMaskOn, e.State);
                EventBus.ShowMessage(e.State ? "방독면을 착용했다. 숨쉬기가 답답하다." : "방독면을 벗었다.");
                s.Poke();
            }
        }
    }
}
