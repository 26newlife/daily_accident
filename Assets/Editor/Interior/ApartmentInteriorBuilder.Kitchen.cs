using UnityEngine;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;

namespace GameUI.EditorTools.Interior
{
    public static partial class ApartmentInteriorBuilder
    {
        // ═════════════════════════ 주방 (x 3.975~6.475, z 2.475~4.525) ═════════════════════════
        // 고정된 상호작용: 가스레인지(6.3,3.0) / 프라이팬 / 후드(6.35,1.85,3.0) / 전자레인지(6.3,3.55) / 생수(6.3,3.95) /
        // 커피포트(6.3,4.25) / 정수기(4.15,1.1,4.4) / 퓨즈콕(4.1,0.6,4.5) / 냄비물(4.7,0.1,3.9)
        private static void BuildKitchen()
        {
            Transform r = Room("Kitchen");

            // 동쪽 벽 하부장 (위에 가스레인지·전자레인지·생수·커피포트가 올라가 있다). 앞면이 서쪽
            Furn("KitchenCounter", r, 6.18f, 3.5f, 270f);

            // 가스레인지 버너 (gas_range 상호작용 박스 윗면 장식) + 식용유 팬(손잡이, 기름)
            foreach (float dz in new[] { -0.14f, 0.14f })
                foreach (float dx in new[] { -0.12f, 0.12f })
                    Prim.Cyl(r, "Burner", new Vector3(6.3f + dx, 1.003f, 3.0f + dz), 0.07f, 0.008f, M.MetalDark);
            Prim.Box(r, "PanHandle", new Vector3(6.06f, 1.04f, 3.0f), new Vector3(0.2f, 0.02f, 0.04f), M.MetalDark);
            Prim.Cyl(r, "PanOil", new Vector3(6.3f, 1.056f, 3.0f), 0.13f, 0.006f, M.Oil);

            // 후드 연통 (range_hood 상호작용 박스 위로 천장까지)
            Prim.Box(r, "HoodDuct", new Vector3(6.33f, 2.19f, 3.0f), new Vector3(0.28f, 0.45f, 0.28f), M.Metal);

            // 전자레인지 앞면 (microwave 상호작용 박스에 문/손잡이 장식)
            Prim.Box(r, "MicrowaveGlass", new Vector3(6.095f, 1.05f, 3.5f), new Vector3(0.01f, 0.18f, 0.26f), M.Screen);
            Prim.Box(r, "MicrowavePanel", new Vector3(6.095f, 1.04f, 3.66f), new Vector3(0.01f, 0.2f, 0.07f), M.MetalDark);
            Prim.Box(r, "MicrowaveHandle", new Vector3(6.085f, 1.04f, 3.62f), new Vector3(0.02f, 0.16f, 0.015f), M.Chrome);

            // 싱크대(수도꼭지 포함). 주방 중앙에서 반도형으로 놓아 냉장고·정수기·퓨즈콕 동선과 겹치지 않게 한다
            Furn("SinkUnit", r, 5.1f, 4.0f, 270f);
            Furn("Cup", r, 5.05f, 3.78f, 0f, 0.9f);

            // 냉장고(상냉장 하냉동) + 냉장고 위 상부장. 달력/메모는 냉장고 문에
            Furn("Fridge", r, 4.35f, 2.8f, 0f);
            Furn("UpperCabinet", r, 4.475f, 2.65f, 0f, 1.85f);
            OnWall("Calendar", r, new Vector3(4.18f, 1.45f, 3.106f), 0f, "Calendar_Kitchen");
            OnWall("PostIt", r, new Vector3(4.5f, 1.55f, 3.104f), 0f, "PostIt_Fridge");

            // 쓰레기통 (싱크대 반도와 하부장 사이 통로 입구 쪽)
            Furn("TrashBin", r, 5.6f, 3.15f);
        }
    }
}
