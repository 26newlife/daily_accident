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

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            SessionState.SetBool("ScenarioSmokeTest.Run", true);
            Debug.Log("[Smoke] entering play mode...");
            EditorApplication.EnterPlaymode();
        }
    }
}
