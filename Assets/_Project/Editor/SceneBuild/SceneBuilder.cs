using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// 테스트 씬 전체를 새로 만든다. 메뉴: QuakeSurvival > Rebuild Test Scene.
    /// 주의: 기존 TestRoom 씬을 덮어쓴다(직접 수정한 내용은 사라짐).
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/TestRoom.unity";

        [MenuItem("QuakeSurvival/Rebuild Test Scene")]
        private static void RebuildFromMenu()
        {
            bool ok = EditorUtility.DisplayDialog("테스트 씬 다시 만들기",
                "TestRoom 씬을 처음부터 다시 만듭니다.\n씬에서 직접 고친 내용은 사라집니다. 계속할까요?", "다시 만들기", "취소");
            if (ok) Debug.Log(BuildAll());
        }

        public static string BuildAll()
        {
            EnsureFolder("Assets/_Project", "Scenes");
            EnsureFolder("Assets/_Project", "Materials");
            CurrentFonts = FontSetup.Ensure();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var world = new SystemsBuilder.Input
            {
                Exterior = ExteriorBuilder.Build(),
                Apartment = ApartmentBuilder.Build(),
                Hallway = HallwayBuilder.Build(),
            };
            world.UpperCollapseHazard = StairwellBuilder.Build();
            world.Player = PlayerBuilder.Build(new Vector3(0f, TopFloorY, 0f) + ApartmentBuilder.SpawnLocal);
            world.Sun = LightingBuilder.Build(world.Player.Camera);
            SystemsBuilder.Build(world);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            return "built " + ScenePath;
        }
    }
}
