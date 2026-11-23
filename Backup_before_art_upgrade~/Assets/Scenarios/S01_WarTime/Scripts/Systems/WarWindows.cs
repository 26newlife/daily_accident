using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 창문 관련 시나리오 1 규칙 (집 에셋은 수정하지 않는다).
    ///   · 창을 열어 밖을 보면 상황을 관찰한다 (거실 창: 군 철수 정황 = 기만 단서)
    ///   · 충격파가 오면 모든 창이 깨진다 (HouseController.SetWindowState)
    /// </summary>
    public static class WarWindows
    {
        public static void OnWindowOpened(string windowId)
        {
            WarState s = WarState.Instance;
            if (s == null || s.Config == null) return;
            if (s.Phase == WarPhase.Fallout || s.Phase == WarPhase.Rescue) { EventBus.ShowMessage("창밖은 온통 잿빛이다. 검은 비가 흩날린다."); return; }
            if (windowId == "window_living" && s.AddClue(WarKeys.ClueWithdrawal))
            {
                EventBus.ShowSubtitle(s.Config.withdrawalClueText, 5f);
                WarHud.Instance?.Notice("군이 후방으로 빠지고 있다… 공식 발표와 다르다. (기만 단서 확보)", 5f);
                return;
            }
            EventBus.ShowMessage("멀리서 연기가 피어오르고, 하늘을 가르는 전투기 소리가 이어진다.");
        }

        public static void BreakAll()
        {
            HouseController house = HouseController.Instance;
            foreach (string id in WarKeys.Windows) if (house != null) house.SetWindowState(id, WindowState.Broken);
            WarState.Instance?.MarkAllWindowsBroken();
        }
    }
}
