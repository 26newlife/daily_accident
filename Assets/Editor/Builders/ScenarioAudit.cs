using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameCore;
using GameGameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 시나리오 체크리스트(Act 1~4 + 공통) 자동 감사.  메뉴: Tools > GameUI > Debug > Run Full Audit
    /// GameScene 을 읽어(열려 있지 않으면 추가로 열었다가 닫는다) 체크리스트의 상호작용 id / 구역 / 시스템 / 아이템 / 엔딩 / 사망 피드백이
    /// 씬에 "존재"하는지, 그리고 Act·시스템의 직렬화 필드나 EvacKeys 상수로 "이벤트에 연결"되어 있는지를 검사해
    /// [Audit] 항목 PASS/FAIL 목록을 Console 에 출력한다. 씬을 수정하지 않는다.
    /// </summary>
    public static class ScenarioAudit
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";

        private enum Wire { Referenced, Self, None }

        private struct Entry
        {
            public string checkId, id, note;
            public Type type;
            public Wire wire;
        }

        private static int pass, fail;
        private static HashSet<string> referenced;
        private static Dictionary<string, InteractableObject> interactables;
        private static HashSet<string> zones;

        [MenuItem("Tools/GameUI/Debug/Run Full Audit")]
        public static void RunFullAudit()
        {
            // 집(HomeBase = GameScene) + 시나리오 3 씬을 함께 읽는다 (시나리오 오브젝트는 Additive 씬에 있다)
            var opened = new System.Collections.Generic.List<Scene>();
            var scenes = new System.Collections.Generic.List<Scene>();
            foreach (string path in new[] { GameScenePath, MainMenuBuilder.S03ScenePath })
            {
                Scene sc = SceneManager.GetSceneByPath(path);
                if (!sc.IsValid() || !sc.isLoaded)
                {
                    if (!System.IO.File.Exists(path)) { Debug.LogError($"[Audit] {path} 가 없습니다. Tools > GameUI > Build Apartment Scene 을 먼저 실행하세요."); foreach (Scene o in opened) EditorSceneManager.CloseScene(o, true); return; }
                    sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    opened.Add(sc);
                }
                scenes.Add(sc);
            }

            try { Run(scenes.ToArray()); }
            finally { foreach (Scene o in opened) EditorSceneManager.CloseScene(o, true); }
        }

        // ═════════════════════ 실행 ═════════════════════
        private static void Run(Scene[] scenes)
        {
            pass = fail = 0;
            Debug.Log("[Audit] ───── 시나리오 감사 시작 ─────");

            var rootList = new System.Collections.Generic.List<GameObject>(); foreach (Scene sc in scenes) rootList.AddRange(sc.GetRootGameObjects());
            GameObject[] roots = rootList.ToArray();
            interactables = new Dictionary<string, InteractableObject>();
            zones = new HashSet<string>();
            var comps = new List<MonoBehaviour>();
            foreach (GameObject r in roots)
            {
                foreach (InteractableObject i in r.GetComponentsInChildren<InteractableObject>(true)) if (!interactables.ContainsKey(i.Id)) interactables[i.Id] = i;
                foreach (HazardZone z in r.GetComponentsInChildren<HazardZone>(true)) zones.Add(z.ZoneId);
                comps.AddRange(r.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m != null && !(m is InteractableObject)));
            }
            referenced = CollectReferencedStrings(comps);

            T Find<T>() where T : Component
            {
                foreach (GameObject r in roots) { T t = r.GetComponentInChildren<T>(true); if (t != null) return t; }
                return null;
            }

            // ─────────────── Act 1 ───────────────
            Interact("A1-1", "alarm_clock", typeof(TriggerInteractable), Wire.Referenced, "알람 끄기");
            Interact("A1-1", "wash_face", typeof(TriggerInteractable), Wire.Referenced, "세수");
            Interact("A1-1", "drink_water", typeof(TriggerInteractable), Wire.Referenced, "물 마시기");
            var act1 = Find<Act1_MorningSigns>();
            var gas = Find<GasLeakController>();
            Field("A1-2", "GasLeakController.leakAudio", gas, "leakAudio");
            Field("A1-3", "Act1.sparkParticles", act1, "sparkParticles");
            Field("A1-3", "Act1.sparkAudio", act1, "sparkAudio");
            Interact("A1-3", "multitap_switch", typeof(TriggerInteractable), Wire.Referenced, "스파크 중 누르면 점화");
            ArrayNonEmpty("A1-4", "Act1.announcementCues", act1, "announcementCues");
            Field("A1-4", "Act1.announcementSource", act1, "announcementSource");
            foreach (string id in new[] { "switch_light_bedroom", "switch_light_living", "coffee_pot", "microwave", "range_hood", "gas_range" })
                Interact("A1-5", id, null, Wire.Referenced, "가스 누출 중 조작 → 폭발 사망");
            Check("A1-6", "PhoneUI.flashlight", Find<PhoneUI>() != null && Prop(Find<PhoneUI>(), "flashlight") != null, "스마트폰 손전등");
            Interact("A1-7", "fuse_cock", typeof(ValveInteractable), Wire.Self, "퓨즈콕");
            Interact("A1-7", "gas_valve_main", typeof(ValveInteractable), Wire.Self, "메인 밸브");
            foreach (string w in new[] { "window_living", "window_kitchen", "window_bedroom" })
                Interact("A1-8", w, typeof(ToggleInteractable), Wire.Self, "창문 열기");
            Interact("A1-9", "multitap_plug", typeof(RemovableInteractable), Wire.Referenced, "코드 뽑기");
            foreach (string id in new[] { "clutter_paper_1", "clutter_cloth_1", "clutter_paper_2" }) Interact("A1-9", id, typeof(RemovableInteractable), Wire.Referenced, "가연물 치우기");
            Check("A1-10", "Act1 → Act2 전환", act1 != null, "Act1_MorningSigns 존재");

            ObjectiveRefs("A1-1", "Act1 objectives", act1, new[] { "objMorning", "objAlarm", "objWash", "objDrink", "objPhone", "objSmell", "objGasResponse", "objFlashlight", "objFuse", "objMainValve", "objVentilate", "objMultitap" });

            // ─────────────── Act 2 ───────────────
            var act2 = Find<Act2_BlastAndSmoke>();
            ArrayNonEmpty("A2-1", "Act2.glassToShatter", act2, "glassToShatter");
            Check("A2-1", "LightGroup exit", HasLightGroup(roots, "exit"), "정전 시 유도등만 점등");
            Interact("A2-2", "fire_curtain", typeof(FireSource), Wire.Self, "커튼 발화");
            Interact("A2-2", "fire_pan", typeof(FireSource), Wire.Self, "식용유 팬 발화");
            Check("A2-3", "SmokeAtmosphere/PlayerVitals", Find<SmokeAtmosphere>() != null && Find<PlayerVitals>() != null, "연기 시야 + 호흡기");
            Check("A2-4", "fire_pan(Oil)/fire_multitap(Electrical)", IsFire("fire_pan", FireType.Oil) && IsFire("fire_multitap", FireType.Electrical), "물 붓기 사망");
            var ext = Interact("A2-5", "pickup_extinguisher", typeof(PickupInteractable), Wire.Self, "소화기 획득");
            Check("A2-5", "ExtinguisherController", Find<ExtinguisherController>() != null, "안전핀 + 분사");
            Interact("A2-6", "sink_wet_towel", typeof(ConvertInteractable), Wire.Self, "수건 적시기");
            Interact("A2-6", "pickup_towel", typeof(PickupInteractable), Wire.Self, "수건");
            Interact("A2-6", "pickup_towel_2", typeof(PickupInteractable), Wire.Self, "수건 2");
            Interact("A2-6", "pickup_water_bottle", typeof(PickupInteractable), Wire.Self, "생수병");
            Interact("A2-7", "door_handle_check", typeof(TriggerInteractable), Wire.Referenced, "손잡이 온도 확인");
            Interact("A2-8", "door_front", typeof(ToggleInteractable), Wire.Referenced, "뜨거운 문 개방 → 백드래프트");
            Interact("A2-8", "fire_corridor", typeof(FireSource), Wire.Self, "복도 불");

            ObjectiveRefs("A2-1", "Act2 objectives", act2, new[] { "objMain", "objExtinguisher", "objSuppress", "objTowel", "objBottle", "objHandle" });

            // ─────────────── Act 3 ───────────────
            var act3 = Find<Act3_FireSpreads>();
            Check("A3-1", "Act3.frontDoor/corridorZone", act3 != null && Prop(act3, "frontDoor") != null && Prop(act3, "corridorZone") != null, "현관문 닫기 + 복도 연기");
            Interact("A3-2", EvacKeys.NpcGrandmother, typeof(GrandmotherNpc), Wire.Self, "할머니 NPC");
            Interact("A3-3", EvacKeys.NpcYoungMan, typeof(YoungManNpc), Wire.Self, "청년 NPC");
            Interact("A3-4", "elevator_door", typeof(ElevatorInteractable), Wire.Self, "엘리베이터 호출");
            Interact("A3-4", "elevator_call_button", typeof(ElevatorInteractable), Wire.Self, "호출 버튼");
            Interact("A3-4", "elevator_cabin", typeof(ElevatorInteractable), Wire.Self, "탑승");
            Check("A3-4", "ElevatorController/Cabin", Find<ElevatorController>() != null && Find<ElevatorCabinInterior>() != null && Prop(Find<ElevatorController>(), "cabin") != null && Prop(Find<ElevatorController>(), "exteriorDoor") != null, "상태 머신 + 내부 세트");
            for (int f = 1; f <= 20; f++) Interact("A3-4", $"elevator_btn_floor_{f:00}", typeof(ElevatorButton), Wire.Self, "층 버튼");
            foreach (string eb in new[] { "elevator_btn_open", "elevator_btn_close", "elevator_btn_alarm", "elevator_exit" }) Interact("A3-4", eb, typeof(ElevatorButton), Wire.Self, "조작 패널");
            Zone("A3-4", "zone_elevator_cabin");
            Check("A3-4", "ElevatorHazardSystem/Config", Find<ElevatorHazardSystem>() != null && Prop(Find<ElevatorHazardSystem>(), "config") != null, "ElevatorHazardConfig");
            Check("A3-5", "Grandmother follow", Find<GrandmotherNpc>() != null && Find<ChoicePanel>() != null, "수건 건네기·동행 대화 UI");
            Interact("A3-6", EvacKeys.StairFireDoor, typeof(ToggleInteractable), Wire.Referenced, "비상계단 방화문");
            Field("A3-6", "Act3.stairSmokeFx", act3, "stairSmokeFx");
            Zone("A3-7", EvacKeys.ZoneCorridor);
            Zone("A3-7", EvacKeys.ZoneStairwell);
            ArrayNonNull("A3-1", "Act3.objectivePool", act3, "objectivePool");

            // ─────────────── Act 4 ───────────────
            var act4 = Find<Act4_Evacuation>();
            Interact("A4-A1", EvacKeys.StairsUp, typeof(TriggerInteractable), Wire.Referenced, "계단 올라가기");
            Interact("A4-A2", EvacKeys.FireDoor14, typeof(ToggleInteractable), Wire.Referenced, "14층 방화문");
            Interact("A4-A2", EvacKeys.FireDoor17, typeof(ToggleInteractable), Wire.Referenced, "17층 방화문");
            Interact("A4-A3", EvacKeys.KeyBox, typeof(TimedTriggerInteractable), Wire.Referenced, "열쇠함 유리 깨기");
            Interact("A4-A3", EvacKeys.RoofDoor, typeof(TriggerInteractable), Wire.Referenced, "옥상 문");
            Interact("A4-A4", EvacKeys.RoofSignal, typeof(TimedTriggerInteractable), Wire.Referenced, "구조 신호");
            foreach (string z in new[] { EvacKeys.ZoneLanding14, EvacKeys.ZoneLanding17, EvacKeys.ZoneLanding20, "zone_roof" }) Zone("A4-A", z);
            Interact("A4-B1", EvacKeys.FlowerPot, typeof(RemovableInteractable), Wire.Self, "화분 치우기");
            Interact("A4-B1", EvacKeys.FlowerPot2, typeof(RemovableInteractable), Wire.Self, "화분 2");
            Interact("A4-B1", EvacKeys.StorageBox, typeof(RemovableInteractable), Wire.Self, "짐 상자");
            Interact("A4-B2", EvacKeys.LightPartition, typeof(TriggerInteractable), Wire.Referenced, "경량칸막이(집 껍데기)"); Check("A4-B2", "PartitionBreakable controller", Find<PartitionBreakable>() != null && Prop(Find<PartitionBreakable>(), "panelRoot") != null, "시나리오 로직 + 집 바인딩");
            Interact("A4-B2", EvacKeys.PickupHammer, typeof(PickupInteractable), Wire.Self, "망치");
            Interact("A4-B3", EvacKeys.Door1003Inside, typeof(TriggerInteractable), Wire.Referenced, "1003호 현관");
            Zone("A4-B3", EvacKeys.Zone1003);
            Interact("A4-C1", EvacKeys.ShelterFireDoor, typeof(ToggleInteractable), Wire.Referenced, "대피공간 방화문");
            foreach (string id in new[] { EvacKeys.SealBottom, EvacKeys.SealLeft, EvacKeys.SealRight, EvacKeys.SealTop }) Interact("A4-C2", id, typeof(SealPoint), Wire.Referenced, "문틈 밀봉");
            Interact("A4-C2", "pickup_duct_tape", typeof(PickupInteractable), Wire.Self, "청테이프 (거실 탁자)");
            Interact("A4-C3", EvacKeys.ShelterWindow, typeof(ToggleInteractable), Wire.Referenced, "대피공간 창문");
            Check("A4-C3", "Call119Controller", Find<Call119Controller>() != null && Find<ChoicePanel>() != null, "119 신고 선택지");
            Check("A4-C4", "ShelterSealSystem", Find<ShelterSealSystem>() != null, "연기 유입 + 구조 대기");
            Interact("A4-D1", EvacKeys.StairsDown, typeof(TriggerInteractable), Wire.Referenced, "계단 하강");
            foreach (string z in new[] { EvacKeys.ZoneDescent1, EvacKeys.ZoneDescent2, EvacKeys.ZoneDescent3 }) Zone("A4-D1", z);   // 계단탑 9~7 / 6~4 / 3~1층 (S03StairTowerRoutes)
            Zone("A4-D2", EvacKeys.ZoneAssembly);
            Check("A4-D2", "Act4.outdoor (OnFireTruckArrive)", act4 != null && Prop(act4, "outdoor") != null, "외부 집결지 연출 연동");
            Check("A4-E1", "RouteResolver", typeof(RouteResolver) != null && act4 != null, "루트 분기 로직");
            foreach (string f in new[] { "stairRoomSpawn", "grandmotherFallenPoint", "shelterSeal", "partition", "roofKeyItem", "helicopter", "rescueLadder" })
                Field("A4-E1", "Act4." + f, act4, f);
            ArrayNonNull("A4-E1", "Act4.objectivePool", act4, "objectivePool");
            var def = AssetDatabase.LoadAssetAtPath<GameData.ScenarioDefinition>(MainMenuBuilder.S03Dir + "/Data/S03_Definition.asset");
            Check("FW", "S03 ScenarioDefinition", def != null && def.IsPlayable && def.ScenarioSceneName == "S03_ApartmentFire", "정의 + 씬 이름");
            foreach (string e in new[] { EvacKeys.EndingRooftop, EvacKeys.EndingPartition, EvacKeys.EndingShelter, EvacKeys.EndingDescent, EvacKeys.EndingHidden })
                Check("A4-E2", e, def != null && def.Endings != null && def.Endings.Any(x => x != null && x.Id == e), "ScenarioDefinition 에 EndingData 등록");
            var score = Find<ScoreSystem>();
            Check("A4-E3", "ScenarioDefinition.scoreRules", def != null && def.ScoreRules != null && def.ScoreRules.rules != null && def.ScoreRules.rules.Length >= 10, "ScoreRules (항목·배점·등급)");

            // ─────────────── 공통 ───────────────
            foreach (string item in new[] { "wet_towel", "towel", "extinguisher", "duct_tape", "water_bottle", "key", "hammer", "roof_key" })
                Check("C-2", "ItemData " + item, AssetDatabase.FindAssets("t:ItemData").Select(g => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g))).Any(i => i != null && i.Id == item), "ItemData 에셋");
            Check("C-3", "PhoneUI / ObjectiveSystem / HintSystem", Find<PhoneUI>() != null && Find<ObjectiveSystem>() != null && Find<HintSystem>() != null, "스마트폰·목표·힌트");
            Check("C-4", "ScenarioDefinition.deathReasons (no null entries)", def != null && def.DeathReasons != null && def.DeathReasons.Length > 0 && !def.DeathReasons.Any(x => x == null), "사망 사유 목록");
            Check("A4-E2", "ScenarioDefinition.endings (no null entries)", def != null && def.Endings != null && def.Endings.Length > 0 && !def.Endings.Any(x => x == null), "엔딩 목록");
            foreach (DeathReason r in Enum.GetValues(typeof(DeathReason)))
                Check("C-4", "DeathReason." + r, r == DeathReason.Unknown || IsWarOnlyReason(r) || IsQuakeOnlyReason(r) || (def != null && def.DeathReasons != null && def.DeathReasons.Any(x => x != null && x.Reason == r && !string.IsNullOrEmpty(x.WhyYouDied) && !string.IsNullOrEmpty(x.CorrectAction))), "교육 피드백 연결");
            Check("C-5", "GameRunContext/RunCleanup", FindObjectOfType_GameSceneEntry(roots), "씬 진입 시 GameRunContext.Begin");
            Check("C-5", "EvacDirector", Find<EvacDirector>() != null, "압축 이동 코루틴 (씬 소속 → 재시작 시 정리)");

            Debug.Log($"[Audit] ───── 완료: PASS {pass} / FAIL {fail} ─────");
            if (fail > 0) Debug.LogWarning($"[Audit] FAIL {fail}건 — Tools > GameUI > Build Apartment Scene 을 (백업 후) 다시 실행했는지 확인하세요.");
        }

        /// <summary>시나리오 1(전시 상황) 전용 사망 사유 — S01 정의에서 검사한다 (시나리오 3 감사 대상 아님)</summary>
        /// <summary>시나리오 2(자연재해) 전용 사망 사유 (QuakeFallingObjects ~ QuakeGroundCrack, Tsunami) — S02 정의에서 검사한다</summary>
        private static bool IsQuakeOnlyReason(DeathReason r) => (int)r >= (int)DeathReason.QuakeFallingObjects && (int)r <= (int)DeathReason.QuakeGroundCrack;

        private static bool IsWarOnlyReason(DeathReason r) => r == DeathReason.NuclearFlash || r == DeathReason.ShockwaveDebris || r == DeathReason.RadiationSickness || r == DeathReason.Hypothermia || r == DeathReason.Starvation;

        // ═════════════════════ 검사 헬퍼 ═════════════════════
        private static InteractableObject Interact(string checkId, string id, Type type, Wire wire, string note)
        {
            interactables.TryGetValue(id, out InteractableObject obj);
            bool exists = obj != null;
            bool typeOk = exists && (type == null || type.IsAssignableFrom(obj.GetType()));
            bool wired = wire == Wire.None || wire == Wire.Self || referenced.Contains(id);
            string detail = !exists ? "씬에 없음" : !typeOk ? $"타입 불일치({obj.GetType().Name})" : !wired ? "이벤트에 연결 안 됨(어떤 Act/시스템도 이 id 를 참조하지 않음)" : note;
            Report(checkId, id, exists && typeOk && wired, detail);
            return obj;
        }

        private static void Zone(string checkId, string zoneId) => Report(checkId, zoneId, zones.Contains(zoneId), zones.Contains(zoneId) ? "구역 존재" : "구역 없음");

        private static void Check(string checkId, string label, bool ok, string note) => Report(checkId, label, ok, ok ? note : note + " — 없음");

        private static void Field(string checkId, string label, Component owner, string field)
        {
            bool ok = owner != null && Prop(owner, field) != null;
            Report(checkId, label, ok, ok ? "연결됨" : owner == null ? "소유 컴포넌트가 씬에 없음" : "필드가 비어 있음(NULL)");
        }

        private static void ArrayNonEmpty(string checkId, string label, Component owner, string field)
        {
            bool ok = false;
            if (owner != null)
            {
                SerializedProperty p = new SerializedObject(owner).FindProperty(field);
                ok = p != null && p.isArray && p.arraySize > 0;
            }
            Report(checkId, label, ok, ok ? "배열 채워짐" : "배열이 비어 있음");
        }

        /// <summary>목표 에셋 필드가 모두 실제 에셋을 가리키는가 (깨진 에셋이면 씬에 {fileID: 0} 으로 남는다)</summary>
        private static void ObjectiveRefs(string checkId, string label, Component owner, string[] fields)
        {
            int missing = 0; var names = new List<string>();
            foreach (string f in fields) if (Prop(owner, f) == null) { missing++; names.Add(f); }
            Report(checkId, label, owner != null && missing == 0, missing == 0 ? $"{fields.Length}개 연결됨" : $"NULL {missing}개: {string.Join(", ", names)}");
        }

        private static void ArrayNonNull(string checkId, string label, Component owner, string field)
        {
            bool ok = false; string detail = "배열이 비어 있음";
            if (owner != null)
            {
                SerializedProperty p = new SerializedObject(owner).FindProperty(field);
                if (p != null && p.isArray && p.arraySize > 0)
                {
                    int nulls = 0;
                    for (int i = 0; i < p.arraySize; i++) if (p.GetArrayElementAtIndex(i).objectReferenceValue == null) nulls++;
                    ok = nulls == 0; detail = ok ? $"{p.arraySize}개 연결됨" : $"NULL 항목 {nulls}개";
                }
            }
            Report(checkId, label, ok, detail);
        }

        private static UnityEngine.Object Prop(Component owner, string field)
        {
            if (owner == null) return null;
            SerializedProperty p = new SerializedObject(owner).FindProperty(field);
            UnityEngine.Object v = p != null && p.propertyType == SerializedPropertyType.ObjectReference ? p.objectReferenceValue : null;
            // 시나리오 씬의 집 참조는 에디터에서는 비어 있고 ScenarioSceneRoot.bindings 에 기록되어 런타임에 복원된다 → 바인딩이 있으면 연결된 것으로 본다
            if (v == null && p != null && IsBound(owner, field)) return owner;
            return v;
        }

        private static bool IsBound(Component owner, string field)
        {
            ScenarioSceneRoot root = owner.gameObject.scene.IsValid() ? FindSceneRoot(owner.gameObject.scene) : null;
            if (root == null) return false;
            string path = ScenarioPaths.PathOf(owner.transform);
            foreach (ScenarioBinding b in root.Bindings) if (b.ownerPath == path && (b.propertyPath == field || b.propertyPath.StartsWith(field + "."))) return true;
            return false;
        }

        private static ScenarioSceneRoot FindSceneRoot(Scene s)
        {
            foreach (GameObject r in s.GetRootGameObjects()) { ScenarioSceneRoot sr = r.GetComponentInChildren<ScenarioSceneRoot>(true); if (sr != null) return sr; }
            return null;
        }

        private static bool IsFire(string id, FireType type) => interactables.TryGetValue(id, out InteractableObject o) && o is FireSource f && f.Type == type;

        private static bool HasLightGroup(GameObject[] roots, string id)
        {
            foreach (GameObject r in roots) foreach (LightGroup g in r.GetComponentsInChildren<LightGroup>(true)) if (g.GroupId == id) return true;
            return false;
        }

        private static bool FindObjectOfType_GameSceneEntry(GameObject[] roots)
        {
            foreach (GameObject r in roots) if (r.GetComponentInChildren<GameSceneEntry>(true) != null) return true;
            return false;
        }

        private static void Report(string checkId, string what, bool ok, string detail)
        {
            if (ok) { pass++; Debug.Log($"[Audit] {checkId} {what} PASS  ({detail})"); }
            else { fail++; Debug.LogWarning($"[Audit] {checkId} {what} FAIL  ({detail})"); }
        }

        /// <summary>Act/시스템이 직렬화 필드에 들고 있는 문자열 + EvacKeys 상수 = "이벤트에 연결된 id" 집합</summary>
        private static HashSet<string> CollectReferencedStrings(List<MonoBehaviour> comps)
        {
            var set = new HashSet<string>();
            foreach (FieldInfo f in typeof(EvacKeys).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                if (f.IsLiteral && f.FieldType == typeof(string)) set.Add((string)f.GetRawConstantValue());

            foreach (MonoBehaviour m in comps)
            {
                if (m == null || m.GetType().Namespace != "GameGameplay") continue;
                var so = new SerializedObject(m);
                SerializedProperty it = so.GetIterator();
                while (it.NextVisible(true))
                    if (it.propertyType == SerializedPropertyType.String && !string.IsNullOrEmpty(it.stringValue)) set.Add(it.stringValue);
            }
            return set;
        }
    }
}
