using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 가스 누출 시뮬레이션. HazardManager.GasLevel 을 시간에 따라 올리고 내린다.
    /// - 상승: 누출 중이고 열려 있는 밸브가 있을 때 (열린 밸브 비율만큼). 밸브를 모두 잠그면 상승이 멈춘다.
    /// - 감소: 열린 창문 수에 비례 (맞통풍) + 아주 느린 자연 감소
    /// - explosionThreshold 를 넘으면 전기/화기 조작이 폭발로 이어진다. 환기로 이 값 이하가 되면 안전.
    /// 농도는 HUD 에 직접 표시하지 않고 GasScreenEffect / 냄새 문구로 간접 표현한다.
    /// </summary>
    public class GasLeakController : MonoBehaviour
    {
        [Header("누출원 (하나라도 열려 있으면 가스가 새어 나온다)")]
        [SerializeField] private ValveInteractable[] valves;
        [Header("환기")]
        [SerializeField] private ToggleInteractable[] windows;

        [Header("속도 (초당 농도 변화, 0~1 스케일)")]
        [SerializeField, Min(0f)] private float risePerSecond = 0.01f;
        [SerializeField, Min(0f)] private float ventilationPerOpenWindow = 0.005f;
        [SerializeField, Min(0f)] private float naturalDecayPerSecond = 0.0005f;

        [Header("폭발 기준")]
        [SerializeField, Range(0f, 1f)] private float explosionThreshold = 0.15f;

        [Header("소리 (3D 오디오 슬롯, 클립은 비워둠)")]
        [SerializeField] private AudioSource leakAudio;

        private bool leaking;

        public static GasLeakController Instance { get; private set; }

        public bool IsLeaking => leaking;
        public float ExplosionThreshold => explosionThreshold;
        public float GasLevel => HazardManager.Instance != null ? HazardManager.Instance.GasLevel : 0f;
        /// <summary>지금 전기/화기를 조작하면 폭발하는가</summary>
        public bool IsExplosive => GasLevel > explosionThreshold;

        public int OpenValveCount
        {
            get { int n = 0; if (valves != null) foreach (ValveInteractable v in valves) if (v != null && v.IsOpen) n++; return n; }
        }

        public int OpenWindowCount
        {
            get { int n = 0; if (windows != null) foreach (ToggleInteractable w in windows) if (w != null && w.IsOn) n++; return n; }
        }

        /// <summary>가스가 계속 새어 나오는 중인가 (누출 시작 + 열린 밸브 있음)</summary>
        public bool SourceActive => leaking && OpenValveCount > 0;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void StartLeak() => leaking = true;
        public void StopLeak() => leaking = false;

        private void Update()
        {
            HazardManager hazard = HazardManager.Instance;
            if (hazard == null) return;

            UpdateAudio();
            if (GameFlow.GameplayBlocked) return;

            float rise = 0f;
            if (leaking && valves != null && valves.Length > 0)
                rise = risePerSecond * OpenValveCount / valves.Length;
            float decay = naturalDecayPerSecond + ventilationPerOpenWindow * OpenWindowCount;

            float current = hazard.GasLevel;
            float next = Mathf.Clamp01(current + (rise - decay) * Time.deltaTime);
            if (!Mathf.Approximately(current, next)) hazard.SetGas(next);
        }

        private void UpdateAudio()
        {
            if (leakAudio == null) return;
            bool want = SourceActive && !GameFlow.IsPaused;
            if (want && !leakAudio.isPlaying && leakAudio.clip != null) leakAudio.Play();
            else if (!want && leakAudio.isPlaying) leakAudio.Stop();
        }
    }
}
