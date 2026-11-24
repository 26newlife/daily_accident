using System.Collections;
using QuakeSurvival.Fx;
using QuakeSurvival.Quake;
using QuakeSurvival.Safety;
using QuakeSurvival.UI;
using UnityEngine;

namespace QuakeSurvival.Audio
{
    /// <summary>
    /// 장면 전체의 배경 소리를 상황에 맞춰 섞는다.
    /// - 비: 실내는 막힌 빗소리, 건물 밖은 거센 빗소리(구역이 바뀌면 교차 페이드)
    /// - 지진: 흔들림 세기를 따라가는 땅울림 + 건물 삐걱임/물건 덜컹거림
    /// - 번개 뒤 천둥, 재난문자 수신음, 지진해일 사이렌과 밀려오는 바다 굉음
    /// 결과가 나면 배경 소리를 낮춘다.
    /// </summary>
    public sealed class QuakeSoundscape : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private QuakeDirector director;
        [SerializeField] private ZoneSensor zones;
        [SerializeField] private TsunamiController tsunami;
        [SerializeField] private MessageUI messages;
        [SerializeField] private LightningFlash lightning;
        [SerializeField] private OutcomeEvaluator evaluator;
        [SerializeField] private Transform listener;

        [Header("반복 소리")]
        [SerializeField] private AudioClip rainOutside;
        [SerializeField] private AudioClip rainInside;
        [SerializeField] private AudioClip rumble;
        [SerializeField] private AudioClip siren;
        [SerializeField] private AudioClip oceanRoar;

        [Header("한 번 소리")]
        [SerializeField] private AudioClip creak;
        [SerializeField] private AudioClip clatter;
        [SerializeField] private AudioClip thunder;
        [SerializeField] private AudioClip alert;

        private LoopVoice rainOut;
        private LoopVoice rainIn;
        private LoopVoice ground;
        private LoopVoice sirenVoice;
        private LoopVoice roar;
        private float nextDebris;
        private float duck = 1f;
        private bool decided;

        private void Awake()
        {
            if (director == null || zones == null || tsunami == null || messages == null || listener == null)
            {
                Debug.LogError("[QuakeSoundscape] 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            rainOut = new LoopVoice(gameObject, rainOutside, AudioCategory.Ambient, 0.8f, false);
            rainIn = new LoopVoice(gameObject, rainInside, AudioCategory.Ambient, 0.8f, false);
            ground = new LoopVoice(gameObject, rumble, AudioCategory.Ambient, 1.5f, false);
            sirenVoice = new LoopVoice(gameObject, siren, AudioCategory.Ambient, 0.3f, false);
            roar = new LoopVoice(gameObject, oceanRoar, AudioCategory.Ambient, 0.5f, false);
        }

        private void OnEnable()
        {
            if (messages != null) messages.AlertShown += OnAlert;
            if (lightning != null) lightning.Flashed += OnLightning;
            if (evaluator != null) evaluator.Decided += OnDecided;
        }

        private void OnDisable()
        {
            if (messages != null) messages.AlertShown -= OnAlert;
            if (lightning != null) lightning.Flashed -= OnLightning;
            if (evaluator != null) evaluator.Decided -= OnDecided;
        }

        private void Update()
        {
            if (rainOut == null) return;
            float dt = Time.deltaTime;
            bool outdoors = zones.CurrentArea.IsOutdoors();
            rainOut.Target = (outdoors ? 0.9f : 0.12f) * duck;
            rainIn.Target = (outdoors ? 0f : 0.55f) * duck;

            float shake = director.Intensity;
            ground.Target = Mathf.Clamp01(shake * 1.2f) * duck;
            ground.Pitch = 0.85f + shake * 0.3f;

            sirenVoice.Target = director.Phase == QuakePhase.Tsunami ? 0.45f * duck : 0f;
            roar.Target = tsunami.Approach * duck;

            rainOut.Tick(dt);
            rainIn.Tick(dt);
            ground.Tick(dt);
            sirenVoice.Tick(dt);
            roar.Tick(dt);

            TickDebris(shake);
        }

        /// <summary>흔들리는 동안 주변 아무 데서나 삐걱임과 덜컹거림이 들린다.</summary>
        private void TickDebris(float shake)
        {
            if (decided || shake < 0.2f || Time.time < nextDebris) return;
            nextDebris = Time.time + Random.Range(0.25f, 1.1f) / Mathf.Max(0.3f, shake);
            Vector3 offset = Random.insideUnitSphere * 4f;
            offset.y = Mathf.Abs(offset.y) * 0.5f + 0.5f;
            AudioClip clip = Random.value < 0.35f ? creak : clatter;
            SoundPlayer.PlayAt(clip, listener.position + offset, AudioCategory.Sfx, 0.4f + shake * 0.5f, 0.15f);
        }

        private void OnAlert()
        {
            SoundPlayer.Play2D(alert, AudioCategory.Ui, 0.6f);
        }

        private void OnLightning()
        {
            StartCoroutine(ThunderAfter(Random.Range(0.4f, 2.5f)));
        }

        private IEnumerator ThunderAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            SoundPlayer.Play2D(thunder, AudioCategory.Ambient, Random.Range(0.55f, 0.9f) * duck, 0.1f);
        }

        private void OnDecided(DeathCause cause)
        {
            decided = true;
            duck = 0.3f;
        }
    }
}
