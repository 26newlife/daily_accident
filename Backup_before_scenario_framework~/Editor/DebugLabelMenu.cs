using GameGameplay;
using UnityEditor;
using UnityEngine;

namespace GameUI.EditorTools
{
    /// <summary>Scene 뷰의 DebugLabel 표시를 켜고 끄는 메뉴 (플레이 카메라와 무관)</summary>
    public static class DebugLabelMenu
    {
        private const string MenuPath = "Tools/GameUI/Toggle Debug Labels";

        [MenuItem(MenuPath)]
        public static void Toggle()
        {
            DebugLabel.ShowInSceneView = !DebugLabel.ShowInSceneView;
            SceneView.RepaintAll();
            Debug.Log("[DebugLabel] Scene 뷰 라벨 " + (DebugLabel.ShowInSceneView ? "표시" : "숨김"));
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, DebugLabel.ShowInSceneView);
            return true;
        }
    }
}
