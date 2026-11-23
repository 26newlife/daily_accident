using QuakeSurvival.Audio;
using System.Collections;
using QuakeSurvival.Player;
using QuakeSurvival.Quake;
using QuakeSurvival.Safety;
using QuakeSurvival.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuakeSurvival.Interaction
{
    /// <summary>
    /// T키: 휴대폰 열기/닫기. 화면에 주변 대피 지도(내 위치, 옥외대피장소, 지진해일 대피소)가 보인다.
    /// 휴대폰을 연 상태에서 1키: 가족에게 연락.
    /// - 대피소에 도착한 뒤에는 문자를 보낸다(점수).
    /// - 대피 도중 전화를 걸면 통화 상태가 나빠 오래 붙잡힌다(함정).
    /// 지도를 보거나 통화하는 중에도 이동/숙이기/상호작용은 가능하지만, 통화 중에는 이동이 느려진다.
    /// </summary>
    public sealed class PhoneAction : MonoBehaviour
    {
        private const string SpeedReason = "phone";

        [SerializeField] private QuakeDirector director;
        [SerializeField] private ZoneSensor zones;
        [SerializeField] private FirstPersonController player;
        [SerializeField] private MessageUI messages;
        [SerializeField] private OutcomeEvaluator evaluator;
        [SerializeField] private PhoneMapUI map;
        [SerializeField] private float callSeconds = 15f;
        [SerializeField, Range(0.1f, 1f)] private float callSpeed = 0.5f;
        [SerializeField] private AudioClip badCallLoop;
        [SerializeField] private AudioClip textSent;

        private static readonly string[] CallLines =
        {
            "(가족에게 전화를 걸었다... 신호가 잘 잡히지 않는다...)",
            "(\"여보세요? ...잘 안 들려... 지금 어디...\" 목소리가 자꾸 끊긴다.)",
            "(지지직... 통화가 끊겼다 이어지기를 반복한다. 휴대폰을 귀에서 뗄 수가 없다...)",
        };

        public bool FamilyContacted { get; private set; }
        public int CallsDuringEvacuation { get; private set; }
        public bool OnCall { get; private set; }

        private bool locked;
        private bool decided;
        private AudioSource callAudio;

        private void Awake()
        {
            if (director == null || zones == null || player == null || messages == null || evaluator == null || map == null)
            {
                Debug.LogError("[PhoneAction] 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            callAudio = gameObject.AddComponent<AudioSource>();
            callAudio.clip = badCallLoop;
            callAudio.loop = true;
            callAudio.playOnAwake = false;
            callAudio.spatialBlend = 0f;
            MixedVolume.Attach(callAudio, AudioCategory.Sfx, 0.5f);
        }

        private void OnEnable()
        {
            if (evaluator != null) evaluator.Decided += OnDecided;
        }

        private void OnDisable()
        {
            if (evaluator != null) evaluator.Decided -= OnDecided;
            // 통화 중에 꺼져도 느려진 속도가 남지 않게 한다
            StopAllCoroutines();
            EndCall();
        }

        // 결과가 나면 통화를 끊는다. 대피소 클리어 뒤에는 가족 문자를 보낼 수 있게 휴대폰을 계속 쓸 수 있다.
        private void OnDecided(DeathCause cause)
        {
            StopAllCoroutines();
            EndCall();
            decided = true;
            locked = cause != DeathCause.None;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (locked || keyboard == null || !player.InputEnabled)
            {
                if (map.IsOpen && (locked || !player.InputEnabled)) map.SetOpen(false);
                return;
            }

            if (keyboard.tKey.wasPressedThisFrame) map.SetOpen(!map.IsOpen);
            if (!map.IsOpen) return;

            map.SetMenu(MenuText());
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) ContactFamily();
        }

        private string MenuText()
        {
            if (OnCall) return "(통화 중... 연결 상태가 나쁘다)\nT  닫기";
            if (FamilyContacted) return "(가족에게 문자를 보냈다)\nT  닫기";
            if (zones.CurrentArea == PlayerArea.Shelter) return "1  가족에게 문자 보내기\nT  닫기";
            return "1  가족에게 전화하기\nT  닫기";
        }

        private void ContactFamily()
        {
            if (OnCall) return;
            if (zones.CurrentArea == PlayerArea.Shelter)
            {
                if (FamilyContacted) return;
                FamilyContacted = true;
                SoundPlayer.Play2D(textSent, AudioCategory.Ui, 0.7f);
                messages.ShowSubtitle("(가족에게 문자를 보냈다: \"대피소에 무사히 도착했어. 걱정하지 마.\")");
                return;
            }
            // 클리어 판정 뒤(결과 화면 전 잠깐)에는 전화로 감점되지 않게 문자만 허용한다
            if (decided)
            {
                messages.ShowSubtitle("(지금은 문자만 보낼 수 있다.)");
                return;
            }
            if (director.Phase == QuakePhase.Calm)
            {
                messages.ShowSubtitle("(별일 없는데 굳이 전화할 필요는 없겠다.)");
                return;
            }
            StartCoroutine(BadCall());
        }

        private IEnumerator BadCall()
        {
            OnCall = true;
            CallsDuringEvacuation++;
            player.SetSpeedModifier(SpeedReason, callSpeed);
            if (badCallLoop != null)
            {
                callAudio.Play();
            }

            float lineInterval = callSeconds / CallLines.Length;
            foreach (string line in CallLines)
            {
                messages.ShowSubtitle(line);
                yield return new WaitForSeconds(lineInterval);
            }

            EndCall();
            messages.ShowSubtitle("(결국 제대로 말 한마디 못 하고 통화가 끊겼다. 시간만 흘렀다...)");
        }

        private void EndCall()
        {
            if (!OnCall) return;
            OnCall = false;
            player.SetSpeedModifier(SpeedReason, 1f);
            if (callAudio != null) callAudio.Stop();
        }
    }
}
