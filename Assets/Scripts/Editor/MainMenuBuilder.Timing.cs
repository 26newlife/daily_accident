using GameCore;
using UnityEditor;
using UnityEngine;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 플레이 템포 설정(GameTimingConfig) 에셋 생성/연결. 이미 있으면 사용자가 고친 값을 그대로 둔다.
    /// 씬 빌더(BuildSystems)가 GameClock 에 이 에셋을 연결하고, 개발용 타이밍 오버레이(F9)를 붙인다.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string TimingConfigPath = "Assets/Data/GameTimingConfig.asset";

        private static GameTimingConfig EnsureTimingConfig()
        {
            EnsureFolder("Assets/Data");
            var cfg = AssetDatabase.LoadAssetAtPath<GameTimingConfig>(TimingConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<GameTimingConfig>();   // 필드 초기값 = 약 7분 템포 기본값
                AssetDatabase.CreateAsset(cfg, TimingConfigPath);
                AssetDatabase.SaveAssets();
            }
            return cfg;
        }

        /// <summary>GameClock 에 설정을 연결하고 F9 오버레이를 붙인다</summary>
        private static void WireTiming(GameObject systemsRoot, GameClock clock)
        {
            var so = SO(clock);
            so.FindProperty("timing").objectReferenceValue = EnsureTimingConfig();
            so.ApplyModifiedPropertiesWithoutUndo();
            if (systemsRoot.GetComponent<TimingOverlay>() == null) systemsRoot.AddComponent<TimingOverlay>();
        }

        private const string ElevatorConfigPath = "Assets/Data/ElevatorHazardConfig.asset";

        /// <summary>엘리베이터 위험 확률 설정(ElevatorHazardConfig) 에셋을 만들고 시스템에 연결한다. 이미 있으면 사용자가 고친 값을 그대로 둔다</summary>
        private static void WireElevator(GameObject systemsRoot)
        {
            EnsureFolder("Assets/Data");
            var cfg = AssetDatabase.LoadAssetAtPath<GameGameplay.ElevatorHazardConfig>(ElevatorConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<GameGameplay.ElevatorHazardConfig>();
                AssetDatabase.CreateAsset(cfg, ElevatorConfigPath);
                AssetDatabase.SaveAssets();
            }
            var sys = systemsRoot.GetComponent<GameGameplay.ElevatorHazardSystem>();
            if (sys == null) sys = systemsRoot.AddComponent<GameGameplay.ElevatorHazardSystem>();
            var so = SO(sys);
            so.FindProperty("config").objectReferenceValue = cfg;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 상태 머신 (ElevatorController) — 소리 슬롯은 비워 둔다
            var ctl = systemsRoot.GetComponent<GameGameplay.ElevatorController>();
            if (ctl == null) ctl = systemsRoot.AddComponent<GameGameplay.ElevatorController>();
            UnityEngine.AudioSource Slot(string n) => AddAudioUnder(systemsRoot.transform, n);
            var cso = SO(ctl);
            cso.FindProperty("config").objectReferenceValue = cfg;
            cso.FindProperty("cabin").objectReferenceValue = elev.cabin;
            cso.FindProperty("exteriorDoor").objectReferenceValue = Obj<GameGameplay.ElevatorInteractable>("elevator_door");
            cso.FindProperty("outsideStand").objectReferenceValue = elev.outsideStand;
            cso.FindProperty("fireball").objectReferenceValue = elev.fireball;
            cso.FindProperty("leakSmoke").objectReferenceValue = elev.leakSmoke;
            cso.FindProperty("blastFire").objectReferenceValue = elev.blastFire;
            cso.FindProperty("cabinZone").objectReferenceValue = elev.zone;
            cso.FindProperty("dingAudio").objectReferenceValue = Slot("ElevatorDing");
            cso.FindProperty("motorAudio").objectReferenceValue = Slot("ElevatorMotor");
            cso.FindProperty("popAudio").objectReferenceValue = Slot("ElevatorPop");
            cso.FindProperty("smokeAudio").objectReferenceValue = Slot("ElevatorSmoke");
            cso.FindProperty("alarmAudio").objectReferenceValue = Slot("ElevatorAlarm");
            cso.FindProperty("buttonAudio").objectReferenceValue = Slot("ElevatorButton");
            cso.FindProperty("denyAudio").objectReferenceValue = Slot("ElevatorDeny");
            cso.FindProperty("coughAudio").objectReferenceValue = Slot("ElevatorCough");
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/GameUI/Reset Game Timing Config (7-minute defaults)")]
        private static void ResetTimingConfig()
        {
            if (!EditorUtility.DisplayDialog("Game Timing Config", "Assets/Data/GameTimingConfig.asset 을 약 7분 템포 기본값으로 되돌립니다. 직접 고친 값은 사라집니다. 계속할까요?", "되돌리기", "취소")) return;
            AssetDatabase.DeleteAsset(TimingConfigPath);
            Selection.activeObject = EnsureTimingConfig();
        }
    }
}
