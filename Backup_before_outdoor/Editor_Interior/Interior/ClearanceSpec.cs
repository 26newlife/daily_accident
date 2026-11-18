using GameGameplay;
using UnityEditor;
using UnityEngine;

namespace GameUI.EditorTools.Interior
{
    /// <summary>
    /// 가구 배치/검증이 공유하는 통로 기준. 플레이어 프리팹의 CharacterController 에서 반지름 R·높이 H 를 읽어 계산한다.
    /// (프리팹이 없거나 읽지 못하면 기본값 R=0.28, H=1.75 사용)
    /// </summary>
    public static class ClearanceSpec
    {
        public const string PlayerPrefabPath = "Assets/Prefabs/Gameplay/Player.prefab";

        // ───────── 기준 폭 (m) ─────────
        public const float MainRouteWidth = 0.9f;          // 주 동선 최소 폭 (현관→거실→주방→각 방 문→베란다/대피공간)
        public const float AuxRouteWidth = 0.7f;           // 보조 동선 최소 폭 (침대 옆, 가구 사이)
        public const float AbsoluteMargin = 0.2f;          // 절대 최소 폭 = 지름 + 이 값 (이보다 좁은 통과 가능 통로는 만들지 않는다)
        public const float DoorSwingMargin = 0.3f;         // 문 열림 반경 바깥으로 추가로 비울 거리
        public const float WindowFrontDepth = 0.8f;        // 창문 앞 비울 깊이
        public const float WindowFrontHalfWidth = 0.4f;    // 창 손잡이 좌우 비울 폭 (한쪽)
        public const float InteractableFront = 0.9f;       // 상호작용 대상 앞 서 있을 수 있어야 하는 거리
        public const float EyeHeight = 1.6f;
        public const float GridStep = 0.05f;               // 검사 격자 간격
        public const float Tolerance = 0.03f;              // 격자 양자화 허용 오차 (폭 기준 한쪽)

        // 가구 앞 사용 공간 (배치 시 참고용 — 방별 주석 및 검증의 근거)
        public const float FrontBedSide = 0.7f, FrontSofaTv = 1.0f, FrontCounter = 0.9f, FrontDiningChairBack = 0.8f,
            FrontWardrobe = 0.8f, FrontFridge = 0.9f;

        public static float Radius { get; private set; } = 0.28f;
        public static float Height { get; private set; } = 1.75f;
        public static float StepOffset { get; private set; } = 0.3f;
        public static float InteractDistance { get; private set; } = 2.0f;

        public static float Diameter => Radius * 2f;
        /// <summary>절대 최소 폭 = 2R + 0.2 (R=0.28 → 0.76)</summary>
        public static float AbsoluteMinWidth => Diameter + AbsoluteMargin;

        /// <summary>플레이어 프리팹에서 값을 읽는다</summary>
        public static void Load()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null) return;
            var cc = prefab.GetComponent<CharacterController>();
            if (cc != null) { Radius = cc.radius; Height = cc.height; StepOffset = cc.stepOffset; }
            var interactor = prefab.GetComponent<PlayerInteractor>();
            if (interactor != null)
            {
                var so = new SerializedObject(interactor);
                var p = so.FindProperty("interactDistance");
                if (p != null) InteractDistance = p.floatValue;
            }
        }
    }
}
