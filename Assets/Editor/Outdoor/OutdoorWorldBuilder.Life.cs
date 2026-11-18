using System.Collections.Generic;
using GameGameplay;
using UnityEditor;
using UnityEngine;
using Prim = GameUI.EditorTools.Interior.Prim;
using OM = GameUI.EditorTools.Outdoor.OutdoorMaterials;

namespace GameUI.EditorTools.Outdoor
{
    public static partial class OutdoorWorldBuilder
    {
        private static readonly List<VehicleController> movingCars = new List<VehicleController>();
        private static readonly List<FacadeResident> residentList = new List<FacadeResident>();

        // ═════════════════════════ 보행자 (최대 25명) ═════════════════════════
        private static Transform[] Route(Transform parent, string name, params Vector2[] pts)
        {
            var g = Group(name, parent);
            var result = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var p = new GameObject($"P{i}").transform;
                p.SetParent(g, false); p.position = new Vector3(pts[i].x, GroundY + 0.03f, pts[i].y);
                result[i] = p;
            }
            return result;
        }

        private static Material[] Palette(string prefix, params string[] hex)
        {
            var arr = new Material[hex.Length];
            for (int i = 0; i < hex.Length; i++) arr[i] = OM.Lit($"{prefix}{i + 1}", OM.Hex(hex[i]), 0.1f);
            return arr;
        }

        private static void BuildPedestrians()
        {
            var go = new GameObject("PedestrianSpawner");
            go.transform.SetParent(root, false);
            var spawner = go.AddComponent<PedestrianSpawner>();
            Transform routes = Group("Routes", go.transform);

            // (이름, 역할, 인원, 속도, 반복, 개 동행, 경로) — 모두 보도(S1 z≈14, S2 z≈26) 위. 합계 21명 (≤ 25)
            var defs = new List<(string, PedestrianNPC.Role, int, float, bool, bool, Transform[])>
            {
                ("Commute_Loop", PedestrianNPC.Role.Commuter, 4, 1.3f, true, false, Route(routes, "Commute_Loop", new Vector2(-60, 14), new Vector2(60, 14), new Vector2(60, 26), new Vector2(-60, 26))),
                ("Commute_Line", PedestrianNPC.Role.Commuter, 2, 1.35f, true, false, Route(routes, "Commute_Line", new Vector2(-110, 14), new Vector2(110, 14))),
                ("Students", PedestrianNPC.Role.Student, 4, 1.5f, true, false, Route(routes, "Students", new Vector2(-30, 14), new Vector2(-30, 27), new Vector2(30, 27), new Vector2(30, 14))),
                ("DogWalk", PedestrianNPC.Role.DogWalker, 2, 1.0f, true, true, Route(routes, "DogWalk", new Vector2(-45, 20), new Vector2(-10, 20), new Vector2(-10, 26), new Vector2(-45, 26))),
                ("Jog", PedestrianNPC.Role.Jogger, 2, 1.2f, true, false, Route(routes, "Jog", new Vector2(-120, 26), new Vector2(120, 26))),
                ("Stroller", PedestrianNPC.Role.Stroller, 2, 0.9f, true, false, Route(routes, "Stroller", new Vector2(-85, 41), new Vector2(-55, 41), new Vector2(-55, 43), new Vector2(-85, 43))),
                ("Guard", PedestrianNPC.Role.Guard, 1, 1f, false, false, Route(routes, "Guard", new Vector2(136, 27.5f))),
                ("Courier", PedestrianNPC.Role.Courier, 1, 1.6f, true, false, Route(routes, "Courier", new Vector2(120, 28), new Vector2(20, 14))),
                ("Sit_A", PedestrianNPC.Role.Sitter, 1, 1f, false, false, Route(routes, "Sit_A", new Vector2(-40, 20))),
                ("Sit_B", PedestrianNPC.Role.Sitter, 1, 1f, false, false, Route(routes, "Sit_B", new Vector2(0, 20))),
                ("Sit_C", PedestrianNPC.Role.Sitter, 1, 1f, false, false, Route(routes, "Sit_C", new Vector2(40, 20))),
            };

            var so = new SerializedObject(spawner);
            so.FindProperty("prefab").objectReferenceValue = prefabPed.GetComponent<PedestrianNPC>();
            so.FindProperty("maxCount").intValue = 25;
            Assign(so.FindProperty("shirtMaterials"), Palette("Shirt", "4A78B8", "C0504D", "6AA84F", "E5B94E", "8E6BB5", "D9D9D9", "37474F"));
            Assign(so.FindProperty("pantsMaterials"), Palette("Pants", "2E3440", "5B4A3A", "384B6E", "6B6B6B"));
            Assign(so.FindProperty("skinMaterials"), Palette("Skin", "E0B58F", "C99673", "F0CBA8"));
            Assign(so.FindProperty("hairMaterials"), Palette("Hair", "2B2118", "5A4632", "9A9A9A", "111111"));
            SerializedProperty arr = so.FindProperty("routes");
            arr.arraySize = defs.Count;
            for (int i = 0; i < defs.Count; i++)
            {
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = defs[i].Item1;
                e.FindPropertyRelative("role").enumValueIndex = (int)defs[i].Item2;
                e.FindPropertyRelative("count").intValue = defs[i].Item3;
                e.FindPropertyRelative("speed").floatValue = defs[i].Item4;
                e.FindPropertyRelative("loop").boolValue = defs[i].Item5;
                e.FindPropertyRelative("companionPrefab").objectReferenceValue = defs[i].Item6 ? prefabDog : null;
                SerializedProperty pts = e.FindPropertyRelative("points");
                pts.arraySize = defs[i].Item7.Length;
                for (int k = 0; k < pts.arraySize; k++) pts.GetArrayElementAtIndex(k).objectReferenceValue = defs[i].Item7[k];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            spawnerRef = spawner;
        }

        private static PedestrianSpawner spawnerRef;

        private static void Assign(SerializedProperty prop, Object[] items)
        {
            prop.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // ═════════════════════════ 자동차 ═════════════════════════
        private static void BuildVehicles()
        {
            Transform t = Group("Vehicles", root);
            movingCars.Clear();
            Material[] paints = new Material[OutdoorPrefabs.CarColors.Length];
            for (int i = 0; i < paints.Length; i++) paints[i] = OM.Lit("CarPaint" + i, OM.Hex(OutdoorPrefabs.CarColors[i]), 0.6f, 0.3f);

            // 주차 차량 26대: 주차장 3줄 x 8대 + 도로변 2대 (정적)
            int n = 0;
            foreach (float z in new[] { 46f, 53f, 60f })
                for (int i = 0; i < 8; i++)
                {
                    if (rng.NextDouble() < 0.08) continue;      // 빈자리 조금
                    PlaceCar(t, new Vector3(26f + i * 10.6f, GroundY + 0.04f, z), rng.NextDouble() < 0.5 ? 90f : 270f, paints, n++);
                }
            PlaceCar(t, new Vector3(-30f, GroundY + 0.04f, 31.2f), 180f, paints, n++);
            PlaceCar(t, new Vector3(-12f, GroundY + 0.04f, 31.2f), 180f, paints, n++);

            // 움직이는 차 3대: 단지 순환도로를 천천히 (차선 오프셋을 다르게)
            Vector3[] loopA = Loop(-135f, 122f, 32f, 239f), loopB = Loop(-135f, 122f, 36f, 241f);
            System.Array.Reverse(loopB);
            movingCars.Add(Mover(t, loopA, 5.5f, paints[2]));
            movingCars.Add(Mover(t, loopB, 4.5f, paints[4]));
            movingCars.Add(Mover(t, loopA, 6f, paints[0], 2));
        }

        private static Vector3[] Loop(float x0, float x1, float z0, float z1)
        {
            float y = GroundY + 0.04f;
            return new[] { new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1) };
        }

        private static void PlaceCar(Transform parent, Vector3 pos, float yaw, Material[] paints, int n)
        {
            GameObject car = Spawn(prefabCar, parent, pos, yaw);
            Transform body = car.transform.Find("Car_Body");
            if (body != null) body.GetComponent<Renderer>().sharedMaterial = paints[n % paints.Length];
        }

        private static VehicleController Mover(Transform parent, Vector3[] path, float speed, Material paint, int startOffset = 0)
        {
            GameObject car = Spawn(prefabCar, parent, path[0], 0f);
            car.name = "MovingCar";
            Transform body = car.transform.Find("Car_Body");
            if (body != null) body.GetComponent<Renderer>().sharedMaterial = paint;
            var vc = car.GetComponent<VehicleController>();
            vc.enabled = true;
            var so = new SerializedObject(vc);
            SerializedProperty p = so.FindProperty("path");
            p.arraySize = path.Length;
            for (int i = 0; i < path.Length; i++) p.GetArrayElementAtIndex(i).vector3Value = path[(i + startOffset) % path.Length];
            so.FindProperty("cruiseSpeed").floatValue = speed;
            so.ApplyModifiedPropertiesWithoutUndo();
            car.transform.position = path[startOffset % path.Length];
            return vc;
        }

        // ═════════════════════════ 새 ═════════════════════════
        private static void BuildBirds()
        {
            Transform t = Group("Birds", root);
            for (int i = 0; i < 6; i++)
            {
                GameObject b = Spawn(prefabBird, t, new Vector3(0, 10f, 40f), 0f);
                var fl = b.GetComponent<BirdFlyer>();
                fl.Configure(new Vector3(Rand(-40f, 40f), Rand(5f, 30f), Rand(40f, 120f)), Rand(25f, 80f), Rand(0.12f, 0.3f));
            }
        }

        // ═════════════════════════ 8층 폭발 불꽃/연기 ═════════════════════════
        private static Transform BuildFireEffect()
        {
            // 베란다 정면 바로 아래층(8층, 10층 바닥 y=0 에서 6m 아래)의 외벽 앞
            var rootGo = new GameObject("FireEffect_8F");
            rootGo.transform.SetParent(root, false);
            rootGo.transform.position = new Vector3(3f, -4.8f, 9.6f);
            Material puffAdd = OM.Particle("FirePuff", new Color(1f, 0.6f, 0.2f, 0.9f), SoftPuff(), true);
            Material puffSmoke = OM.Particle("SmokePuff", new Color(0.1f, 0.1f, 0.1f, 0.7f), SoftPuff(), false);

            ParticleSystem flame = MakePs(rootGo.transform, "Flame", Vector3.zero, puffAdd, 200);
            var fm = flame.main; fm.startLifetime = 0.9f; fm.startSpeed = 2.6f; fm.gravityModifier = -0.1f;
            fm.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.6f); fm.startColor = new Color(1f, 0.7f, 0.3f, 0.9f);
            var fs = flame.shape; fs.shapeType = ParticleSystemShapeType.Cone; fs.angle = 20f; fs.radius = 0.5f;
            var fc = flame.colorOverLifetime; fc.enabled = true;
            var fg = new Gradient();
            fg.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.08f), 0.6f), new GradientColorKey(new Color(0.3f, 0.05f, 0f), 1f) },
                       new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fc.color = fg;
            var fz = flame.sizeOverLifetime; fz.enabled = true; fz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));

            ParticleSystem smoke = MakePs(rootGo.transform, "Smoke", new Vector3(0f, 0.6f, 0.3f), puffSmoke, 800);
            var sm = smoke.main; sm.startLifetime = new ParticleSystem.MinMaxCurve(11f, 15f); sm.startSpeed = 3.4f; sm.gravityModifier = -0.04f;
            sm.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f); sm.startColor = new Color(0.1f, 0.1f, 0.1f, 0.75f);
            var ss = smoke.shape; ss.shapeType = ParticleSystemShapeType.Cone; ss.angle = 24f; ss.radius = 0.8f;
            var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 3.6f));
            var sc = smoke.colorOverLifetime; sc.enabled = true;
            var sg = new Gradient();
            sg.SetKeys(new[] { new GradientColorKey(new Color(0.08f, 0.08f, 0.08f), 0f), new GradientColorKey(new Color(0.25f, 0.25f, 0.26f), 1f) },
                       new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.55f, 0.6f), new GradientAlphaKey(0f, 1f) });
            sc.color = sg;
            var sv = smoke.velocityOverLifetime; sv.enabled = true; sv.space = ParticleSystemSimulationSpace.World;
            sv.x = new ParticleSystem.MinMaxCurve(0.2f, 1.2f); sv.y = 0f; sv.z = new ParticleSystem.MinMaxCurve(0f, 0f);   // 바람에 살짝 옆으로

            ParticleSystem sparks = MakePs(rootGo.transform, "Sparks", Vector3.zero, puffAdd, 120);
            var pm = sparks.main; pm.startLifetime = 0.7f; pm.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f); pm.gravityModifier = 0.6f;
            pm.startSize = 0.08f; pm.startColor = new Color(1f, 0.8f, 0.4f);
            var ps2 = sparks.shape; ps2.shapeType = ParticleSystemShapeType.Cone; ps2.angle = 45f; ps2.radius = 0.3f;

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(rootGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.4f, 1.8f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point; light.range = 24f; light.intensity = 0f; light.color = new Color(1f, 0.55f, 0.25f); light.shadows = LightShadows.None; light.enabled = false;

            var audio = AddAudio(rootGo.transform, "FireAudio", Vector3.zero, true);
            var fx = rootGo.AddComponent<OutdoorFireEffect>();
            var so = new SerializedObject(fx);
            so.FindProperty("flame").objectReferenceValue = flame;
            so.FindProperty("smoke").objectReferenceValue = smoke;
            so.FindProperty("sparks").objectReferenceValue = sparks;
            so.FindProperty("fireLight").objectReferenceValue = light;
            so.FindProperty("loopAudio").objectReferenceValue = audio;
            so.ApplyModifiedPropertiesWithoutUndo();
            return rootGo.transform;
        }

        private static ParticleSystem MakePs(Transform parent, string name, Vector3 localPos, Material mat, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);      // 위로 분출
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            return ps;
        }

        private static Texture2D SoftPuff()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            return OM.SaveTexture("soft_puff", tex, true, true, TextureWrapMode.Clamp);
        }

        private static AudioSource AddAudio(Transform parent, string name, Vector3 pos, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = parent.position + pos;
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false; a.loop = loop; a.spatialBlend = 1f; a.minDistance = 8f; a.maxDistance = 300f; a.rolloffMode = AudioRolloffMode.Linear;
            return a;
        }

        // ═════════════════════════ 컨트롤러 / 창문 구역 / 주민 ═════════════════════════
        private static void BuildController(Transform fireRoot)
        {
            var go = new GameObject("OutdoorWorldController");
            go.transform.SetParent(root, false);
            var ctrl = go.AddComponent<OutdoorWorldController>();

            // 창문 앞 풍경 구역: 창마다 바라보는 방향과 보이는 풍경 메모
            Transform zones = Group("WindowZones", go.transform);
            var zoneList = new List<WindowViewZone>();
            (string id, Vector3 pos, Vector3 dir, string note)[] zdefs =
            {
                ("door_balcony", new Vector3(2.7f, 1.5f, 8.8f), Vector3.forward, "베란다: 단지 정면 → 맞은편 동 → 하천/건너편 산 (남향)"),
                ("window_living", new Vector3(3.2f, 1.5f, 7.1f), Vector3.forward, "거실: 단지 정면"),
                ("window_bedroom", new Vector3(1.3f, 1.5f, 7.1f), Vector3.forward, "안방: 측면 동이 비스듬히 보임"),
                ("window_kitchen", new Vector3(6.3f, 1.5f, 3.5f), Vector3.right, "주방: 옆 단지(109·110동)와 동쪽 능선"),
            };
            foreach (var d in zdefs)
            {
                var z = new GameObject("Zone_" + d.id);
                z.transform.SetParent(zones, false); z.transform.position = d.pos;
                var comp = z.AddComponent<WindowViewZone>();
                var zso = new SerializedObject(comp);
                zso.FindProperty("windowId").stringValue = d.id;
                zso.FindProperty("outward").vector3Value = d.dir;
                zso.FindProperty("viewNote").stringValue = d.note;
                zso.ApplyModifiedPropertiesWithoutUndo();
                zoneList.Add(comp);
            }

            // 환경음 슬롯 (클립은 비워둠): 새소리 / 차 소리 / 멀리서 아이들 / 개 짖는 소리
            Transform audios = Group("AmbientAudio", go.transform);
            (string name, Vector3 pos, float vol)[] adefs =
            {
                ("Birds", new Vector3(2.7f, 3f, 14f), 0.8f), ("Traffic", new Vector3(0f, -26f, 60f), 0.7f),
                ("Kids", new Vector3(-70f, -25f, 55f), 0.6f), ("Dog", new Vector3(30f, -26f, 40f), 0.5f),
            };
            var slots = new List<AudioSource>();
            foreach (var a in adefs)
            {
                var src = AddAudio(audios, "Amb_" + a.name, a.pos, true);
                src.transform.position = a.pos; slots.Add(src);
            }
            var siren = AddAudio(go.transform, "Siren", Vector3.zero, true);
            siren.transform.position = new Vector3(0f, -20f, 260f);

            // 맞은편 정면 동(102동)의 주민 6명: 앞 3명은 평소에도 가끔 보이고, 폭발 후에는 6명 모두 베란다에 나와 손짓
            residentList.Clear();
            if (frontTower != null)
            {
                Transform rg = Group("Residents", frontTower);
                float[] xs = { -18f, -11f, -4f, 4f, 11f, 18f };
                int[] floors = { 10, 11, 9, 10, 11, 9 };
                for (int i = 0; i < xs.Length; i++)
                    residentList.Add(BuildResident(rg, xs[i], floors[i], i < 3));
            }

            var so = new SerializedObject(ctrl);
            Assign(so.FindProperty("windowZones"), zoneList.ToArray());
            so.FindProperty("spawner").objectReferenceValue = spawnerRef;
            Assign(so.FindProperty("residents"), residentList.ToArray());
            so.FindProperty("fire").objectReferenceValue = fireRoot.GetComponent<OutdoorFireEffect>();
            Assign(so.FindProperty("movingVehicles"), movingCars.ToArray());
            so.FindProperty("explosionPoint").objectReferenceValue = fireRoot;
            so.FindProperty("sirenSource").objectReferenceValue = siren;
            SerializedProperty sp = so.FindProperty("ambientSlots");
            sp.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++)
            {
                sp.GetArrayElementAtIndex(i).FindPropertyRelative("source").objectReferenceValue = slots[i];
                sp.GetArrayElementAtIndex(i).FindPropertyRelative("baseVolume").floatValue = adefs[i].vol;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static FacadeResident BuildResident(Transform parent, float localX, int floor, bool idle)
        {
            var go = new GameObject("Resident");
            go.transform.SetParent(parent, false);
            // 부모(동)가 GroundY 에 놓여 있으므로 로컬 y 는 층 높이만 사용한다
            go.transform.localPosition = new Vector3(localX, floor * FloorHeight + 0.35f, -frontTowerDepth * 0.5f - 0.35f);
            Material cloth = OM.Lit("ResidentCloth" + (floor % 3), OM.Hex(new[] { "4A4F66", "7A4B4B", "4B6B5A" }[floor % 3]), 0.1f);
            Material skin = OM.Lit("Skin1", OM.Hex("E0B58F"), 0.1f);
            var vis = new GameObject("Visual").transform;
            vis.SetParent(go.transform, false);
            Prim.Cyl(vis, "Body", new Vector3(0, 0.85f, 0), 0.22f, 1.1f, cloth);
            Prim.Sph(vis, "Head", new Vector3(0, 1.55f, 0), Vector3.one * 0.26f, skin);
            var arm = new GameObject("ArmR").transform;
            arm.SetParent(vis, false); arm.localPosition = new Vector3(0.26f, 1.3f, 0);
            Prim.Box(arm, "Arm", new Vector3(0, 0.22f, 0), new Vector3(0.1f, 0.44f, 0.1f), cloth);
            var res = go.AddComponent<FacadeResident>();
            var so = new SerializedObject(res);
            so.FindProperty("visual").objectReferenceValue = vis.gameObject;
            so.FindProperty("rightArm").objectReferenceValue = arm;
            so.FindProperty("idleAppearance").boolValue = idle;
            so.ApplyModifiedPropertiesWithoutUndo();
            return res;
        }

        /// <summary>창가에서 손을 흔들어 119 구조 신호를 보내는 상호작용 자리 (id: window_signal). 동작은 비워 두고 비활성으로 둔다</summary>
        private static void BuildWindowSignal()
        {
            GameObject lv = GameObject.Find("Gameplay/Level");
            if (lv == null) lv = GameObject.Find("Level");
            Transform interactables = lv != null ? lv.transform.Find("Interactables") : null;
            if (interactables == null) return;
            Transform old = interactables.Find("window_signal");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject("window_signal");
            go.transform.SetParent(interactables, false);
            go.transform.position = new Vector3(2.7f, 1.5f, 8.7f);
            go.AddComponent<BoxCollider>().size = new Vector3(0.6f, 0.6f, 0.2f);
            var trig = go.AddComponent<TriggerInteractable>();
            var so = new SerializedObject(trig);
            so.FindProperty("id").stringValue = "window_signal";
            so.FindProperty("prompt").stringValue = "창가에서 손을 흔들어 신호 보내기";
            so.FindProperty("interactable").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(false);   // 자리만 만들어 둠 (Act 4 루트 C 에서 활성화)
        }

        // ═════════════════════════ 성능: 그림자/정적 배칭 ═════════════════════════
        private static void ApplyShadowAndStatic()
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                bool nearOrLive = r.transform.IsChildOf(nearField) || r.GetComponentInParent<PedestrianNPC>() != null
                                  || r.GetComponentInParent<VehicleController>() != null;
                // 그림자는 근경(보도/나무/벤치/보행자/차량)만. 중경·원경은 끈다
                r.shadowCastingMode = nearOrLive ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = nearOrLive;

                bool moving = r.GetComponentInParent<PedestrianNPC>() != null || r.GetComponentInParent<VehicleController>() != null
                              || r.GetComponentInParent<BirdFlyer>() != null || r.GetComponentInParent<TreeSway>() != null
                              || r.GetComponentInParent<CloudDrift>() != null || r.GetComponentInParent<FlagWave>() != null
                              || r.GetComponentInParent<DogFollower>() != null || r.GetComponentInParent<FacadeResident>() != null;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, moving ? 0 : flags);
            }
        }
    }
}
