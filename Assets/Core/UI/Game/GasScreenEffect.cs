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

        private float shown;

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
            float gas = HazardManager.Instance != null ? HazardManager.Instance.PlayerGas : 0f;
            float target = Mathf.InverseLerp(vignetteStart, vignetteFull, gas);
            shown = Mathf.Lerp(shown, target, Time.unscaledDeltaTime * smoothing);

            if (yellowVignette != null)
                yellowVignette.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, shown * maxVignetteAlpha);
            if (player != null)
                player.SetDizziness(Mathf.InverseLerp(dizzyStart, 1f, gas));
        }
    }
}
