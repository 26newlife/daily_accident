using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 런타임 스모크 테스트 실행기. 메뉴: Tools > GameUI > Debug > Run Runtime Smoke Test
    /// GameScene 을 열고 플레이 모드로 들어가면 ScenarioSmokeTest 가 자동으로 시작해 재시작 5회 / Act 3~4 / 루트 A~D / 엔딩 / 타임아웃을 점검한다.
    /// 배치 모드: -executeMethod GameUI.EditorTools.ScenarioSmokeMenu.RunBatch (끝나면 PASS 면 종료코드 0, FAIL 이면 3 으로 에디터 종료)
    /// </summary>
    public static class ScenarioSmokeMenu
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";

        [MenuItem("Tools/GameUI/Debug/Run Runtime Smoke Test")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RunBatch();
        }

        [MenuItem("Tools/GameUI/Debug/Run S01 Smoke Test")]
        public static void RunS01()
        {
            if (!EditorApplication.isPlaying && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RunS01Batch();
        }

        public static void RunS01Batch()
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            SessionState.SetBool("S01SmokeTest.Run", true);
            Debug.Log("[S01Smoke] entering play mode...");
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/GameUI/Debug/Run S02 Smoke Test")]
        public static void RunS02()
        {
            if (!EditorApplication.isPlaying && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RunS02Batch();
        }

        /// <summary>배치: -executeMethod GameUI.EditorTools.ScenarioSmokeMenu.RunS02Batch (비상계단 1층·옥상 걷기 포함)</summary>
        public static void RunS02Batch()
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            SessionState.SetBool("S02SmokeTest.Run", true);
            Debug.Log("[S02Smoke] entering play mode...");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>배치: S01 로 비상계단·바깥 동선 걷기 검사 (시작 위치 포함). 결과 smoke_results_stairs_s01.txt</summary>
        public static void RunStairsS01Batch() => RunStairsBatch("Assets/Core/Data/Scenarios/Scenario_01.asset");

        /// <summary>배치: S03 로 비상계단·바깥 동선 걷기 검사. 결과 smoke_results_stairs_s03.txt</summary>
        public static void RunStairsS03Batch() => RunStairsBatch("Assets/Core/Data/Scenarios/Scenario_03.asset");

        [MenuItem("Tools/GameUI/Debug/Run Stairwell Walk Test (S01)")]
        public static void RunStairsS01() { if (!EditorApplication.isPlaying && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return; RunStairsS01Batch(); }

        [MenuItem("Tools/GameUI/Debug/Run Stairwell Walk Test (S03)")]
        public static void RunStairsS03() { if (!EditorApplication.isPlaying && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return; RunStairsS03Batch(); }

        private static void RunStairsBatch(string scenarioPath)
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            SessionState.SetString("S02SmokeTest.Scenario", scenarioPath);
            SessionState.SetBool("S02SmokeTest.Run", true);
            Debug.Log("[S02Smoke] stairwell walk test: " + scenarioPath);
            EditorApplication.EnterPlaymode();
        }

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            SessionState.SetBool("ScenarioSmokeTest.Run", true);
            Debug.Log("[Smoke] entering play mode...");
            EditorApplication.EnterPlaymode();
        }
    }
}
