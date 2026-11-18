using UnityEngine;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;

namespace GameUI.EditorTools.Interior
{
    public static partial class ApartmentInteriorBuilder
    {
        // ═════════════════════════ 거실 (x 2.675~6.475, z 4.6~7.275) + 복도 홀(x 2.675~3.9, z 2.4~4.6) ═════════════════════════
        private static void BuildLiving()
        {
            Transform r = Room("Living");

            // TV 장 + TV (tv_living 상호작용 박스는 렌더러만 숨기고 콜라이더를 유지). 멀티탭(3.0, 4.65)은 장 남쪽 끝 바로 옆
            Furn("TVStand", r, 2.9f, 5.6f, 90f);
            Furn("TV", r, 2.88f, 5.6f, 90f, 0.5f);
            OnWall("WallClock", r, new Vector3(2.69f, 1.95f, 5.6f), 90f, "Clock_Living");   // TV 위 벽시계

            // 소파(폭 2.0, 앞면이 서쪽) + 쿠션 2 (불붙을 수 있는 가연물, 소파 불 fire_sofa 와 연결)
            Furn("Sofa", r, 5.55f, 5.6f, 270f);      // 옛 소파 위치(fire_sofa 중심 5.5)와 맞춤 — 불꽃이 소파 위에 보이도록
            GameObject c1 = Furn("Cushion", r, 5.68f, 5.0f, 250f, 0.42f, "Cushion_1");
            GameObject c2 = Furn("Cushion", r, 5.68f, 6.2f, 290f, 0.42f, "Cushion_2");
            LinkFire(c1, "fire_sofa"); LinkFire(c2, "fire_sofa");

            // 커피 테이블 (pickup_duct_tape 가 윗면 4.2, 5.3 에 그대로 놓인다) + 리모컨/컵/신문
            Furn("CoffeeTable", r, 4.2f, 5.5f, 90f);
            Furn("Remote", r, 4.1f, 5.42f, 0f, 0.42f);
            Furn("Cup", r, 4.38f, 5.45f, 0f, 0.42f);
            Furn("Newspaper", r, 4.2f, 5.8f, 90f, 0.42f);
            Furn("Rug", r, new Vector3(4.25f, 0f, 5.75f), 0f, new Vector3(1.9f, 1f, 1.7f), "Rug_Living");

            // 책장 / 스탠드 조명 / 화분 / 벽 액자
            Furn("Bookshelf", r, 5.9f, 7.125f, 180f);
            Furn("FloorLamp", r, 5.25f, 6.95f);
            Furn("Plant", r, 2.93f, 6.95f);
            OnWall("FramePortrait", r, new Vector3(6.458f, 1.55f, 5.6f), 270f, "Frame_Living");

            // 식탁(2인) — 창문 앞이지만 상판이 낮아 창 잠금장치(y 1.2)를 가리지 않는다
            Furn("DiningTable", r, 3.65f, 6.85f);
            Furn("DiningChair", r, 3.4f, 6.15f);
            Furn("DiningChair", r, 3.9f, 6.15f);
            Furn("Cup", r, 3.5f, 6.8f, 0f, 0.75f);
            Furn("Book", r, 3.9f, 6.75f, 25f, 0.75f);

            // 커튼 — 화재 때 불붙는 대상이므로 따로 분리된 오브젝트. 거실 창(2.7~3.7) + 베란다 문(3.85~5.15) 양끝에 모음
            Transform curtain = Group("Curtain_Living", r);
            GameObject p1 = Furn("CurtainPanel", curtain, 2.97f, 7.2f, 0f, 0.12f, "Panel_L");
            GameObject p2 = Furn("CurtainPanel", curtain, 4.9f, 7.2f, 0f, 0.12f, "Panel_R");
            LinkFire(p1, "fire_curtain"); LinkFire(p2, "fire_curtain");
            Prim.Cyl(curtain, "Rod", new Vector3(3.95f, 2.2f, 7.22f), 0.012f, 2.6f, M.Metal, new Vector3(0, 0, 90f));
        }
    }
}
