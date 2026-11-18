using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;
using Object = UnityEngine.Object;

namespace GameUI.EditorTools.Interior
{
    public static partial class ApartmentInteriorBuilder
    {
        // ═════════════════════════ 베란다 통유리 ═════════════════════════
        // 예전에는 SkinStructure 가 Wall_ 아래 모든 렌더러에 불투명 벽 재질을 덮어써서 balcony_glass 가 "벽"으로 보였다(창밖이 안 보이던 원인).
        // 이제 베란다 북쪽 외벽의 개구부를 바닥~천장 통유리로 넓히고, 얇은 알루미늄 프레임과 세로 중간 프레임 2개를 둔다.
        // 거실↔베란다 미닫이문(door_balcony)도 문 위 벽(상인방)을 걷어 통유리로 만든다 (id/ToggleInteractable 은 그대로).
        private const string BalconyGlassName = "balcony_glass";
        private const float GlassX0 = 0.1f, GlassX1 = 5.3f;          // 대피공간 벽(x 5.4~)과 서쪽 외벽 사이 개구부
        private const float GlassPlaneZ = 8.9f;                      // 벽 안쪽 면(8.875) 바로 앞
        private const float GlassThickness = 0.03f, GlassHeight = 2.4f;
        private const float FrameWidth = 0.06f, FrameDepth = 0.08f;  // 프레임 5~8cm
        private const int MullionCount = 2;
        private const float DoorTransomBottom = 2.05f;

        private static void BuildBalconyFullGlass()
        {
            Transform wall = structure.Find("Wall_BalconyNorth");
            Transform glass = wall != null ? wall.Find(BalconyGlassName) : null;
            if (wall == null || glass == null) { Debug.LogWarning("[Interior] Wall_BalconyNorth/balcony_glass 를 찾지 못해 베란다 통유리 변환을 건너뜁니다."); return; }

            // 1) 개구부 구간의 벽 조각 제거/축소 (이미 변환된 씬에서도 같은 결과)
            foreach (Transform piece in Snapshot(wall))
            {
                if (piece == glass) continue;
                ClipPieceAlongX(piece, GlassX0, GlassX1);
            }

            // 2) 유리: 바닥~천장 한 장, 그림자 없음(햇빛 차단 방지), 콜라이더는 유지(파손 시 Act2 가 끈다)
            float cx = (GlassX0 + GlassX1) * 0.5f, width = GlassX1 - GlassX0;
            glass.localPosition = new Vector3(cx, GlassHeight * 0.5f, GlassPlaneZ);
            glass.localScale = new Vector3(width, GlassHeight, GlassThickness);
            var rend = glass.GetComponent<Renderer>();
            rend.sharedMaterial = M.BalconyGlass;
            NoShadow(rend);

            // 3) 프레임 (콜라이더 없음). 중간 세로 프레임 MullionCount 개
            Transform frame = Group("BalconyGlassFrame", root);
            float zc = GlassPlaneZ;
            Prim.Box(frame, "Rail_Bottom", new Vector3(cx, FrameWidth * 0.5f, zc), new Vector3(width, FrameWidth, FrameDepth), M.BalconyFrame);
            Prim.Box(frame, "Rail_Top", new Vector3(cx, GlassHeight - FrameWidth * 0.5f, zc), new Vector3(width, FrameWidth, FrameDepth), M.BalconyFrame);
            Prim.Box(frame, "Stile_L", new Vector3(GlassX0 + FrameWidth * 0.5f, GlassHeight * 0.5f, zc), new Vector3(FrameWidth, GlassHeight, FrameDepth), M.BalconyFrame);
            Prim.Box(frame, "Stile_R", new Vector3(GlassX1 - FrameWidth * 0.5f, GlassHeight * 0.5f, zc), new Vector3(FrameWidth, GlassHeight, FrameDepth), M.BalconyFrame);
            for (int i = 1; i <= MullionCount; i++)
            {
                float x = GlassX0 + width * i / (MullionCount + 1);
                Prim.Box(frame, "Mullion_" + i, new Vector3(x, GlassHeight * 0.5f, zc), new Vector3(FrameWidth, GlassHeight, FrameDepth), M.BalconyFrame);
            }

            BuildBalconyDoorTransom();
        }

        /// <summary>미닫이문 위 벽(상인방)을 없애고 유리판을 문짝 위로 이어 붙인다. 문짝(Pane)의 자식이라 문과 함께 움직인다</summary>
        private static void BuildBalconyDoorTransom()
        {
            Transform door = Interactable("door_balcony");
            Transform pane = door != null ? door.Find("Pane") : null;
            Transform northWall = structure.Find("Wall_North");
            if (pane == null || northWall == null) return;

            float doorX = door.position.x;
            foreach (Transform piece in Snapshot(northWall))
            {
                var r = piece.GetComponent<Renderer>();
                if (r == null) continue;
                Bounds b = r.bounds;
                if (b.min.y >= DoorTransomBottom && Mathf.Abs(b.center.x - doorX) < 0.7f && b.size.x < 1.6f) Object.DestroyImmediate(piece.gameObject);
            }

            var paneRenderer = pane.GetComponent<Renderer>();
            paneRenderer.sharedMaterial = M.BalconyGlass;
            NoShadow(paneRenderer);

            Transform old = pane.Find("Transom");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Vector3 ps = pane.lossyScale;
            float paneTop = pane.position.y + ps.y * 0.5f;
            float height = GlassHeight - paneTop;
            if (height < 0.05f) return;
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.name = "Transom";
            Object.DestroyImmediate(t.GetComponent<Collider>());
            t.transform.SetParent(pane, false);
            t.transform.localPosition = new Vector3(0f, (ps.y * 0.5f + height * 0.5f) / ps.y, 0f);
            t.transform.localScale = new Vector3(1f, height / ps.y, 1f);
            var tr = t.GetComponent<Renderer>();
            tr.sharedMaterial = M.BalconyGlass;
            NoShadow(tr);
        }

        private static void NoShadow(Renderer r)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static List<Transform> Snapshot(Transform parent)
        {
            var list = new List<Transform>();
            foreach (Transform c in parent) list.Add(c);
            return list;
        }

        /// <summary>벽 조각(큐브)이 x 구간 [open0, open1] 과 겹치면 구간 밖만 남기도록 자르거나 제거한다</summary>
        private static void ClipPieceAlongX(Transform piece, float open0, float open1)
        {
            if (!piece.name.StartsWith("Wall_BalconyNorth_")) return;
            float a = piece.localPosition.x - piece.localScale.x * 0.5f, b = piece.localPosition.x + piece.localScale.x * 0.5f;
            if (b <= open0 + 0.001f || a >= open1 - 0.001f) return;                  // 겹치지 않음
            if (a >= open0 - 0.001f && b <= open1 + 0.001f) { Object.DestroyImmediate(piece.gameObject); return; }
            if (a < open0) b = open0; else a = open1;                                // 한쪽만 겹침 → 남는 쪽만 유지
            Vector3 p = piece.localPosition, s = piece.localScale;
            p.x = (a + b) * 0.5f; s.x = b - a;
            piece.localPosition = p; piece.localScale = s;
        }
    }
}
