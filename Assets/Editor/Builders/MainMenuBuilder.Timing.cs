using System.IO;
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
        private const string TimingConfigPath = "Assets/Core/Data/GameTimingConfig.asset";

        private static GameTimingConfig EnsureTimingConfig()
        {
            EnsureFolder("Assets/Core/Data");
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

        private const string ElevatorConfigPath = "Assets/Scenarios/S03_ApartmentFire/Data/ElevatorHazardConfig.asset";
        private const string ElevatorBaseConfigPath = "Assets/House/Data/ElevatorConfig.asset";

        /// <summary>엘리베이터 연결: 집의 ElevatorController(정상 작동)는 Systems 에, 시나리오 3 위험 규칙(ElevatorHazardSystem)은 시나리오 시스템에 둔다. 에셋은 이미 있으면 사용자가 고친 값을 그대로 둔다</summary>
        private static void WireElevator(GameObject systemsRoot)
        {
            EnsureFolder("Assets/House/Data");
            EnsureFolder("Assets/Scenarios/S03_ApartmentFire/Data");
            var baseCfg = AssetDatabase.LoadAssetAtPath<GameGameplay.ElevatorConfig>(ElevatorBaseConfigPath);
            if (baseCfg == null)
            {
                if (File.Exists(ElevatorBaseConfigPath)) AssetDatabase.DeleteAsset(ElevatorBaseConfigPath);
                baseCfg = ScriptableObject.CreateInstance<GameGameplay.ElevatorConfig>();
                AssetDatabase.CreateAsset(baseCfg, ElevatorBaseConfigPath);
            }
            var hazardCfg = AssetDatabase.LoadAssetAtPath<GameGameplay.ElevatorHazardConfig>(ElevatorConfigPath);
            if (hazardCfg == null)
            {
                if (File.Exists(ElevatorConfigPath)) AssetDatabase.DeleteAsset(ElevatorConfigPath);
                hazardCfg = ScriptableObject.CreateInstance<GameGameplay.ElevatorHazardConfig>();
                AssetDatabase.CreateAsset(hazardCfg, ElevatorConfigPath);
            }
            AssetDatabase.SaveAssets();

            var ctl = systemsRoot.AddComponent<GameGameplay.ElevatorController>();
            var cso = SO(ctl);
            cso.FindProperty("config").objectReferenceValue = baseCfg;
            cso.FindProperty("cabin").objectReferenceValue = elev.cabin;
            cso.FindProperty("exteriorDoor").objectReferenceValue = Obj<GameGameplay.ElevatorInteractable>("elevator_door");
            cso.FindProperty("outsideStand").objectReferenceValue = elev.outsideStand;
            cso.FindProperty("dingAudio").objectReferenceValue = AddAudioUnder(systemsRoot.transform, "ElevatorDing");
            cso.FindProperty("motorAudio").objectReferenceValue = AddAudioUnder(systemsRoot.transform, "ElevatorMotor");
            cso.FindProperty("alarmAudio").objectReferenceValue = AddAudioUnder(systemsRoot.transform, "ElevatorAlarm");
            cso.FindProperty("buttonAudio").objectReferenceValue = AddAudioUnder(systemsRoot.transform, "ElevatorButton");
            cso.FindProperty("denyAudio").objectReferenceValue = AddAudioUnder(systemsRoot.transform, "ElevatorDeny");
            cso.ApplyModifiedPropertiesWithoutUndo();

            var hz = scenarioSystemsRoot.gameObject.AddComponent<GameGameplay.ElevatorHazardSystem>();
            var hso = SO(hz);
            hso.FindProperty("config").objectReferenceValue = hazardCfg;
            hso.FindProperty("fireball").objectReferenceValue = elev.fireball;
            hso.FindProperty("leakSmoke").objectReferenceValue = elev.leakSmoke;
            hso.FindProperty("blastFire").objectReferenceValue = elev.blastFire;
            hso.FindProperty("cabinZone").objectReferenceValue = elev.zone;
            hso.FindProperty("popAudio").objectReferenceValue = AddAudioUnder(scenarioSystemsRoot, "ElevatorPop");
            hso.FindProperty("smokeAudio").objectReferenceValue = AddAudioUnder(scenarioSystemsRoot, "ElevatorSmoke");
            hso.FindProperty("coughAudio").objectReferenceValue = AddAudioUnder(scenarioSystemsRoot, "ElevatorCough");
            hso.ApplyModifiedPropertiesWithoutUndo();
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
