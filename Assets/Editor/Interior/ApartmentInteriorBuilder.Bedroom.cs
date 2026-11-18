using UnityEngine;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;

namespace GameUI.EditorTools.Interior
{
    // 좌표계: x = 동쪽, z = 북쪽, y = 위. 벽 안쪽 면: 서쪽 x=0.125 / 침실-거실 경계 x=2.525(침실쪽) / 북쪽 z=7.275
    // yaw: 0 → 앞면이 +Z, 90 → +X, 180 → -Z, 270 → -X
    public static partial class ApartmentInteriorBuilder
    {
        // ═════════════════════════ 침실 (시작 위치 1.7, 4.0) ═════════════════════════
        private static void BuildBedroom()
        {
            Transform r = Room("Bedroom");

            // 침대: 머리판이 북쪽. 침대 상호작용(bed_bedroom) 위치(발치 쪽 z=5.0)가 이불 위에 오도록 기존 위치 유지
            Furn("Bed", r, 0.88f, 5.6f, 180f);
            // 협탁 + 스탠드 + 알람시계(alarm_clock 이 협탁 위 y=0.58 에 그대로 놓인다) + 휴대폰 충전
            Furn("Nightstand", r, 1.85f, 5.0f, 180f);
            Furn("TableLamp", r, 1.85f, 5.13f, 0f, 0.52f);
            Furn("PhoneCharger", r, 1.72f, 4.9f, 20f, 0.52f);
            // 알람시계 윗면 종 (상호작용 박스 위의 장식)
            Prim.Sph(r, "AlarmBell_L", new Vector3(1.81f, 0.655f, 5.0f), Vector3.one * 0.045f, M.MetalDark);
            Prim.Sph(r, "AlarmBell_R", new Vector3(1.89f, 0.655f, 5.0f), Vector3.one * 0.045f, M.MetalDark);

            // 옷장(문 2개): closet_bedroom 상호작용 박스와 같은 자리, 앞면이 동쪽(+x)
            // 콜라이더는 closet_bedroom(x 0.05~0.65)이 담당 → 앞면(x 0.66)을 그 면에 맞추고 뒷면은 벽 속으로 숨긴다
            UseInteractableCollider(Furn("Wardrobe", r, 0.36f, 3.3f, 90f));

            // 책상 + 의자 + 모니터 + 책 (pickup_key 가 책상 위 2.15, 6.6 에 그대로 놓인다)
            Furn("Desk", r, 2.15f, 6.6f, 180f);
            Furn("DeskChair", r, 2.1f, 5.95f, 0f);
            Furn("Monitor", r, 2.3f, 6.78f, 180f, 0.74f);
            Furn("Keyboard", r, 2.1f, 6.45f, 180f, 0.74f);
            Furn("BookStack", r, 1.88f, 6.62f, 15f, 0.74f);
            Furn("Cup", r, 2.42f, 6.45f, 0f, 0.74f);
            GameObject postit = Furn("PostIt", r, 2.48f, 6.765f, 180f, 0.98f);
            postit.name = "PostIt_Monitor";
            GameObject clothes = Furn("HungClothes", r, 2.1f, 5.72f, 0f, 0.82f);   // 의자에 걸친 옷가지
            clothes.name = "Clothes_Chair";

            // 러그: 시작 위치 아래
            Furn("Rug", r, new Vector3(1.55f, 0f, 3.9f), 0f, new Vector3(1.4f, 1f, 1.0f), "Rug_Bedroom");

            // 커튼 + 창문(window_bedroom: x 0.6~2.0 에 놓인 창). 창 잠금장치가 가려지지 않게 양옆으로 모아 둔다
            Transform curtain = Group("Curtain_Bedroom", r);
            GameObject cl = Furn("CurtainPanel", curtain, 0.42f, 7.2f, 0f, 0.12f, "Panel_L");
            GameObject cr = Furn("CurtainPanel", curtain, 2.2f, 7.2f, 0f, 0.12f, "Panel_R");
            Prim.Cyl(curtain, "Rod", new Vector3(1.3f, 2.2f, 7.22f), 0.012f, 2.4f, M.Metal, new Vector3(0, 0, 90f));
            Prim.Sph(curtain, "RodEnd_L", new Vector3(0.09f, 2.2f, 7.22f), Vector3.one * 0.04f, M.MetalDark);
            Prim.Sph(curtain, "RodEnd_R", new Vector3(2.51f, 2.2f, 7.22f), Vector3.one * 0.04f, M.MetalDark);

            // 벽 장식: 액자 2, 달력, 벽시계
            OnWall("FrameLandscape", r, new Vector3(0.142f, 1.55f, 5.6f), 90f, "Frame_Bed");
            OnWall("FramePortrait", r, new Vector3(0.142f, 1.6f, 4.75f), 90f, "Frame_Portrait");
            OnWall("Calendar", r, new Vector3(2.51f, 1.5f, 4.55f), 270f, "Calendar_Bedroom");
            OnWall("PostIt", r, new Vector3(2.508f, 1.62f, 4.78f), 270f, "PostIt_Wall");
            OnWall("WallClock", r, new Vector3(2.51f, 1.95f, 6.0f), 270f, "Clock_Bedroom");
        }
    }
}
