using UnityEngine;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;

namespace GameUI.EditorTools.Interior
{
    public static partial class ApartmentInteriorBuilder
    {
        // ═════════════════════════ 베란다 (x 0.125~5.325, z 7.525~8.875) ═════════════════════════
        // 고정된 상호작용: 화분 flower_pot(0.45,0.2,8.6) / 경량칸막이 light_partition(서쪽 벽 z 7.75~8.65) /
        // 베란다 문(x 3.85~5.15) / 방화문(x=5.4, z 7.75~8.65 — 대피공간 쪽으로 열림, 베란다 쪽 접근 공간을 비워 둔다)
        private static void BuildBalcony()
        {
            Transform r = Room("Balcony");

            Furn("Planter", r, 1.5f, 8.75f, 180f);
            Furn("Planter", r, 2.2f, 8.75f, 180f, 0f, "Planter_2");
            Furn("OutdoorUnit", r, 3.2f, 8.7f, 180f);              // 에어컨 실외기
            Furn("CrateStack", r, 4.2f, 8.7f, 180f);                // 짐 상자 (방화문 접근 경로 x>4.5 는 비움)

            // 빨래걸이: 천장 고리 + 봉 + 걸린 옷 (콜라이더 없음 — 머리 위 장식)
            Prim.Cyl(r, "LaundryRod", new Vector3(1.9f, 2.0f, 8.3f), 0.012f, 3.0f, M.Metal, new Vector3(0, 0, 90f));
            Prim.Cyl(r, "LaundryHook_L", new Vector3(0.4f, 2.2f, 8.3f), 0.008f, 0.4f, M.Metal);
            Prim.Cyl(r, "LaundryHook_R", new Vector3(3.4f, 2.2f, 8.3f), 0.008f, 0.4f, M.Metal);
            float[] shirtX = { 0.8f, 1.3f, 1.85f, 2.5f };
            float[] shirtYaw = { 5f, -8f, 3f, 10f };
            for (int i = 0; i < shirtX.Length; i++)
                Furn("HangingShirt", r, shirtX[i], 8.3f, shirtYaw[i], 2.0f, "Laundry_" + i);

            // 경량칸막이: 다른 벽과 확연히 다른 연한 합판색 + 테두리 + 안내판 (기존 light_partition 상호작용 박스의 재질만 교체)
            Transform part = Interactable("light_partition");
            if (part != null)
            {
                foreach (Renderer rd in part.GetComponentsInChildren<Renderer>()) rd.sharedMaterial = M.Partition;
                Vector3 c = part.position;
                Prim.Box(r, "PartitionEdge_T", new Vector3(0.2f, 2.02f, c.z), new Vector3(0.2f, 0.04f, 0.94f), M.PartitionEdge);
                Prim.Box(r, "PartitionEdge_S", new Vector3(0.2f, 1.0f, c.z - 0.47f), new Vector3(0.2f, 2.0f, 0.03f), M.PartitionEdge);
                Prim.Box(r, "PartitionEdge_N", new Vector3(0.2f, 1.0f, c.z + 0.47f), new Vector3(0.2f, 2.0f, 0.03f), M.PartitionEdge);
                Prim.Box(r, "PartitionSign", new Vector3(0.235f, 1.3f, c.z), new Vector3(0.01f, 0.14f, 0.32f), M.RedPaint);
            }

            // 베란다 고정 유리창 하부 걸레받이 느낌의 난간 장식은 생략 (벽 조각이 이미 있음)
        }

        // ═════════════════════════ 대피공간 (x 5.475~6.475, z 7.525~8.875) ═════════════════════════
        private static void BuildShelter()
        {
            Transform r = Room("Shelter");

            Furn("ShelterShelf", r, 5.975f, 8.7f, 180f);            // 비상용품 선반(북쪽 벽, 생수·구급상자·손전등·담요)
            OnWall("VentWindow", r, new Vector3(6.452f, 1.85f, 8.2f), 270f, "VentWindow");   // 환기창(루버)

            // 바닥 비상 표지
            Prim.Box(r, "ShelterFloorMark", new Vector3(5.95f, 0.014f, 8.2f), new Vector3(0.5f, 0.004f, 0.5f), M.GreenSign);
        }

        // ═════════════════════════ 10층 복도 (x -14~9.5, z -2.2~0) ═════════════════════════
        private static void BuildCorridor()
        {
            Transform r = Room("Corridor");

            // 소화전함 (옛 FireBox_Corridor 자리: x=-7, 벽 매입)
            Furn("HydrantBox", r, -7f, -0.215f, 180f, 0.55f);
            Prim.Box(r, "FireAlarmButton", new Vector3(-5.9f, 1.3f, -0.15f), new Vector3(0.1f, 0.12f, 0.04f), M.RedPaint);

            // 엘리베이터: 호출 버튼 + 층 표시
            OnWall("ElevatorPanel", r, new Vector3(-0.4f, 1.0f, -2.062f), 0f, "ElevatorCallPanel");
            Prim.Box(r, "FloorIndicator", new Vector3(-1.5f, 2.22f, -2.05f), new Vector3(0.3f, 0.12f, 0.03f), M.Screen);
            AddLabel(r, "FloorIndicatorText", "10", new Vector3(-1.5f, 2.22f, -2.03f), 0.025f, new Color(1f, 0.35f, 0.25f), 180f);

            // 세대 호수 번호판 (1004호 판은 기존 Plate_1004 위에 숫자만 올린다)
            AddLabel(r, "Number_1004", "1004", new Vector3(3.0f, 1.65f, -0.172f), 0.03f, Color.white);
            AddPlate(r, "Plate_1003", "1003", Interactable("door_1003"));
            AddPlate(r, "Plate_1002", "1002", Interactable("door_1002"));
        }

        private static void AddPlate(Transform parent, string name, string text, Transform door)
        {
            if (door == null) return;
            Vector3 p = door.position; // FlatDoor 루트: 문 바닥 중앙, 두께 0.06
            Prim.Box(parent, name, new Vector3(p.x, 1.65f, p.z - 0.04f), new Vector3(0.3f, 0.14f, 0.03f), M.Metal);
            AddLabel(parent, name + "_Text", text, new Vector3(p.x, 1.65f, p.z - 0.058f), 0.03f, Color.white);
        }

        /// <summary>TextMesh 라벨 (복도 방향 -z 에서 읽히도록 회전 0). 내장 폰트를 못 찾으면 건너뛴다</summary>
        private static void AddLabel(Transform parent, string name, string text, Vector3 pos, float characterSize, Color color, float yaw = 0f)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) { Debug.LogWarning("[Interior] 내장 폰트를 찾지 못해 라벨 '" + text + "' 을 건너뜁니다."); return; }
            var go = new GameObject(name, typeof(TextMesh));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var tm = go.GetComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = characterSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
        }
    }
}
