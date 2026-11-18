using System.Collections.Generic;
using GameGameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameUI.EditorTools.Interior
{
    public static partial class ApartmentInteriorBuilder
    {
        // ───────── 조명 상수 (한곳에서 조절) ─────────
        // 환경광: 예전엔 단색 0.16 으로 꺼진 방이 새까맣고 평평했다. 3색 환경광으로 천장/벽/바닥의 밝기 차이를 살리되,
        // 정전·화재 때 어두운 분위기가 유지되도록 낮게 잡았다. 너무 밝으면 아래 값만 낮추면 된다.
        private static readonly Color AmbientSky = new Color(0.30f, 0.33f, 0.38f);
        private static readonly Color AmbientEquator = new Color(0.22f, 0.22f, 0.22f);
        private static readonly Color AmbientGround = new Color(0.11f, 0.10f, 0.09f);

        private static readonly Color SunColor = new Color(1.0f, 0.93f, 0.80f);     // 아침 햇빛 (약간 따뜻하게)
        private const float SunIntensity = 1.0f;
        private static readonly Vector3 SunEuler = new Vector3(38f, 205f, 0f);      // 북·동쪽 창으로 비스듬히 들어온다
        private static readonly Color WindowLightColor = new Color(1.0f, 0.95f, 0.85f);
        private const float WindowLightIntensity = 1.1f, WindowLightRange = 3.5f, WindowLightInset = 0.6f;
        private const int ProbeResolution = 128;

        private static void BuildLighting()
        {
            Transform lights = Group("Lights", root);

            // 환경광. 연기 안개(SmokeAtmosphere)는 RenderSettings.fog 만 바꾸므로 여기서 Volume/Fog 를 쓰지 않는다
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.fog = false;

            // 아침 햇빛(Directional): 천장 슬래브와 벽이 막고 창문으로만 들어와 빛줄기가 생긴다. 그림자는 이 라이트 하나만 사용
            var sunGo = new GameObject("Sun_Morning");
            sunGo.transform.SetParent(lights, false);
            sunGo.transform.rotation = Quaternion.Euler(SunEuler);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;

            // 창가 약한 면광 느낌의 포인트 라이트 (그림자 없음)
            var daylight = new List<Light> { sun };
            AddWindowLight(lights, daylight, "window_bedroom", "Pane");
            AddWindowLight(lights, daylight, "window_living", "Pane");
            AddWindowLight(lights, daylight, "window_kitchen", "Pane");
            AddWindowLight(lights, daylight, "door_balcony", "Pane");
            Transform balconyGlass = structure.Find("Wall_BalconyNorth/balcony_glass");
            if (balconyGlass != null) AddWindowLightAt(lights, daylight, "balcony_glass", balconyGlass.GetComponent<Renderer>().bounds);

            // LightGroup("daylight"): 정전/화재 연출에서 LightGroup.SetAllOn(false) 가 햇빛도 같이 끈다 (기존 방 조명과 동일하게 일괄 제어)
            var lgGo = new GameObject("LG_daylight");
            lgGo.transform.SetParent(lights, false);
            var group = lgGo.AddComponent<LightGroup>();
            var so = new SerializedObject(group);
            so.FindProperty("groupId").stringValue = "daylight";
            SetArray(so.FindProperty("lights"), daylight.ToArray());
            so.FindProperty("startOn").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 방마다 반사 프로브 1개 (Baked — Lighting 창에서 Generate Lighting 으로 굽는다)
            var probes = Group("ReflectionProbes", root);
            AddProbe(probes, "Bedroom", new Vector3(1.3f, 1.2f, 4.9f), new Vector3(2.5f, 2.4f, 4.9f));
            AddProbe(probes, "LivingHall", new Vector3(4.6f, 1.2f, 5.2f), new Vector3(4.0f, 2.4f, 5.6f));
            AddProbe(probes, "Kitchen", new Vector3(5.2f, 1.2f, 3.5f), new Vector3(2.7f, 2.4f, 2.2f));
            AddProbe(probes, "Utility", new Vector3(5.35f, 1.2f, 1.2f), new Vector3(2.4f, 2.4f, 2.3f));
            AddProbe(probes, "Bathroom", new Vector3(0.95f, 1.2f, 1.2f), new Vector3(1.8f, 2.4f, 2.3f));
            AddProbe(probes, "Entry", new Vector3(3.0f, 1.2f, 1.2f), new Vector3(2.2f, 2.4f, 2.3f));
            AddProbe(probes, "Balcony", new Vector3(2.7f, 1.2f, 8.2f), new Vector3(5.4f, 2.4f, 1.6f));
            AddProbe(probes, "Shelter", new Vector3(6.0f, 1.2f, 8.2f), new Vector3(1.2f, 2.4f, 1.6f));
            AddProbe(probes, "Corridor", new Vector3(-2.2f, 1.2f, -1.1f), new Vector3(23.5f, 2.4f, 2.2f));
        }

        private static void AddWindowLight(Transform parent, List<Light> list, string id, string paneName)
        {
            Transform t = Interactable(id);
            Transform pane = t != null ? t.Find(paneName) : null;
            var r = pane != null ? pane.GetComponent<Renderer>() : null;
            if (r != null) AddWindowLightAt(parent, list, id, r.bounds);
        }

        /// <summary>창 중심에서 방 안쪽으로 0.6m 들어온 위치에 포인트 라이트</summary>
        private static void AddWindowLightAt(Transform parent, List<Light> list, string name, Bounds b)
        {
            bool thinX = b.size.x < b.size.z;
            float inward = thinX ? -Mathf.Sign(b.center.x - ApartmentCenter.x) : -Mathf.Sign(b.center.z - ApartmentCenter.y);
            Vector3 pos = b.center + (thinX ? Vector3.right : Vector3.forward) * inward * WindowLightInset;

            var go = new GameObject("WindowLight_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = WindowLightColor;
            l.intensity = WindowLightIntensity;
            l.range = WindowLightRange;
            l.shadows = LightShadows.None;
            list.Add(l);
        }

        private static void AddProbe(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Probe_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Baked;
            p.resolution = ProbeResolution;
            p.size = size;
            p.boxProjection = true;
            p.importance = 1;
        }

        // ═════════════════════════ 점프 대비 안전 콜라이더 ═════════════════════════
        // 열린 창문의 개구부(바닥에서 1.0m 안팎)는 가구 위에서 점프하면 넘을 수 있다. 창문 바깥쪽 외벽 면에 투명 콜라이더를 둬서
        // 창밖으로 나가는 경로를 막는다. 안쪽에서 보면 창문 유리/손잡이가 먼저 맞으므로 상호작용 레이캐스트는 가려지지 않는다.
        private const float WallHalfExterior = 0.125f, BarrierThickness = 0.1f, BarrierGap = 0.02f;

        private static void BuildSafetyColliders()
        {
            Transform safety = Group("Safety", root);
            foreach (string id in new[] { "window_bedroom", "window_living", "window_kitchen" })
            {
                Transform t = Interactable(id);
                Transform pane = t != null ? t.Find("Pane") : null;
                var r = pane != null ? pane.GetComponent<Renderer>() : null;
                if (r == null) continue;

                Bounds b = r.bounds;
                bool thinX = b.size.x < b.size.z;
                float outward = thinX ? Mathf.Sign(b.center.x - ApartmentCenter.x) : Mathf.Sign(b.center.z - ApartmentCenter.y);
                Vector3 axis = thinX ? Vector3.right : Vector3.forward;

                var go = new GameObject("WindowBarrier_" + id);
                go.transform.SetParent(safety, false);
                go.transform.position = b.center + axis * outward * (WallHalfExterior + BarrierGap + BarrierThickness * 0.5f);
                var bc = go.AddComponent<BoxCollider>();
                float along = thinX ? b.size.z : b.size.x;
                bc.size = thinX
                    ? new Vector3(BarrierThickness, b.size.y + 0.6f, along + 0.4f)
                    : new Vector3(along + 0.4f, b.size.y + 0.6f, BarrierThickness);
            }

            // 베란다: 폭발로 고정 유리(balcony_glass)가 깨지면 개구부가 열린다. 외벽 바깥(z>9.125)에 전폭 투명 벽을 세워 추락/탈출을 막는다
            var balcony = new GameObject("WindowBarrier_balcony");
            balcony.transform.SetParent(safety, false);
            balcony.transform.position = new Vector3(2.7f, 1.5f, 9.125f + BarrierGap + BarrierThickness * 0.5f);
            balcony.AddComponent<BoxCollider>().size = new Vector3(5.8f, 3.4f, BarrierThickness);

            // 통유리 위치의 보이지 않는 안전 콜라이더: 유리가 깨져 콜라이더가 꺼져도 베란다 끝(난간 높이 이상)에서 막는다.
            // 베란다 안쪽 상호작용 대상(화분/칸막이/방화문)은 모두 유리보다 안쪽이라 레이캐스트를 가리지 않는다
            var inner = new GameObject("WindowBarrier_balcony_glass");
            inner.transform.SetParent(safety, false);
            inner.transform.position = new Vector3((GlassX0 + GlassX1) * 0.5f, 1.5f, GlassPlaneZ + 0.03f);
            inner.AddComponent<BoxCollider>().size = new Vector3(GlassX1 - GlassX0 + 0.2f, 3.4f, BarrierThickness * 0.6f);
        }
    }
}
