using UnityEngine;

namespace GameData
{
    /// <summary>시간대</summary>
    public enum TimeOfDayKind { Morning = 0, Day = 1, Evening = 2, Night = 3 }

    /// <summary>창밖 풍경 모드 (확장 가능: 새 값을 추가하고 HouseController 가 처리)</summary>
    public enum WindowViewMode { Normal = 0, RainStorm = 1, DarkSmoky = 2 }

    /// <summary>
    /// 환경 프리셋 (ScriptableObject): 시간대, 하늘·태양 각도, 포그, 앰비언트 사운드, 창밖 풍경 모드, 보행자·차량 밀도.
    /// 각 항목은 apply* 체크가 켜져 있을 때만 적용된다 — "평상시 아침"은 모두 꺼 두어 기존 장면을 그대로 유지한다.
    /// 적용은 집 에셋(HouseController.ApplyEnvironmentPreset)이 담당한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Env_New", menuName = "GameData/Environment Preset", order = 3)]
    public class EnvironmentPreset : ScriptableObject
    {
        public string displayName = "평상시 아침";
        public TimeOfDayKind timeOfDay = TimeOfDayKind.Morning;

        [Header("조명 (체크한 항목만 적용)")]
        public bool applySun;
        public Vector3 sunEuler = new Vector3(50f, -30f, 0f);
        [Min(0f)] public float sunIntensity = 1f;
        public Color sunColor = new Color(1f, 0.96f, 0.88f);
        public bool applyAmbient;
        public Color ambientColor = new Color(0.16f, 0.16f, 0.18f);
        public bool applyFog;
        public Color fogColor = new Color(0.6f, 0.65f, 0.7f);
        [Min(0f)] public float fogDensity = 0.002f;

        [Header("창밖 풍경")]
        public WindowViewMode windowView = WindowViewMode.Normal;
        public bool applyTraffic;
        public bool pedestrians = true;
        public bool vehicles = true;
        [Range(0f, 1f)] public float trafficDensity = 1f;

        [Header("사운드")]
        public AudioClip ambientSound;
        [Range(0f, 1f)] public float ambientVolume = 0.4f;
    }
}
