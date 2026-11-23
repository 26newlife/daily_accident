using UnityEngine;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;

namespace GameUI.EditorTools.Interior
{
    // 좌표계: x = 동쪽, z = 북쪽, y = 위. 벽 안쪽 면: 서쪽 x=0.125 / 침실-거실 경계 x=2.525(침실쪽) / 북쪽 z=7.275
    // yaw: 0 → 앞면이 +Z, 90 → +X, 180 → -Z, 270 → -X
    // 배치 원칙(ClearanceSpec): 가구는 벽에 붙이고 방 중앙을 비운다. 통로는 보조 0.7m 이상, 틈은 0cm(밀착) 아니면 0.7m 이상.
    public static partial class ApartmentInteriorBuilder
    {
        // ═════════════════════════ 침실 (시작 위치 1.7, 4.0) ═════════════════════════
        // 문(경첩 2.6, 2.875)은 침실 안쪽으로 열린다: 반경 0.85+0.3 안(x≥1.45, z 2.9~4.0)은 비운다.
        // 창문(window_bedroom x 0.6~2.0): 창 앞 0.8m 가 비도록 침대·책상을 서쪽 벽으로 몰았다.
        private static void BuildBedroom()
        {
            Transform r = Room("Bedroom");

            // 침대(폭 1.2 = 싱글~슈퍼싱글): 서쪽 벽에 붙이고 머리판이 북쪽, 발치는 옷장 끝(z 3.95)과 맞닿게. 동쪽 통로 0.8m
            Furn("Bed", r, new Vector3(0.725f, 0f, 4.95f), 180f, new Vector3(0.8f, 1f, 1f));
            MoveInteractable("bed_bedroom", new Vector3(0.725f, 0.65f, 4.7f));       // 침대 상호작용(이불 위)도 침대와 같이 이동
            Transform bedVisual = Interactable("bed_bedroom") != null ? Interactable("bed_bedroom").Find("Visual") : null;
            if (bedVisual != null) bedVisual.localScale = new Vector3(1.0f, 0.1f, 0.8f);   // 침대 폭에 맞춰 콜라이더도 줄임

            // 협탁: 침대 머리 쪽 동편에 밀착. 알람시계는 협탁 위로 같이 이동 (소리 위치 AlarmAudio 포함)
            Furn("Nightstand", r, 1.525f, 5.75f, 180f);
            MoveInteractable("alarm_clock", new Vector3(1.525f, 0.58f, 5.75f));
            Transform alarmAudio = level.Find("AlarmAudio");
            if (alarmAudio != null) alarmAudio.position = new Vector3(1.525f, 0.6f, 5.75f);
            Furn("TableLamp", r, 1.525f, 5.86f, 0f, 0.52f);
            Furn("PhoneCharger", r, 1.45f, 5.62f, 20f, 0.52f);
            Prim.Sph(r, "AlarmBell_L", new Vector3(1.485f, 0.655f, 5.75f), Vector3.one * 0.045f, M.MetalDark);
            Prim.Sph(r, "AlarmBell_R", new Vector3(1.565f, 0.655f, 5.75f), Vector3.one * 0.045f, M.MetalDark);

            // 옷장(문 2개): closet_bedroom 상호작용 박스와 같은 자리, 앞면이 동쪽(+x). 앞 0.8m 비움
            // 콜라이더는 closet_bedroom(x 0.05~0.65)이 담당 → 앞면(x 0.66)을 그 면에 맞추고 뒷면은 벽 속으로 숨긴다
            UseInteractableCollider(Furn("Wardrobe", r, 0.36f, 3.3f, 90f));

            // 책상: 서쪽 벽(침대 머리 뒤)에 밀착, 창 앞 영역 밖. 열쇠(pickup_key)도 책상 위로 같이 이동
            Furn("Desk", r, 0.4f, 6.405f, 90f);
            MoveInteractable("pickup_key", new Vector3(0.5f, 0.8f, 6.2f));
            Furn("Monitor", r, 0.25f, 6.52f, 90f, 0.74f);
            Furn("Keyboard", r, 0.42f, 6.55f, 90f, 0.74f);
            Furn("BookStack", r, 0.3f, 6.12f, 15f, 0.74f);
            Furn("DeskLamp", r, 0.22f, 6.72f, 90f, 0.74f);      // 참고 이미지: 책상 위 검은 스탠드
            OnWall("AirConditioner", r, new Vector3(0.225f, 2.02f, 5.6f), 90f, "AirCon_Bedroom");   // 참고 이미지: 벽걸이 에어컨
            Furn("Cup", r, 0.58f, 6.7f, 0f, 0.74f);
            Furn("PostIt", r, 0.27f, 6.7f, 90f, 0.98f, "PostIt_Monitor");
            // 의자: 책상 밑으로 밀어 넣어 통로로 튀어나오지 않게
            Furn("DeskChair", r, 0.62f, 6.405f, 270f);
            Furn("HungClothes", r, 0.86f, 6.405f, 90f, 0.82f, "Clothes_Chair");

            // 러그: 시작 위치 옆 (콜라이더 없음)
            Furn("Rug", r, new Vector3(1.75f, 0f, 3.55f), 0f, new Vector3(1.2f, 1f, 1.0f), "Rug_Bedroom");

            // 커튼 + 창문: 창 잠금장치가 가려지지 않게 양옆으로 모은다 (콜라이더 없음)
            Transform curtain = Group("Curtain_Bedroom", r);
            Furn("CurtainPanel", curtain, 0.42f, 7.2f, 0f, 0.12f, "Panel_L");
            Furn("CurtainPanel", curtain, 2.2f, 7.2f, 0f, 0.12f, "Panel_R");
            Prim.Cyl(curtain, "Rod", new Vector3(1.3f, 2.2f, 7.22f), 0.012f, 2.4f, M.Metal, new Vector3(0, 0, 90f));
            Prim.Sph(curtain, "RodEnd_L", new Vector3(0.09f, 2.2f, 7.22f), Vector3.one * 0.04f, M.MetalDark);
            Prim.Sph(curtain, "RodEnd_R", new Vector3(2.51f, 2.2f, 7.22f), Vector3.one * 0.04f, M.MetalDark);

            // 벽 장식: 액자 2, 달력, 포스트잇, 벽시계
            OnWall("FrameLandscape", r, new Vector3(0.142f, 1.55f, 4.95f), 90f, "Frame_Bed");
            OnWall("FramePortrait", r, new Vector3(0.142f, 1.5f, 6.4f), 90f, "Frame_Portrait");
            OnWall("Calendar", r, new Vector3(2.51f, 1.5f, 4.55f), 270f, "Calendar_Bedroom");
            OnWall("PostIt", r, new Vector3(2.508f, 1.62f, 4.78f), 270f, "PostIt_Wall");
            OnWall("WallClock", r, new Vector3(2.51f, 1.95f, 6.0f), 270f, "Clock_Bedroom");
        }
    }
}
