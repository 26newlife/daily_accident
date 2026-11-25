using UnityEngine;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 가스 농도의 간접 표현: 화면 가장자리의 연한 노란 비네트 + 어지러움(카메라 기울어짐).
    /// 농도 수치는 어디에도 직접 표시하지 않는다.
    /// </summary>
    public class GasScreenEffect : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Image yellowVignette;
        [SerializeField] private Color vignetteColor = new Color(0.95f, 0.85f, 0.25f, 1f);
        [SerializeField, Range(0f, 1f), Tooltip("이 농도부터 비네트가 보이기 시작")] private float vignetteStart = 0.05f;
        [SerializeField, Range(0f, 1f), Tooltip("이 농도에서 비네트 최대")] private float vignetteFull = 0.7f;
        [SerializeField, Range(0f, 1f)] private float maxVignetteAlpha = 0.45f;
        [SerializeField, Range(0f, 1f), Tooltip("이 농도부터 어지러움 시작")] private float dizzyStart = 0.3f;
        [SerializeField] private float smoothing = 2f;
        [SerializeField, Tooltip("어지러움이 심해지는 속도 (초당)")] private float dizzyRisePerSecond = 0.6f;
        [SerializeField, Tooltip("가스에서 벗어난 뒤 어지러움이 풀리는 속도 (초당). 0.08 이면 최대치에서 약 12초")] private float dizzyRecoverPerSecond = 0.08f;

        private float shown, dizzy;

        private void Awake()
        {
            if (yellowVignette != null)
            {
                if (yellowVignette.sprite == null) yellowVignette.sprite = VitalsScreenEffect.CreateVignetteSprite(128);
                yellowVignette.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, 0f);
            }
        }

        private void Update()
        {
            // 엔딩/사망 화면에서는 가스 효과를 걷어낸다 (탈출한 뒤에도 화면이 기울어 있지 않게)
            float gas = HazardManager.Instance != null && !GameCore.GameFlow.IsEnded ? HazardManager.Instance.PlayerGas : 0f;
            float target = Mathf.InverseLerp(vignetteStart, vignetteFull, gas);
            // 가스에서 벗어나도 노란 비네트가 한순간에 사라지지 않고 서서히 걷힌다 (올라갈 때보다 3배 느리게)
            shown = Mathf.Lerp(shown, target, Time.unscaledDeltaTime * (target > shown ? smoothing : smoothing * 0.35f));

            if (yellowVignette != null)
                yellowVignette.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, shown * maxVignetteAlpha);
            if (player != null)
            {
                // 가스를 마시면 빠르게 어지러워지고, 신선한 공기로 나와도 바로 멀쩡해지지 않고 서서히 풀린다
                float want = Mathf.InverseLerp(dizzyStart, 1f, gas);
                float rate = want > dizzy ? dizzyRisePerSecond : dizzyRecoverPerSecond;
                dizzy = Mathf.MoveTowards(dizzy, want, rate * Time.unscaledDeltaTime);
                player.SetDizziness(dizzy);
            }
        }
    }
}
