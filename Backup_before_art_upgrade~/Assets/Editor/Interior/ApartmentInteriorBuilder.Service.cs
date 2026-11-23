using GameGameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;

namespace GameUI.EditorTools.Interior
{
    public static partial class ApartmentInteriorBuilder
    {
        // ═════════════════════════ 다용도실 (x 4.175~6.475, z 0.125~2.325) ═════════════════════════
        // 고정된 상호작용: 메인 가스 밸브(6.46,1.2,1.2) + 배관(6.5,*,1.2). 다용도실 문(경첩 4.95,2.4)은 안쪽(-z)으로 열린다.
        private static void BuildUtility()
        {
            Transform r = Room("Utility");

            Furn("WashingMachine", r, 4.6f, 0.5f, 0f);
            Furn("UtilityShelf", r, 6.3f, 0.45f, 270f);          // 세제·박스 선반 (밸브 z=1.2 와 겹치지 않는 남쪽 끝)
            // 빨래건조대는 접어서 남쪽 벽에 세워둔다 (깊이 0.14m). 밸브(6.46,1.2) 앞 0.9m 는 비움
            Furn("DryingRack", r, new Vector3(5.5f, 0f, 0.195f), 0f, new Vector3(1f, 1.2f, 0.25f), "DryingRack_Folded");
            Furn("TrashBin", r, 4.44f, 0.94f);                    // 주방에서 옮겨 온 쓰레기통 (세탁기 옆)
            Furn("Boiler", r, 4.33f, 1.65f, 90f, 1.25f);          // 벽걸이 보일러

            // 가스 배관: 메인 밸브 기둥 상단 → 천장 부근 수평 → 보일러 위에서 내려오는 구조 (Cylinder)
            Prim.Cyl(r, "GasPipe_ToBoilerA", new Vector3(6.5f, 2.2f, 1.425f), 0.02f, 0.45f, M.GasPipe, new Vector3(90f, 0, 0));
            Prim.Cyl(r, "GasPipe_ToBoilerB", new Vector3(5.415f, 2.2f, 1.65f), 0.02f, 2.17f, M.GasPipe, new Vector3(0, 0, 90f));
            Prim.Cyl(r, "GasPipe_Drop", new Vector3(4.33f, 2.075f, 1.65f), 0.02f, 0.25f, M.GasPipe);
            Prim.Sph(r, "GasPipe_Elbow1", new Vector3(6.5f, 2.2f, 1.65f), Vector3.one * 0.05f, M.GasPipe);
            Prim.Sph(r, "GasPipe_Elbow2", new Vector3(4.33f, 2.2f, 1.65f), Vector3.one * 0.05f, M.GasPipe);

            Prim.Cyl(r, "FloorDrain", new Vector3(5.35f, 0.014f, 1.5f), 0.07f, 0.006f, M.MetalDark);
        }

        // ═════════════════════════ 화장실 (x 0.125~1.825, z 0.125~2.325) ═════════════════════════
        // 고정된 상호작용: 세수(faucet 1.3,0.98,2.28) / 수건 적시기(1.1,0.85,2.12) / 수건 2장(0.2,0.98,1.25·1.6) / 문(경첩 1.9,0.8 — 화장실 안쪽으로 열림)
        private static void BuildBathroom()
        {
            Transform r = Room("Bathroom");

            Furn("Toilet", r, 0.35f, 0.45f, 0f);                  // 남쪽 벽에 탱크를 붙임
            Furn("Vanity", r, 1.3f, 2.125f, 180f);                // 세면대 아래 장 (위에 기존 세면대/수도꼭지 상호작용)
            // 욕조(0.65 x 1.3): 서쪽 벽. 문 열림 반경(+0.3) 밖으로 줄였다. 수건걸이(0.16,1.15)가 욕조 위 벽에 있다
            Furn("Bathtub", r, new Vector3(0.45f, 0f, 1.65f), 0f, new Vector3(0.93f, 1f, 0.93f));
            Furn("StandMirror", r, new Vector3(1.3f, 1.0f, 2.305f), 180f, new Vector3(1.1f, 0.45f, 1f), "Mirror_Bath");

            // 샤워: 벽 파이프 + 샤워기 헤드 + 커튼 레일/커튼(북쪽 끝에 모음 — 수건 상호작용을 가리지 않는다)
            Prim.Cyl(r, "ShowerPipe", new Vector3(0.16f, 1.2f, 2.1f), 0.012f, 1.8f, M.Chrome);
            Prim.Box(r, "ShowerArm", new Vector3(0.24f, 2.08f, 2.1f), new Vector3(0.16f, 0.02f, 0.02f), M.Chrome);
            Prim.Cyl(r, "ShowerHead", new Vector3(0.34f, 2.06f, 2.1f), 0.06f, 0.025f, M.Chrome);
            Prim.Cyl(r, "CurtainRod", new Vector3(0.79f, 1.95f, 1.65f), 0.01f, 1.4f, M.Metal, new Vector3(90f, 0, 0));
            for (int i = 0; i < 3; i++)
                Prim.Box(r, "ShowerCurtain", new Vector3(0.79f, 1.28f, 2.2f - i * 0.1f), new Vector3(0.012f, 1.3f, 0.11f), i % 2 == 0 ? M.CurtainSheer : M.Pillow);

            // 타일 벽 (허리~가슴 높이까지) — 기존 벽 면에서 3mm 띄움
            const float tileH = 1.4f;
            Prim.Quad(r, "WallTile_West", new Vector3(0.128f, 0f, 0.125f), Vector3.forward, 2.2f, Vector3.up, tileH, Vector3.right, M.WallTileBath, M.TileTexMeters);
            Prim.Quad(r, "WallTile_North", new Vector3(0.125f, 0f, 2.322f), Vector3.right, 1.7f, Vector3.up, tileH, Vector3.back, M.WallTileBath, M.TileTexMeters);
            Prim.Quad(r, "WallTile_South", new Vector3(0.125f, 0f, 0.128f), Vector3.right, 1.7f, Vector3.up, tileH, Vector3.forward, M.WallTileBath, M.TileTexMeters);
            Prim.Quad(r, "WallTile_East_A", new Vector3(1.822f, 0f, 0.125f), Vector3.forward, 0.675f, Vector3.up, tileH, Vector3.left, M.WallTileBath, M.TileTexMeters);
            Prim.Quad(r, "WallTile_East_B", new Vector3(1.822f, 0f, 1.6f), Vector3.forward, 0.725f, Vector3.up, tileH, Vector3.left, M.WallTileBath, M.TileTexMeters);

            // 바닥 배수구 + 세면 소품
            Prim.Cyl(r, "FloorDrain", new Vector3(1.35f, 0.014f, 1.25f), 0.07f, 0.006f, M.MetalDark);
            Furn("ToothbrushCup", r, 1.52f, 2.0f, 0f, 0.8f);
            Furn("Soap", r, 1.5f, 2.2f, 0f, 0.8f);
        }

        /// <summary>
        /// 현관문 도어 스코프(문구멍): 문 가운데 높이 1.5m 의 렌즈. 문과 함께 움직이도록 Hinge 의 자식으로 둔다.
        /// 오브젝트 forward 가 문밖(복도) 방향이며, ViewPoint 는 문짝 바깥면보다 약간 바깥쪽에 있는 시점이다.
        /// </summary>
        private static void BuildPeephole(Transform hinge)
        {
            Transform existing = hinge.Find("door_peephole");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject("door_peephole");
            go.transform.SetParent(hinge, false);
            go.transform.localPosition = new Vector3(0.475f, 1.5f, 0.055f);        // 문폭 0.95 의 한가운데, 안쪽 면
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);           // forward = 문밖(-z)

            Prim.Cyl(go.transform, "Ring", Vector3.zero, 0.022f, 0.012f, M.Chrome, new Vector3(90f, 0f, 0f));
            Prim.Cyl(go.transform, "Lens", new Vector3(0f, 0f, -0.004f), 0.012f, 0.008f, M.Glass, new Vector3(90f, 0f, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.09f, 0.09f, 0.04f);                           // 조준하기 쉽도록 렌즈보다 크게
            col.center = new Vector3(0f, 0f, -0.01f);

            var view = new GameObject("ViewPoint").transform;
            view.SetParent(go.transform, false);
            view.localPosition = new Vector3(0f, 0f, 0.12f);                        // 문밖으로 0.12m (문짝 두께 바깥)

            var ph = go.AddComponent<DoorPeephole>();
            var so = new SerializedObject(ph);
            so.FindProperty("id").stringValue = "door_peephole";
            so.FindProperty("prompt").stringValue = "문구멍으로 보기";
            so.FindProperty("viewPoint").objectReferenceValue = view;
            so.FindProperty("outsideOffset").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            MakeDynamic(go);
        }

        // ═════════════════════════ 현관 (x 1.975~4.025, z 0.125~2.4) ═════════════════════════
        // 고정된 상호작용: 현관문(경첩 2.525,0 — 실내로 열림, 반경 0.95 안은 비워 둔다) / 소화기(3.92,0.32,1.9) / door_handle_check
        private static void BuildEntry()
        {
            Transform r = Room("Entry");

            // 신발장 (shoe_cabinet 상호작용 박스와 같은 자리, 앞면이 서쪽). 소화기는 신발장 남쪽 끝 바로 앞 바닥에 그대로 있다
            // 콜라이더는 shoe_cabinet(x 3.68~4.08)이 담당. 뒷면 일부는 벽 속에 묻힌다
            // 현관문 열림 반경(+0.3) 밖으로 z 0.6~1.6 으로 옮김 (shoe_cabinet 상호작용도 같이 이동)
            MoveInteractable("shoe_cabinet", new Vector3(3.88f, 0.55f, 1.1f));
            UseInteractableCollider(Furn("ShoeCabinet", r, 3.88f, 1.1f, 270f));
            Furn("UmbrellaStand", r, 2.1f, 0.3f);                 // 화장실 문 앞을 피해 남서쪽 구석으로
            Furn("ShoePair", r, 2.15f, 0.3f, 8f, 0f, "Shoes_1");
            Furn("ShoePair", r, 2.4f, 0.36f, -10f, 0f, "Shoes_2");
            Furn("ShoePair", r, 2.2f, 1.2f, 85f, 0f, "Shoes_3");
            Furn("ShoePair", r, 3.5f, 0.55f, 92f, 0f, "Shoes_4");

            // 전신거울 (화장실 문 옆 벽), 센서등 (화장실 문 위)
            Furn("StandMirror", r, 1.995f, 2.05f, 90f, 0f, "Mirror_Entry");
            OnWall("SensorLight", r, new Vector3(1.985f, 2.25f, 1.2f), 90f, "SensorLight_Entry");

            // 소화기 표지 (소화기 위 벽)
            Prim.Box(r, "ExtinguisherSign", new Vector3(4.01f, 1.45f, 1.9f), new Vector3(0.01f, 0.18f, 0.18f), M.RedPaint);

            // 도어락: 현관문과 함께 움직이므로 Hinge 의 자식(정적 아님). 손잡이 온도 확인 플레이트(y 1.0) 위쪽에 둔다
            Transform hinge = Interactable("door_front") != null ? Interactable("door_front").Find("Hinge") : null;
            if (hinge != null)
            {
                GameObject lockGo = Furn("DoorLock", r, 0f, 0f, 0f);
                lockGo.transform.SetParent(hinge, false);
                lockGo.transform.localPosition = new Vector3(0.83f, 1.24f, 0.075f);
                lockGo.transform.localRotation = Quaternion.identity;
                lockGo.name = "DoorLock";
                MakeDynamic(lockGo);

                BuildPeephole(hinge);
            }
        }
    }
}
