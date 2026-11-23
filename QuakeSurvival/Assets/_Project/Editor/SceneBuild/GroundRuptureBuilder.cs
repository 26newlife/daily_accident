using System.Collections.Generic;
using QuakeSurvival.Art;
using QuakeSurvival.Quake;
using QuakeSurvival.Safety;
using UnityEditor;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// 땅 균열과 싱크홀 배치(GroundRupture).
    /// - 본진: 도로를 가로지르는 긴 균열들이 차례로 벌어진다. 다 벌어진 균열을 밟으면 사망(점프로 넘을 수 있음).
    /// - 싱크홀 4곳: 아파트 앞 도로(본진), 학교 가는 길(본진 직후), 언덕 가는 길(여진), 학교 서쪽 길(해일 경보 중).
    ///   싱크홀 둘레에는 구멍이 열리기 직전에 사방으로 금이 간다.
    /// 대피지역(학교 운동장 x75~96, z-50.5~-29.5 / 언덕 위)과 차, 나무 자리는 피했다.
    /// </summary>
    internal static class GroundRuptureBuilder
    {
        private const float GroundY = 0.03f;

        private readonly struct HoleSpec
        {
            public readonly Vector2 Center;
            public readonly float Radius;
            public readonly QuakePhase Phase;
            public readonly float Delay;

            public HoleSpec(Vector2 center, float radius, QuakePhase phase, float delay)
            {
                Center = center;
                Radius = radius;
                Phase = phase;
                Delay = delay;
            }
        }

        private static readonly HoleSpec[] Holes =
        {
            new HoleSpec(new Vector2(14f, -23f), 3.5f, QuakePhase.MainShock, 18f),
            new HoleSpec(new Vector2(45f, -25f), 3f, QuakePhase.Aftermath, 20f),
            new HoleSpec(new Vector2(-4f, -60f), 3.5f, QuakePhase.Aftershock, 5f),
            new HoleSpec(new Vector2(30f, -48f), 3f, QuakePhase.Tsunami, 12f),
        };

        public static void Build(CatastropheBuilder.Deps d)
        {
            Transform root = Root("GroundRupture", Vector3.zero);
            Material crackMat = UnlitMat("GroundCrack", Palette.Hex(0x0A0A0B));
            Material rubble = Mat("SinkholeRubble", Palette.Hex(0x4B4640), 0.15f);
            var random = new System.Random(20261118);

            var cracks = new List<GroundRupture.Crack>();
            BuildRoadCracks(root, crackMat, random, cracks);
            var holes = new List<(Transform pit, GameObject hazard, HoleSpec spec)>();
            foreach (HoleSpec spec in Holes)
            {
                holes.Add(BuildHole(root, spec, crackMat, rubble, random, cracks));
            }

            var rupture = d.Host.AddComponent<GroundRupture>();
            SetRef(rupture, "director", d.Director);
            SetRef(rupture, "fx", d.Fx);
            SetRef(rupture, "shake", d.Player.Shake);
            SetRef(rupture, "player", d.Player.Controller.transform);
            SetRef(rupture, "messages", d.Messages);
            WriteEntries(rupture, cracks, holes);
        }

        // ---------- 균열 ----------

        /// <summary>본진 동안 도로와 인도를 가로지르는 긴 균열(왼쪽에서 오른쪽으로 번진다).</summary>
        private static void BuildRoadCracks(Transform root, Material mat, System.Random random, List<GroundRupture.Crack> cracks)
        {
            var starts = new[]
            {
                (new Vector2(-40f, -17f), 0f, 6f), (new Vector2(-8f, -28f), 20f, 10f),
                (new Vector2(24f, -30f), -10f, 13f), (new Vector2(52f, -20f), 5f, 16f),
                (new Vector2(-14f, -50f), -80f, 21f),
            };
            foreach (var (start, heading, delay) in starts)
            {
                Zigzag(root, mat, random, start, heading, 7, 3.5f, 0.35f, QuakePhase.MainShock, delay, cracks);
            }
        }

        /// <summary>
        /// 꺾인 균열 한 줄. 조각마다 이전 조각의 끝에서 시작하고, 조금씩 늦게 벌어져 번지는 것처럼 보인다.
        /// heading은 +x 기준 시계 반대 방향 각도(도).
        /// </summary>
        private static void Zigzag(Transform root, Material mat, System.Random random, Vector2 start, float heading,
            int segments, float segmentLength, float width, QuakePhase phase, float delay, List<GroundRupture.Crack> cracks)
        {
            Vector2 point = start;
            for (int i = 0; i < segments; i++)
            {
                float angle = heading + Range(random, -35f, 35f);
                float length = segmentLength * Range(random, 0.7f, 1.2f);
                float w = width * Range(random, 0.6f, 1.2f) * (1f - 0.5f * i / segments);
                Transform pivot = Pivot(root, "Crack", new Vector3(point.x, GroundY, point.y));
                pivot.localRotation = Quaternion.Euler(0f, -angle, 0f);
                ApartmentShellBuilder.Decor("Crack_Gap", pivot, new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.01f, w), mat);
                // 밟으면 사망하는 낮은 위험 구역(높이 0.3m라서 점프하면 닿지 않는다). 균열과 같이 x로 늘어난다
                GameObject hazard = ZoneBuilder.Hazard("Hazard_Crack", pivot, new Vector3(0.5f, 0.1f, 0f),
                    new Vector3(1f, 0.3f, w), DeathCause.GroundCrack);
                hazard.SetActive(false);
                pivot.localScale = new Vector3(length, 1f, 1f);
                cracks.Add(new GroundRupture.Crack { pivot = pivot, hazard = hazard, phase = phase, delay = delay + i * 0.35f, openSeconds = 0.5f });

                float rad = angle * Mathf.Deg2Rad;
                point += new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * length;
            }
        }

        // ---------- 싱크홀 ----------

        private static (Transform, GameObject, HoleSpec) BuildHole(Transform root, HoleSpec spec, Material crackMat,
            Material rubble, System.Random random, List<GroundRupture.Crack> cracks)
        {
            var center = new Vector3(spec.Center.x, GroundY, spec.Center.y);
            Transform pit = Pivot(root, "Sinkhole", center);
            Disc("Sinkhole_Dark", pit, spec.Radius, 0f, crackMat);
            Disc("Sinkhole_Rim", pit, spec.Radius + 0.35f, -0.005f, Mat("SinkholeRim", Palette.Hex(0x2B2723), 0.1f));
            for (int i = 0; i < 10; i++)
            {
                float a = (i + Range(random, 0f, 0.5f)) / 10f * Mathf.PI * 2f;
                float r = spec.Radius + Range(random, 0.1f, 0.6f);
                float s = Range(random, 0.3f, 0.8f);
                GameObject chunk = ApartmentShellBuilder.Decor("Sinkhole_Rubble", pit,
                    new Vector3(Mathf.Cos(a) * r, s * 0.25f, Mathf.Sin(a) * r), new Vector3(s, s * 0.5f, s * 0.8f), rubble);
                chunk.transform.localRotation = Quaternion.Euler(Range(random, -25f, 25f), Range(random, 0f, 360f), Range(random, -25f, 25f));
            }

            // 구멍이 열리기 직전(경고 시간 동안) 둘레로 금이 간다
            float crackDelay = Mathf.Max(0f, spec.Delay - 3f);
            for (int i = 0; i < 6; i++)
            {
                float heading = i * 60f + Range(random, -15f, 15f);
                float rad = heading * Mathf.Deg2Rad;
                Vector2 start = spec.Center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (spec.Radius * 0.6f);
                Zigzag(root, crackMat, random, start, heading, 3, 2.2f, 0.25f, spec.Phase, crackDelay, cracks);
            }

            float hazardSize = spec.Radius * 1.6f;
            GameObject hazard = ZoneBuilder.Hazard("Hazard_Sinkhole", root, new Vector3(spec.Center.x, 1f, spec.Center.y),
                new Vector3(hazardSize, 3f, hazardSize), DeathCause.Sinkhole);
            hazard.SetActive(false);
            return (pit, hazard, spec);
        }

        /// <summary>충돌체 없는 얇은 원판(원기둥 기본 도형). 원기둥은 높이 2라서 y 크기를 반으로 준다.</summary>
        private static void Disc(string name, Transform parent, float radius, float y, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = new Vector3(radius * 2f, 0.005f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        private static void WriteEntries(GroundRupture rupture, List<GroundRupture.Crack> cracks,
            List<(Transform pit, GameObject hazard, HoleSpec spec)> holes)
        {
            var so = new SerializedObject(rupture);
            SerializedProperty crackList = so.FindProperty("cracks");
            crackList.arraySize = cracks.Count;
            for (int i = 0; i < cracks.Count; i++)
            {
                SerializedProperty e = crackList.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("pivot").objectReferenceValue = cracks[i].pivot;
                e.FindPropertyRelative("hazard").objectReferenceValue = cracks[i].hazard;
                e.FindPropertyRelative("phase").enumValueIndex = (int)cracks[i].phase;
                e.FindPropertyRelative("delay").floatValue = cracks[i].delay;
                e.FindPropertyRelative("openSeconds").floatValue = cracks[i].openSeconds;
            }
            SerializedProperty holeList = so.FindProperty("sinkholes");
            holeList.arraySize = holes.Count;
            for (int i = 0; i < holes.Count; i++)
            {
                SerializedProperty e = holeList.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("pit").objectReferenceValue = holes[i].pit;
                e.FindPropertyRelative("hazard").objectReferenceValue = holes[i].hazard;
                e.FindPropertyRelative("phase").enumValueIndex = (int)holes[i].spec.Phase;
                e.FindPropertyRelative("delay").floatValue = holes[i].spec.Delay;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
