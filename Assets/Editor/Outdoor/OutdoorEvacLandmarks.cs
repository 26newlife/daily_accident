using System.Collections.Generic;
using GameUI.EditorTools.Interior;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using OM = GameUI.EditorTools.Outdoor.OutdoorMaterials;

namespace GameUI.EditorTools.Outdoor
{
    /// <summary>
    /// 공통 바깥 풍경에 시나리오 2 "대피 지도"의 장소를 실제로 놓는다 (모든 시나리오에서 보인다).
    ///   · 강: 놀이터 남쪽 z 68~76 (지도 아래쪽 "강 (지진해일 주의)"). 둑·난간·경고판, 순환도로가 지나는 두 곳(x -135, x 123)에 다리
    ///   · 지진 옥외대피장소: 놀이터 옆 공터 x -92~-48, z 44~68. 파란 경계선 + 안내판 + 위층에서도 읽히는 바닥 글씨
    ///   · 지진해일 대피소: 건물 뒤(북쪽) 뒷산 x -42~12, z -52~-118, 높이 14m. 비탈 + 언덕 위 쉼터·벤치·안내판
    /// 지도 좌표(MainMenuBuilder.S02 BuildPhonePanel)와 같은 월드 좌표다. 지면 y = -28.5.
    /// 걸을 수 있는 부분(언덕·다리·난간)은 콜라이더가 있다 — 비상계단 확장으로 건물 밖에 나갈 수 있기 때문.
    /// 모든 오브젝트는 OutdoorWorld/EvacLandmarks 아래에 있고, 여러 번 실행해도 결과가 같다 (지우고 다시 만든다).
    /// 메시는 기본 큐브·원기둥만 쓴다 (프리팹에 저장되지 않는 임시 메시를 만들지 않는다).
    /// 호출: BaseHouseFixups.Apply (집을 다시 빌드해도 유지) / 메뉴 Tools > GameUI > Base House > Build Evac Landmarks.
    /// </summary>
    public static class OutdoorEvacLandmarks
    {
        public const string RootName = "EvacLandmarks";
        private const string PrefabPath = "Assets/House/Prefabs/Apartment_1004_Base.prefab";
        private const string FontPath = "Assets/Fonts/NotoSansKR SDF.asset";
        private const string PinePrefab = "Assets/Prefabs/Outdoor/Tree_Pine.prefab";
        private const float G = -28.5f;

        // 강
        public const float RiverZ0 = 68f, RiverZ1 = 76f, RiverX0 = -220f, RiverX1 = 220f;
        private static readonly Vector2[] Bridges = { new Vector2(-141f, -129f), new Vector2(117f, 129f) };   // x 범위 (순환도로 x -135 / Road_B x 120~128)
        // 지진 옥외대피장소
        public const float SiteX0 = -92f, SiteX1 = -48f, SiteZ0 = 44f, SiteZ1 = 68f;
        // 지진해일 대피소 (뒷산)
        public const float HillX0 = -42f, HillX1 = 12f, RampZ0 = -52f, RampZ1 = -82f, HillZ1 = -118f, HillH = 14f;
        public static readonly Vector3 ShelterCenter = new Vector3(-15f, G + HillH, -99f);

        private static Transform root;
        private static TMP_FontAsset font;
        private static Material water, bank, rail, railDark, concrete, asphalt, siteBlue, siteWhite, grass, earth, shelterRoof, bench, signBlue, signGreen, signRed, post;

        [MenuItem("Tools/GameUI/Base House/Build Evac Landmarks")]
        public static void BuildMenu()
        {
            if (!EditorUtility.DisplayDialog("대피 장소 만들기", "집 프리팹의 바깥 풍경에 강, 지진 옥외대피장소, 지진해일 대피소(뒷산)를 만듭니다.\n여러 번 실행해도 같은 결과입니다.", "만들기", "취소")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyToAssets();
        }

        /// <summary>배치모드: -executeMethod GameUI.EditorTools.Outdoor.OutdoorEvacLandmarks.ApplyToAssets</summary>
        public static void ApplyToAssets()
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { Build(prefab.transform); PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            Debug.Log("[EvacLandmarks] 집 프리팹 바깥 풍경에 대피 장소를 저장했습니다: " + PrefabPath);
        }

        /// <summary>houseRoot = Apartment_1004_Base (프리팹 편집 루트 또는 빌드 중인 씬 루트)</summary>
        public static void Build(Transform houseRoot)
        {
            if (houseRoot == null) return;
            if (PrefabUtility.IsPartOfPrefabInstance(houseRoot.gameObject))
            {
                if (houseRoot.Find("OutdoorWorld/" + RootName) == null)
                    Debug.LogWarning("[EvacLandmarks] 씬의 집 프리팹 인스턴스에는 직접 만들지 않습니다. Tools > GameUI > Base House > Build Evac Landmarks 로 프리팹에 만드세요.");
                return;
            }
            Transform outdoor = houseRoot.Find("OutdoorWorld");
            if (outdoor == null) { Debug.LogWarning("[EvacLandmarks] OutdoorWorld 를 찾지 못해 만들지 않습니다."); return; }

            Transform old = outdoor.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Materials();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var go = new GameObject(RootName);
            root = go.transform;
            root.SetParent(outdoor, false);
            // 그룹 좌표 = 집 루트 기준 월드 좌표가 되도록 OutdoorWorld 의 위치를 되돌린다 (보통 원점)
            root.localPosition = -outdoor.localPosition;

            int trees = ClearTrees(outdoor);
            BuildRiver(Group("River", root));
            BuildOutdoorSite(Group("EarthquakeOutdoorSite", root));
            BuildTsunamiShelter(Group("TsunamiShelterHill", root));
            Debug.Log($"[EvacLandmarks] 강·지진 옥외대피장소·지진해일 대피소 생성 (겹치는 나무 {trees}그루 제거)");
        }

        // ═════════════════════════ 나무 정리 ═════════════════════════
        /// <summary>강·대피장소 위에 무작위로 심어진 나무를 치운다</summary>
        private static int ClearTrees(Transform outdoor)
        {
            Transform near = outdoor.Find("NearField");
            if (near == null) return 0;
            var kill = new List<GameObject>();
            foreach (Transform t in near)
            {
                if (!t.name.StartsWith("Tree")) continue;
                Vector3 p = outdoor.InverseTransformPoint(t.position) + outdoor.localPosition;
                bool inRiver = p.z > RiverZ0 - 4f && p.z < RiverZ1 + 4f;
                bool inSite = p.x > SiteX0 - 2f && p.x < SiteX1 + 2f && p.z > SiteZ0 - 2f && p.z < SiteZ1 + 2f;
                if (inRiver || inSite) kill.Add(t.gameObject);
            }
            foreach (GameObject g in kill) Object.DestroyImmediate(g);
            return kill.Count;
        }

        // ═════════════════════════ 강 ═════════════════════════
        private static void BuildRiver(Transform g)
        {
            // 물 (윗면이 잔디보다 살짝 위 — 위층에서 내려다보면 물길로 보인다)
            Visual(g, "Water", new Vector3(0f, G - 0.03f, (RiverZ0 + RiverZ1) * 0.5f), new Vector3(RiverX1 - RiverX0, 0.1f, RiverZ1 - RiverZ0), water)
                .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 물 가운데 결 (조금 밝은 물살)
            for (int i = 0; i < 3; i++)
                Visual(g, "Current", new Vector3(-150f + i * 120f, G + 0.025f, RiverZ0 + 2.5f + i * 1.4f), new Vector3(70f, 0.01f, 0.35f), OM.Lit("Evac_RiverCurrent", OM.Hex("6E98AE"), 0.9f));

            // 둑 (돌 축대) — 강 양쪽
            Visual(g, "Bank_N", new Vector3(0f, G + 0.15f, RiverZ0 - 0.25f), new Vector3(RiverX1 - RiverX0, 0.3f, 0.5f), bank);
            Visual(g, "Bank_S", new Vector3(0f, G + 0.15f, RiverZ1 + 0.25f), new Vector3(RiverX1 - RiverX0, 0.3f, 0.5f), bank);

            // 난간 + 걸어서 물로 들어가지 않게 막는 콜라이더 (다리 구간은 비운다)
            var spans = Spans(RiverX0, RiverX1);
            foreach (float z in new[] { RiverZ0 - 0.3f, RiverZ1 + 0.3f })
            {
                Transform side = Group(z < RiverZ0 ? "Rail_N" : "Rail_S", g);
                foreach (Vector2 s in spans)
                {
                    float len = s.y - s.x, cx = (s.x + s.y) * 0.5f;
                    Visual(side, "TopRail", new Vector3(cx, G + 1.05f, z), new Vector3(len, 0.06f, 0.06f), rail);
                    Visual(side, "MidRail", new Vector3(cx, G + 0.6f, z), new Vector3(len, 0.04f, 0.04f), rail);
                    for (float x = s.x; x <= s.y + 0.01f; x += 4f) Visual(side, "Post", new Vector3(x, G + 0.55f, z), new Vector3(0.08f, 1.1f, 0.08f), railDark);
                    Collider(side, "Block", new Vector3(cx, G + 0.7f, z), new Vector3(len, 1.4f, 0.3f));
                }
            }

            // 다리 (순환도로가 강을 건너는 곳)
            for (int i = 0; i < Bridges.Length; i++)
            {
                Vector2 b = Bridges[i];
                Transform br = Group("Bridge_" + i, g);
                float cx = (b.x + b.y) * 0.5f, w = b.y - b.x, cz = (RiverZ0 + RiverZ1) * 0.5f, len = RiverZ1 - RiverZ0 + 2.4f;
                Solid(br, "Deck", new Vector3(cx, G + 0.04f, cz), new Vector3(w, 0.16f, len), concrete);
                Visual(br, "Road", new Vector3(cx, G + 0.125f, cz), new Vector3(w - 2f, 0.01f, len), asphalt);
                Visual(br, "Girder", new Vector3(cx, G - 0.4f, cz), new Vector3(w - 1f, 0.6f, len - 0.6f), bank);
                foreach (float x in new[] { b.x + 0.15f, b.y - 0.15f })
                    Solid(br, "Parapet", new Vector3(x, G + 0.6f, cz), new Vector3(0.3f, 1.0f, len), concrete);
            }

            // 경고판 (강가, 건물 쪽을 향함)
            Sign(g, "Sign_River_W", new Vector3(-58f, G, RiverZ0 - 1.4f), 180f, "강 — 지진해일 주의\n물가 접근 금지", signRed, new Vector2(2.4f, 0.9f));
            Sign(g, "Sign_River_E", new Vector3(14f, G, RiverZ0 - 1.4f), 180f, "강 — 지진해일 주의\nTSUNAMI CAUTION", signRed, new Vector2(2.4f, 0.9f));
        }

        /// <summary>x0~x1 에서 다리 구간을 뺀 구간들</summary>
        private static List<Vector2> Spans(float x0, float x1)
        {
            var list = new List<Vector2>();
            float cur = x0;
            foreach (Vector2 b in Bridges)
            {
                if (b.x > cur) list.Add(new Vector2(cur, b.x));
                cur = Mathf.Max(cur, b.y);
            }
            if (x1 > cur) list.Add(new Vector2(cur, x1));
            return list;
        }

        // ═════════════════════════ 지진 옥외대피장소 ═════════════════════════
        private static void BuildOutdoorSite(Transform g)
        {
            float cx = (SiteX0 + SiteX1) * 0.5f, cz = (SiteZ0 + SiteZ1) * 0.5f, w = SiteX1 - SiteX0, d = SiteZ1 - SiteZ0;
            const float line = 0.5f, ly = G + 0.055f;
            // 파란 경계선
            Visual(g, "Border_N", new Vector3(cx, ly, SiteZ0 + line * 0.5f), new Vector3(w, 0.01f, line), siteBlue);
            Visual(g, "Border_S", new Vector3(cx, ly, SiteZ1 - line * 0.5f), new Vector3(w, 0.01f, line), siteBlue);
            Visual(g, "Border_W", new Vector3(SiteX0 + line * 0.5f, ly, cz), new Vector3(line, 0.01f, d), siteBlue);
            Visual(g, "Border_E", new Vector3(SiteX1 - line * 0.5f, ly, cz), new Vector3(line, 0.01f, d), siteBlue);
            // 모서리 볼라드 (흰 기둥 + 파란 띠)
            foreach (Vector2 c in new[] { new Vector2(SiteX0, SiteZ0), new Vector2(SiteX1, SiteZ0), new Vector2(SiteX0, SiteZ1), new Vector2(SiteX1, SiteZ1) })
            {
                Solid(g, "Bollard", new Vector3(c.x, G + 0.45f, c.y), new Vector3(0.3f, 0.9f, 0.3f), siteWhite);
                Visual(g, "BollardBand", new Vector3(c.x, G + 0.7f, c.y), new Vector3(0.32f, 0.12f, 0.32f), siteBlue);
            }
            // 위층(창가)에서도 읽히는 바닥 글씨 — 놀이기구(z 52~58)를 피해 북쪽 가장자리에
            GroundText(g, "GroundText", "지진 옥외대피장소", new Vector3(cx + 2f, G + 0.06f, SiteZ0 + 3.2f), 2.6f, new Color(0.13f, 0.38f, 0.78f), 34f);
            GroundText(g, "GroundText_En", "EARTHQUAKE EVACUATION AREA", new Vector3(cx + 2f, G + 0.06f, SiteZ0 + 5.6f), 1.0f, new Color(0.13f, 0.38f, 0.78f), 34f);
            // 안내판 (보도 쪽, 건물을 향함)
            Sign(g, "Sign_Site", new Vector3(SiteX1 - 6f, G, SiteZ0 - 1.2f), 180f, "지진 옥외대피장소\nEARTHQUAKE EVACUATION AREA", signBlue, new Vector2(3.2f, 1.2f));
        }

        // ═════════════════════════ 지진해일 대피소 (뒷산) ═════════════════════════
        private static void BuildTsunamiShelter(Transform g)
        {
            float top = G + HillH;
            float run = RampZ0 - RampZ1, len = Mathf.Sqrt(run * run + HillH * HillH), ang = Mathf.Atan2(HillH, run) * Mathf.Rad2Deg;
            float cx = (HillX0 + HillX1) * 0.5f, w = HillX1 - HillX0;
            // 비탈: x 축 +ang 회전 — 남쪽(+z) 끝이 지면, 북쪽 끝이 언덕 위. 윗면 법선 (0, cos, sin) 반대로 두께 절반만큼 내린다
            float r = ang * Mathf.Deg2Rad;
            Vector3 mid = new Vector3(cx, G + HillH * 0.5f, (RampZ0 + RampZ1) * 0.5f) + new Vector3(0f, -0.5f * Mathf.Cos(r), -0.5f * Mathf.Sin(r));
            Solid(g, "HillSlope", mid, new Vector3(w, 1f, len), grass, new Vector3(ang, 0f, 0f));
            // 비탈 아래 흙 (옆에서 보면 비어 보이지 않게)
            for (int i = 0; i < 4; i++)
            {
                float z0 = Mathf.Lerp(RampZ0, RampZ1, (i + 1) / 5f), h = HillH * (i + 1) / 5f;
                Visual(g, "SlopeFill", new Vector3(cx, G + h * 0.5f - 0.3f, z0 - (run / 5f) * 0.5f - 0.6f), new Vector3(w - 0.2f, h, run / 5f), earth);
            }
            Solid(g, "HillTop", new Vector3(cx, top - 0.5f, (RampZ1 + HillZ1) * 0.5f), new Vector3(w, 1f, RampZ1 - HillZ1), grass);
            Visual(g, "HillBody", new Vector3(cx, G + (HillH - 1f) * 0.5f, (RampZ1 + HillZ1) * 0.5f), new Vector3(w - 0.2f, HillH - 1f, RampZ1 - HillZ1 - 0.2f), earth);
            Solid(g, "HillSide_W", new Vector3(HillX0 + 0.5f, G + HillH * 0.5f, (RampZ1 + HillZ1) * 0.5f), new Vector3(1f, HillH, RampZ1 - HillZ1), earth);
            Solid(g, "HillSide_E", new Vector3(HillX1 - 0.5f, G + HillH * 0.5f, (RampZ1 + HillZ1) * 0.5f), new Vector3(1f, HillH, RampZ1 - HillZ1), earth);
            Solid(g, "HillBack", new Vector3(cx, G + HillH * 0.5f, HillZ1 + 0.5f), new Vector3(w, HillH, 1f), earth);
            // 비탈 가운데 계단식 산책로 (밝은 흙길)
            Visual(g, "SlopePath", mid + new Vector3(0f, 0.51f * Mathf.Cos(r), 0.51f * Mathf.Sin(r)), new Vector3(3f, 0.02f, len), OM.Lit("Evac_HillPath", OM.Hex("B9A57E"), 0.05f), new Vector3(ang, 0f, 0f));
            // 언덕 위 난간 (가장자리에서 떨어지지 않게)
            Solid(g, "Rail_W", new Vector3(HillX0 + 0.3f, top + 0.5f, (RampZ1 + HillZ1) * 0.5f), new Vector3(0.08f, 1f, RampZ1 - HillZ1), rail);
            Solid(g, "Rail_E", new Vector3(HillX1 - 0.3f, top + 0.5f, (RampZ1 + HillZ1) * 0.5f), new Vector3(0.08f, 1f, RampZ1 - HillZ1), rail);
            Solid(g, "Rail_N", new Vector3(cx, top + 0.5f, HillZ1 + 0.3f), new Vector3(w, 1f, 0.08f), rail);

            // 쉼터: 지붕 + 기둥 + 벤치
            Vector3 c = ShelterCenter;
            Visual(g, "Shelter_Roof", new Vector3(c.x, top + 3.1f, c.z), new Vector3(14f, 0.25f, 9f), shelterRoof);
            Visual(g, "Shelter_RoofTrim", new Vector3(c.x, top + 2.93f, c.z), new Vector3(14.2f, 0.12f, 9.2f), siteWhite);
            foreach (Vector2 p in new[] { new Vector2(-6.5f, -4f), new Vector2(6.5f, -4f), new Vector2(-6.5f, 4f), new Vector2(6.5f, 4f) })
                Solid(g, "Shelter_Post", new Vector3(c.x + p.x, top + 1.5f, c.z + p.y), new Vector3(0.2f, 3f, 0.2f), post);
            for (int i = 0; i < 3; i++) Solid(g, "Bench", new Vector3(c.x - 4f + i * 4f, top + 0.22f, c.z - 2f), new Vector3(2.4f, 0.44f, 0.5f), bench);
            // 바닥 글씨 (옥상에서 내려다보면 보인다) + 안내판
            GroundText(g, "GroundText", "지진해일 대피소", new Vector3(c.x, top + 0.02f, c.z + 7f), 2.2f, new Color(0.1f, 0.45f, 0.28f), 30f, true);   // 옥상(남쪽)에서 읽힌다
            Sign(g, "Sign_Shelter", new Vector3(c.x, top, c.z + 5.5f), 0f, "지진해일 긴급대피장소\nTSUNAMI EVACUATION SHELTER", signGreen, new Vector2(3.2f, 1.2f));
            Sign(g, "Sign_Ramp", new Vector3(cx + 3f, G, RampZ0 + 1.5f), 0f, "지진해일 대피소 ↑\n뒷산 고지대 (해발 +14m)", signGreen, new Vector2(2.6f, 1.0f));

            // 언덕 위 소나무 (가장자리)
            var pine = AssetDatabase.LoadAssetAtPath<GameObject>(PinePrefab);
            if (pine != null)
            {
                var rng = new System.Random(11);
                for (int i = 0; i < 10; i++)
                {
                    float x = i % 2 == 0 ? HillX0 + 2.5f : HillX1 - 2.5f;
                    float z = Mathf.Lerp(RampZ1 - 3f, HillZ1 + 3f, (i / 2) / 4f) + (float)rng.NextDouble() * 2f;
                    var t = (GameObject)PrefabUtility.InstantiatePrefab(pine, g);
                    t.transform.localPosition = new Vector3(x, top, z);
                    t.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    t.transform.localScale = Vector3.one * (0.9f + (float)rng.NextDouble() * 0.4f);
                }
            }
        }

        // ═════════════════════════ 재질 ═════════════════════════
        private static void Materials()
        {
            water = OM.Lit("Evac_RiverWater", OM.Hex("3D6E86"), 0.95f, 0.1f);
            bank = OM.Lit("Evac_RiverBank", OM.Hex("8A8172"), 0.08f);
            rail = OM.Lit("Evac_Rail", OM.Hex("C8CCD1"), 0.55f, 0.6f);
            railDark = OM.Lit("Evac_RailPost", OM.Hex("4A5058"), 0.4f, 0.5f);
            concrete = OM.Lit("Evac_Concrete", OM.Hex("A7A6A1"), 0.1f);
            asphalt = OM.Lit("Evac_Asphalt", OM.Hex("3A3B3E"), 0.15f);
            siteBlue = OM.Lit("Evac_SiteBlue", OM.Hex("2F6FC1"), 0.2f);
            siteWhite = OM.Lit("Evac_White", OM.Hex("F2F2F0"), 0.2f);
            grass = OM.Lit("Evac_HillGrass", OM.Hex("5A8F45"), 0.05f);
            earth = OM.Lit("Evac_HillEarth", OM.Hex("6B5A42"), 0.05f);
            shelterRoof = OM.Lit("Evac_ShelterRoof", OM.Hex("2E6A4F"), 0.2f);
            bench = OM.Lit("Evac_Bench", OM.Hex("7A5A3E"), 0.15f);
            post = OM.Lit("Evac_Post", OM.Hex("D9DCDF"), 0.4f, 0.4f);
            signBlue = OM.Lit("Evac_SignBlue", OM.Hex("1F5FAF"), 0.3f);
            signGreen = OM.Lit("Evac_SignGreen", OM.Hex("218C59"), 0.3f);
            signRed = OM.Lit("Evac_SignRed", OM.Hex("B3261E"), 0.3f);
        }

        // ═════════════════════════ 헬퍼 ═════════════════════════
        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // 그룹은 모두 루트 기준 무회전·원점이라 루트 좌표로 바로 넣는다
        private static Vector3 Local(Transform parent, Vector3 p)
        {
            Vector3 o = Vector3.zero;
            for (Transform t = parent; t != null && t != root; t = t.parent) o += t.localPosition;
            return p - o;
        }

        private static GameObject Visual(Transform parent, string name, Vector3 center, Vector3 size, Material mat, Vector3 euler = default)
            => Prim.Box(parent, name, Local(parent, center), size, mat, euler);

        private static GameObject Solid(Transform parent, string name, Vector3 center, Vector3 size, Material mat, Vector3 euler = default)
        {
            GameObject go = Visual(parent, name, center, size, mat, euler);
            go.AddComponent<BoxCollider>();
            return go;
        }

        private static void Collider(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Local(parent, center);
            go.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>바닥에 눕힌 글씨. fromSouth = false 면 북쪽에서, true 면 남쪽에서 읽힌다</summary>
        private static void GroundText(Transform parent, string name, string text, Vector3 pos, float letterM, Color color, float width, bool fromSouth = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Local(parent, pos);
            go.transform.localRotation = Quaternion.Euler(90f, fromSouth ? 180f : 0f, 0f);
            var t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = letterM * 10f;
            t.fontStyle = FontStyles.Bold;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(width, letterM * 1.6f);
        }

        /// <summary>기둥 두 개 + 판 + 글씨. yaw 0 = 남쪽(+z)에서 읽힌다, 180 = 북쪽(건물 쪽)에서 읽힌다</summary>
        private static void Sign(Transform parent, string name, Vector3 foot, float yaw, string text, Material board, Vector2 size)
        {
            var s = new GameObject(name).transform;
            s.SetParent(parent, false);
            s.localPosition = Local(parent, foot);
            s.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float bottom = 1.4f, h = size.y;
            foreach (float x in new[] { -size.x * 0.4f, size.x * 0.4f })
            {
                GameObject p = Prim.Box(s, "Post", new Vector3(x, (bottom + h) * 0.5f, 0f), new Vector3(0.08f, bottom + h, 0.08f), post);
                p.AddComponent<BoxCollider>();
            }
            Prim.Box(s, "Board", new Vector3(0f, bottom + h * 0.5f, 0.05f), new Vector3(size.x, h, 0.05f), board);
            var go = new GameObject("Text");
            go.transform.SetParent(s, false);
            go.transform.localPosition = new Vector3(0f, bottom + h * 0.5f, 0.085f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = h * 2.6f; t.color = Color.white; t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(size.x - 0.1f, h - 0.05f);
        }
    }
}
