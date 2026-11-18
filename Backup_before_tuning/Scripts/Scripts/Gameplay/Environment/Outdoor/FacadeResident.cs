using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 맞은편 동 창문/베란다의 주민. 평소에는 가끔 나타났다 사라지고(실루엣),
    /// 폭발 후에는 베란다에 나와 아래층을 쳐다보며 손을 흔든다.
    /// </summary>
    public class FacadeResident : MonoBehaviour
    {
        [SerializeField] private GameObject visual;
        [SerializeField] private Transform rightArm;
        [SerializeField, Tooltip("평소 모습 사용 여부 (false 면 폭발 후에만 나타남)")] private bool idleAppearance = true;
        [SerializeField] private Vector2 idleVisibleSeconds = new Vector2(4f, 12f);
        [SerializeField] private Vector2 idleHiddenSeconds = new Vector2(10f, 30f);

        private bool reacting;
        private float timer;
        private bool shown;

        private void Awake()
        {
            SetShown(false);
            timer = Random.Range(idleHiddenSeconds.x, idleHiddenSeconds.y);
        }

        private void Update()
        {
            if (reacting)
            {
                if (rightArm != null) rightArm.localRotation = Quaternion.Euler(0f, 0f, 150f + Mathf.Sin(Time.time * 8f) * 25f);   // 손 흔들기
                return;
            }
            if (!idleAppearance) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            SetShown(!shown);
            timer = shown ? Random.Range(idleVisibleSeconds.x, idleVisibleSeconds.y) : Random.Range(idleHiddenSeconds.x, idleHiddenSeconds.y);
        }

        /// <summary>폭발 반응: delay 초 후 나타나서 손짓</summary>
        public void React(float delay) => Invoke(nameof(BeginReaction), delay);

        private void BeginReaction() { reacting = true; SetShown(true); }

        private void SetShown(bool on) { shown = on; if (visual != null) visual.SetActive(on); }
    }
}
