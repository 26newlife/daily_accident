using System.Collections.Generic;
using QuakeSurvival.Fx;
using QuakeSurvival.Quake;
using QuakeSurvival.Safety;
using UnityEditor;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>연쇄 폭발 경로, 지진해일 컨트롤러, 비 효과 배치.</summary>
    internal static class DisasterBuilder
    {
        public static ChainDisaster BuildChain(GameObject host, FxLibrary fx, PlayerBuilder.Parts player, ZoneSensor zones)
        {
            var chain = host.AddComponent<ChainDisaster>();
            SetRef(chain, "fx", fx);
            SetRef(chain, "shake", player.Shake);
            SetRef(chain, "player", player.Controller.transform);
            SetRef(chain, "zones", zones);

            float y = TopFloorY + 1f;
            var so = new SerializedObject(chain);
            so.FindProperty("gasOrigin").vector3Value = new Vector3(ApartmentBuilder.GasValveLocal.x - 0.4f, TopFloorY + ApartmentBuilder.GasValveLocal.y, ApartmentBuilder.GasValveLocal.z);
            so.FindProperty("breakerOrigin").vector3Value = new Vector3(ApartmentBuilder.BreakerLocal.x + 0.3f, TopFloorY + ApartmentBuilder.BreakerLocal.y, ApartmentBuilder.BreakerLocal.z);
            so.FindProperty("apartmentDoor").vector3Value = new Vector3(-3.75f, y, -4f);
            so.FindProperty("hallway").vector3Value = new Vector3(-1f, y, -5.5f);
            so.FindProperty("buildingExit").vector3Value = new Vector3(0.6f, 1f, -8f);
            SetVectors(so.FindProperty("stairLandings"), StairPath());
            SetVectors(so.FindProperty("facadeFires"), new[]
            {
                new Vector3(-3.5f, y, -9.4f), new Vector3(-1f, y, -9.4f),
                new Vector3(-3f, y, 4.4f), new Vector3(2f, y, 4.4f),
                new Vector3(3f, y + 3f, -7.4f),
            });
            so.ApplyModifiedPropertiesWithoutUndo();
            return chain;
        }

        public static TsunamiController BuildTsunami(GameObject host, QuakeDirector director, PlayerBuilder.Parts player, Transform sea)
        {
            var tsunami = host.AddComponent<TsunamiController>();
            SetRef(tsunami, "director", director);
            SetRef(tsunami, "player", player.Controller.transform);
            SetRef(tsunami, "water", sea);
            // 해일은 낮은 땅(아파트, 학교 운동장)을 모두 덮고 남쪽 언덕 비탈을 높이 약 4m까지 타고 올라와 멈춘다
            var so = new SerializedObject(tsunami);
            so.FindProperty("peakFrontZ").floatValue = ExteriorBuilder.HillBaseZ - 10f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return tsunami;
        }

        public static void BuildRain(PlayerBuilder.Parts player, ZoneSensor zones)
        {
            var go = new GameObject("Rain");
            var rain = go.AddComponent<RainFx>();
            SetRef(rain, "player", player.Controller.transform);
            SetRef(rain, "zones", zones);
            SetRef(rain, "rainMaterial", FxBuilder.RainMaterial());
        }

        private static List<Vector3> StairPath()
        {
            var points = new List<Vector3>();
            for (int floor = PlayerFloor; floor >= 1; floor--)
            {
                float y0 = FloorY(floor);
                points.Add(new Vector3(0.6f, y0 + 1f, -5.5f));
                if (floor > 1) points.Add(new Vector3(4.7f, y0 - 0.5f, -5.5f));
            }
            return points;
        }

        private static void SetVectors(SerializedProperty prop, IReadOnlyList<Vector3> values)
        {
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                prop.GetArrayElementAtIndex(i).vector3Value = values[i];
            }
        }
    }
}
