using QuakeSurvival.Player;
using UnityEngine;
using UnityEngine.UI;

namespace QuakeSurvival.UI
{
    /// <summary>
    /// 화면 아래 얇은 달리기 체력 막대. 체력이 가득 차 있으면 서서히 사라진다.
    /// 지친 상태(다시 달릴 수 없음)에서는 막대 색이 경고색으로 바뀐다.
    /// </summary>
    public sealed class StaminaBarUI : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color exhaustedColor = Color.red;
        [SerializeField] private float fadeSpeed = 3f;

        private void Awake()
        {
            if (player == null || group == null || fill == null || fillImage == null)
            {
                Debug.LogError("[StaminaBarUI] 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            group.alpha = 0f;
        }

        private void Update()
        {
            float ratio = player.StaminaRatio;
            fill.localScale = new Vector3(ratio, 1f, 1f);
            fillImage.color = player.StaminaExhausted ? exhaustedColor : normalColor;

            float targetAlpha = ratio >= 0.999f ? 0f : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
        }
    }
}
