using System.Collections.Generic;
using GameCore;
using GameGameplay;
using UnityEditor;
using UnityEngine;

namespace GameUI.EditorTools
{
    /// <summary>
    /// Act 3~4 월드 그레이박스 (프리미티브만, 외부 에셋 없음).
    ///   · 복도 NPC 2종, 비상계단 위/아래 상호작용, 계단 연기 파티클
    ///   · 층 압축 연출용 계단참 3개(14·17·20층) + 옥상, 계단 하강 3구간 + 1층 외부 집결지
    ///   · 1003호 내부(루트 B), 경량칸막이 파괴/베란다 짐 치우기 연결, 대피공간 문틈 밀봉 4면 + 구조 사다리
    ///   · 다용도실 망치
    /// 층 구간은 1004호에서 멀리 떨어진 별도 구역에 만들고 EvacDirector 가 페이드 + 순간이동으로 이어 준다.
    /// 모든 상호작용 id 는 snake_case, 기존 id 는 바꾸지 않는다.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        // ───────── 월드 구역 원점 (1004호 + 복도 + 계단실 영역 밖) ─────────
        private const float LandingX14 = -40f, LandingX17 = -52f, LandingX20 = -64f;   // 계단참 (z 0~4)
        private const float DescentX = -40f, DescentZ1 = 20f, DescentZ2 = 26f, DescentZ3 = 32f;
        private const float PlazaX = -40f, PlazaZ = 60f;

        private struct EvacWorld
        {
            public Transform root, spawns;
            public GrandmotherNpc grandmother;
            public YoungManNpc youngMan;
            public Transform landing14, landing17, landing20, descent1, descent2, descent3, plaza, stairRoom, grandFallen;
            public Transform heliStart, heliEnd, roofLadderPivot, rescueLadderPivot;
            public GameObject roofDoor, keyBoxGlass, helicopter, roofLadder, rescueLadder, fireTruck, ambulance;
            public ParticleSystem keyShards, stairSmoke;
            public SealPoint[] seals;
            public RemovableInteractable[] clearables;
            public PartitionBreakable partition;
        }

        private static EvacWorld evac;

        private static void BuildEvacWorld(Transform level)
        {
            evac = new EvacWorld();
            evac.root = Group("EvacWorld", level);
            evac.spawns = Group("EvacSpawns", evac.root);

            BuildEvacNpcs();
            BuildStairwellTriggers();
            BuildLanding(14, LandingX14, EvacKeys.FireDoor14, EvacKeys.StairsUp14, EvacKeys.ZoneLanding14);
            BuildLanding(17, LandingX17, EvacKeys.FireDoor17, EvacKeys.StairsUp17, EvacKeys.ZoneLanding17);
            BuildLanding(20, LandingX20, null, null, EvacKeys.ZoneLanding20);
            BuildRoof(LandingX20 + 4.5f);
            BuildDescentCorridor(1, DescentX, DescentZ1, EvacKeys.ZoneDescent1);
            BuildDescentCorridor(2, DescentX, DescentZ2, EvacKeys.ZoneDescent2);
            BuildDescentCorridor(3, DescentX, DescentZ3, EvacKeys.ZoneDescent3);
            BuildPlaza();
            BuildUnit1003();
            BuildBalconyRoute();
            BuildShelterSeals();
            BuildHammer();
        }

        // ═════════════════════ 헬퍼 ═════════════════════
        private static Transform Spawn(string name, Vector3 pos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(evac.spawns, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return go.transform;
        }

        /// <summary>zone_* 를 만들고 격리/웅크림/사망 사유를 설정한다</summary>
        private static HazardZone EvacZone(string zoneId, Vector3 min, Vector3 max, bool isolated, float crouchScale, DeathReason reason)
        {
            AddZone(zoneId, min, max);
            Transform t = lvZones.Find("Zone_" + zoneId);
            var z = t.GetComponent<HazardZone>();
            var so = SO(z);
            so.FindProperty("isolated").boolValue = isolated;
            so.FindProperty("crouchSmokeScale").floatValue = crouchScale;
            so.FindProperty("suffocationReason").enumValueIndex = System.Array.IndexOf(System.Enum.GetValues(typeof(DeathReason)), reason);
            so.ApplyModifiedPropertiesWithoutUndo();
            return z;
        }

        private static void EmergencyLight(Transform parent, string name, Vector3 pos, float intensity = 1.1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = 7f; l.intensity = intensity; l.color = new Color(1f, 0.92f, 0.8f); l.shadows = LightShadows.None;
        }

        private static void ExitSignAt(Transform parent, string name, Vector3 pos, Vector3 size)
            => Box(parent, name, pos, size, GB("ExitSign", Hex("22C55E"), true), false);

        private static GameObject TimedTrig(string id, string prompt, Vector3 center, Vector3 size, Material mat, TimedTriggerInteractable.HoldSource hold, string message = "", Transform parent = null)
        {
            var root = new GameObject(id);
            root.transform.SetParent(parent != null ? parent : lvInteract, false);
            root.transform.position = center;
            Box(root.transform, "Visual", Vector3.zero, size, mat);
            var t = root.AddComponent<TimedTriggerInteractable>();
            var so = SO(t);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("message").stringValue = message;
            so.FindProperty("holdSource").enumValueIndex = (int)hold;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject NpcBody(Transform parent, Color color, float height, float radius, bool withArm)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);
            Material m = GB("Npc_" + ColorUtility.ToHtmlStringRGB(color), color);
            Cyl(visual.transform, "Body", new Vector3(0f, height * 0.4f, 0f), radius, height * 0.8f, m, false);
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(visual.transform, false);
            head.transform.localPosition = new Vector3(0f, height * 0.9f, 0f);
            head.transform.localScale = Vector3.one * radius * 1.45f;
            head.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(head.GetComponent<Collider>());
            if (withArm)
            {
                var arm = new GameObject("Arm");
                arm.transform.SetParent(visual.transform, false);
                arm.transform.localPosition = new Vector3(radius * 0.8f, height * 0.62f, 0.18f);
                Box(arm.transform, "ArmBox", new Vector3(0f, 0f, 0.15f), new Vector3(0.08f, 0.08f, 0.35f), m, false);
            }
            return visual;
        }

        // ═════════════════════ 복도 NPC ═════════════════════
        private static void BuildEvacNpcs()
        {
            Transform npcs = Group("Npcs", evac.root);

            // 1003호 할머니: 엘리베이터 호출 패널 앞 (엘리베이터 문 x=-1.5, 패널 x=-0.4)
            var g = new GameObject("NPC_Grandmother");
            g.transform.SetParent(npcs, false);
            g.transform.SetPositionAndRotation(new Vector3(-0.55f, 0.03f, -1.6f), Quaternion.Euler(0f, 180f, 0f));
            var cc = g.AddComponent<CharacterController>();
            cc.height = 1.45f; cc.radius = 0.26f; cc.center = new Vector3(0f, 0.725f, 0f); cc.stepOffset = 0.2f; cc.skinWidth = 0.03f;
            GameObject gv = NpcBody(g.transform, new Color(0.62f, 0.52f, 0.56f), 1.45f, 0.26f, true);
            var gm = g.AddComponent<GrandmotherNpc>();
            var stand = new GameObject("ElevatorStand").transform;
            stand.SetParent(npcs, false);
            stand.position = new Vector3(-1.2f, 0.03f, -1.55f);
            var gso = SO(gm);
            gso.FindProperty("id").stringValue = EvacKeys.NpcGrandmother;
            gso.FindProperty("prompt").stringValue = "할머니에게 말 걸기";
            gso.FindProperty("displayName").stringValue = "할머니";
            gso.FindProperty("visualRoot").objectReferenceValue = gv.transform;
            gso.FindProperty("arm").objectReferenceValue = gv.transform.Find("Arm");
            gso.FindProperty("elevatorStandPoint").objectReferenceValue = stand;
            gso.FindProperty("voice").objectReferenceValue = AddNpcAudio(g.transform, "Voice");
            gso.FindProperty("buttonAudio").objectReferenceValue = AddNpcAudio(g.transform, "ButtonAudio");
            gso.ApplyModifiedPropertiesWithoutUndo();
            evac.grandmother = gm;

            // 1002호 청년: 복도 중간. 수건 없이 계단 방화문(x=-14)으로 달려간다
            var y = new GameObject("NPC_YoungMan");
            y.transform.SetParent(npcs, false);
            y.transform.SetPositionAndRotation(new Vector3(-8.6f, 0.03f, -1.3f), Quaternion.Euler(0f, 270f, 0f));
            var ycc = y.AddComponent<CharacterController>();
            ycc.height = 1.75f; ycc.radius = 0.28f; ycc.center = new Vector3(0f, 0.875f, 0f); ycc.stepOffset = 0.25f; ycc.skinWidth = 0.03f;
            GameObject yv = NpcBody(y.transform, new Color(0.25f, 0.33f, 0.5f), 1.75f, 0.28f, false);
            var ym = y.AddComponent<YoungManNpc>();
            var runTarget = new GameObject("StairRunTarget").transform;
            runTarget.SetParent(npcs, false);
            runTarget.position = new Vector3(-13.0f, 0.03f, -1.1f);
            var yso = SO(ym);
            yso.FindProperty("id").stringValue = EvacKeys.NpcYoungMan;
            yso.FindProperty("prompt").stringValue = "청년에게 말 걸기";
            yso.FindProperty("displayName").stringValue = "청년";
            yso.FindProperty("visualRoot").objectReferenceValue = yv.transform;
            yso.FindProperty("stairRunTarget").objectReferenceValue = runTarget;
            yso.FindProperty("stairDoor").objectReferenceValue = Obj<ToggleInteractable>(EvacKeys.StairFireDoor);
            yso.FindProperty("voice").objectReferenceValue = AddNpcAudio(y.transform, "Voice");
            yso.ApplyModifiedPropertiesWithoutUndo();
            evac.youngMan = ym;
        }

        private static AudioSource AddNpcAudio(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(AudioSource));
            go.transform.SetParent(parent, false);
            var a = go.GetComponent<AudioSource>();
            a.playOnAwake = false; a.spatialBlend = 1f; a.minDistance = 1f; a.maxDistance = 14f; a.rolloffMode = AudioRolloffMode.Linear;
            return a;
        }

        // ═════════════════════ 비상계단 입구 (위/아래 + 연기) ═════════════════════
        private static void BuildStairwellTriggers()
        {
            // 위층으로: 계단 윗부분 서쪽 벽의 표지
            Trig(EvacKeys.StairsUp, "위층(옥상 방향)으로 올라가기", new Vector3(-17.38f, 1.7f, -1.6f), new Vector3(0.08f, 0.7f, 1.4f), GB("StairSignUp", Hex("22C55E"), true),
                "계단이 위로 이어진다.");
            // 아래층으로: 남쪽 벽의 어두운 계단 입구
            Trig(EvacKeys.StairsDown, "아래층으로 내려가기 (1층 방향)", new Vector3(-14.75f, 0.55f, -3.07f), new Vector3(1.0f, 1.0f, 0.08f), GB("StairDownDark", Hex("1F2937")),
                "계단이 아래로 이어진다.");

            // 방화문을 열었을 때 올라오는 검은 연기 (Act 3 가 제어)
            evac.stairSmoke = MakeParticles(lvFurniture, "StairSmokeFx", new Vector3(-14.8f, 0.2f, -2.8f), new Vector3(-90f, 0f, 0f), ParticleMat("StairSmoke", new Color(0.05f, 0.05f, 0.05f, 0.6f), false));
            var m = evac.stairSmoke.main;
            m.startLifetime = 3f; m.startSpeed = 0.9f; m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            m.startColor = new Color(0.05f, 0.05f, 0.05f, 0.6f); m.gravityModifier = -0.04f;
            var s = evac.stairSmoke.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(1.4f, 1.0f, 0.2f);
            evac.stairSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            evac.stairRoom = Spawn("Spawn_StairRoom", new Vector3(-14.6f, 0.02f, -1.2f), 270f);

            // 계단실 구역 설정: 격리 + 계단 연기 사망 사유 (기존 구역 값 수정)
            Transform zt = lvZones.Find("Zone_" + EvacKeys.ZoneStairwell);
            if (zt != null)
            {
                var zso = SO(zt.GetComponent<HazardZone>());
                zso.FindProperty("isolated").boolValue = true;
                zso.FindProperty("suffocationReason").enumValueIndex = System.Array.IndexOf(System.Enum.GetValues(typeof(DeathReason)), DeathReason.StairwellSmoke);
                zso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ═════════════════════ 계단참 (14 / 17 / 20층) ═════════════════════
        private static void BuildLanding(int floor, float ox, string doorId, string upId, string zoneId)
        {
            Transform g = Group($"Landing{floor}", evac.root);
            float x1 = ox + 4.5f;
            // 바닥/천장 (콜라이더 포함)
            Box(g, "Floor", new Vector3((ox + x1) * 0.5f, -0.15f, 2f), new Vector3(x1 - ox, 0.3f, 4f), Concrete());
            Box(g, "Ceiling", new Vector3((ox + x1) * 0.5f, WallH + 0.15f, 2f), new Vector3(x1 - ox, 0.3f, 4f), Ceiling());
            BuildWall($"L{floor}_WallS", true, 0f, ox, x1, WallT);
            BuildWall($"L{floor}_WallN", true, 4f, ox, x1, WallT);
            BuildWall($"L{floor}_WallW", false, ox, 0f, 4f, WallT);

            if (doorId != null)
            {
                // 열려 있는 방화문 (닫는 상호작용). 문 너머는 아래층에서 연기가 올라오는 복도 자투리
                Opening door = Op(OpenKind.Door, 2.0f, 0.9f, 0f, DoorH, doorId, "방화문 닫기", "방화문 열기", swing: 1, inward: -1, mat: FireDoorMat());
                door.startOpen = true;
                BuildWall($"L{floor}_WallE", false, x1, 0f, 4f, WallTExt, door);
                BuildCornerStub(g, x1, 1.0f, 3.0f);
                Trig(upId, "위층으로 올라가기", new Vector3(ox + 2.0f, 1.4f, 3.88f), new Vector3(0.9f, 0.7f, 0.06f), GB("StairSignUp", Hex("22C55E"), true), "계단이 위로 이어진다.");
            }
            else
            {
                // 20층: 옥상 출입문 자리 (잠금). 열쇠함은 북쪽 벽
                Opening gap = Op(OpenKind.Gap, 2.0f, 1.1f, 0f, DoorH);
                BuildWall($"L{floor}_WallE", false, x1, 0f, 4f, WallTExt, gap);
                var door = new GameObject(EvacKeys.RoofDoor);
                door.transform.SetParent(lvInteract, false);
                door.transform.position = new Vector3(x1, 0f, 2.0f);
                Box(door.transform, "Visual", new Vector3(0f, DoorH * 0.5f, 0f), new Vector3(0.12f, DoorH, 1.1f), FireDoorMat());
                Box(door.transform, "Handle", new Vector3(-0.1f, 1.0f, 0.4f), new Vector3(0.06f, 0.18f, 0.06f), Metal(), false);
                AddTrigger(door, EvacKeys.RoofDoor, "옥상 문 열기", "");
                evac.roofDoor = door;

                var box = new GameObject(EvacKeys.KeyBox);
                box.transform.SetParent(lvInteract, false);
                box.transform.position = new Vector3(ox + 1.2f, 1.3f, 3.88f);
                Box(box.transform, "Frame", Vector3.zero, new Vector3(0.6f, 0.8f, 0.14f), Hot("KeyBoxFrame", "DC2626"));
                Box(box.transform, "Key", new Vector3(0f, 0f, -0.04f), new Vector3(0.12f, 0.06f, 0.03f), Hot("KeyOnHook", "FACC15"), false);
                GameObject glass = Box(box.transform, "Glass", new Vector3(0f, 0f, -0.08f), new Vector3(0.54f, 0.74f, 0.02f), Glass(), false);
                var t = box.AddComponent<TimedTriggerInteractable>();
                var tso = SO(t);
                tso.FindProperty("id").stringValue = EvacKeys.KeyBox;
                tso.FindProperty("prompt").stringValue = "비상 열쇠함 유리 깨기 (길게 누르기)";
                tso.FindProperty("holdSource").enumValueIndex = (int)TimedTriggerInteractable.HoldSource.RoofKeyBox;
                tso.ApplyModifiedPropertiesWithoutUndo();
                evac.keyBoxGlass = glass;
                ParticleSystem shards = MakeParticles(box.transform, "KeyShards", new Vector3(0f, 0f, -0.15f), Vector3.zero, ParticleMat("GlassShard", new Color(0.8f, 0.92f, 1f, 0.9f), false));
                var sm = shards.main; sm.loop = false; sm.startLifetime = 1.4f; sm.gravityModifier = 1.2f; sm.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                sm.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f); sm.startColor = new Color(0.8f, 0.92f, 1f, 0.9f);
                var ss = shards.shape; ss.shapeType = ParticleSystemShapeType.Box; ss.scale = new Vector3(0.5f, 0.7f, 0.05f);
                shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                evac.keyShards = shards;
                // 열쇠함 앞에서 서 있을 공간: 북쪽 벽 앞 0.9m 비움 (가구 없음)
            }

            EmergencyLight(g, "EmergencyLight", new Vector3(ox + 2.2f, 2.2f, 2.0f), 1.2f);
            ExitSignAt(g, "ExitSign", new Vector3(ox + 0.2f, 2.15f, 2.0f), new Vector3(0.06f, 0.22f, 0.7f));
            Box(g, "StairRailing", new Vector3(ox + 2.2f, 0.95f, 0.2f), new Vector3(3.0f, 0.05f, 0.05f), Metal(), false);

            HazardZone z = EvacZone(zoneId, new Vector3(ox + 0.1f, 0f, 0.1f), new Vector3(x1 + 3.2f, 2.4f, 3.9f), true, 1f, DeathReason.StairwellSmoke);
            if (floor == 14) evac.landing14 = Spawn("Spawn_Landing14", new Vector3(ox + 1.2f, 0.02f, 1.2f), 0f);
            else if (floor == 17) evac.landing17 = Spawn("Spawn_Landing17", new Vector3(ox + 1.2f, 0.02f, 1.2f), 0f);
            else evac.landing20 = Spawn("Spawn_Landing20", new Vector3(ox + 1.2f, 0.02f, 1.2f), 0f);
        }

        /// <summary>방화문 너머의 막다른 복도 자투리 (문이 열려도 떨어지지 않게 바닥·벽·천장)</summary>
        private static void BuildCornerStub(Transform g, float x, float z0, float z1)
        {
            Box(g, "StubFloor", new Vector3(x + 1.6f, -0.15f, (z0 + z1) * 0.5f), new Vector3(3.2f, 0.3f, z1 - z0 + 0.3f), Concrete());
            Box(g, "StubCeiling", new Vector3(x + 1.6f, WallH + 0.15f, (z0 + z1) * 0.5f), new Vector3(3.2f, 0.3f, z1 - z0 + 0.3f), Ceiling());
            Box(g, "StubWallS", new Vector3(x + 1.6f, WallH * 0.5f, z0 - 0.1f), new Vector3(3.2f, WallH, 0.2f), Wall());
            Box(g, "StubWallN", new Vector3(x + 1.6f, WallH * 0.5f, z1 + 0.1f), new Vector3(3.2f, WallH, 0.2f), Wall());
            Box(g, "StubWallE", new Vector3(x + 3.2f, WallH * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.2f, WallH, z1 - z0 + 0.4f), Wall());
        }

        // ═════════════════════ 옥상 ═════════════════════
        private static void BuildRoof(float x0)
        {
            Transform g = Group("Roof", evac.root);
            const float len = 16f, z0 = -3f, z1 = 7f;
            Box(g, "RoofFloor", new Vector3(x0 + len * 0.5f, -0.15f, (z0 + z1) * 0.5f), new Vector3(len, 0.3f, z1 - z0), GB("RoofSlab", Hex("6B7078")));
            // 난간 (추락 방지 콜라이더 — 옥상 가장자리)
            Material para = GB("Parapet", Hex("9AA0A8"));
            Box(g, "Parapet_N", new Vector3(x0 + len * 0.5f, 0.55f, z1), new Vector3(len, 1.1f, 0.2f), para);
            Box(g, "Parapet_S", new Vector3(x0 + len * 0.5f, 0.55f, z0), new Vector3(len, 1.1f, 0.2f), para);
            Box(g, "Parapet_E", new Vector3(x0 + len, 0.55f, (z0 + z1) * 0.5f), new Vector3(0.2f, 1.1f, z1 - z0), para);
            Box(g, "Parapet_W_South", new Vector3(x0, 0.55f, (z0 + 0f) * 0.5f), new Vector3(0.2f, 1.1f, 0f - z0), para);
            Box(g, "Parapet_W_North", new Vector3(x0, 0.55f, (4f + z1) * 0.5f), new Vector3(0.2f, 1.1f, z1 - 4f), para);
            // 옥상 소품: 물탱크, 환기구
            Box(g, "WaterTank", new Vector3(x0 + 5.5f, 1.2f, 5.0f), new Vector3(2.2f, 2.4f, 2.2f), GB("Tank", Hex("7A8794")));
            Box(g, "Vent", new Vector3(x0 + 8.5f, 0.5f, -1.8f), new Vector3(1.0f, 1.0f, 0.8f), GB("Vent", Hex("5B6670")));

            // 구조 신호 지점 (가장자리 쪽): 깃대 + 붉은 천
            float sx = x0 + len - 2.0f;
            var pole = TimedTrig(EvacKeys.RoofSignal, "구조 신호 보내기 (팔 흔들기, 길게 누르기)", new Vector3(sx, 0.9f, 2.0f), new Vector3(0.1f, 1.8f, 0.1f), Hot("SignalPole", "EF4444"),
                TimedTriggerInteractable.HoldSource.RoofSignal, "");
            Box(pole.transform, "Flag", new Vector3(0.3f, 0.6f, 0f), new Vector3(0.5f, 0.35f, 0.02f), Hot("SignalFlag", "DC2626"), false);

            HazardZone roof = EvacZone("zone_roof", new Vector3(x0 + 0.2f, 0f, z0 + 0.2f), new Vector3(x0 + len - 0.2f, 12f, z1 - 0.2f), false, 1f, DeathReason.Suffocation);

            // 헬기 (구조 신호 후 접근) — 프리미티브 조립, 처음엔 꺼 둔다
            var heli = new GameObject("Helicopter");
            heli.transform.SetParent(g, false);
            Material hm = GB("Heli", Hex("DC2626"));
            Box(heli.transform, "Body", Vector3.zero, new Vector3(3.2f, 1.3f, 1.3f), hm, false);
            Box(heli.transform, "Tail", new Vector3(-3.0f, 0.2f, 0f), new Vector3(3.0f, 0.3f, 0.3f), hm, false);
            Box(heli.transform, "Rotor", new Vector3(0f, 1.0f, 0f), new Vector3(7f, 0.05f, 0.25f), Metal(), false);
            Box(heli.transform, "Skid_L", new Vector3(0f, -0.8f, 0.5f), new Vector3(2.6f, 0.06f, 0.06f), Metal(), false);
            Box(heli.transform, "Skid_R", new Vector3(0f, -0.8f, -0.5f), new Vector3(2.6f, 0.06f, 0.06f), Metal(), false);
            heli.SetActive(false);
            evac.helicopter = heli;
            evac.heliStart = Spawn("HeliStart", new Vector3(x0 + len + 40f, 24f, 2f), 0f);
            evac.heliEnd = Spawn("HeliEnd", new Vector3(x0 + len - 4.5f, 9f, 2f), 0f);
            heli.transform.position = evac.heliStart.position;

            // 고가사다리차의 사다리 (옥상 가장자리 아래에서 올라온다)
            var ladder = new GameObject("RoofLadder");
            ladder.transform.SetParent(g, false);
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(ladder.transform, false);
            pivot.position = new Vector3(x0 + len + 0.6f, -22f, 3.2f);
            Box(pivot, "Rail", new Vector3(0f, 11f, 0f), new Vector3(0.18f, 22f, 0.9f), Hot("LadderRail", "DC2626"), false);
            Box(pivot, "Basket", new Vector3(-0.6f, 22f, 0f), new Vector3(1.0f, 0.12f, 1.2f), Metal(), false);
            ladder.SetActive(false);
            evac.roofLadder = ladder;
            evac.roofLadderPivot = pivot;
        }

        // ═════════════════════ 계단 하강 3구간 (루트 D) ═════════════════════
        private static void BuildDescentCorridor(int stage, float ox, float z0, string zoneId)
        {
            Transform g = Group($"Descent{stage}", evac.root);
            const float len = 14f, wid = 2.2f;
            Box(g, "Floor", new Vector3(ox + len * 0.5f, -0.15f, z0 + wid * 0.5f), new Vector3(len, 0.3f, wid), Concrete());
            Box(g, "Ceiling", new Vector3(ox + len * 0.5f, WallH + 0.15f, z0 + wid * 0.5f), new Vector3(len, 0.3f, wid), Ceiling());
            BuildWall($"D{stage}_WallS", true, z0, ox, ox + len, WallT);
            BuildWall($"D{stage}_WallN", true, z0 + wid, ox, ox + len, WallT);
            BuildWall($"D{stage}_WallW", false, ox, z0, z0 + wid, WallT);
            BuildWall($"D{stage}_WallE", false, ox + len, z0, z0 + wid, WallT);
            Box(g, "HandRail", new Vector3(ox + len * 0.5f, 0.95f, z0 + wid - 0.12f), new Vector3(len - 0.4f, 0.05f, 0.05f), Metal(), false);   // 벽을 짚고 내려가는 손잡이
            // 층 표지 / 층계 줄무늬 (내려가는 느낌)
            for (int i = 0; i < 12; i++) Box(g, $"StepMark_{i}", new Vector3(ox + 1.0f + i * 1.0f, 0.012f, z0 + wid * 0.5f), new Vector3(0.06f, 0.004f, wid - 0.3f), GB("StepMark", Hex("A0A5AB")), false);
            ExitSignAt(g, "ExitSign_End", new Vector3(ox + len - 0.2f, 2.15f, z0 + wid * 0.5f), new Vector3(0.06f, 0.22f, 0.7f));
            EmergencyLight(g, "Light_A", new Vector3(ox + 3.5f, 2.2f, z0 + wid * 0.5f), 0.9f);
            EmergencyLight(g, "Light_B", new Vector3(ox + 10.5f, 2.2f, z0 + wid * 0.5f), 0.9f);

            // 연기 구역: 격리 + 웅크리면 체감 20% (낮은 연기층을 피한다) + 계단 연기 사망 사유
            EvacZone(zoneId, new Vector3(ox + 0.1f, 0f, z0 + 0.1f), new Vector3(ox + len - 0.1f, 2.4f, z0 + wid - 0.1f), true, 0.2f, DeathReason.StairwellSmoke);
            // 구간 끝(다음 구간으로 이어지는 압축 지점)
            EvacZone($"zone_descent_end_{stage}", new Vector3(ox + len - 1.5f, 0f, z0 + 0.1f), new Vector3(ox + len - 0.2f, 2.4f, z0 + wid - 0.1f), false, 1f, DeathReason.Suffocation);

            Transform sp = Spawn($"Spawn_Descent{stage}", new Vector3(ox + 0.8f, 0.02f, z0 + wid * 0.5f), 90f);
            if (stage == 1) evac.descent1 = sp; else if (stage == 2) evac.descent2 = sp; else evac.descent3 = sp;
        }

        // ═════════════════════ 1층 외부 집결지 ═════════════════════
        private static void BuildPlaza()
        {
            Transform g = Group("AssemblyPlaza", evac.root);
            const float w = 36f, d = 26f;
            Box(g, "Ground", new Vector3(PlazaX + w * 0.5f, -0.15f, PlazaZ + d * 0.5f), new Vector3(w, 0.3f, d), GB("PlazaAsphalt", Hex("4B5058")));
            Box(g, "GrassPatch", new Vector3(PlazaX + 8f, 0.005f, PlazaZ + 18f), new Vector3(12f, 0.01f, 6f), GB("PlazaGrass", Hex("4E7A45")), false);
            // 보이지 않는 경계 (맵 밖으로 나가지 않게)
            Material inv = GB("PlazaBound", new Color(1f, 1f, 1f, 0f), false, true);
            foreach (var b in new[] { (new Vector3(PlazaX + w * 0.5f, 1.5f, PlazaZ - 0.1f), new Vector3(w, 3f, 0.2f)), (new Vector3(PlazaX + w * 0.5f, 1.5f, PlazaZ + d + 0.1f), new Vector3(w, 3f, 0.2f)),
                                       (new Vector3(PlazaX - 0.1f, 1.5f, PlazaZ + d * 0.5f), new Vector3(0.2f, 3f, d)), (new Vector3(PlazaX + w + 0.1f, 1.5f, PlazaZ + d * 0.5f), new Vector3(0.2f, 3f, d)) })
            {
                GameObject wall = Box(g, "Bound", b.Item1, b.Item2, inv);
                wall.GetComponent<Renderer>().enabled = false;
            }
            EmergencyLight(g, "PlazaLight", new Vector3(PlazaX + 14f, 6f, PlazaZ + 12f), 2f);

            // 소방차 / 구급차 (도착 연출 전에는 숨김)
            var truck = new GameObject("FireTruck");
            truck.transform.SetParent(g, false);
            truck.transform.position = new Vector3(PlazaX + 24f, 0f, PlazaZ + 17f);
            Box(truck.transform, "Cab", new Vector3(-2.6f, 1.3f, 0f), new Vector3(2.2f, 2.4f, 2.6f), GB("TruckRed", Hex("DC2626")));
            Box(truck.transform, "Body", new Vector3(1.4f, 1.4f, 0f), new Vector3(5.2f, 2.6f, 2.6f), GB("TruckRed", Hex("DC2626")));
            Box(truck.transform, "Stripe", new Vector3(1.4f, 1.0f, 1.31f), new Vector3(5.2f, 0.3f, 0.02f), GB("TruckStripe", Hex("F3F4F6")), false);
            Box(truck.transform, "Ladder", new Vector3(1.4f, 2.9f, 0f), new Vector3(5.0f, 0.15f, 0.8f), Metal(), false);
            truck.SetActive(false);
            evac.fireTruck = truck;
            var amb = new GameObject("Ambulance");
            amb.transform.SetParent(g, false);
            amb.transform.position = new Vector3(PlazaX + 14f, 0f, PlazaZ + 20f);
            Box(amb.transform, "Body", new Vector3(0f, 1.2f, 0f), new Vector3(5.0f, 2.3f, 2.2f), GB("AmbWhite", Hex("F3F4F6")));
            Box(amb.transform, "Cross", new Vector3(0f, 1.4f, 1.11f), new Vector3(0.9f, 0.3f, 0.02f), GB("AmbCross", Hex("DC2626")), false);
            amb.SetActive(false);
            evac.ambulance = amb;

            // 모여 있는 주민들 (캡슐 실루엣)
            Color[] cols = { Hex("8B5E3C"), Hex("5B7FA6"), Hex("9A6B8E"), Hex("6E8B5B"), Hex("B0813E"), Hex("57606B") };
            Transform crowd = Group("Residents", g);
            for (int i = 0; i < 8; i++)
            {
                var p = new GameObject($"Resident_{i}");
                p.transform.SetParent(crowd, false);
                p.transform.position = new Vector3(PlazaX + 5f + (i % 4) * 1.3f, 0f, PlazaZ + 8f + (i / 4) * 1.4f);
                NpcBody(p.transform, cols[i % cols.Length], 1.5f + 0.05f * (i % 4), 0.25f, false);
            }
            for (int i = 0; i < 4; i++)   // 안전 고깔
                Cyl(g, $"Cone_{i}", new Vector3(PlazaX + 19f + i * 1.6f, 0.25f, PlazaZ + 9f), 0.14f, 0.5f, GB("Cone", Hex("F97316")), false);

            EvacZone(EvacKeys.ZoneAssembly, new Vector3(PlazaX + 0.5f, 0f, PlazaZ + 0.5f), new Vector3(PlazaX + w - 0.5f, 6f, PlazaZ + d - 0.5f), false, 1f, DeathReason.Suffocation);
            evac.plaza = Spawn("Spawn_Plaza", new Vector3(PlazaX + 4f, 0.02f, PlazaZ + 4f), 45f);
        }

        // ═════════════════════ 1003호 (루트 B) ═════════════════════
        private static void BuildUnit1003()
        {
            Transform g = Group("Unit1003", evac.root);
            // 바닥/천장 슬래브는 Level 의 큰 슬래브(x -18~10, z -3.6~9.4)가 이미 덮고 있다. 서쪽/북쪽 벽만 세운다
            BuildWall("U1003_West", false, -6.6f, 0f, BalconyD, WallTExt);
            BuildWall("U1003_North", true, BalconyD, -6.6f, 0f, WallTExt);
            FloorPatch("Unit1003", -6.6f, 0f, 0f, BalconyD, Wood());

            // 가구 (서쪽·북쪽 벽 쪽으로 붙여 칸막이 구멍 ↔ 현관 동선을 비운다)
            Box(g, "Sofa", new Vector3(-5.9f, 0.4f, 6.0f), new Vector3(0.9f, 0.8f, 2.2f), Furn("Sofa1003", "8A7F8F"));
            Box(g, "TVBoard", new Vector3(-6.3f, 0.3f, 3.0f), new Vector3(0.4f, 0.6f, 1.6f), Furn("TV1003", "3B3F46"));
            Box(g, "Table", new Vector3(-4.6f, 0.35f, 7.8f), new Vector3(1.2f, 0.7f, 0.7f), Furn("Table1003", "8A6F55"));
            Box(g, "Cabinet", new Vector3(-6.3f, 0.9f, 1.2f), new Vector3(0.4f, 1.8f, 1.2f), Furn("Cab1003", "5A4636"));
            EmergencyLight(g, "Light1003", new Vector3(-3.2f, 2.2f, 4.5f), 0.8f);

            // 현관 안쪽: 문을 열고 복도로 나간다 (복도 쪽 장식 문 door_1003 의 안쪽 면)
            var leaf = new GameObject(EvacKeys.Door1003Inside);
            leaf.transform.SetParent(lvInteract, false);
            leaf.transform.position = new Vector3(-3.6f, 0f, 0.2f);
            Box(leaf.transform, "Visual", new Vector3(0f, 1.0f, 0f), new Vector3(0.95f, 2.0f, 0.06f), FireDoorMat());
            Box(leaf.transform, "Handle", new Vector3(0.35f, 1.0f, 0.06f), new Vector3(0.06f, 0.18f, 0.06f), Metal(), false);
            AddTrigger(leaf, EvacKeys.Door1003Inside, "1003호 현관문 열고 복도로 나가기", "");

            evac.grandFallen = Spawn("Spawn_GrandmotherFallen", new Vector3(-2.2f, 0.03f, 4.6f), 90f);
            EvacZone(EvacKeys.Zone1003, new Vector3(-6.4f, 0f, 0.3f), new Vector3(-0.2f, 2.4f, 8.9f), true, 1f, DeathReason.Suffocation);
        }

        // ═════════════════════ 경량칸막이 / 베란다 짐 (루트 B) ═════════════════════
        private static void BuildBalconyRoute()
        {
            // 경량칸막이: 기존 상호작용 박스(TriggerInteractable)를 PartitionBreakable 로 교체 (id 유지)
            Transform part = lvInteract.Find(Ids.LightPartition);
            if (part != null)
            {
                var trig = part.GetComponent<TriggerInteractable>();
                if (trig != null) Object.DestroyImmediate(trig);
                var pb = part.gameObject.AddComponent<PartitionBreakable>();
                Transform visual = part.Find("Visual");

                var cracks = new List<GameObject>();
                for (int i = 0; i < 3; i++)
                {
                    var c = Box(part, $"Crack_{i + 1}", new Vector3(0.11f, 0.4f * (i - 1) + 0.1f, 0.12f * (1 - i)), new Vector3(0.012f, 0.45f + 0.15f * i, 0.04f), GB("Crack", Hex("2A2A2A")), false, new Vector3(20f * (i + 1), 0f, 0f));
                    c.SetActive(false);
                    cracks.Add(c);
                }
                ParticleSystem debris = MakeParticles(part, "Debris", new Vector3(0.25f, 0.2f, 0f), Vector3.zero, ParticleMat("Debris", new Color(0.85f, 0.82f, 0.75f, 1f), false));
                var dm = debris.main; dm.loop = false; dm.startLifetime = 1.3f; dm.gravityModifier = 1.4f; dm.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.2f);
                dm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.11f); dm.startColor = new Color(0.85f, 0.82f, 0.75f, 1f);
                var dsh = debris.shape; dsh.shapeType = ParticleSystemShapeType.Box; dsh.scale = new Vector3(0.2f, 1.2f, 0.7f);
                debris.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                // 인테리어가 붙인 테두리/안내판도 부서질 때 함께 사라진다
                var hide = new List<GameObject>();
                Transform bal = lvInteract.parent.Find("Interior/Balcony");
                if (bal != null)
                    foreach (string n in new[] { "PartitionEdge_T", "PartitionEdge_S", "PartitionEdge_N", "PartitionSign" })
                    { Transform t = bal.Find(n); if (t != null) hide.Add(t.gameObject); }

                var so = SO(pb);
                so.FindProperty("id").stringValue = Ids.LightPartition;
                so.FindProperty("prompt").stringValue = "칸막이 걷어차기";
                so.FindProperty("visualRoot").objectReferenceValue = visual != null ? visual : part;
                SetObjectArray(so.FindProperty("crackStages"), cracks.ToArray());
                SetObjectArray(so.FindProperty("hideOnBreak"), hide.ToArray());
                so.FindProperty("debris").objectReferenceValue = debris;
                so.FindProperty("hitAudio").objectReferenceValue = AddAudio("PartitionHitAudio", part.position, false, true);
                so.ApplyModifiedPropertiesWithoutUndo();
                evac.partition = pb;
            }
            else Debug.LogWarning("[EvacBuilder] light_partition 을 찾지 못했습니다 (경량칸막이 파괴 연결 생략).");

            // 베란다 화분 / 짐 상자: 치울 수 있는 오브젝트로 (flower_pot 은 id 유지, 컴포넌트만 교체)
            var cl = new List<RemovableInteractable>();
            Transform pot = lvInteract.Find(Ids.FlowerPot);
            if (pot != null)
            {
                var trig = pot.GetComponent<TriggerInteractable>();
                if (trig != null) Object.DestroyImmediate(trig);
                cl.Add(MakeRemovable(pot.gameObject, Ids.FlowerPot, "화분 치우기", "화분을 한쪽으로 치웠다.", 1.0f));
            }
            Transform interior = lvInteract.parent.Find("Interior/Balcony");
            Transform planter = interior != null ? interior.Find("Planter") : null;
            Transform crate = interior != null ? interior.Find("CrateStack") : null;
            if (planter != null) cl.Add(MakeRemovable(planter.gameObject, EvacKeys.FlowerPot2, "화분 치우기", "화분을 치웠다.", 1.0f));
            if (crate != null) cl.Add(MakeRemovable(crate.gameObject, EvacKeys.StorageBox, "짐 상자 치우기", "짐 상자를 한쪽으로 밀어 두었다.", 1.4f));
            evac.clearables = cl.ToArray();
        }

        private static RemovableInteractable MakeRemovable(GameObject go, string id, string prompt, string message, float hold)
        {
            var r = go.GetComponent<RemovableInteractable>();
            if (r == null) r = go.AddComponent<RemovableInteractable>();
            var so = SO(r);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("message").stringValue = message;
            so.FindProperty("holdSeconds").floatValue = hold;
            so.ApplyModifiedPropertiesWithoutUndo();
            return r;
        }

        // ═════════════════════ 대피공간: 문틈 밀봉 4면 + 창문 + 구조 사다리 ═════════════════════
        private static void BuildShelterSeals()
        {
            // 방화문(x=5.4, z 7.75~8.65)의 대피공간 쪽 면에 4면 밀봉 지점을 붙인다 (얇은 띠)
            const float x = 5.49f, zc = 8.2f;
            var seals = new List<SealPoint>();
            seals.Add(MakeSeal(EvacKeys.SealBottom, SealPoint.Side.Bottom, new Vector3(x, 0.05f, zc), new Vector3(0.03f, 0.1f, 0.94f)));
            seals.Add(MakeSeal(EvacKeys.SealLeft, SealPoint.Side.Left, new Vector3(x, 1.05f, zc - 0.5f), new Vector3(0.03f, 2.1f, 0.08f)));
            seals.Add(MakeSeal(EvacKeys.SealRight, SealPoint.Side.Right, new Vector3(x, 1.05f, zc + 0.5f), new Vector3(0.03f, 2.1f, 0.08f)));
            seals.Add(MakeSeal(EvacKeys.SealTop, SealPoint.Side.Top, new Vector3(x, 2.08f, zc), new Vector3(0.03f, 0.08f, 1.0f)));
            evac.seals = seals.ToArray();
            Transform shelterDoor = lvInteract.Find(Ids.ShelterFireDoor);
            if (shelterDoor != null) foreach (SealPoint s in seals) s.transform.SetParent(shelterDoor, true);   // 문틀의 일부로 취급 (통로 검증에서 문 열림 반경을 침범하지 않는다)

            // 구조 사다리 (고가사다리차): 대피공간 창문(동쪽 벽 x=6.6, z=8.2) 밖에서 올라온다
            var ladder = new GameObject("RescueLadder");
            ladder.transform.SetParent(evac.root, false);
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(ladder.transform, false);
            pivot.position = new Vector3(7.7f, -22f, 8.2f);
            Box(pivot, "Rail", new Vector3(0f, 11f, 0f), new Vector3(0.18f, 22f, 0.9f), Hot("LadderRail2", "DC2626"), false);
            Box(pivot, "Basket", new Vector3(-0.55f, 21.95f, 0f), new Vector3(1.0f, 0.12f, 1.2f), Metal(), false);
            ladder.SetActive(false);
            evac.rescueLadder = ladder;
            evac.rescueLadderPivot = pivot;
        }

        private static SealPoint MakeSeal(string id, SealPoint.Side side, Vector3 center, Vector3 size)
        {
            var root = new GameObject(id);
            root.transform.SetParent(lvInteract, false);
            root.transform.position = center;
            Box(root.transform, "Visual", Vector3.zero, size, GB("SealGap", Hex("FFF3C4"), true));          // 밀봉 전: 틈으로 새는 밝은 선
            GameObject tape = Box(root.transform, "Sealed", Vector3.zero, size * 1.02f, GB("SealTape", Hex("9CA3AF")), false);
            tape.SetActive(false);
            var sp = root.AddComponent<SealPoint>();
            var so = SO(sp);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("prompt").stringValue = "문틈 막기";
            so.FindProperty("side").enumValueIndex = (int)side;
            so.FindProperty("gapVisual").objectReferenceValue = root.transform.Find("Visual").gameObject;
            so.FindProperty("sealedVisual").objectReferenceValue = tape;
            so.FindProperty("sealAudio").objectReferenceValue = AddAudio(id + "_Audio", center, false, true);
            so.ApplyModifiedPropertiesWithoutUndo();
            return sp;
        }

        // ═════════════════════ 다용도실 망치 ═════════════════════
        private static void BuildHammer()
        {
            // 세탁기 위에 놓인 공구함의 망치 (세탁기 (4.6, 0.5) 위 — 바닥에 새 가구를 늘리지 않아 통로 검증에 영향이 없다)
            AddPickup(EvacKeys.PickupHammer, lvData.hammer, new Vector3(4.6f, 0.96f, 0.5f), new Vector3(0.32f, 0.07f, 0.1f), Hot("Hammer", "B91C1C"));
        }
    }
}
