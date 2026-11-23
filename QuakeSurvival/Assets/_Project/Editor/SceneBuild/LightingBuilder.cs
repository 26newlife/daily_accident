using QuakeSurvival.Art;
using QuakeSurvival.Fx;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// 조명 이야기: 폭우 내리는 해 질 녘. 낮게 깔린 구름 빛(키 라이트) + 3색 그라데이션 환경광 + 안개,
    /// 번개 섬광, 창밖 비. 전역 Volume(ACES 톤매핑, 블룸, 비네트, 색 보정, 화이트밸런스)으로 분위기를 잡는다.
    /// 값은 craft-game-floors/scene-lighting 의 "Moody" 행을 출발점으로 폭풍 분위기에 맞게 조정했다.
    /// </summary>
    internal static class LightingBuilder
    {
        private const string ProfilePath = "Assets/_Project/Settings/StormDuskVolume.asset";

        public static Light Build(Camera camera)
        {
            Light sun = BuildSun();
            BuildAmbient();
            BuildVolume();
            EnablePostProcessing(camera);

            var lightning = sun.gameObject.AddComponent<LightningFlash>();
            SetRef(lightning, "sky", sun);
            var so = new SerializedObject(lightning);
            so.FindProperty("flashColor").colorValue = Palette.Lightning;
            so.ApplyModifiedPropertiesWithoutUndo();

            BuildWindowRain();
            return sun;
        }

        private static Light BuildSun()
        {
            var go = new GameObject("Sun (Storm Dusk)");
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Palette.DuskKey;
            sun.intensity = 0.7f;
            sun.shadows = LightShadows.Soft;
            // 서쪽 바다 위 낮은 해: 고도 22도, 북서쪽에서 들어와 북쪽 창으로 빛이 든다
            go.transform.rotation = Quaternion.Euler(22f, 150f, 0f);
            return sun;
        }

        private static void BuildAmbient()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = Palette.AmbientEquator;
            RenderSettings.ambientGroundColor = Palette.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogColor = Palette.Fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.011f;
        }

        private static void BuildVolume()
        {
            EnsureFolder("Assets/_Project", "Settings");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            Configure(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("Global Volume (Storm Dusk)");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
        }

        private static void Configure(VolumeProfile profile)
        {
            var tone = Get<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = Get<Bloom>(profile);
            bloom.intensity.Override(0.4f);
            bloom.threshold.Override(1.1f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.3f);
            vignette.color.Override(Palette.Shadow);

            var color = Get<ColorAdjustments>(profile);
            color.saturation.Override(-8f);
            color.contrast.Override(14f);
            color.postExposure.Override(0.2f);

            var wb = Get<WhiteBalance>(profile);
            wb.temperature.Override(-10f);
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            T added = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(added, profile);
            return added;
        }

        /// <summary>스크립트로 만든 카메라는 후처리가 꺼져 있어 Volume이 아무 효과도 없다. 반드시 켠다.</summary>
        private static void EnablePostProcessing(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camera.allowHDR = true;
        }

        /// <summary>북쪽 창 너머(바다 쪽)에 고정된 비. 10층 창밖으로 폭우가 보인다.</summary>
        private static void BuildWindowRain()
        {
            var go = new GameObject("WindowRain");
            go.transform.position = new Vector3(0f, TopFloorY + 6f, 11f);
            var rain = go.AddComponent<StaticRain>();
            SetRef(rain, "rainMaterial", FxBuilder.RainMaterial());
            var so = new SerializedObject(rain);
            so.FindProperty("color").colorValue = Palette.Rain;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
