using QuakeSurvival.Art;
using QuakeSurvival.Interaction;
using QuakeSurvival.Player;
using QuakeSurvival.Quake;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// 1인칭 플레이어: Player(CharacterController) > CameraRoot(시점) > ShakeOffset(흔들림) > Camera(+손전등).
    /// </summary>
    internal static class PlayerBuilder
    {
        public sealed class Parts
        {
            public FirstPersonController Controller;
            public Interactor Interactor;
            public CameraShake Shake;
            public Light FlashlightSpot;
            public Camera Camera;
        }

        public static Parts Build(Vector3 spawn)
        {
            var player = new GameObject("Player");
            player.transform.position = spawn;
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.7f;
            // 반지름 0.25 -> 숙였을 때 최소 키 0.5m, skinWidth 0.03 -> 탁자 아래 0.72m에 여유 있게 들어간다
            cc.radius = 0.25f;
            cc.skinWidth = 0.03f;
            cc.center = new Vector3(0f, 0.85f, 0f);

            Transform camRoot = Pivot(player.transform, "CameraRoot", new Vector3(0f, 1.6f, 0f));
            Transform shakeNode = Pivot(camRoot, "ShakeOffset", Vector3.zero);
            var shake = shakeNode.gameObject.AddComponent<CameraShake>();

            Camera cam = BuildCamera(shakeNode);
            Light spot = BuildFlashlight(cam.transform);

            var fpc = player.AddComponent<FirstPersonController>();
            SetRef(fpc, "cameraRoot", camRoot);

            var interactor = player.AddComponent<Interactor>();
            SetRef(interactor, "viewCamera", cam);
            return new Parts { Controller = fpc, Interactor = interactor, Shake = shake, FlashlightSpot = spot, Camera = cam };
        }

        private static Camera BuildCamera(Transform parent)
        {
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(parent, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            // 폭우 내리는 해 질 녘: 하늘 대신 어두운 회청색 배경(안개 색과 맞춤)
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Fog;
            camGo.AddComponent<AudioListener>();
            return cam;
        }

        private static Light BuildFlashlight(Transform cam)
        {
            var go = new GameObject("Flashlight");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0.2f, -0.15f, 0f);
            var spot = go.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 18f;
            spot.spotAngle = 55f;
            spot.intensity = 4f;
            spot.color = Palette.LampWarm;
            spot.enabled = false;
            return spot;
        }
    }
}
