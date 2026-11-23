using System;
using System.Reflection;
using GameUI.EditorTools.Interior;
using UnityEditor;
using UnityEngine;

namespace GameUI.EditorTools.Art
{
    /// <summary>
    /// 인테리어 머티리얼(Assets/Materials/Interior/Int_*.mat)을 최신 팔레트 + PBR 텍스처로 일괄 갱신한다.
    /// 머티리얼은 이름으로 공유되는 에셋이라 씬·프리팹을 다시 빌드하지 않아도 모든 곳에 반영된다 (GUID 유지).
    /// 같은 코드를 빌더(InteriorMaterialFactory)가 쓰므로 빌더를 다시 돌려도 같은 결과가 나온다.
    /// </summary>
    public static class InteriorMaterialUpgrade
    {
        [MenuItem("Tools/GameUI/Art/Upgrade Interior Materials (Palette + PBR)")]
        public static void UpgradeMenu() { Debug.Log(Upgrade()); }

        /// <summary>배치모드: -executeMethod GameUI.EditorTools.Art.InteriorMaterialUpgrade.UpgradeBatch</summary>
        public static void UpgradeBatch()
        {
            string text; try { text = Upgrade(); } catch (Exception e) { text = "오류: " + e; Debug.LogException(e); }
            Debug.Log(text);
            EditorApplication.Exit(text.StartsWith("오류") ? 1 : 0);
        }

        public static string Upgrade()
        {
            InteriorMaterialFactory.ResetCache();
            int n = 0;
            foreach (PropertyInfo p in typeof(InteriorMaterialFactory).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (p.PropertyType != typeof(Material)) continue;
                if (p.GetValue(null) != null) n++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"[Art] 인테리어 머티리얼 {n}개 갱신 (팔레트 + PBR 텍스처: {PbrTextureFactory.Dir})";
        }
    }
}
