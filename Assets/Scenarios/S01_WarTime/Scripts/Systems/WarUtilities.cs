using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 1 기반시설 붕괴 연출: 정전 / 단수.
    /// 집은 건드리지 않고 HouseController 의 공개 API(SetBlackout / SetEmergencyLights / ReplaceInteraction)만 쓴다 — 베이스 오브젝트는 상태만 바뀐다([V] 상태 토글).
    ///   · 정전: 모든 조명 소등 + 주변광 어둡게, 비상구 유도등("exit")만 켠다. 거짓 평화(Act 4)에서 복구된다.
    ///   · 단수: 세수·물 마시기 상호작용이 "물이 나오지 않는다" 로 바뀐다 (판이 끝날 때까지 유지).
    /// 타이밍 값은 WarConfig(waterCutTime / blackoutTime / powerRestoreDelayAct4). 재시작(RunCleanup) 때 상태가 초기화된다.
    /// </summary>
    public static class WarUtilities
    {
        private static readonly string[] SinkIds = { "wash_face", "drink_water" };

        public static bool PowerIsOut { get; private set; }
        public static bool WaterIsCut { get; private set; }

        public static void Reset() { PowerIsOut = false; WaterIsCut = false; RunCleanup.Register(Reset); }

        public static void PowerOut()
        {
            HouseController house = HouseController.Instance;
            if (house == null || PowerIsOut) return;
            PowerIsOut = true;
            house.SetBlackout(true);
            house.SetEmergencyLights(true);
            EventBus.ShowSubtitle("쾅— 어딘가의 송전 시설이 맞았다. 집 안의 불이 일제히 꺼졌다.", 4.5f);
            EventBus.ShowMessage("정전. 손전등 없이는 어둡다. 복도의 비상구 유도등만 희미하게 켜져 있다.");
        }

        public static void PowerRestore()
        {
            HouseController house = HouseController.Instance;
            if (house == null || !PowerIsOut) return;
            PowerIsOut = false;
            house.SetBlackout(false);
            house.SetEmergencyLights(false);
            EventBus.ShowMessage("전기가 들어왔다. 정말 끝난 걸까?");
        }

        public static void CutWater()
        {
            HouseController house = HouseController.Instance;
            if (house == null || WaterIsCut) return;
            WaterIsCut = true;
            foreach (string id in SinkIds)
                house.ReplaceInteraction(id, new InteractableObject.InteractionOverride
                {
                    prompt = () => "수도꼭지 (단수)",
                    onInteract = _ => EventBus.ShowMessage("수도꼭지를 틀었지만 물이 나오지 않는다. 단수다."),
                });
            EventBus.ShowMessage("수돗물이 끊겼다. 남은 물을 아껴야 한다.");
        }
    }
}
