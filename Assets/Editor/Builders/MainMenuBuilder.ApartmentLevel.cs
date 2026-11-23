using System;
using System.Collections.Generic;
using GameGameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 1004호 + 10층 복도 + 비상계단 입구 그레이박스 레벨 생성 (Unity 기본 Cube + 색상 머티리얼).
    /// 좌표계: x = 동쪽, z = 북쪽, y = 위. 1004호 내부는 x 0~6.6, z 0~7.4 (+ 베란다 z 7.4~9.0), 복도는 z -2.2~0 (x -14~9.5).
    /// 모든 오브젝트는 "루트(상호작용 컴포넌트) + Visual 자식(프리미티브)" 구조라서
    /// 나중에 Visual 만 모델로 교체하면 된다.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string MaterialDir = "Assets/Materials/Graybox";

        // ───────── 치수 (m) ─────────
        private const float WallH = 2.4f;        // 벽 높이
        private const float WallT = 0.15f;       // 실내 벽 두께
        private const float WallTExt = 0.25f;    // 외벽/세대 경계 두께
        private const float DoorH = 2.1f;
        private const float AptW = 6.6f, AptD = 7.4f, BalconyD = 9.0f;

        // ───────── 상호작용 id (에디터 빌더에서만 사용하는 배치 데이터) ─────────
        private static class Ids
        {
            public const string FrontDoor = "door_front";
            public const string BedroomDoor = "door_bedroom", BathroomDoor = "door_bathroom", UtilityDoor = "door_utility";
            public const string BalconyDoor = "door_balcony", ShelterFireDoor = "fire_door_shelter", StairFireDoor = "fire_door_stairs";
            public const string Door1003 = "door_1003", Door1002 = "door_1002", Elevator = "elevator_door";
            public const string WindowBedroom = "window_bedroom", WindowLiving = "window_living", WindowKitchen = "window_kitchen";
            public const string GasValveMain = "gas_valve_main", FuseCock = "fuse_cock", GasRange = "gas_range";
            public const string RangeHood = "range_hood", FryingPan = "frying_pan";
            public const string Tv = "tv_living", Multitap = "multitap_living";
            public const string SwitchBedroom = "switch_light_bedroom", SwitchLiving = "switch_light_living";
            public const string ShoeCabinet = "shoe_cabinet", Closet = "closet_bedroom", Bed = "bed_bedroom";
            public const string LightPartition = "light_partition", FlowerPot = "flower_pot";
            public const string PickupTowel = "pickup_towel", PickupExtinguisher = "pickup_extinguisher", PickupKey = "pickup_key";
            public const string PickupTape = "pickup_duct_tape", PickupWater = "pickup_water_bottle";
            // Act 1
            public const string AlarmClock = "alarm_clock", WashFace = "wash_face", DrinkWater = "drink_water";
            public const string CoffeePot = "coffee_pot", Microwave = "microwave";
            public const string MultitapSwitch = "multitap_switch", MultitapPlug = "multitap_plug";
            public const string Clutter1 = "clutter_paper_1", Clutter2 = "clutter_cloth_1", Clutter3 = "clutter_paper_2";
            // Act 2
            public const string FireCurtain = "fire_curtain", FireSofa = "fire_sofa", FirePan = "fire_pan";
            public const string FireMultitap = "fire_multitap", FireCorridor = "fire_corridor";
            public const string SinkWet = "sink_wet_towel", PotWater = "pickup_water_pot", PickupTowel2 = "pickup_towel_2";
            public const string DoorHandleCheck = "door_handle_check";
        }

        private static readonly Dictionary<string, Material> graybox = new Dictionary<string, Material>();
        private static Transform lvStructure, lvFurniture, lvInteract, lvLights, lvZones;
        private static readonly Dictionary<string, LightGroup> lightGroups = new Dictionary<string, LightGroup>();

        // Act 1 연출 슬롯 (레벨 빌드 시 생성, 씬 빌더가 Act1 에 연결)
        private static AudioSource lvAlarmAudio, lvSparkAudio, lvLeakAudio;
        private static ParticleSystem lvSparks;

        private const float WindowHoldSeconds = 1.5f, WindowLockHoldSeconds = 1.2f;

        private static void SetHold(Object component, float seconds)
        {
            var so = SO(component);
            so.FindProperty("holdSeconds").floatValue = seconds;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>자리표시 상호작용의 추가 설정 (길게 누르기 / 1회성)</summary>
        private static void ConfigTrigger(GameObject root, float hold, bool oneShot)
        {
            var t = root.GetComponent<TriggerInteractable>();
            var so = SO(t);
            so.FindProperty("holdSeconds").floatValue = hold;
            so.FindProperty("oneShot").boolValue = oneShot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioSource AddAudio(string name, Vector3 pos, bool loop, bool spatial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(lvInteract.parent, false);
            go.transform.position = pos;
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = loop;
            a.spatialBlend = spatial ? 1f : 0f;
            a.minDistance = 1f;
            a.maxDistance = 14f;
            a.rolloffMode = AudioRolloffMode.Linear;
            return a;
        }

        private static RemovableInteractable AddRemovable(string id, string prompt, string message, float hold, Vector3 center, Vector3 size, Material mat)
        {
            var root = new GameObject(id);
            root.transform.SetParent(lvInteract, false);
            root.transform.position = center;
            Box(root.transform, "Visual", Vector3.zero, size, mat);
            var r = root.AddComponent<RemovableInteractable>();
            var so = SO(r);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("message").stringValue = message;
            so.FindProperty("holdSeconds").floatValue = hold;
            so.ApplyModifiedPropertiesWithoutUndo();
            return r;
        }

        // ═════════════════════════ Act 2: 불 / 연기 / 연출 ═════════════════════════
        private static GameplayAssets lvData;
        private static ParticleSystem lvGlassShards;
        private static FireSource fireCurtain, fireSofa, firePan, fireMultitap, fireCorridor;

        private static Material ParticleMat(string name, Color color, bool additive)
        {
            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/GB_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                mat = new Material(sh) { name = "GB_" + name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", additive ? 2f : 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>월드 좌표계 파티클 시스템 생성 (정지 상태, 방출 0)</summary>
        private static ParticleSystem MakeParticles(Transform parent, string name, Vector3 localPos, Vector3 euler, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            var ps = go.AddComponent<ParticleSystem>();
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private static FireSource AddFire(string id, FireType type, Vector3 pos, Vector3 colliderSize, string zoneId, bool apartment, string prompt)
        {
            var root = new GameObject(id);
            root.transform.SetParent(lvInteract, false);
            root.transform.position = pos;
            root.AddComponent<BoxCollider>().size = colliderSize;

            // 불꽃
            ParticleSystem flame = MakeParticles(root.transform, "Flame", Vector3.zero, new Vector3(-90f, 0f, 0f), ParticleMat("Flame", new Color(1f, 0.6f, 0.2f, 0.9f), true));
            var fm = flame.main;
            fm.startLifetime = 0.8f; fm.startSpeed = 0.9f; fm.gravityModifier = -0.15f;
            fm.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            fm.startColor = new Color(1f, 0.6f, 0.2f, 0.9f);
            var fs = flame.shape; fs.shapeType = ParticleSystemShapeType.Cone; fs.angle = 14f; fs.radius = 0.18f;
            var fc = flame.colorOverLifetime; fc.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.05f), 0.6f), new GradientColorKey(new Color(0.3f, 0.05f, 0f), 1f) },
                         new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fc.color = grad;
            var fz = flame.sizeOverLifetime; fz.enabled = true; fz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            // 검은 연기
            ParticleSystem smoke = MakeParticles(root.transform, "Smoke", new Vector3(0f, 0.3f, 0f), new Vector3(-90f, 0f, 0f), ParticleMat("SmokePuff", new Color(0.1f, 0.1f, 0.1f, 0.55f), false));
            var sm = smoke.main;
            sm.startLifetime = 3.5f; sm.startSpeed = 0.5f; sm.gravityModifier = -0.03f;
            sm.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            sm.startColor = new Color(0.1f, 0.1f, 0.1f, 0.55f);
            var ss = smoke.shape; ss.shapeType = ParticleSystemShapeType.Cone; ss.angle = 25f; ss.radius = 0.2f;
            var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));

            // 열원(조명)
            var lgo = new GameObject("FireLight");
            lgo.transform.SetParent(root.transform, false);
            lgo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var light = lgo.AddComponent<Light>();
            light.type = LightType.Point; light.range = 6f; light.intensity = 0f;
            light.color = new Color(1f, 0.55f, 0.25f); light.shadows = LightShadows.None; light.enabled = false;

            // 불 소리 슬롯
            var ago = new GameObject("FireAudio");
            ago.transform.SetParent(root.transform, false);
            var audio = ago.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.loop = true; audio.spatialBlend = 1f; audio.maxDistance = 14f; audio.rolloffMode = AudioRolloffMode.Linear;

            var fire = root.AddComponent<FireSource>();
            var so = SO(fire);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("fireType").enumValueIndex = (int)type;
            so.FindProperty("countsForApartment").boolValue = apartment;
            so.FindProperty("flame").objectReferenceValue = flame;
            so.FindProperty("smokeFx").objectReferenceValue = smoke;
            so.FindProperty("fireLight").objectReferenceValue = light;
            so.FindProperty("loopAudio").objectReferenceValue = audio;
            Transform zt = lvZones.Find("Zone_" + zoneId);
            if (zt != null) so.FindProperty("zone").objectReferenceValue = zt.GetComponent<HazardZone>();
            SetObjectArray(so.FindProperty("waterItems"), new[] { lvData.waterPot, lvData.water });
            so.ApplyModifiedPropertiesWithoutUndo();
            return fire;
        }

        private static void BuildFireSources()
        {
            fireCurtain = AddFire(Ids.FireCurtain, FireType.Normal, new Vector3(3.95f, 1.1f, 7.15f), new Vector3(2.4f, 1.8f, 0.3f), "zone_living", true, "불타는 커튼");
            fireSofa = AddFire(Ids.FireSofa, FireType.Normal, new Vector3(5.5f, 0.6f, 5.6f), new Vector3(0.9f, 0.8f, 2.0f), "zone_living", true, "불타는 소파");
            firePan = AddFire(Ids.FirePan, FireType.Oil, new Vector3(6.3f, 1.2f, 3.0f), new Vector3(0.4f, 0.45f, 0.4f), "zone_kitchen", true, "불타는 식용유 팬");
            fireMultitap = AddFire(Ids.FireMultitap, FireType.Electrical, new Vector3(3.0f, 0.25f, 4.65f), new Vector3(0.55f, 0.4f, 0.35f), "zone_living", true, "불타는 멀티탭");
            fireCorridor = AddFire(Ids.FireCorridor, FireType.Normal, new Vector3(3.0f, 0.6f, -1.2f), new Vector3(1.2f, 1.1f, 0.9f), "zone_corridor", false, "복도의 불길");

            // 커튼 → 소파 / 멀티탭 으로 번진다
            var cso = SO(fireCurtain);
            SetObjectArray(cso.FindProperty("spreadTargets"), new[] { fireSofa, fireMultitap });
            cso.ApplyModifiedPropertiesWithoutUndo();

            // 멀티탭 플러그를 뽑았다면 전기가 흐르지 않는다 (물을 붓는 것이 더 이상 감전 사망이 아님)
            Transform plug = lvInteract.Find(Ids.MultitapPlug);
            if (plug != null)
            {
                var mso = SO(fireMultitap);
                mso.FindProperty("unplugSource").objectReferenceValue = plug.GetComponent<RemovableInteractable>();
                mso.ApplyModifiedPropertiesWithoutUndo();
            }

            // 베란다 유리 파편 (폭발 시 1회 분출)
            lvGlassShards = MakeParticles(lvFurniture, "GlassShards", new Vector3(3.0f, 1.5f, 8.2f), Vector3.zero, ParticleMat("GlassShard", new Color(0.8f, 0.92f, 1f, 0.9f), false));
            var gm = lvGlassShards.main;
            gm.loop = false; gm.duration = 1f;
            gm.startLifetime = 1.8f; gm.gravityModifier = 1.2f;
            gm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            gm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            gm.startColor = new Color(0.8f, 0.92f, 1f, 0.9f);
            var gs = lvGlassShards.shape; gs.shapeType = ParticleSystemShapeType.Box; gs.scale = new Vector3(3.2f, 1.2f, 1.8f);
            var ge = lvGlassShards.emission; ge.rateOverTime = 0f; ge.SetBursts(new[] { new ParticleSystem.Burst(0f, 100) });
            lvGlassShards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>현관문 안쪽 손잡이 부근에 "손잡이 온도 확인" 상호작용 (문과 함께 움직인다)</summary>
        private static void BuildDoorHandleCheck()
        {
            Transform hinge = lvInteract.Find(Ids.FrontDoor + "/Hinge");
            if (hinge == null) return;
            var plate = new GameObject(Ids.DoorHandleCheck);
            plate.transform.SetParent(hinge, false);
            plate.transform.localPosition = new Vector3(0.95f - 0.12f, 1.0f, 0.09f);
            Box(plate.transform, "Visual", Vector3.zero, new Vector3(0.14f, 0.14f, 0.06f), Hot("HandlePlate", "FDE68A"));
            AddTrigger(plate, Ids.DoorHandleCheck, "문손잡이 온도 확인 (손등 대기)");
            ConfigTrigger(plate, 1.5f, false);
        }

        /// <summary>비상구 유도등을 LightGroup("exit")으로 묶는다 — 정전 시에도 이 그룹만 다시 켠다</summary>
        private static void BuildExitLightGroup(List<GameObject> signs)
        {
            var root = new GameObject("LG_exit");
            root.transform.SetParent(lvLights, false);
            var lights = new List<Light>();
            var lamps = new List<Renderer>();
            int i = 0;
            foreach (GameObject s in signs)
            {
                var lgo = new GameObject($"Light_{i++}");
                lgo.transform.SetParent(root.transform, false);
                lgo.transform.position = s.transform.position + Vector3.down * 0.3f;
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point; l.range = 5f; l.intensity = 0.8f;
                l.color = new Color(0.3f, 1f, 0.5f); l.shadows = LightShadows.None;
                lights.Add(l);
                lamps.Add(s.GetComponent<Renderer>());
            }
            var group = root.AddComponent<LightGroup>();
            var so = SO(group);
            so.FindProperty("groupId").stringValue = "exit";
            SetObjectArray(so.FindProperty("lights"), lights.ToArray());
            SetObjectArray(so.FindProperty("lampRenderers"), lamps.ToArray());
            so.FindProperty("lampEmission").colorValue = Hex("22C55E") * 2.2f;
            so.FindProperty("startOn").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material SparkMaterial()
        {
            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialDir);
            string path = MaterialDir + "/GB_Spark.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                mat = new Material(sh) { name = "GB_Spark" };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.8f, 0.35f));
            mat.color = new Color(1f, 0.8f, 0.35f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ═════════════════════════ 머티리얼 ═════════════════════════
        private static Material GB(string name, Color color, bool emissive = false, bool transparent = false)
        {
            if (graybox.TryGetValue(name, out Material cached) && cached != null) return cached;

            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/GB_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                mat = new Material(sh) { name = "GB_" + name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", color * 2.2f);
            }
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            graybox[name] = mat;
            return mat;
        }

        private static Material Wall() => GB("Wall", Hex("D9D4CC"));
        private static Material Ceiling() => GB("Ceiling", Hex("EBE8E2"));
        private static Material Wood() => GB("FloorWood", Hex("94704D"));
        private static Material Tile() => GB("FloorTile", Hex("B8C2C7"));
        private static Material Vinyl() => GB("FloorVinyl", Hex("C9B89A"));
        private static Material Concrete() => GB("FloorCorridor", Hex("8C8C90"));
        private static Material DoorMat() => GB("Door", Hex("7F5C3D"));
        private static Material FireDoorMat() => GB("FireDoor", Hex("59666F"));
        private static Material Glass() => GB("Glass", new Color(0.65f, 0.82f, 1f, 0.22f), false, true);
        private static Material Metal() => GB("Metal", Hex("3B3D42"));
        private static Material Furn(string n, string hex) => GB("Furn_" + n, Hex(hex));
        private static Material Hot(string n, string hex) => GB("Hot_" + n, Hex(hex)); // 상호작용 대상 강조색

        // ═════════════════════════ 기본 도형 ═════════════════════════
        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = true, Vector3 euler = default)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Cyl(Transform parent, string name, Vector3 center, float radius, float height, Material mat, bool collider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f); // 실린더 기본 높이 2
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static SerializedObject SO(Object o) => new SerializedObject(o);

        // ═════════════════════════ 벽 + 개구부 ═════════════════════════
        private enum OpenKind { Gap, Door, Window, SlideDoor, Panel, FixedGlass, FlatDoor }

        private class Opening
        {
            public OpenKind kind;
            public float center, width, bottom, top;
            public string id = "", prompt = "", promptOff = "", message = "";
            public int swing = 1;        // 문 여는 방향: +1 → Y +90°, -1 → Y -90°
            public int inward = 1;       // 방 안쪽이 벽의 어느 쪽인지 (벽 수직축의 +1 / -1)
            public bool lockable;
            public bool startOpen;
            public Material mat;
        }

        private static Opening Op(OpenKind kind, float center, float width, float bottom, float top, string id = "", string prompt = "", string promptOff = "",
            int swing = 1, int inward = 1, bool lockable = false, string message = "", Material mat = null)
            => new Opening { kind = kind, center = center, width = width, bottom = bottom, top = top, id = id, prompt = prompt, promptOff = promptOff,
                swing = swing, inward = inward, lockable = lockable, message = message, mat = mat };

        /// <summary>
        /// 벽 1개 생성. alongX = true 면 x 방향으로 뻗는 벽(z = line), false 면 z 방향(x = line).
        /// 개구부(문/창문)는 벽 조각 사이를 비우고 그 자리에 문/창문 오브젝트를 만든다.
        /// </summary>
        private static void BuildWall(string name, bool alongX, float line, float a0, float a1, float t, params Opening[] openings)
        {
            Vector3 Pt(float along, float y, float across) => alongX ? new Vector3(along, y, across) : new Vector3(across, y, along);
            Vector3 Sz(float len, float h, float thick) => alongX ? new Vector3(len, h, thick) : new Vector3(thick, h, len);

            var parent = Group(name, lvStructure);
            var list = new List<Opening>(openings);
            list.Sort((a, b) => a.center.CompareTo(b.center));

            int n = 0;
            void Piece(float from, float to, float y0, float y1)
            {
                if (to - from < 0.001f || y1 - y0 < 0.001f) return;
                Box(parent, $"{name}_{n++}", Pt((from + to) * 0.5f, (y0 + y1) * 0.5f, line), Sz(to - from, y1 - y0, t), Wall());
            }

            float cursor = a0 - t * 0.5f;
            foreach (Opening op in list)
            {
                if (op.kind == OpenKind.FlatDoor) continue;
                float lo = op.center - op.width * 0.5f, hi = op.center + op.width * 0.5f;
                Piece(cursor, lo, 0f, WallH);
                Piece(lo, hi, 0f, op.bottom);
                Piece(lo, hi, op.top, WallH);
                cursor = hi;
            }
            Piece(cursor, a1 + t * 0.5f, 0f, WallH);

            foreach (Opening op in list) BuildOpening(parent, op, alongX, line, t, Pt, Sz);
        }

        private static void BuildOpening(Transform wall, Opening op, bool alongX, float line, float t,
            Func<float, float, float, Vector3> Pt, Func<float, float, float, Vector3> Sz)
        {
            float lo = op.center - op.width * 0.5f;
            float h = op.top - op.bottom;
            Vector3 alongDir = alongX ? Vector3.right : Vector3.forward;

            switch (op.kind)
            {
                case OpenKind.Gap:
                    break;

                case OpenKind.FixedGlass:
                    Box(wall, op.id.Length > 0 ? op.id : "FixedGlass", Pt(op.center, (op.bottom + op.top) * 0.5f, line), Sz(op.width, h, 0.04f), Glass());
                    break;

                case OpenKind.Door:
                {
                    // 루트 = 경첩 위치. Hinge(회전체) → Visual(문짝)
                    var root = new GameObject(op.id);
                    root.transform.SetParent(lvInteract, false);
                    root.transform.position = Pt(lo, 0f, line);
                    var hinge = new GameObject("Hinge").transform;
                    hinge.SetParent(root.transform, false);
                    Material m = op.mat != null ? op.mat : DoorMat();
                    Box(hinge, "Visual", Pt(op.width * 0.5f, (op.top - 0.02f) * 0.5f, 0f), Sz(op.width - 0.02f, op.top - 0.02f, 0.05f), m);
                    Box(hinge, "Handle", Pt(op.width - 0.12f, 1.0f, 0.045f * op.inward), Sz(0.1f, 0.04f, 0.05f), Metal(), false);

                    var tog = root.AddComponent<ToggleInteractable>();
                    ConfigureToggle(tog, op, hinge, Vector3.zero, Vector3.zero, Vector3.zero, new Vector3(0f, 90f * op.swing, 0f), 0.6f);
                    break;
                }

                case OpenKind.Window:
                case OpenKind.SlideDoor:
                {
                    // 루트 = 개구부 중앙. Pane(미닫이 유리창) 이 벽 안쪽으로 밀려 들어가며 열린다.
                    var root = new GameObject(op.id);
                    root.transform.SetParent(lvInteract, false);
                    root.transform.position = Pt(op.center, 0f, line);
                    Vector3 paneOff = Pt(0f, (op.bottom + op.top) * 0.5f, 0f);
                    GameObject pane = Box(root.transform, "Pane", paneOff, Sz(op.width - 0.02f, h - 0.02f, 0.04f), Glass());
                    // 창문: 한쪽(-방향)으로 절반만 열림 / 미닫이 문: 옆 벽 쪽(+방향)으로 끝까지 열림 (옆 창과 겹치지 않게)
                    bool isDoor = op.kind == OpenKind.SlideDoor;
                    Vector3 paneOn = paneOff + alongDir * ((isDoor ? 1f : -1f) * op.width * (isDoor ? 0.97f : 0.5f));

                    float handleY = isDoor ? 1.0f : op.bottom + 0.18f;
                    float handleX = isDoor ? -op.width * 0.5f + 0.1f : op.width * 0.5f - 0.1f;
                    Box(root.transform, "Handle", Pt(handleX, handleY, 0.04f * op.inward), Sz(0.06f, 0.18f, 0.05f), Metal(), true);

                    var tog = root.AddComponent<ToggleInteractable>();
                    ConfigureToggle(tog, op, pane.transform, paneOff, paneOn, Vector3.zero, Vector3.zero, 0.8f);

                    if (op.lockable)
                    {
                        var lockGo = new GameObject(op.id + "_lock");
                        lockGo.transform.SetParent(lvInteract, false);
                        lockGo.transform.position = Pt(op.center + op.width * 0.5f - 0.12f, op.bottom + h * 0.5f, line + op.inward * (t * 0.5f + 0.03f));
                        Box(lockGo.transform, "Visual", Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), Hot("Lock", "F59E0B"));
                        var lk = lockGo.AddComponent<LockInteractable>();
                        var lso = SO(lk);
                        lso.FindProperty("id").stringValue = op.id + "_lock";
                        lso.FindProperty("prompt").stringValue = "잠금장치";
                        lso.FindProperty("target").objectReferenceValue = tog;
                        lso.FindProperty("startLocked").boolValue = true;
                        lso.FindProperty("promptWhenLocked").stringValue = "창문 잠금 풀기";
                        lso.FindProperty("promptWhenUnlocked").stringValue = "창문 잠그기";
                        lso.FindProperty("holdSeconds").floatValue = WindowLockHoldSeconds;
                        lso.ApplyModifiedPropertiesWithoutUndo();

                        var tso = SO(tog);
                        tso.FindProperty("startLocked").boolValue = true;
                        tso.FindProperty("lockedMessage").stringValue = "창문이 잠겨 있다. 잠금장치를 먼저 풀어야 한다.";
                        tso.FindProperty("holdSeconds").floatValue = WindowHoldSeconds;
                        tso.ApplyModifiedPropertiesWithoutUndo();
                    }
                    break;
                }

                case OpenKind.Panel:
                {
                    var root = new GameObject(op.id);
                    root.transform.SetParent(lvInteract, false);
                    root.transform.position = Pt(op.center, h * 0.5f + op.bottom, line);
                    Box(root.transform, "Visual", Vector3.zero, Sz(op.width, h, t * 0.7f), op.mat != null ? op.mat : Furn("Partition", "E8D9B5"));
                    AddTrigger(root, op.id, op.prompt, op.message);
                    break;
                }

                case OpenKind.FlatDoor:
                {
                    // 개구부 없이 벽면에 붙은 장식 문 (옆집 현관, 엘리베이터)
                    var root = new GameObject(op.id);
                    root.transform.SetParent(lvInteract, false);
                    root.transform.position = Pt(op.center, 0f, line + op.inward * (t * 0.5f + 0.03f));
                    Box(root.transform, "Visual", Pt(0f, op.top * 0.5f, 0f), Sz(op.width, op.top, 0.06f), op.mat != null ? op.mat : FireDoorMat());
                    if (op.id == Ids.Elevator) Box(root.transform, "Seam", Pt(0f, op.top * 0.5f, 0.035f * op.inward), Sz(0.03f, op.top, 0.02f), Metal(), false);
                    if (op.id == Ids.Elevator)
                    {
                        // 막는 메시지 대신 항상 상호작용 가능: 호출 → (평상시 정상 / 화재 중 확률적 위험) — ElevatorHazardSystem
                        BuildElevatorParts(root, Pt(0f, op.top * 0.5f, 0.055f * op.inward), Sz(op.width - 0.12f, op.top - 0.06f, 0.04f));
                        break;
                    }
                    AddTrigger(root, op.id, op.prompt, op.message);
                    break;
                }
            }
        }

        private static void ConfigureToggle(ToggleInteractable tog, Opening op, Transform moving,
            Vector3 offPos, Vector3 onPos, Vector3 offEuler, Vector3 onEuler, float duration)
        {
            var so = SO(tog);
            so.FindProperty("id").stringValue = op.id;
            so.FindProperty("prompt").stringValue = op.prompt;
            so.FindProperty("promptWhenOn").stringValue = op.prompt;
            so.FindProperty("promptWhenOff").stringValue = op.promptOff;
            so.FindProperty("isOn").boolValue = op.startOpen;
            so.FindProperty("movingPart").objectReferenceValue = moving;
            so.FindProperty("offLocalPosition").vector3Value = offPos;
            so.FindProperty("onLocalPosition").vector3Value = onPos;
            so.FindProperty("offLocalEuler").vector3Value = offEuler;
            so.FindProperty("onLocalEuler").vector3Value = onEuler;
            so.FindProperty("moveDuration").floatValue = duration;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═════════════════════════ 상호작용 오브젝트 헬퍼 ═════════════════════════
        /// <summary>루트에 TriggerInteractable 을 붙이고 필드를 설정</summary>
        private static TriggerInteractable AddTrigger(GameObject root, string id, string prompt, string message = "")
        {
            var t = root.AddComponent<TriggerInteractable>();
            var so = SO(t);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("message").stringValue = message;
            so.ApplyModifiedPropertiesWithoutUndo();
            return t;
        }

        /// <summary>
        /// 엘리베이터 문(elevator_door): 닫힌 문짝(Visual)과 열렸을 때 나타나는 탑승 칸(elevator_cabin, 기본 비활성)을 구성한다.
        /// 탑승 칸의 콜라이더는 열린 문 위치를 막되 상호작용(탑승) 레이캐스트는 받는다.
        /// </summary>
        private static void BuildElevatorParts(GameObject root, Vector3 interiorCenter, Vector3 interiorSize)
        {
            Transform visual = root.transform.Find("Visual");
            Transform seam = root.transform.Find("Seam");
            if (visual != null && seam != null) seam.SetParent(visual, true);   // 문이 열리면 이음선도 함께 사라진다

            var cabin = new GameObject("elevator_cabin");
            cabin.transform.SetParent(root.transform, false);
            Box(cabin.transform, "Interior", interiorCenter, interiorSize, Furn("ElevatorInterior", "2B2A28"));
            var cabinItem = cabin.AddComponent<ElevatorInteractable>();
            var cso = SO(cabinItem);
            cso.FindProperty("id").stringValue = "elevator_cabin";
            cso.FindProperty("prompt").stringValue = "엘리베이터 탑승";
            cso.FindProperty("role").enumValueIndex = (int)ElevatorInteractable.PartRole.Cabin;
            cso.ApplyModifiedPropertiesWithoutUndo();
            cabin.SetActive(false);

            var door = root.AddComponent<ElevatorInteractable>();
            var dso = SO(door);
            dso.FindProperty("id").stringValue = Ids.Elevator;
            dso.FindProperty("prompt").stringValue = "엘리베이터 호출";
            dso.FindProperty("role").enumValueIndex = (int)ElevatorInteractable.PartRole.Door;
            dso.FindProperty("closedVisual").objectReferenceValue = visual != null ? visual.gameObject : null;
            dso.FindProperty("cabinObject").objectReferenceValue = cabin;
            dso.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>자리표시용 상호작용 오브젝트 (동작 없음): 루트 + Visual</summary>
        private static GameObject Trig(string id, string prompt, Vector3 center, Vector3 size, Material mat, string message = "", bool cylinder = false)
        {
            var root = new GameObject(id);
            root.transform.SetParent(lvInteract, false);
            root.transform.position = center;
            if (cylinder) Cyl(root.transform, "Visual", Vector3.zero, size.x * 0.5f, size.y, mat);
            else Box(root.transform, "Visual", Vector3.zero, size, mat);
            AddTrigger(root, id, prompt, message);
            return root;
        }

        private static ToggleInteractable AddToggleObject(string id, string promptOn, string promptOff, bool isOn, Vector3 center, Vector3 size, Material mat, params LightGroup[] linked)
        {
            var root = new GameObject(id);
            root.transform.SetParent(lvInteract, false);
            root.transform.position = center;
            Box(root.transform, "Visual", Vector3.zero, size, mat);
            var tog = root.AddComponent<ToggleInteractable>();
            var so = SO(tog);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("prompt").stringValue = promptOff;
            so.FindProperty("promptWhenOn").stringValue = promptOn;
            so.FindProperty("promptWhenOff").stringValue = promptOff;
            so.FindProperty("isOn").boolValue = isOn;
            if (linked != null && linked.Length > 0) SetObjectArray(so.FindProperty("linkedLights"), linked);
            so.ApplyModifiedPropertiesWithoutUndo();
            return tog;
        }

        private static ValveInteractable AddValve(string id, string promptWhenOpen, string promptWhenClosed, Vector3 center, Vector3 size, Material mat, Vector3 handleEulerClosed)
        {
            var root = new GameObject(id);
            root.transform.SetParent(lvInteract, false);
            root.transform.position = center;
            var handle = Box(root.transform, "Visual", Vector3.zero, size, mat);
            var valve = root.AddComponent<ValveInteractable>();
            var so = SO(valve);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("prompt").stringValue = promptWhenOpen;
            so.FindProperty("promptWhenOn").stringValue = promptWhenOpen;
            so.FindProperty("promptWhenOff").stringValue = promptWhenClosed;
            so.FindProperty("isOn").boolValue = true; // 평상시 = 열림
            so.FindProperty("movingPart").objectReferenceValue = handle.transform;
            so.FindProperty("offLocalEuler").vector3Value = handleEulerClosed;
            so.FindProperty("onLocalEuler").vector3Value = Vector3.zero;
            so.FindProperty("moveDuration").floatValue = 0.4f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return valve;
        }

        private static PickupInteractable AddPickup(string id, ItemData item, Vector3 center, Vector3 size, Material mat, bool cylinder = false)
        {
            var root = new GameObject(id);
            root.transform.SetParent(lvInteract, false);
            root.transform.position = center;
            if (cylinder) Cyl(root.transform, "Visual", Vector3.zero, size.x * 0.5f, size.y, mat);
            else Box(root.transform, "Visual", Vector3.zero, size, mat);
            var p = root.AddComponent<PickupInteractable>();
            var so = SO(p);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("item").objectReferenceValue = item;
            so.ApplyModifiedPropertiesWithoutUndo();
            return p;
        }

        // ═════════════════════════ 조명 / 구역 ═════════════════════════
        /// <summary>방 조명 묶음 생성: 포인트 라이트 + 천장 등 외형, LightGroup 으로 묶는다</summary>
        private static LightGroup AddLightGroup(string groupId, bool on, params Vector3[] positions)
        {
            var root = new GameObject("LG_" + groupId);
            root.transform.SetParent(lvLights, false);
            var lights = new List<Light>();
            var lamps = new List<Renderer>();
            int i = 0;
            foreach (Vector3 p in positions)
            {
                var lgo = new GameObject($"Light_{i}");
                lgo.transform.SetParent(root.transform, false);
                lgo.transform.position = p;
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 5.5f;
                l.intensity = 1.6f;
                l.color = new Color(1f, 0.95f, 0.85f);
                l.shadows = LightShadows.None;
                lights.Add(l);

                GameObject lamp = Box(root.transform, $"Lamp_{i}", p + Vector3.up * 0.12f, new Vector3(0.45f, 0.04f, 0.45f), GB("Lamp", Hex("FFF1D0"), true), false);
                lamps.Add(lamp.GetComponent<Renderer>());
                i++;
            }

            var group = root.AddComponent<LightGroup>();
            var so = SO(group);
            so.FindProperty("groupId").stringValue = groupId;
            SetObjectArray(so.FindProperty("lights"), lights.ToArray());
            SetObjectArray(so.FindProperty("lampRenderers"), lamps.ToArray());
            so.FindProperty("startOn").boolValue = on;
            so.ApplyModifiedPropertiesWithoutUndo();
            lightGroups[groupId] = group;
            return group;
        }

        private static void AddZone(string zoneId, Vector3 min, Vector3 max)
        {
            var go = new GameObject("Zone_" + zoneId);
            go.transform.SetParent(lvZones, false);
            go.transform.position = (min + max) * 0.5f;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = max - min;
            var z = go.AddComponent<HazardZone>();
            var so = SO(z);
            so.FindProperty("zoneId").stringValue = zoneId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>시각용 바닥 (콜라이더 없음)</summary>
        private static void FloorPatch(string name, float x0, float x1, float z0, float z1, Material mat)
            => Box(lvStructure, "Floor_" + name, new Vector3((x0 + x1) * 0.5f, 0.005f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.01f, z1 - z0), mat, false);

        // ═════════════════════════ 레벨 전체 ═════════════════════════
        private static Transform BuildApartmentLevel(ItemData towel, ItemData extinguisher, ItemData key, ItemData tape, ItemData water)
        {
            graybox.Clear();
            lightGroups.Clear();

            var level = new GameObject("Level").transform;
            lvStructure = Group("Structure", level);
            lvFurniture = Group("Furniture", level);
            lvInteract = Group("Interactables", level);
            lvLights = Group("Lights", level);
            lvZones = Group("HazardZones", level);

            // 바닥/천장 슬래브 (콜라이더 있음)
            const float x0 = -18f, x1 = 10f, z0 = -3.6f, z1 = 9.4f;
            Box(lvStructure, "FloorSlab", new Vector3((x0 + x1) * 0.5f, -0.15f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.3f, z1 - z0), Concrete());
            Box(lvStructure, "CeilingSlab", new Vector3((x0 + x1) * 0.5f, WallH + 0.15f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.3f, z1 - z0), Ceiling());

            // 바닥 마감 (시각용)
            FloorPatch("Bedroom", 0f, 2.6f, 2.4f, AptD, Wood());
            FloorPatch("Living", 2.6f, AptW, 2.4f, AptD, Wood());
            FloorPatch("Entry", 1.9f, 4.1f, 0f, 2.4f, Tile());
            FloorPatch("Bathroom", 0f, 1.9f, 0f, 2.4f, Tile());
            FloorPatch("Utility", 4.1f, AptW, 0f, 2.4f, Tile());
            FloorPatch("Kitchen", 3.9f, AptW, 2.4f, 4.6f, Vinyl());
            FloorPatch("Balcony", 0f, 5.4f, AptD, BalconyD, Tile());
            FloorPatch("Shelter", 5.4f, AptW, AptD, BalconyD, Tile());
            FloorPatch("Corridor", -14f, 9.5f, -2.2f, 0f, Concrete());

            BuildApartmentWalls();
            BuildCorridorAndStairs();

            // 조명 그룹
            LightGroup lgBed = AddLightGroup("bedroom", true, new Vector3(1.3f, 2.25f, 4.9f));
            LightGroup lgLiving = AddLightGroup("living", false, new Vector3(4.6f, 2.25f, 6.0f), new Vector3(3.2f, 2.25f, 3.5f));
            AddLightGroup("kitchen", false, new Vector3(5.2f, 2.25f, 3.5f));
            AddLightGroup("utility", false, new Vector3(5.35f, 2.25f, 1.2f));
            AddLightGroup("bathroom", false, new Vector3(0.95f, 2.25f, 1.2f));
            AddLightGroup("entry", false, new Vector3(3.0f, 2.25f, 1.2f));
            AddLightGroup("balcony", false, new Vector3(2.7f, 2.25f, 8.2f));
            AddLightGroup("shelter", false, new Vector3(6.0f, 2.25f, 8.2f));
            AddLightGroup("corridor", true, new Vector3(-9f, 2.25f, -1.1f), new Vector3(-1f, 2.25f, -1.1f), new Vector3(6f, 2.25f, -1.1f));
            AddLightGroup("stairwell", true, new Vector3(-15.7f, 2.25f, -1.6f));

            BuildBedroom(lgBed, key);
            BuildLiving(lgLiving, tape);
            BuildKitchen(water);
            BuildUtilityBathEntry(towel, extinguisher);
            BuildBalconyAndShelter();

            // 위험 구역 (방 단위 트리거)
            AddZone("zone_bedroom", new Vector3(0.1f, 0f, 2.5f), new Vector3(2.5f, 2.4f, 7.3f));
            AddZone("zone_living", new Vector3(2.7f, 0f, 2.5f), new Vector3(6.5f, 2.4f, 7.3f));
            AddZone("zone_kitchen", new Vector3(4.0f, 0f, 2.5f), new Vector3(6.5f, 2.4f, 4.5f));
            AddZone("zone_utility", new Vector3(4.2f, 0f, 0.1f), new Vector3(6.5f, 2.4f, 2.3f));
            AddZone("zone_bathroom", new Vector3(0.1f, 0f, 0.1f), new Vector3(1.8f, 2.4f, 2.3f));
            AddZone("zone_entry", new Vector3(2.0f, 0f, 0.1f), new Vector3(4.0f, 2.4f, 2.3f));
            AddZone("zone_balcony", new Vector3(0.1f, 0f, 7.5f), new Vector3(5.3f, 2.4f, 8.9f));
            AddZone("zone_shelter", new Vector3(5.5f, 0f, 7.5f), new Vector3(6.5f, 2.4f, 8.9f));
            AddZone("zone_corridor", new Vector3(-13.9f, 0f, -2.1f), new Vector3(9.4f, 2.4f, -0.1f));
            AddZone("zone_stairwell", new Vector3(-17.4f, 0f, -3.1f), new Vector3(-14.1f, 2.4f, -0.1f));
            BuildFireSources();
            BuildDoorHandleCheck();

            // 인테리어: 상호작용/불/조명 그룹이 모두 만들어진 뒤에 그 위치를 피해서 가구·재질·조명을 입힌다.
            // (단독 재생성은 Tools > GameUI > Build Apartment Interior)
            GameUI.EditorTools.Interior.ApartmentInteriorBuilder.Build(level);
            return level;
        }

        // ───────── 1004호 벽 ─────────
        private static void BuildApartmentWalls()
        {
            // 현관문이 있는 남쪽 벽(복도 쪽)은 복도 빌더에서 한 번에 만든다.

            // 서쪽 외벽 (옆집과 공유) + 베란다 경량칸막이
            BuildWall("Wall_West", false, 0f, 0f, BalconyD, WallTExt,
                Op(OpenKind.Panel, 8.2f, 0.9f, 0f, 2.0f, Ids.LightPartition, "경량칸막이 살펴보기", message: "얇은 칸막이 너머는 옆집 베란다다."));

            // 동쪽 외벽 + 주방 창문
            BuildWall("Wall_East", false, AptW, 0f, BalconyD, WallTExt,
                Op(OpenKind.Window, 3.5f, 1.4f, 1.0f, 2.1f, Ids.WindowKitchen, "창문 닫기", "창문 열기", inward: -1),
                // 대피공간 창문 (루트 C: 신선한 공기 / 고가사다리차 구조). 잠금 없이 바로 열린다
                Op(OpenKind.Window, 8.2f, 0.8f, 1.1f, 2.1f, "window_shelter", "창문 닫기", "창문 열기", inward: -1));

            // 북쪽 벽: 안방 창 / 거실 창 / 베란다 문
            BuildWall("Wall_North", true, AptD, 0f, AptW, WallTExt,
                Op(OpenKind.Window, 1.3f, 1.4f, 0.9f, 2.1f, Ids.WindowBedroom, "창문 닫기", "창문 열기", inward: -1),
                Op(OpenKind.Window, 3.2f, 1.0f, 0.9f, 2.1f, Ids.WindowLiving, "창문 닫기", "창문 열기", inward: -1),
                Op(OpenKind.SlideDoor, 4.5f, 1.3f, 0f, 2.1f, Ids.BalconyDoor, "베란다 문 닫기", "베란다 문 열기", inward: -1));

            // 베란다 북쪽 외벽 (고정 유리창)
            BuildWall("Wall_BalconyNorth", true, BalconyD, 0f, AptW, WallTExt,
                Op(OpenKind.FixedGlass, 2.7f, 3.6f, 0.9f, 2.1f, "balcony_glass"));

            // 대피공간 벽 (방화문)
            BuildWall("Wall_Shelter", false, 5.4f, AptD, BalconyD, WallT,
                Op(OpenKind.Door, 8.2f, 0.9f, 0f, DoorH, Ids.ShelterFireDoor, "방화문 닫기", "방화문 열기", swing: 1, inward: 1, mat: FireDoorMat()));

            // 침실 동쪽 벽 (침실 문)
            BuildWall("Wall_BedroomEast", false, 2.6f, 2.4f, AptD, WallT,
                Op(OpenKind.Door, 3.3f, 0.85f, 0f, DoorH, Ids.BedroomDoor, "문 닫기", "문 열기", swing: -1, inward: -1, mat: null));
            // 침실/화장실 경계 (남쪽)
            BuildWall("Wall_BedroomSouth", true, 2.4f, 0f, 2.6f, WallT);

            // 화장실 동쪽 벽 (화장실 문)
            BuildWall("Wall_BathEast", false, 1.9f, 0f, 2.4f, WallT,
                Op(OpenKind.Door, 1.2f, 0.8f, 0f, DoorH, Ids.BathroomDoor, "문 닫기", "문 열기", swing: -1, inward: -1));

            // 현관/다용도실 경계
            BuildWall("Wall_EntryEast", false, 4.1f, 0f, 2.4f, WallT);

            // 주방 남쪽 벽 (다용도실 문)
            BuildWall("Wall_KitchenSouth", true, 2.4f, 3.9f, AptW, WallT,
                Op(OpenKind.Door, 5.35f, 0.8f, 0f, DoorH, Ids.UtilityDoor, "문 닫기", "문 열기", swing: 1, inward: -1));

            // 주방 서쪽 벽 (거실 쪽으로 열린 통로)
            BuildWall("Wall_KitchenWest", false, 3.9f, 2.4f, 4.6f, WallT,
                Op(OpenKind.Gap, 3.5f, 1.2f, 0f, WallH));

            // 주방 북쪽 벽 (개방형 통로)
            BuildWall("Wall_KitchenNorth", true, 4.6f, 3.9f, AptW, WallT,
                Op(OpenKind.Gap, 5.25f, 1.8f, 0f, WallH));
        }

        // ───────── 복도 + 비상계단 ─────────
        private static void BuildCorridorAndStairs()
        {
            // 복도 북쪽 벽(= 세대 현관 벽): 1004호 현관문(작동) + 옆집 장식 문
            BuildWall("Wall_CorridorNorth", true, 0f, -17.5f, 9.5f, WallTExt,
                Op(OpenKind.Door, 3.0f, 0.95f, 0f, DoorH, Ids.FrontDoor, "현관문 닫기", "현관문 열기", swing: -1, inward: 1, mat: FireDoorMat()),
                Op(OpenKind.FlatDoor, -3.6f, 0.95f, 0f, DoorH, Ids.Door1003, "1003호 현관문", message: "옆집(1003호) 현관문이다. 잠겨 있다.", inward: -1, mat: FireDoorMat()),
                Op(OpenKind.FlatDoor, -10.2f, 0.95f, 0f, DoorH, Ids.Door1002, "1002호 현관문", message: "1002호 현관문이다. 잠겨 있다.", inward: -1, mat: FireDoorMat()));

            // 복도 남쪽 벽: 엘리베이터
            BuildWall("Wall_CorridorSouth", true, -2.2f, -14f, 9.5f, WallTExt,
                Op(OpenKind.FlatDoor, -1.5f, 1.8f, 0f, DoorH, Ids.Elevator, "엘리베이터", message: "엘리베이터는 화재 시 사용하면 안 된다.", inward: 1, mat: Furn("Elevator", "9AA3AD")));

            // 복도 동쪽 끝
            BuildWall("Wall_CorridorEast", false, 9.5f, -2.2f, 0f, WallTExt);

            // 비상계단실: 복도와 방화문으로 연결
            BuildWall("Wall_StairDoor", false, -14f, -3.2f, 0f, WallTExt,
                Op(OpenKind.Door, -1.1f, 1.0f, 0f, DoorH, Ids.StairFireDoor, "방화문 닫기", "비상계단 방화문 열기", swing: -1, inward: 1, mat: FireDoorMat()));
            BuildWall("Wall_StairWest", false, -17.5f, -3.2f, 0f, WallTExt);
            BuildWall("Wall_StairSouth", true, -3.2f, -17.5f, -14f, WallTExt);

            // 비상계단: 위층(옥상 방향)으로 오르는 계단 8단 (입구 블록아웃)
            var stairs = Group("EmergencyStairs", lvStructure);
            for (int i = 0; i < 8; i++)
            {
                float h = 0.17f * (i + 1);
                Box(stairs, $"Step_{i + 1}", new Vector3(-15.15f - 0.3f * i, h * 0.5f, -1.6f), new Vector3(0.3f, h, 2.4f), Furn("Stairs", "A0A5AB"));
            }
            Box(stairs, "StairRailing", new Vector3(-16.2f, 1.0f, -0.35f), new Vector3(2.6f, 0.05f, 0.05f), Metal(), false);

            // 비상구 유도등 (초록 발광)
            var exitSigns = new List<GameObject>();
            Material exit = GB("ExitSign", Hex("22C55E"), true);
            exitSigns.Add(Box(lvStructure, "ExitSign_StairDoor", new Vector3(-13.84f, 2.2f, -1.1f), new Vector3(0.06f, 0.22f, 0.7f), exit, false));
            exitSigns.Add(Box(lvStructure, "ExitSign_CorridorEnd", new Vector3(8.0f, 2.25f, -2.05f), new Vector3(0.7f, 0.22f, 0.06f), exit, false));
            exitSigns.Add(Box(lvStructure, "ExitSign_StairInside", new Vector3(-16.6f, 2.2f, -3.05f), new Vector3(0.7f, 0.22f, 0.06f), exit, false));

            BuildExitLightGroup(exitSigns);

            // 복도 소품: 호수 번호판, 소화기 함
            Box(lvFurniture, "Plate_1004", new Vector3(3.0f, 1.65f, -0.15f), new Vector3(0.3f, 0.14f, 0.03f), Metal(), false);
            Box(lvFurniture, "FireBox_Corridor", new Vector3(-7f, 1.0f, -0.17f), new Vector3(0.5f, 0.7f, 0.12f), Hot("FireBox", "B91C1C"), false);
        }

        // ───────── 안방 ─────────
        private static void BuildBedroom(LightGroup lg, ItemData key)
        {
            Box(lvFurniture, "Bed", new Vector3(0.85f, 0.3f, 5.6f), new Vector3(1.5f, 0.6f, 2.0f), Furn("Bed", "6B7A99"));
            Box(lvFurniture, "Bed_Head", new Vector3(0.85f, 0.65f, 6.6f), new Vector3(1.5f, 0.7f, 0.12f), Furn("BedHead", "4B3B2B"));
            Trig(Ids.Bed, "침대", new Vector3(0.85f, 0.65f, 5.0f), new Vector3(1.4f, 0.1f, 0.8f), Furn("Pillow", "E5E7EB"), "방금 일어난 침대다.");
            Trig(Ids.Closet, "옷장 열기", new Vector3(0.35f, 1.0f, 3.3f), new Vector3(0.6f, 2.0f, 1.3f), Furn("Closet", "5A4636"));
            Box(lvFurniture, "Desk", new Vector3(2.15f, 0.37f, 6.6f), new Vector3(0.7f, 0.74f, 0.55f), Furn("Desk", "6B5B4B"));
            AddPickup(Ids.PickupKey, key, new Vector3(2.15f, 0.8f, 6.6f), new Vector3(0.1f, 0.03f, 0.2f), Hot("Key", "FACC15"));
            Box(lvFurniture, "Nightstand", new Vector3(1.85f, 0.26f, 5.0f), new Vector3(0.4f, 0.52f, 0.4f), Furn("Nightstand", "5A4636"));
            GameObject alarm = Trig(Ids.AlarmClock, "알람 끄기", new Vector3(1.85f, 0.58f, 5.0f), new Vector3(0.16f, 0.1f, 0.1f), Hot("Alarm", "EF4444"), "알람을 껐다.");
            ConfigTrigger(alarm, 0f, true);
            lvAlarmAudio = AddAudio("AlarmAudio", new Vector3(1.85f, 0.6f, 5.0f), true, true);
            AddToggleObject(Ids.SwitchBedroom, "안방 불 끄기", "안방 불 켜기", true, new Vector3(2.5f, 1.2f, 3.95f), new Vector3(0.04f, 0.12f, 0.08f), Metal(), lg);
        }

        // ───────── 거실 ─────────
        private static void BuildLiving(LightGroup lg, ItemData tape)
        {
            Box(lvFurniture, "Curtain", new Vector3(3.95f, 1.15f, 7.3f), new Vector3(2.5f, 2.1f, 0.04f), Furn("Curtain", "B45F5F"), false);
            Box(lvFurniture, "TV_Cabinet", new Vector3(2.9f, 0.25f, 5.6f), new Vector3(0.4f, 0.5f, 1.6f), Furn("TVCabinet", "4B3B2B"));
            Trig(Ids.Tv, "TV", new Vector3(2.78f, 0.95f, 5.6f), new Vector3(0.08f, 0.6f, 1.05f), Furn("TV", "111827"));
            Box(lvFurniture, "Sofa", new Vector3(5.5f, 0.4f, 5.6f), new Vector3(0.9f, 0.8f, 2.0f), Furn("Sofa", "7C8B9E"));
            Box(lvFurniture, "Sofa_Back", new Vector3(5.92f, 0.75f, 5.6f), new Vector3(0.14f, 0.7f, 2.0f), Furn("SofaBack", "6A788A"));
            Box(lvFurniture, "CoffeeTable", new Vector3(4.2f, 0.2f, 5.6f), new Vector3(0.6f, 0.4f, 1.0f), Furn("Table", "8A6F55"));
            AddPickup(Ids.PickupTape, tape, new Vector3(4.2f, 0.45f, 5.3f), new Vector3(0.12f, 0.06f, 0.12f), Hot("Tape", "9CA3AF"), true);
            // 문어발 멀티탭 (과열): 본체 + 전원 스위치 + 플러그 + 주변 가연물
            Box(lvFurniture, "Multitap_Body", new Vector3(3.0f, 0.035f, 4.65f), new Vector3(0.35f, 0.05f, 0.1f), Hot("Overheat", "F97316"));
            Trig(Ids.MultitapSwitch, "멀티탭 전원 스위치", new Vector3(3.12f, 0.07f, 4.65f), new Vector3(0.06f, 0.03f, 0.06f), Hot("SwitchRed", "EF4444"));
            AddRemovable(Ids.MultitapPlug, "코드 본체 뽑기", "코드 본체를 조심스럽게 뽑았다.", 2.0f, new Vector3(2.8f, 0.04f, 4.65f), new Vector3(0.08f, 0.07f, 0.08f), Furn("Plug", "1F2937"));
            AddRemovable(Ids.Clutter1, "종이 치우기", "종이를 치웠다.", 0.8f, new Vector3(2.9f, 0.03f, 4.4f), new Vector3(0.25f, 0.02f, 0.3f), Furn("Paper", "F3F4F6"));
            AddRemovable(Ids.Clutter2, "옷가지 치우기", "옷가지를 치웠다.", 0.8f, new Vector3(3.15f, 0.05f, 4.42f), new Vector3(0.3f, 0.08f, 0.25f), Furn("Cloth", "A78BFA"));
            AddRemovable(Ids.Clutter3, "종이 치우기", "종이를 치웠다.", 0.8f, new Vector3(3.35f, 0.03f, 4.7f), new Vector3(0.25f, 0.02f, 0.3f), Furn("Paper", "F3F4F6"));

            var sparkGo = new GameObject("MultitapSparks");
            sparkGo.transform.SetParent(lvFurniture, false);
            sparkGo.transform.SetPositionAndRotation(new Vector3(3.0f, 0.1f, 4.65f), Quaternion.Euler(-90f, 0f, 0f));
            var ps = sparkGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = 0.35f; main.startSpeed = 1.8f; main.startSize = 0.025f; main.gravityModifier = 1f;
            main.startColor = new Color(1f, 0.8f, 0.35f);
            var emission = ps.emission; emission.rateOverTime = 18f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 35f; shape.radius = 0.02f;
            sparkGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = SparkMaterial();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            lvSparks = ps;
            lvSparkAudio = AddAudio("SparkAudio", new Vector3(3.0f, 0.2f, 4.65f), true, true);
            AddToggleObject(Ids.SwitchLiving, "거실 불 끄기", "거실 불 켜기", false, new Vector3(6.52f, 1.2f, 6.6f), new Vector3(0.04f, 0.12f, 0.08f), Metal(), lg);
        }

        // ───────── 주방 ─────────
        private static void BuildKitchen(ItemData water)
        {
            Box(lvFurniture, "Counter", new Vector3(6.3f, 0.45f, 3.5f), new Vector3(0.5f, 0.9f, 1.8f), Furn("Counter", "C7C2B8"));
            Trig(Ids.GasRange, "가스레인지", new Vector3(6.3f, 0.95f, 3.0f), new Vector3(0.48f, 0.1f, 0.55f), Furn("Range", "1F2937"));
            Trig(Ids.FryingPan, "프라이팬", new Vector3(6.3f, 1.03f, 3.0f), new Vector3(0.3f, 0.05f, 0.3f), Furn("Pan", "374151"), cylinder: true);
            AddToggleObject(Ids.RangeHood, "후드 끄기", "후드 켜기", false, new Vector3(6.35f, 1.85f, 3.0f), new Vector3(0.4f, 0.25f, 0.7f), Furn("Hood", "9CA3AF"));
            AddPickup(Ids.PickupWater, water, new Vector3(6.3f, 1.08f, 3.95f), new Vector3(0.08f, 0.22f, 0.08f), Hot("Water", "60A5FA"), true);
            AddPickup(Ids.PotWater, lvData.waterPot, new Vector3(4.7f, 0.1f, 3.9f), new Vector3(0.3f, 0.2f, 0.3f), Furn("Pot", "9CA3AF"), true);
            Trig(Ids.CoffeePot, "커피포트 켜기", new Vector3(6.3f, 1.07f, 4.25f), new Vector3(0.16f, 0.24f, 0.16f), Furn("Kettle", "DC2626"), "커피포트의 전원을 켰다.", true);
            Trig(Ids.Microwave, "전자레인지 사용", new Vector3(6.3f, 1.04f, 3.55f), new Vector3(0.4f, 0.28f, 0.4f), Furn("Microwave", "D1D5DB"), "전자레인지를 작동시켰다.");
            GameObject drink = Trig(Ids.DrinkWater, "물 마시기", new Vector3(4.15f, 1.1f, 4.4f), new Vector3(0.3f, 0.4f, 0.25f), Furn("Dispenser", "93C5FD"), "시원한 물을 마셨다.");
            ConfigTrigger(drink, 1.2f, true);
            AddValve(Ids.FuseCock, "퓨즈콕 잠그기", "퓨즈콕 열기", new Vector3(4.1f, 0.6f, 4.5f), new Vector3(0.2f, 0.06f, 0.06f), Hot("Valve", "EAB308"), new Vector3(0f, 0f, 90f));
            Box(lvFurniture, "GasPipe_Kitchen", new Vector3(4.1f, 0.3f, 4.52f), new Vector3(0.04f, 0.6f, 0.04f), Hot("Pipe", "EAB308"), false);
            Box(lvFurniture, "Fridge", new Vector3(4.35f, 0.9f, 2.75f), new Vector3(0.7f, 1.8f, 0.6f), Furn("Fridge", "D1D5DB"));
        }

        // ───────── 다용도실 / 화장실 / 현관 ─────────
        private static void BuildUtilityBathEntry(ItemData towel, ItemData extinguisher)
        {
            // 다용도실: 메인 가스 밸브
            Box(lvFurniture, "GasPipe_Main", new Vector3(6.5f, 1.2f, 1.2f), new Vector3(0.06f, 2.4f, 0.06f), Hot("Pipe", "EAB308"), false);
            AddValve(Ids.GasValveMain, "가스 밸브 잠그기", "가스 밸브 열기", new Vector3(6.46f, 1.2f, 1.2f), new Vector3(0.1f, 0.35f, 0.06f), Hot("MainValve", "DC2626"), new Vector3(90f, 0f, 0f));
            lvLeakAudio = AddAudio("LeakAudio", new Vector3(6.2f, 1.2f, 1.4f), true, true);
            Box(lvFurniture, "WashingMachine", new Vector3(4.6f, 0.45f, 0.5f), new Vector3(0.6f, 0.9f, 0.6f), Furn("Washer", "E5E7EB"));

            // 화장실
            Box(lvFurniture, "Toilet", new Vector3(0.35f, 0.2f, 0.45f), new Vector3(0.4f, 0.4f, 0.6f), Furn("Toilet", "F3F4F6"));
            Box(lvFurniture, "Sink", new Vector3(1.3f, 0.4f, 2.15f), new Vector3(0.6f, 0.8f, 0.4f), Furn("Sink", "F3F4F6"));
            GameObject faucet = Trig(Ids.WashFace, "세수하기", new Vector3(1.3f, 0.98f, 2.28f), new Vector3(0.1f, 0.2f, 0.1f), Furn("Faucet", "CBD5E1"), "상쾌하게 세수를 했다.");
            ConfigTrigger(faucet, 2.0f, true);
            // 수건걸이 (마른 수건 2장) + 세면대에서 수건 적시기
            Box(lvFurniture, "TowelRack", new Vector3(0.16f, 1.15f, 1.42f), new Vector3(0.05f, 0.05f, 0.7f), Metal(), false);
            AddPickup(Ids.PickupTowel, lvData.towelDry, new Vector3(0.2f, 0.98f, 1.25f), new Vector3(0.1f, 0.3f, 0.25f), Furn("Towel", "E5E7EB"));
            AddPickup(Ids.PickupTowel2, lvData.towelDry, new Vector3(0.2f, 0.98f, 1.6f), new Vector3(0.1f, 0.3f, 0.25f), Furn("Towel", "E5E7EB"));

            var sinkGo = new GameObject(Ids.SinkWet);
            sinkGo.transform.SetParent(lvInteract, false);
            sinkGo.transform.position = new Vector3(1.1f, 0.85f, 2.12f);
            Box(sinkGo.transform, "Visual", Vector3.zero, new Vector3(0.3f, 0.08f, 0.25f), Furn("Basin", "9CA3AF"));
            var conv = sinkGo.AddComponent<ConvertInteractable>();
            var cso = SO(conv);
            cso.FindProperty("id").stringValue = Ids.SinkWet;
            cso.FindProperty("prompt").stringValue = "수건 적시기";
            cso.FindProperty("holdSeconds").floatValue = 2.0f;
            cso.FindProperty("requiredItem").objectReferenceValue = lvData.towelDry;
            cso.FindProperty("resultItem").objectReferenceValue = towel;
            cso.ApplyModifiedPropertiesWithoutUndo();

            // 현관
            Box(lvFurniture, "ShoeCabinet_Body", new Vector3(3.88f, 0.55f, 0.9f), new Vector3(0.4f, 1.1f, 1.0f), Furn("Shoe", "6B5B4B"), false);
            Trig(Ids.ShoeCabinet, "신발장", new Vector3(3.88f, 0.55f, 0.9f), new Vector3(0.4f, 1.1f, 1.0f), Furn("Shoe", "6B5B4B"));
            AddPickup(Ids.PickupExtinguisher, extinguisher, new Vector3(3.92f, 0.32f, 1.9f), new Vector3(0.16f, 0.6f, 0.16f), Hot("Extinguisher", "DC2626"), true);
        }

        // ───────── 베란다 / 대피공간 ─────────
        private static void BuildBalconyAndShelter()
        {
            Trig(Ids.FlowerPot, "화분", new Vector3(0.45f, 0.2f, 8.6f), new Vector3(0.3f, 0.4f, 0.3f), Furn("Pot", "B45309"), cylinder: true);
            Box(lvFurniture, "FlowerPot_2", new Vector3(1.0f, 0.2f, 8.65f), new Vector3(0.3f, 0.4f, 0.3f), Furn("Pot", "B45309"));
            Box(lvFurniture, "Balcony_Storage", new Vector3(4.6f, 0.4f, 8.75f), new Vector3(0.8f, 0.8f, 0.4f), Furn("Storage", "6B7280"));
            Box(lvFurniture, "Shelter_Bench", new Vector3(6.35f, 0.25f, 8.4f), new Vector3(0.4f, 0.5f, 1.0f), Furn("Bench", "9CA3AF"));
            Box(lvFurniture, "Shelter_Sign", new Vector3(6.0f, 1.9f, 8.95f), new Vector3(0.6f, 0.2f, 0.04f), GB("ShelterSign", Hex("22C55E"), true), false);
        }
    }
}
