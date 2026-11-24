using QuakeSurvival.Fx;
using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>
    /// 밀려오는 해일 앞면(임시 도형): 수면 위로 솟은 물 벽(body) + 앞으로 기운 흰 물마루(crest) + 물보라 파티클.
    /// TsunamiController의 FrontZ/TopY를 따라 움직이고, 높이는 밀려오는 진행도에 따라 커졌다가 줄어든다.
    /// 나중에 실제 파도 모델로 바꿀 때는 body/crest 자리에 모델을 넣으면 된다.
    /// </summary>
    public sealed class TsunamiWave : MonoBehaviour
    {
        [SerializeField] private TsunamiController tsunami;
        [SerializeField] private FxLibrary fx;
        [SerializeField] private Transform player;
        [SerializeField] private Transform body;   // 아래쪽 중심이 원점, 높이 1인 물 벽(앞면이 원점 z)
        [SerializeField] private Transform crest;
        [SerializeField] private float maxHeight = 9f;
        [SerializeField] private Color sprayColor = new Color(0.85f, 0.9f, 0.92f, 0.6f);

        private Transform sprayRoot;
        private ParticleSystem spray;
        private bool visible = true;

        private void Awake()
        {
            if (tsunami == null || fx == null || !fx.IsValid || player == null || body == null || crest == null)
            {
                Debug.LogError("[TsunamiWave] 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            sprayRoot = new GameObject("WaveSpray").transform;
            sprayRoot.SetParent(transform, false);
            var spec = new ParticleFactory.Spec(sprayColor, 2.5f, 7f, 3.5f, 0.5f, 12f);
            spray = ParticleFactory.Loop(sprayRoot, fx.Smoke, spec, 90f);
            SetVisible(false);
        }

        private void LateUpdate()
        {
            float p = tsunami.ArrivalProgress;
            if (p < 0f)
            {
                SetVisible(false);
                return;
            }
            SetVisible(true);

            float rise = Mathf.SmoothStep(0f, 1f, p / 0.12f);
            // 끝까지 밀려오면 물마루가 무너져 수면 아래로 가라앉는다
            float settle = 1f - Mathf.SmoothStep(0f, 1f, (p - 0.8f) / 0.2f);
            float height = maxHeight * rise * settle;
            if (p >= 1f && height < 0.05f)
            {
                SetVisible(false);
                return;
            }

            float front = tsunami.FrontZ;
            float top = tsunami.TopY;
            body.position = new Vector3(body.position.x, top - 0.5f, front);
            body.localScale = new Vector3(1f, height + 0.5f, 1f);
            crest.position = new Vector3(crest.position.x, top + height, front - 0.6f);
            sprayRoot.position = new Vector3(player.position.x, top + height, front);
        }

        private void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            body.gameObject.SetActive(value);
            crest.gameObject.SetActive(value);
            if (value) spray.Play();
            else spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
