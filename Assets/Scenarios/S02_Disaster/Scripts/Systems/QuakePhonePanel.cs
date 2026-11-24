using GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 스마트폰(Tab)을 열면 옆에 붙는 시나리오 2 화면 (원본 PhoneAction + PhoneMapUI 이식):
    ///   · 대피 지도 — 우리 동·지진 옥외대피장소(단지 놀이터 옆 공터)·지진해일 대피소(뒷산)·강, 현재 위치 표시
    ///   · 가족 연락 — 대피소에서는 "문자 보내기"(안부 알림, 보너스), 그 밖에서는 "전화하기"(대피 중 통화는 감점, 연결도 잘 안 된다)
    /// 공통 폰의 119 버튼도 대피 중 통화로 센다. UI 는 빌더가 만들고 여기서는 표시/동작만 맡는다.
    /// </summary>
    public class QuakePhonePanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform mapArea;
        [SerializeField] private RectTransform youMarker;
        [SerializeField] private Button contactButton;
        [SerializeField] private TMP_Text contactLabel;
        [SerializeField] private TMP_Text statusText;
        [SerializeField, Tooltip("지도에 그리는 월드 범위 (x 최소·최대, z 최소·최대) — 위쪽이 북쪽(-z)")] private Vector4 worldRect = new Vector4(-110f, 50f, -130f, 75f);
        [SerializeField] private AudioClip badCallLoop;
        [SerializeField] private AudioClip textSent;

        private float callUntil;
        private AudioSource callAudio;

        private void Awake()
        {
            if (group != null) { group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false; }
            if (contactButton != null) contactButton.onClick.AddListener(OnContact);
            callAudio = gameObject.AddComponent<AudioSource>();
            callAudio.clip = badCallLoop; callAudio.loop = true; callAudio.playOnAwake = false; callAudio.volume = 0.5f;
            callAudio.outputAudioMixerGroup = QuakeFx.SfxGroup;
        }

        public void Hook()
        {
            EventBus.ScenarioEventRaised -= OnEvent;
            EventBus.ScenarioEventRaised += OnEvent;
        }

        private void OnDestroy() { EventBus.ScenarioEventRaised -= OnEvent; }

        private static void OnEvent(ScenarioEvent e)
        {
            if (e.Type == ScenarioEventType.PhoneAction && e.SourceId == "call_119" && QuakeRecord.Instance != null) QuakeRecord.Instance.CountCall();
        }

        private bool InShelter => QuakeAreas.Instance != null && QuakeAreas.Instance.Current == QuakeArea.Shelter;
        private bool OnCall => Time.time < callUntil;

        private void Update()
        {
            bool open = GameFlow.IsPhoneOpen && !GameFlow.IsEnded;
            if (group != null)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, open ? 1f : 0f, Time.unscaledDeltaTime * 8f);
                group.interactable = open; group.blocksRaycasts = open;
            }
            if (!open && callAudio.isPlaying && !OnCall) callAudio.Stop();
            if (OnCall == false && callAudio.isPlaying) callAudio.Stop();
            if (!open) return;

            QuakeRecord rec = QuakeRecord.Instance;
            if (contactLabel != null)
                contactLabel.text = OnCall ? "통화 중... 연결 상태가 나쁘다" : rec != null && rec.FamilyContacted ? "가족에게 문자를 보냈다" : InShelter ? "가족에게 문자 보내기" : "가족에게 전화하기";
            if (contactButton != null) contactButton.interactable = !OnCall && !(rec != null && rec.FamilyContacted);
            UpdateMarker();
        }

        private void UpdateMarker()
        {
            if (youMarker == null || mapArea == null || QuakeTimeline.Instance == null || QuakeTimeline.Instance.Player == null) return;
            Vector3 p = QuakeTimeline.Instance.Player.transform.position;
            float u = Mathf.InverseLerp(worldRect.x, worldRect.y, p.x);
            float v = 1f - Mathf.InverseLerp(worldRect.z, worldRect.w, p.z);   // 위쪽 = 북쪽(-z)
            Rect r = mapArea.rect;
            youMarker.anchoredPosition = new Vector2(Mathf.Lerp(r.xMin, r.xMax, u), Mathf.Lerp(r.yMin, r.yMax, v));
        }

        private void OnContact()
        {
            QuakeRecord rec = QuakeRecord.Instance;
            if (rec == null || OnCall || rec.FamilyContacted) return;
            if (InShelter)
            {
                rec.FamilyContacted = true;
                QuakeFx.Play2D(textSent, 0.6f);
                if (statusText != null) statusText.text = "\"대피소에 무사히 도착했어요. 걱정하지 마세요.\"";
                EventBus.Raise(ScenarioEventType.PhoneAction, QuakeKeys.PhoneTextFamily, true);
                return;
            }
            rec.CountCall();
            callUntil = Time.time + 4f;
            if (callAudio.clip != null) callAudio.Play();
            if (statusText != null) statusText.text = "신호음만 이어진다... 지금은 통화가 몰려 연결되지 않는다.";
            EventBus.Raise(ScenarioEventType.PhoneAction, QuakeKeys.PhoneCallFamily, true);
        }

        public void SetWorldRect(Vector4 r) => worldRect = r;
    }
}
