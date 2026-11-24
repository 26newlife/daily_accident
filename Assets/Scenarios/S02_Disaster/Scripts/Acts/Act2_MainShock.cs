using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>Act 2 — 본진: 큰 흔들림, 정전(비상등만), 창문 파손, 머리 위 낙하물</summary>
    public class Act2_MainShock : QuakeActBase
    {
        [SerializeField] private string[] windowsToBreak = { "window_living", "window_kitchen" };
        [SerializeField] private AudioClip glassBreak;

        protected override void OnEnter()
        {
            base.OnEnter();
            StartOutage();
        }

        private void StartOutage()
        {
            HouseController h = HouseController.Instance;
            if (h == null) return;
            h.SetBlackout(true);
            h.SetEmergencyLights(true);
            EventBus.ShowSubtitle("(쾅! 훨씬 더 큰 흔들림이 덮쳤다! 불이 모두 꺼졌다!)", 4f);
            foreach (string id in windowsToBreak) h.SetWindowState(id, WindowState.Broken);
            QuakeFx.Play2D(glassBreak, 0.8f);
        }
    }
}
