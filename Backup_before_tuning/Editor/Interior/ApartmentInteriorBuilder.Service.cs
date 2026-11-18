using GameGameplay;
using UnityEditor;
using UnityEngine;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;

namespace GameUI.EditorTools.Interior
{
    public static partial class ApartmentInteriorBuilder
    {
        // ?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧???ㅼ슜?꾩떎 (x 4.175~6.475, z 0.125~2.325) ?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧??
        // 怨좎젙???곹샇?묒슜: 硫붿씤 媛??諛몃툕(6.46,1.2,1.2) + 諛곌?(6.5,*,1.2). ?ㅼ슜?꾩떎 臾?寃쎌꺽 4.95,2.4)? ?덉そ(-z)?쇰줈 ?대┛??
        private static void BuildUtility()
        {
            Transform r = Room("Utility");

            Furn("WashingMachine", r, 4.6f, 0.5f, 0f);
            Furn("UtilityShelf", r, 6.3f, 0.45f, 270f);          // ?몄젣쨌諛뺤뒪 ?좊컲 (諛몃툕 z=1.2 ? 寃뱀튂吏 ?딅뒗 ?⑥そ ??
            // 鍮⑤옒嫄댁“????묒뼱???⑥そ 踰쎌뿉 ?몄썙?붾떎 (源딆씠 0.14m). 諛몃툕(6.46,1.2) ??0.9m ??鍮꾩?
            Furn("DryingRack", r, new Vector3(5.5f, 0f, 0.195f), 0f, new Vector3(1f, 1.2f, 0.25f), "DryingRack_Folded");
            Furn("TrashBin", r, 4.44f, 0.94f);                    // 二쇰갑?먯꽌 ??꺼 ???곕젅湲고넻 (?명긽湲???
            Furn("Boiler", r, 4.33f, 1.65f, 90f, 1.25f);          // 踰쎄구??蹂댁씪??

            // 媛??諛곌?: 硫붿씤 諛몃툕 湲곕뫁 ?곷떒 ??泥쒖옣 遺洹??섑룊 ??蹂댁씪???꾩뿉???대젮?ㅻ뒗 援ъ“ (Cylinder)
            Prim.Cyl(r, "GasPipe_ToBoilerA", new Vector3(6.5f, 2.2f, 1.425f), 0.02f, 0.45f, M.GasPipe, new Vector3(90f, 0, 0));
            Prim.Cyl(r, "GasPipe_ToBoilerB", new Vector3(5.415f, 2.2f, 1.65f), 0.02f, 2.17f, M.GasPipe, new Vector3(0, 0, 90f));
            Prim.Cyl(r, "GasPipe_Drop", new Vector3(4.33f, 2.075f, 1.65f), 0.02f, 0.25f, M.GasPipe);
            Prim.Sph(r, "GasPipe_Elbow1", new Vector3(6.5f, 2.2f, 1.65f), Vector3.one * 0.05f, M.GasPipe);
            Prim.Sph(r, "GasPipe_Elbow2", new Vector3(4.33f, 2.2f, 1.65f), Vector3.one * 0.05f, M.GasPipe);

            Prim.Cyl(r, "FloorDrain", new Vector3(5.35f, 0.014f, 1.5f), 0.07f, 0.006f, M.MetalDark);
        }

        // ?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧???붿옣??(x 0.125~1.825, z 0.125~2.325) ?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧??
        // 怨좎젙???곹샇?묒슜: ?몄닔(faucet 1.3,0.98,2.28) / ?섍굔 ?곸떆湲?1.1,0.85,2.12) / ?섍굔 2??0.2,0.98,1.25쨌1.6) / 臾?寃쎌꺽 1.9,0.8 ???붿옣???덉そ?쇰줈 ?대┝)
        private static void BuildBathroom()
        {
            Transform r = Room("Bathroom");

            Furn("Toilet", r, 0.35f, 0.45f, 0f);                  // ?⑥そ 踰쎌뿉 ?깊겕瑜?遺숈엫
            Furn("Vanity", r, 1.3f, 2.125f, 180f);                // ?몃㈃? ?꾨옒 ??(?꾩뿉 湲곗〈 ?몃㈃?/?섎룄瑗?? ?곹샇?묒슜)
            // ?뺤“(0.65 x 1.3): ?쒖そ 踰? 臾??대┝ 諛섍꼍(+0.3) 諛뽰쑝濡?以꾩??? ?섍굔嫄몄씠(0.16,1.15)媛 ?뺤“ ??踰쎌뿉 ?덈떎
            Furn("Bathtub", r, new Vector3(0.45f, 0f, 1.65f), 0f, new Vector3(0.93f, 1f, 0.93f));
            Furn("StandMirror", r, new Vector3(1.3f, 1.0f, 2.305f), 180f, new Vector3(1.1f, 0.45f, 1f), "Mirror_Bath");

            // ?ㅼ썙: 踰??뚯씠??+ ?ㅼ썙湲??ㅻ뱶 + 而ㅽ듉 ?덉씪/而ㅽ듉(遺곸そ ?앹뿉 紐⑥쓬 ???섍굔 ?곹샇?묒슜??媛由ъ? ?딅뒗??
            Prim.Cyl(r, "ShowerPipe", new Vector3(0.16f, 1.2f, 2.1f), 0.012f, 1.8f, M.Chrome);
            Prim.Box(r, "ShowerArm", new Vector3(0.24f, 2.08f, 2.1f), new Vector3(0.16f, 0.02f, 0.02f), M.Chrome);
            Prim.Cyl(r, "ShowerHead", new Vector3(0.34f, 2.06f, 2.1f), 0.06f, 0.025f, M.Chrome);
            Prim.Cyl(r, "CurtainRod", new Vector3(0.79f, 1.95f, 1.65f), 0.01f, 1.4f, M.Metal, new Vector3(90f, 0, 0));
            for (int i = 0; i < 3; i++)
                Prim.Box(r, "ShowerCurtain", new Vector3(0.79f, 1.28f, 2.2f - i * 0.1f), new Vector3(0.012f, 1.3f, 0.11f), i % 2 == 0 ? M.CurtainSheer : M.Pillow);

            // ???踰?(?덈━~媛???믪씠源뚯?) ??湲곗〈 踰?硫댁뿉??3mm ?꾩?
            const float tileH = 1.4f;
            Prim.Quad(r, "WallTile_West", new Vector3(0.128f, 0f, 0.125f), Vector3.forward, 2.2f, Vector3.up, tileH, Vector3.right, M.WallTileBath, M.TileTexMeters);
            Prim.Quad(r, "WallTile_North", new Vector3(0.125f, 0f, 2.322f), Vector3.right, 1.7f, Vector3.up, tileH, Vector3.back, M.WallTileBath, M.TileTexMeters);
            Prim.Quad(r, "WallTile_South", new Vector3(0.125f, 0f, 0.128f), Vector3.right, 1.7f, Vector3.up, tileH, Vector3.forward, M.WallTileBath, M.TileTexMeters);
            Prim.Quad(r, "WallTile_East_A", new Vector3(1.822f, 0f, 0.125f), Vector3.forward, 0.675f, Vector3.up, tileH, Vector3.left, M.WallTileBath, M.TileTexMeters);
            Prim.Quad(r, "WallTile_East_B", new Vector3(1.822f, 0f, 1.6f), Vector3.forward, 0.725f, Vector3.up, tileH, Vector3.left, M.WallTileBath, M.TileTexMeters);

            // 諛붾떏 諛곗닔援?+ ?몃㈃ ?뚰뭹
            Prim.Cyl(r, "FloorDrain", new Vector3(1.35f, 0.014f, 1.25f), 0.07f, 0.006f, M.MetalDark);
            Furn("ToothbrushCup", r, 1.52f, 2.0f, 0f, 0.8f);
            Furn("Soap", r, 1.5f, 2.2f, 0f, 0.8f);
        }

        /// <summary>
        /// ?꾧?臾??꾩뼱 ?ㅼ퐫???몄떆寃?: 臾?媛?대뜲 ?덈넂??1.5m)???뚯쫰. 臾멸낵 ?④퍡 ?吏곸씠?꾨줉 Hinge ???먯떇?쇰줈 ?붾떎.
        /// ?ㅻ툕?앺듃 forward 媛 臾몃컰(蹂듬룄) 諛⑺뼢?대ŉ, ViewPoint ??臾몄쭩 諛붽묑硫대낫??5mm ???섍컙 吏?먯씠?? ?ㅻⅨ 諛⑸Ц?먮뒗 ?ъ? ?딅뒗??
        /// </summary>
        private static void BuildPeephole(Transform hinge)
        {
            Transform existing = hinge.Find("door_peephole");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject("door_peephole");
            go.transform.SetParent(hinge, false);
            go.transform.localPosition = new Vector3(0.475f, 1.5f, 0.055f);        // 臾???0.95 ???쒓??대뜲, ?덉そ 硫?
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);           // forward = 臾몃컰(-z)

            GameObject ring = Prim.Cyl(go.transform, "Ring", Vector3.zero, 0.022f, 0.012f, M.Chrome, new Vector3(90f, 0f, 0f));
            GameObject lens = Prim.Cyl(go.transform, "Lens", new Vector3(0f, 0f, -0.004f), 0.012f, 0.008f, M.Glass, new Vector3(90f, 0f, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.09f, 0.09f, 0.04f);                           // 議곗??섍린 ?쎈룄濡??뚯쫰蹂대떎 ?ш쾶
            col.center = new Vector3(0f, 0f, -0.01f);

            var view = new GameObject("ViewPoint").transform;
            view.SetParent(go.transform, false);
            view.localPosition = new Vector3(0f, 0f, 0.12f);                        // 臾몃컰?쇰줈 0.12m (臾몄쭩 ?먭퍡 0.1 諛붽묑)

            var ph = go.AddComponent<DoorPeephole>();
            var so = new SerializedObject(ph);
            so.FindProperty("id").stringValue = "door_peephole";
            so.FindProperty("prompt").stringValue = "臾멸뎄硫띿쑝濡?蹂닿린";
            so.FindProperty("viewPoint").objectReferenceValue = view;
            so.FindProperty("outsideOffset").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            MakeDynamic(go);
        }

        // ?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧???꾧? (x 1.975~4.025, z 0.125~2.4) ?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧?먥븧??
        // 怨좎젙???곹샇?묒슜: ?꾧?臾?寃쎌꺽 2.525,0 ???ㅻ궡濡??대┝, 諛섍꼍 0.95 ?덉? 鍮꾩썙 ?붾떎) / ?뚰솕湲?3.92,0.32,1.9) / door_handle_check
        private static void BuildEntry()
        {
            Transform r = Room("Entry");

            // ?좊컻??(shoe_cabinet ?곹샇?묒슜 諛뺤뒪? 媛숈? ?먮━, ?욌㈃???쒖そ). ?뚰솕湲곕뒗 ?좊컻???⑥そ ??諛붾줈 ??諛붾떏??洹몃?濡??덈떎
            // 肄쒕씪?대뜑??shoe_cabinet(x 3.68~4.08)???대떦. ?룸㈃ ?쇰???踰??띿뿉 臾삵엺??
            // ?꾧?臾??대┝ 諛섍꼍(+0.3) 諛뽰쑝濡?z 0.6~1.6 ?쇰줈 ??? (shoe_cabinet ?곹샇?묒슜??媛숈씠 ?대룞)
            MoveInteractable("shoe_cabinet", new Vector3(3.88f, 0.55f, 1.1f));
            UseInteractableCollider(Furn("ShoeCabinet", r, 3.88f, 1.1f, 270f));
            Furn("UmbrellaStand", r, 2.1f, 0.3f);                 // ?붿옣??臾??욎쓣 ?쇳빐 ?⑥꽌履?援ъ꽍?쇰줈
            Furn("ShoePair", r, 2.15f, 0.3f, 8f, 0f, "Shoes_1");
            Furn("ShoePair", r, 2.4f, 0.36f, -10f, 0f, "Shoes_2");
            Furn("ShoePair", r, 2.2f, 1.2f, 85f, 0f, "Shoes_3");
            Furn("ShoePair", r, 3.5f, 0.55f, 92f, 0f, "Shoes_4");

            // ?꾩떊嫄곗슱 (?붿옣??臾???踰?, ?쇱꽌??(?붿옣??臾???
            Furn("StandMirror", r, 1.995f, 2.05f, 90f, 0f, "Mirror_Entry");
            OnWall("SensorLight", r, new Vector3(1.985f, 2.25f, 1.2f), 90f, "SensorLight_Entry");

            // ?뚰솕湲??쒖? (?뚰솕湲???踰?
            Prim.Box(r, "ExtinguisherSign", new Vector3(4.01f, 1.45f, 1.9f), new Vector3(0.01f, 0.18f, 0.18f), M.RedPaint);

            // ?꾩뼱?? ?꾧?臾멸낵 ?④퍡 ?吏곸씠誘濡?Hinge ???먯떇(?뺤쟻 ?꾨떂). ?먯옟???⑤룄 ?뺤씤 ?뚮젅?댄듃(y 1.0) ?꾩そ???붾떎
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

