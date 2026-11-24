using QuakeSurvival.Safety;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>구역(AreaVolume), 몸 보호 구역(CoverZone), 위험 구역(HazardVolume) 생성.</summary>
    internal static class ZoneBuilder
    {
        public static void Area(string name, Transform parent, Vector3 localPos, Vector3 size, PlayerArea area, int priority = 0)
        {
            var go = Trigger(name, parent, localPos, size);
            var volume = go.AddComponent<AreaVolume>();
            SetEnum(volume, "area", (int)area);
            var so = new UnityEditor.SerializedObject(volume);
            so.FindProperty("priority").intValue = priority;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Cover(string name, Transform parent, Vector3 localPos, Vector3 size, bool requireCrouch)
        {
            var go = Trigger(name, parent, localPos, size);
            var zone = go.AddComponent<CoverZone>();
            SetBool(zone, "requireCrouch", requireCrouch);
        }

        public static GameObject Hazard(string name, Transform parent, Vector3 localPos, Vector3 size, DeathCause cause)
        {
            var go = Trigger(name, parent, localPos, size);
            var hazard = go.AddComponent<HazardVolume>();
            SetEnum(hazard, "cause", (int)cause);
            return go;
        }
    }
}
