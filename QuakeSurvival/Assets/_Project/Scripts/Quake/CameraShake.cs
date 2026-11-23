using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>
    /// QuakeDirector의 흔들림 세기에 따라 이 Transform(카메라를 담는 오프셋 노드)을 흔든다.
    /// 플레이어 컨트롤러가 움직이는 CameraRoot와 분리해서 서로 덮어쓰지 않게 한다.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        [SerializeField] private QuakeDirector director;
        [SerializeField] private float maxOffset = 0.08f;
        [SerializeField] private float maxAngle = 3f;
        [SerializeField] private float frequency = 9f;

        [SerializeField] private float impulseDecay = 2.5f;

        private float impulse;

        /// <summary>설정 메뉴의 "화면 흔들림 세기"(0~1).</summary>
        public float UserScale { get; set; } = 1f;

        /// <summary>폭발 등 순간 충격. 시간이 지나면 줄어든다.</summary>
        public void AddImpulse(float amount)
        {
            impulse = Mathf.Clamp(impulse + amount, 0f, 1.5f);
        }

        private void Awake()
        {
            if (director == null)
            {
                Debug.LogError("[CameraShake] director가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            impulse = Mathf.MoveTowards(impulse, 0f, impulseDecay * Time.deltaTime);
            float amount = (director.Intensity + impulse) * UserScale;
            if (amount <= 0f)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                return;
            }

            float t = Time.time * frequency;
            transform.localPosition = new Vector3(Noise(t, 0f), Noise(t, 10f), Noise(t, 20f)) * (maxOffset * amount);
            transform.localRotation = Quaternion.Euler(
                Noise(t, 30f) * maxAngle * amount,
                Noise(t, 40f) * maxAngle * amount,
                Noise(t, 50f) * maxAngle * amount);
        }

        private static float Noise(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;
    }
}
