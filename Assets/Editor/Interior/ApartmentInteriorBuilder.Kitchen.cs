using UnityEngine;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;

namespace GameUI.EditorTools.Interior
{
    public static partial class ApartmentInteriorBuilder
    {
        // ═════════════════════════ 주방 (x 3.975~6.475, z 2.475~4.525) ═════════════════════════
        // 고정된 상호작용: 가스레인지(6.3,3.0) / 프라이팬 / 후드(6.35,1.85,3.0) / 전자레인지(6.3,3.55) / 생수(6.3,3.95) /
        // 커피포트(6.3,4.25) / 정수기(4.15,1.1,4.4) / 퓨즈콕(4.1,0.6,4.5)
        // 작업 동선: 하부장 앞(x 5.88)~싱크 반도(x 5.12) 0.76m, 냉장고~반도 사이 0.95m. 다용도실 문 앞(x 4.65~5.85, z 2.4~3.4)은 비운다.
        private static void BuildKitchen()
        {
            Transform r = Room("Kitchen");

            // 동쪽 벽 하부장: 다용도실 문 열림 반경 밖으로 z 2.7~4.4 (길이 1.7). 앞면이 서쪽
            Furn("KitchenCounter", r, new Vector3(6.18f, 0f, 3.55f), 270f, new Vector3(0.944f, 1f, 1f));

            // 가스레인지 버너 + 식용유 팬(손잡이, 기름) — gas_range/frying_pan 상호작용 박스 위 장식
            foreach (float dz in new[] { -0.14f, 0.14f })
                foreach (float dx in new[] { -0.12f, 0.12f })
                    Prim.Cyl(r, "Burner", new Vector3(6.3f + dx, 1.003f, 3.0f + dz), 0.07f, 0.008f, M.MetalDark);
            Prim.Box(r, "PanHandle", new Vector3(6.06f, 1.04f, 3.0f), new Vector3(0.2f, 0.02f, 0.04f), M.MetalDark);
            Prim.Cyl(r, "PanOil", new Vector3(6.3f, 1.056f, 3.0f), 0.13f, 0.006f, M.Oil);

            // 후드 연통, 전자레인지 문/손잡이 장식
            Prim.Box(r, "HoodDuct", new Vector3(6.33f, 2.19f, 3.0f), new Vector3(0.28f, 0.45f, 0.28f), M.Metal);
            Prim.Box(r, "MicrowaveGlass", new Vector3(6.095f, 1.05f, 3.5f), new Vector3(0.01f, 0.18f, 0.26f), M.Screen);
            Prim.Box(r, "MicrowavePanel", new Vector3(6.095f, 1.04f, 3.66f), new Vector3(0.01f, 0.2f, 0.07f), M.MetalDark);
            Prim.Box(r, "MicrowaveHandle", new Vector3(6.085f, 1.04f, 3.62f), new Vector3(0.02f, 0.16f, 0.015f), M.Chrome);

            // 참고 이미지: 하부장 위 벽선반 + 식기건조대. 생수/커피포트(y~1.1) 위, 후드(z 2.65~3.35) 옆 z 3.5~4.4
            Furn("DishShelf", r, new Vector3(6.375f, 1.55f, 3.95f), 270f, Vector3.one, "DishShelf_Kitchen");

            // 싱크대(수도꼭지): 북쪽 개구부 쪽 반도형(길이 0.72). 뒷면(거실 쪽)이 식탁처럼 쓰인다. 개구부는 동쪽에 0.76m 남는다
            Furn("SinkUnit", r, new Vector3(4.76f, 0f, 4.275f), 180f, new Vector3(0.8f, 1f, 0.9f));
            MoveInteractable("pickup_water_pot", new Vector3(4.7f, 0.1f, 3.8f));        // 바닥 냄비: 반도 앞 통로 밖으로 살짝 이동
            Furn("Cup", r, 4.9f, 4.2f, 0f, 0.9f);

            // 냉장고(폭 0.65, 상냉장 하냉동): 남서쪽 모서리에 밀착 + 냉장고 위 상부장. 달력/메모는 냉장고 문에
            Furn("Fridge", r, new Vector3(4.3f, 0f, 2.775f), 0f, new Vector3(0.929f, 1f, 1f));
            Furn("UpperCabinet", r, 4.45f, 2.65f, 0f, 1.85f);
            OnWall("Calendar", r, new Vector3(4.15f, 1.45f, 3.106f), 0f, "Calendar_Kitchen");
            OnWall("PostIt", r, new Vector3(4.45f, 1.55f, 3.104f), 0f, "PostIt_Fridge");
            // 쓰레기통은 다용도실로 옮겼다 (주방은 동선 폭 때문에 둘 곳이 없다)
        }
    }
}
