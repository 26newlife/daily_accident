using UnityEngine;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;

namespace GameUI.EditorTools.Interior
{
    public static partial class ApartmentInteriorBuilder
    {
        // ═════════════════════════ 거실 (x 2.675~6.475, z 4.6~7.275) + 복도 홀(x 2.675~3.825, z 2.4~4.6) ═════════════════════════
        // 거실 중앙(x 3.0~5.5)과 창/베란다 문 앞(z 6.4~7.3)은 비운다. 홀→거실 입구는 TV장 모서리와 벽 끝 사이 0.9m 를 지키도록 TV장을 얕게(0.28m) 만들었다.
        private static void BuildLiving()
        {
            Transform r = Room("Living");

            // TV 장 + TV: 서쪽 벽. 장을 1.2m x 0.28m 로 줄였다 (tv_living 상호작용 박스는 렌더러만 숨기고 콜라이더를 유지)
            Furn("TVStand", r, new Vector3(2.84f, 0f, 5.65f), 90f, new Vector3(0.75f, 1f, 0.7f));
            Furn("TV", r, 2.84f, 5.6f, 90f, 0.5f);
            OnWall("WallClock", r, new Vector3(2.69f, 1.95f, 5.6f), 90f, "Clock_Living");   // TV 위 벽시계
            // 거실 스위치는 소파 뒤로 가려지지 않게 홀 쪽 벽(TV장 남쪽)으로 옮긴다 (거실 조명 LightGroup 연결은 그대로)
            MoveInteractable("switch_light_living", new Vector3(2.695f, 1.2f, 4.85f));

            // 소파(폭 2.0): 동쪽 벽에 밀착(등받이 뒤 틈 0). 소파 불(fire_sofa)도 소파 위로 같이 이동
            Furn("Sofa", r, 6.025f, 5.975f, 270f);
            MoveInteractable("fire_sofa", new Vector3(6.025f, 0.6f, 5.975f));
            GameObject c1 = Furn("Cushion", r, 6.14f, 5.275f, 250f, 0.42f, "Cushion_1");
            GameObject c2 = Furn("Cushion", r, 6.14f, 6.675f, 290f, 0.42f, "Cushion_2");
            LinkFire(c1, "fire_sofa"); LinkFire(c2, "fire_sofa");

            // 커피 테이블: 0.8 x 0.5 로 축소, 소파 앞 0.475m(발 놓는 공간). pickup_duct_tape 도 테이블 위로 같이 이동
            Furn("CoffeeTable", r, new Vector3(4.85f, 0f, 5.975f), 90f, new Vector3(0.8f, 1f, 0.833f));
            MoveInteractable("pickup_duct_tape", new Vector3(4.85f, 0.45f, 6.15f));
            Furn("Remote", r, 4.75f, 5.6f, 0f, 0.42f);
            Furn("Cup", r, 4.95f, 5.62f, 0f, 0.42f);
            Furn("Newspaper", r, 4.85f, 5.9f, 90f, 0.42f);
            Furn("Rug", r, new Vector3(4.4f, 0f, 5.7f), 0f, new Vector3(1.9f, 1f, 1.7f), "Rug_Living");

            // 책장: 소파 북쪽 끝에 밀착(북쪽 벽), 스탠드 조명은 책장과 베란다 문 사이 빈틈에, 화분은 TV장 끝에 밀착
            Furn("Bookshelf", r, new Vector3(6.025f, 0f, 7.125f), 180f, new Vector3(0.9f, 1f, 1f));
            Furn("FloorLamp", r, 5.375f, 6.95f);
            Furn("Plant", r, 2.93f, 6.42f);
            OnWall("FramePortrait", r, new Vector3(6.458f, 1.55f, 5.975f), 270f, "Frame_Living");

            // 커튼 — 화재 때 불붙는 대상이므로 따로 분리된 오브젝트. 거실 창(2.7~3.7) + 베란다 문(3.85~5.15) 양끝에 모음
            Transform curtain = Group("Curtain_Living", r);
            GameObject p1 = Furn("CurtainPanel", curtain, 2.97f, 7.2f, 0f, 0.12f, "Panel_L");
            GameObject p2 = Furn("CurtainPanel", curtain, 4.9f, 7.2f, 0f, 0.12f, "Panel_R");
            LinkFire(p1, "fire_curtain"); LinkFire(p2, "fire_curtain");
            Prim.Cyl(curtain, "Rod", new Vector3(3.95f, 2.2f, 7.22f), 0.012f, 2.6f, M.Metal, new Vector3(0, 0, 90f));

            // 식탁 세트(식탁+의자 2)는 거실 창·베란다 문 동선과 겹쳐서 제거했다. 주방 반도형 싱크대 뒷면이 식탁 역할을 한다.
        }
    }
}
