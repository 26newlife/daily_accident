using System.Collections.Generic;
using GameCore;
using GameGameplay;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 엘리베이터 재구현용 월드/연결 생성 (Build Apartment Scene 에서 호출, 씬을 새로 만들므로 여러 번 실행해도 중복되지 않는다).
    ///   · 내부 세트 ElevatorCabinInterior: 금속 벽/바닥/천장, 손잡이, 거울, 조명(+붉은 비상등), 안쪽 문, 층 표시(TMP), 오른쪽 벽 조작 패널
    ///     (층 버튼 1~20 / 열림 / 닫힘 / 비상 호출 / 내리기) — 복도와 겹치지 않는 (-100, 0, 0) 영역
    ///   · 복도 쪽: 폭발 연출(ElevatorFireballEffect), 도착 징조 연기, 폭발 후 붙는 불 fire_elevator
    ///   · ElevatorController / ElevatorHazardSystem 연결 (WireElevator)
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const float CabX = -100f, CabZ = 0f;

        private struct ElevatorWorld
        {
            public ElevatorCabinInterior cabin;
            public HazardZone zone;
            public Transform outsideStand;
            public ElevatorFireballEffect fireball;
            public ParticleSystem leakSmoke;
            public FireSource blastFire;
        }

        private static ElevatorWorld elev;

        private static void BuildElevatorWorld(Transform level)
        {
            elev = new ElevatorWorld();
            Transform g = Group("ElevatorCabinInterior", level);
            Material metal = GB("ElevMetal", Hex("A9AEB5"));
            Material dark = GB("ElevDark", Hex("2B2D31"));
            Material panelMat = GB("ElevPanel", Hex("6B7078"));

            // ── 방 (내부 x -100.9~-99.1, z 0~2.0, 높이 2.4) — 벽 콜라이더가 물리 경계 ──
            Box(g, "Floor", new Vector3(CabX, -0.15f, 1.0f), new Vector3(2.2f, 0.3f, 2.4f), GB("ElevFloor", Hex("6E7278")));
            Box(g, "Ceiling", new Vector3(CabX, 2.55f, 1.0f), new Vector3(2.2f, 0.3f, 2.4f), metal);
            Box(g, "Wall_Left", new Vector3(CabX - 1.0f, 1.2f, 1.0f), new Vector3(0.2f, 2.4f, 2.4f), metal);
            Box(g, "Wall_Right", new Vector3(CabX + 1.0f, 1.2f, 1.0f), new Vector3(0.2f, 2.4f, 2.4f), metal);
            Box(g, "Wall_Back", new Vector3(CabX, 1.2f, 2.1f), new Vector3(2.2f, 2.4f, 0.2f), metal);
            Box(g, "Wall_Front", new Vector3(CabX, 1.2f, -0.1f), new Vector3(2.2f, 2.4f, 0.2f), dark);
            Box(g, "Mirror", new Vector3(CabX, 1.45f, 1.985f), new Vector3(1.2f, 1.0f, 0.02f), GB("ElevMirror", Hex("C9DCE6")), false);
            Box(g, "HandRail", new Vector3(CabX, 0.95f, 1.94f), new Vector3(1.6f, 0.04f, 0.05f), Metal(), false);
            Box(g, "CeilingPanel", new Vector3(CabX, 2.38f, 1.0f), new Vector3(0.8f, 0.03f, 0.8f), GB("ElevLamp", Hex("FFF1D0"), true), false);

            // 안쪽에서 보이는 문 (좌우 슬라이드, 콜라이더 없음)
            GameObject dl = Box(g, "DoorLeft", new Vector3(CabX - 0.25f, 1.05f, 0.04f), new Vector3(0.5f, 2.1f, 0.05f), GB("ElevDoor", Hex("8F959C")), false);
            GameObject dr = Box(g, "DoorRight", new Vector3(CabX + 0.25f, 1.05f, 0.04f), new Vector3(0.5f, 2.1f, 0.05f), GB("ElevDoor", Hex("8F959C")), false);

            // 층 표시 디스플레이 (TMP, 문 위) — 플레이어는 문 쪽(-z)을 바라보므로 180° 돌려 둔다
            TMP_FontAsset font = FindFont();
            TextMeshPro display = MakeWorldText(g, "FloorDisplay", "^ 10", new Vector3(CabX, 2.2f, 0.06f), Quaternion.Euler(0f, 180f, 0f), 1.1f, new Color(1f, 0.35f, 0.25f), new Vector2(0.9f, 0.4f), font);
            Box(g, "DisplayFrame", new Vector3(CabX, 2.2f, 0.03f), new Vector3(0.95f, 0.45f, 0.03f), Metal(), false);

            // 조명 / 비상등 / 연기
            var ml = new GameObject("MainLight"); ml.transform.SetParent(g, false); ml.transform.position = new Vector3(CabX, 2.2f, 1.0f);
            Light mainLight = ml.AddComponent<Light>(); mainLight.type = LightType.Point; mainLight.range = 4.5f; mainLight.intensity = 1.4f; mainLight.color = new Color(1f, 0.95f, 0.85f); mainLight.shadows = LightShadows.None;
            var el = new GameObject("EmergencyLight"); el.transform.SetParent(g, false); el.transform.position = new Vector3(CabX, 2.1f, 1.2f);
            Light emergency = el.AddComponent<Light>(); emergency.type = LightType.Point; emergency.range = 3.5f; emergency.intensity = 0.9f; emergency.color = new Color(1f, 0.1f, 0.05f); emergency.shadows = LightShadows.None; emergency.enabled = false;

            ParticleSystem smoke = MakeParticles(g, "CabinSmoke", new Vector3(CabX, 2.3f, 1.6f), new Vector3(90f, 0f, 0f), ParticleMat("ElevSmoke", new Color(0.18f, 0.18f, 0.18f, 0.5f), false));
            var sm = smoke.main; sm.startLifetime = 3f; sm.startSpeed = 0.4f; sm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f); sm.startColor = new Color(0.18f, 0.18f, 0.18f, 0.55f); sm.gravityModifier = 0f;
            var sem = smoke.emission; sem.rateOverTime = 28f;
            var ssh = smoke.shape; ssh.shapeType = ParticleSystemShapeType.Cone; ssh.angle = 35f; ssh.radius = 0.2f;
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 격리 구역: 안에서는 집 전체 연기를 무시하고, 호흡기 0 → 엘리베이터 연기 질식 사유
            elev.zone = EvacZone("zone_elevator_cabin", new Vector3(CabX - 0.9f, 0f, 0.05f), new Vector3(CabX + 0.9f, 2.4f, 1.95f), false, 1f, DeathReason.Suffocation);   // 격리/사망 사유는 시나리오(S03Bootstrap)가 적용
            Transform spawn = Spawn("Spawn_ElevatorCabin", new Vector3(CabX, 0.02f, 1.2f), 180f);

            // ── 조작 패널: 오른쪽 벽(문을 바라볼 때 -x 쪽) ──
            Box(g, "PanelPlate", new Vector3(CabX - 0.885f, 1.2f, 1.05f), new Vector3(0.03f, 0.95f, 0.7f), panelMat, false);
            var panelGo = new GameObject("FloorPanel"); panelGo.transform.SetParent(g, false);
            var panel = panelGo.AddComponent<ElevatorFloorPanel>();
            var buttons = new List<ElevatorButton>();
            float px = CabX - 0.86f;
            for (int f = 1; f <= 20; f++)
            {
                int row = (f - 1) / 4, col = (f - 1) % 4;
                buttons.Add(MakeElevatorButton($"elevator_btn_floor_{f:00}", $"{f}층 선택", ElevatorButton.ButtonKind.Floor, f, new Vector3(px, 1.00f + 0.15f * row, 0.80f + 0.15f * col), $"{f}", font));
            }
            buttons.Add(MakeElevatorButton("elevator_btn_open", "문 열림", ElevatorButton.ButtonKind.Open, 1, new Vector3(px, 0.85f, 0.80f), "<>", font));
            buttons.Add(MakeElevatorButton("elevator_btn_close", "문 닫힘", ElevatorButton.ButtonKind.Close, 1, new Vector3(px, 0.85f, 0.95f), "><", font));
            buttons.Add(MakeElevatorButton("elevator_btn_alarm", "비상 호출", ElevatorButton.ButtonKind.Alarm, 1, new Vector3(px, 0.85f, 1.10f), "!", font));
            buttons.Add(MakeElevatorButton("elevator_exit", "엘리베이터에서 내리기", ElevatorButton.ButtonKind.Exit, 1, new Vector3(px, 0.85f, 1.25f), "EX", font));
            panel.Init(buttons.ToArray());
            var pso = SO(panel); SetObjectArray(pso.FindProperty("buttons"), buttons.ToArray()); pso.ApplyModifiedPropertiesWithoutUndo();

            var cabin = g.gameObject.AddComponent<ElevatorCabinInterior>();
            var cso = SO(cabin);
            cso.FindProperty("doorLeft").objectReferenceValue = dl.transform;
            cso.FindProperty("doorRight").objectReferenceValue = dr.transform;
            cso.FindProperty("floorText").objectReferenceValue = display;
            cso.FindProperty("mainLight").objectReferenceValue = mainLight;
            cso.FindProperty("emergencyLight").objectReferenceValue = emergency;
            cso.FindProperty("smokeFx").objectReferenceValue = smoke;
            cso.FindProperty("zone").objectReferenceValue = elev.zone;
            cso.FindProperty("playerSpawn").objectReferenceValue = spawn;
            cso.FindProperty("panel").objectReferenceValue = panel;
            cso.ApplyModifiedPropertiesWithoutUndo();
            elev.cabin = cabin;

            // ── 복도 쪽 연출 (엘리베이터 문 x=-1.5, 복도 z -2.2~0) ──
            Transform fx = Group("ElevatorHallFx", level);
            elev.outsideStand = Spawn("Spawn_ElevatorOutside", new Vector3(-1.5f, 0.02f, -1.2f), 0f);

            var fbGo = new GameObject("ElevatorFireball"); fbGo.transform.SetParent(fx, false); fbGo.transform.position = new Vector3(-1.5f, 1.0f, -2.0f);
            ParticleSystem flame = MakeParticles(fbGo.transform, "Flame", Vector3.zero, Vector3.zero, ParticleMat("Flame", new Color(1f, 0.6f, 0.2f, 0.9f), true));
            var fm = flame.main; fm.loop = false; fm.startLifetime = 1.0f; fm.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f); fm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f); fm.startColor = new Color(1f, 0.55f, 0.15f, 0.9f); fm.gravityModifier = -0.1f;
            var fs = flame.shape; fs.shapeType = ParticleSystemShapeType.Cone; fs.angle = 32f; fs.radius = 0.5f;
            flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem blastSmoke = MakeParticles(fbGo.transform, "BlastSmoke", Vector3.zero, Vector3.zero, ParticleMat("SmokePuff", new Color(0.1f, 0.1f, 0.1f, 0.55f), false));
            var bm = blastSmoke.main; bm.loop = false; bm.startLifetime = 3.5f; bm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f); bm.startSize = new ParticleSystem.MinMaxCurve(1f, 2f); bm.startColor = new Color(0.1f, 0.1f, 0.1f, 0.6f);
            var bs = blastSmoke.shape; bs.shapeType = ParticleSystemShapeType.Cone; bs.angle = 40f; bs.radius = 0.6f;
            blastSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var fl = new GameObject("FlashLight"); fl.transform.SetParent(fbGo.transform, false); fl.transform.localPosition = new Vector3(0f, 0f, 0.8f);
            Light flash = fl.AddComponent<Light>(); flash.type = LightType.Point; flash.range = 9f; flash.color = new Color(1f, 0.6f, 0.25f); flash.intensity = 0f; flash.shadows = LightShadows.None; flash.enabled = false;
            var fb = fbGo.AddComponent<ElevatorFireballEffect>();
            var fbso = SO(fb);
            fbso.FindProperty("flame").objectReferenceValue = flame;
            fbso.FindProperty("smoke").objectReferenceValue = blastSmoke;
            fbso.FindProperty("flashLight").objectReferenceValue = flash;
            fbso.FindProperty("blastAudio").objectReferenceValue = AddAudio("ElevatorBlastAudio", fbGo.transform.position, false, true);
            fbso.ApplyModifiedPropertiesWithoutUndo();
            elev.fireball = fb;

            // 도착 징조: 문틈으로 새는 작은 연기
            elev.leakSmoke = MakeParticles(fx, "ElevatorLeakSmoke", new Vector3(-1.5f, 2.05f, -2.0f), Vector3.zero, ParticleMat("LeakSmoke", new Color(0.35f, 0.35f, 0.35f, 0.4f), false));
            var lm = elev.leakSmoke.main; lm.startLifetime = 2.5f; lm.startSpeed = 0.25f; lm.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f); lm.startColor = new Color(0.35f, 0.35f, 0.35f, 0.45f); lm.gravityModifier = -0.02f;
            var lem = elev.leakSmoke.emission; lem.rateOverTime = 7f;
            var lsh = elev.leakSmoke.shape; lsh.shapeType = ParticleSystemShapeType.Box; lsh.scale = new Vector3(1.4f, 0.05f, 0.05f);
            elev.leakSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 폭발 후 복도에 붙는 불 (반경 밖일 때)
            elev.blastFire = AddFire("fire_elevator", FireType.Normal, new Vector3(-1.5f, 0.5f, -1.4f), new Vector3(1.2f, 1.0f, 0.8f), "zone_corridor", false, "엘리베이터 앞 불길");
        }

        private static TextMeshPro MakeWorldText(Transform parent, string name, string text, Vector3 pos, Quaternion rot, float size, Color color, Vector2 box, TMP_FontAsset font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = box;
            return t;
        }

        private static ElevatorButton MakeElevatorButton(string id, string prompt, ElevatorButton.ButtonKind kind, int floor, Vector3 pos, string label, TMP_FontAsset font)
        {
            var root = new GameObject(id);
            root.transform.SetParent(lvInteract, false);
            root.transform.position = pos;
            GameObject vis = Box(root.transform, "Visual", Vector3.zero, new Vector3(0.03f, 0.09f, 0.09f), GB("ElevBtn", Hex("C0C4CA")));
            MakeWorldText(root.transform, "Label", label, new Vector3(-0.02f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), 0.45f, Color.black, new Vector2(0.09f, 0.09f), font);
            var b = root.AddComponent<ElevatorButton>();
            var so = SO(b);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("floor").intValue = floor;
            so.FindProperty("lamp").objectReferenceValue = vis.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return b;
        }
    }
}
