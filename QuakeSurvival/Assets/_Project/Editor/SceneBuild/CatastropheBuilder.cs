using System.Collections.Generic;
using System.Linq;
using QuakeSurvival.Art;
using QuakeSurvival.Fx;
using QuakeSurvival.Quake;
using QuakeSurvival.Safety;
using QuakeSurvival.UI;
using UnityEditor;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// 큰 재해 연출 배치: 집 안 구조물 붕괴, 본진 뒤 아파트 폭발, 밀려오는 해일 앞면, 해일에 무너지는 건물/차/나무/가로등.
    /// </summary>
    internal static class CatastropheBuilder
    {
        public sealed class Deps
        {
            public GameObject Host;
            public QuakeDirector Director;
            public ZoneSensor Zones;
            public CoverTracker Cover;
            public OutcomeEvaluator Evaluator;
            public TsunamiController Tsunami;
            public FxLibrary Fx;
            public MessageUI Messages;
            public PlayerBuilder.Parts Player;
            public ExteriorBuilder.Parts Exterior;
        }

        public static void Build(Deps d)
        {
            Material debris = Mat("Debris", Palette.Concrete, 0.2f);
            BuildIndoor(d, debris);
            BuildExplosion(d, debris);
            BuildWave(d);
            BuildCollapses(d);
            GroundRuptureBuilder.Build(d);
        }

        // ---------- 1. 집 안 구조물 ----------

        private static void BuildIndoor(Deps d, Material debris)
        {
            Transform apartment = GameObject.Find("Apartment_10F").transform;
            var fallers = new List<IndoorCollapse.Faller>();

            float delay = 1f;
            foreach (Transform books in Named(apartment, "Books"))
            {
                fallers.Add(Faller(MakeBody(books, 2f), QuakePhase.Foreshock, delay, new Vector3(0.6f, 0f, -0.4f)));
                delay += 1.2f;
            }
            foreach (Transform tv in Named(apartment, "Tv"))
            {
                fallers.Add(Faller(MakeBody(tv, 15f), QuakePhase.MainShock, 1.5f, new Vector3(0f, 0f, -12f)));
            }
            delay = 2f;
            foreach (Transform lamp in Named(apartment, "CeilingLight"))
            {
                fallers.Add(Faller(MakeBody(lamp, 3f), QuakePhase.MainShock, delay, Vector3.zero));
                delay += 2.5f;
            }
            foreach (Transform shelf in Named(apartment, "Bookshelf"))
            {
                fallers.Add(Faller(MakeBody(shelf, 40f), QuakePhase.MainShock, 4f, new Vector3(55f, 0f, 0f)));
            }
            foreach (Transform wardrobe in Named(apartment, "Wardrobe"))
            {
                fallers.Add(Faller(MakeBody(wardrobe, 60f), QuakePhase.MainShock, 7f, new Vector3(-75f, 0f, 0f)));
            }
            fallers.AddRange(CeilingPanels(apartment));

            var indoor = d.Host.AddComponent<IndoorCollapse>();
            SetRef(indoor, "director", d.Director);
            SetRef(indoor, "zones", d.Zones);
            SetRef(indoor, "cover", d.Cover);
            SetRef(indoor, "player", d.Player.Controller.transform);
            SetRef(indoor, "shake", d.Player.Shake);
            SetRef(indoor, "messages", d.Messages);
            SetRef(indoor, "evaluator", d.Evaluator);
            SetRef(indoor, "debrisMaterial", debris);
            var so = new SerializedObject(indoor);
            so.FindProperty("ceilingHeight").floatValue = ApartmentShellBuilder.Height;
            SerializedProperty list = so.FindProperty("fallers");
            list.arraySize = fallers.Count;
            for (int i = 0; i < fallers.Count; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("body").objectReferenceValue = fallers[i].body;
                e.FindPropertyRelative("phase").enumValueIndex = (int)fallers[i].phase;
                e.FindPropertyRelative("delay").floatValue = fallers[i].delay;
                e.FindPropertyRelative("push").vector3Value = fallers[i].push;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>본진 중 금이 가서 떨어지는 천장 마감판. 식탁 위에도 하나 떨어져 '탁자가 막아 준다'를 보여 준다.</summary>
        private static IEnumerable<IndoorCollapse.Faller> CeilingPanels(Transform apartment)
        {
            Material plaster = Mat("Ceiling", Palette.Trim);
            var spots = new[]
            {
                new Vector2(2.8f, -1.1f), new Vector2(-0.6f, -1.6f), new Vector2(-1.6f, -2.8f),
                new Vector2(3.6f, -2.6f), new Vector2(-3.4f, -2.4f), new Vector2(0.8f, -3f),
            };
            float y = ApartmentShellBuilder.Height - 0.05f;
            for (int i = 0; i < spots.Length; i++)
            {
                GameObject panel = Box("CeilingPanel", apartment, new Vector3(spots[i].x, y, spots[i].y), new Vector3(0.8f, 0.08f, 0.8f), plaster);
                yield return Faller(MakeBody(panel.transform, 6f), QuakePhase.MainShock, 3f + i * 3f, Vector3.zero);
            }
        }

        private static IEnumerable<Transform> Named(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
        }

        /// <summary>처음엔 고정(kinematic)된 Rigidbody를 붙인다. 충돌체가 없는 장식이면 상자 충돌체를 더한다.</summary>
        private static Rigidbody MakeBody(Transform t, float mass)
        {
            if (t.GetComponent<Collider>() == null) t.gameObject.AddComponent<BoxCollider>();
            var body = t.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            return body;
        }

        private static IndoorCollapse.Faller Faller(Rigidbody body, QuakePhase phase, float delay, Vector3 push)
        {
            return new IndoorCollapse.Faller { body = body, phase = phase, delay = delay, push = push };
        }

        // ---------- 4. 아파트 폭발 ----------

        private static void BuildExplosion(Deps d, Material debris)
        {
            var blast = d.Host.AddComponent<BuildingExplosion>();
            SetRef(blast, "director", d.Director);
            SetRef(blast, "fx", d.Fx);
            SetRef(blast, "shake", d.Player.Shake);
            SetRef(blast, "player", d.Player.Controller.transform);
            SetRef(blast, "zones", d.Zones);
            SetRef(blast, "evaluator", d.Evaluator);
            SetRef(blast, "messages", d.Messages);
            SetRef(blast, "debrisMaterial", debris);

            var so = new SerializedObject(blast);
            so.FindProperty("buildingCenter").vector3Value = new Vector3(0.2f, 0f, -2.5f);
            SetVectors(so.FindProperty("warningFires"), new[]
            {
                new Vector3(-2.5f, FloorY(3) + 1.5f, -9.4f),
                new Vector3(2.7f, FloorY(5) + 1.5f, -4.4f),
            });
            var blasts = new List<Vector3>();
            var faces = new[] { new Vector3(-2.5f, 0f, -9.5f), new Vector3(5.8f, 0f, 0f), new Vector3(0.2f, 0f, 4.5f), new Vector3(-5.4f, 0f, -2.5f) };
            int[] floors = { 2, 3, 5, 6, 8, 9, 11, 12 };
            for (int i = 0; i < floors.Length; i++)
            {
                Vector3 face = faces[i % faces.Length];
                blasts.Add(new Vector3(face.x, FloorY(floors[i]) + 1.5f, face.z));
            }
            SetVectors(so.FindProperty("blastPoints"), blasts);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- 3. 해일 앞면 + 무너짐 ----------

        private static void BuildWave(Deps d)
        {
            Transform root = new GameObject("TsunamiWave").transform;
            Transform body = Pivot(root, "WaveBody", new Vector3(25f, 0f, 25f));
            ApartmentShellBuilder.Decor("WaveFace", body, new Vector3(0f, 0.5f, 4f), new Vector3(400f, 1f, 8f), UnlitMat("WaveFace", Palette.Hex(0x2F5566)));
            Transform crest = Pivot(root, "WaveCrest", new Vector3(25f, 0f, 25f));
            GameObject foam = ApartmentShellBuilder.Decor("Foam", crest, Vector3.zero, new Vector3(400f, 1.6f, 3.5f), UnlitMat("WaveFoam", Palette.Hex(0xD5E3E8)));
            foam.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);

            var wave = root.gameObject.AddComponent<TsunamiWave>();
            SetRef(wave, "tsunami", d.Tsunami);
            SetRef(wave, "fx", d.Fx);
            SetRef(wave, "player", d.Player.Controller.transform);
            SetRef(wave, "body", body);
            SetRef(wave, "crest", crest);
        }

        private static void BuildCollapses(Deps d)
        {
            ExteriorBuilder.Parts ex = d.Exterior;
            var apartment = new[]
            {
                ex.ApartmentMass, GameObject.Find("Apartment_10F").transform,
                GameObject.Find("Hallway_10F").transform, GameObject.Find("Stairwell").transform,
            };
            Collapse(d, "Collapse_Apartment", apartment, new Vector3(0.2f, 0f, -2.5f), 4.5f,
                new Vector3(-14f, 0f, 4f), 48f, new Vector3(0f, 0f, -6f), 9f, 4f);

            Transform school = ex.SchoolBuilding;
            var schoolPivot = new Vector3((SchoolBuilder.BuildingMinX + SchoolBuilder.BuildingMaxX) / 2f, 0f, SchoolBuilder.FieldCenter.z);
            Collapse(d, "Collapse_School", new[] { school }, schoolPivot, SchoolBuilder.FieldMaxZ,
                new Vector3(-10f, 0f, -6f), 9f, new Vector3(0f, 0f, -3f), 6f, 2f);

            foreach (Transform car in ex.Cars)
            {
                Collapse(d, "Collapse_Car", new[] { car }, car.position, car.position.z + 1f,
                    new Vector3(0f, 40f, 75f), 0f, new Vector3(0f, 0.5f, -16f), 4f, 0.4f);
            }
            foreach (Transform tree in ex.Trees)
            {
                Collapse(d, "Collapse_Tree", new[] { tree }, tree.position, tree.position.z + 1f,
                    new Vector3(-82f, 0f, 0f), 0f, new Vector3(0f, 0f, -3f), 2.5f, 0.4f);
            }
            foreach (Transform post in ex.StreetLightPosts)
            {
                Collapse(d, "Collapse_StreetLight", new[] { post }, post.position, post.position.z + 0.5f,
                    new Vector3(-78f, 0f, 0f), 0f, new Vector3(0f, 0f, -2f), 2f, 0.3f);
            }
        }

        private static void Collapse(Deps d, string name, Transform[] targets, Vector3 pivot, float triggerZ,
            Vector3 tilt, float sink, Vector3 push, float duration, float dust)
        {
            var go = new GameObject(name);
            go.transform.SetParent(d.Host.transform, false);
            var c = go.AddComponent<WaveCollapse>();
            SetRef(c, "tsunami", d.Tsunami);
            SetRef(c, "fx", d.Fx);
            SetArray(c, "targets", targets);
            var so = new SerializedObject(c);
            so.FindProperty("pivot").vector3Value = pivot;
            so.FindProperty("triggerZ").floatValue = triggerZ;
            so.FindProperty("tiltEuler").vector3Value = tilt;
            so.FindProperty("sink").floatValue = sink;
            so.FindProperty("push").vector3Value = push;
            so.FindProperty("duration").floatValue = duration;
            so.FindProperty("dustScale").floatValue = dust;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVectors(SerializedProperty prop, IReadOnlyList<Vector3> values)
        {
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).vector3Value = values[i];
        }
    }
}
