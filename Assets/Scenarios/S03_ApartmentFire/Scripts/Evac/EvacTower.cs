using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameGameplay
{
    /// <summary>
    /// 집의 "걸어서 오르내리는 비상계단"(계단탑) 좌표 — 시나리오 3 의 루트 A(옥상)·D(1층 하강)가 이 계단을 직접 걷는다.
    /// 값은 HouseStairwellBuilder(에디터)와 같다: 계단탑 내부 x -23.9 ~ -18, z -3.08 ~ -0.13, 층 바닥 y = (층 - 10) x 3,
    /// 옥상 출입층 y 33 (20층 위), 1층 아래 반층참 = 지면 y -28.5 (서쪽 벽 출구).
    /// </summary>
    public static class EvacTower
    {
        public const float FloorHeight = 3f;
        public const int HomeFloor = 10;
        public const float RoofY = 33f;
        public const float GroundY = -28.5f;

        /// <summary>계단탑 안 (10층 기존 계단실 제외)</summary>
        public static bool Contains(Vector3 p) => p.x > -24.4f && p.x < -17.9f && p.z > -3.45f && p.z < 0.3f && p.y > GroundY - 1f && p.y < RoofY + 3.5f;

        /// <summary>높이로 층 번호 (층참 바닥 0.5m 아래부터 그 층)</summary>
        public static int FloorOf(float y) => Mathf.FloorToInt((y + 0.5f) / FloorHeight) + HomeFloor;

        public static float FloorY(int floor) => (floor - HomeFloor) * FloorHeight;

        /// <summary>
        /// 계단탑 층참의 장식용 방화문(닫힌 그림)을 숨기거나 보인다. 시나리오 3 은 14·17층에 실제로 여닫는 방화문을 둔다.
        /// 집 루트(Apartment_1004_Base)/WalkableStairwell/Shaft/Landing_{층}F 아래 FireDoor·FireDoor_Handle.
        /// </summary>
        public static void SetDecorDoorsVisible(bool visible, params int[] floors)
        {
            Transform shaft = FindShaft();
            if (shaft == null) return;
            foreach (int f in floors)
            {
                Transform landing = shaft.Find($"Landing_{f}F");
                if (landing == null) continue;
                foreach (string n in new[] { "FireDoor", "FireDoor_Handle" })
                {
                    Transform t = landing.Find(n);
                    if (t != null) t.gameObject.SetActive(visible);
                }
            }
        }

        private static Transform FindShaft()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                foreach (GameObject g in s.GetRootGameObjects())
                {
                    Transform t = g.transform.Find("WalkableStairwell/Shaft");
                    if (t != null) return t;
                }
            }
            return null;
        }
    }
}
