using System;
using System.Collections;
using QuakeSurvival.Safety;
using QuakeSurvival.UI;
using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>
    /// 단계별로 정해진 시간에 단서(재난문자, 상황 자막)를 띄운다.
    /// 정답을 직접 알려주지 않고 상황만 묘사한다.
    /// </summary>
    public sealed class ClueSchedule : MonoBehaviour
    {
        [Serializable]
        public struct Clue
        {
            public QuakePhase phase;
            public float delay;
            public bool isAlert;
            [TextArea] public string text;
            [Tooltip("이 조치(SafetyChecklist actionId)가 이미 완료돼 있으면 띄우지 않는다. 비우면 항상 띄운다.")]
            public string skipIfDone;

            public Clue(QuakePhase phase, float delay, bool isAlert, string text, string skipIfDone = "")
            {
                this.phase = phase;
                this.delay = delay;
                this.isAlert = isAlert;
                this.text = text;
                this.skipIfDone = skipIfDone;
            }
        }

        [SerializeField] private QuakeDirector director;
        [SerializeField] private MessageUI messages;
        [SerializeField] private SafetyChecklist checklist;
        [SerializeField] private Clue[] clues =
        {
            new Clue(QuakePhase.Calm, 0.5f, false, "WASD 이동 / Shift 달리기 / Space 점프 / 마우스 시점 / F 상호작용 / Ctrl 숙이기 / T 휴대폰(지도)"),
            new Clue(QuakePhase.Calm, 4f, false, "(창밖으로 폭우가 쏟아진다. 바다가 거칠다...)"),
            new Clue(QuakePhase.Foreshock, 0.5f, false, "(갑자기 바닥이 흔들리기 시작했다!)"),
            new Clue(QuakePhase.Prepare, 0.5f, true,
                "[긴급재난문자] 해안시 동쪽 해역 규모 4.2 지진 발생\n낙하물로부터 몸 보호, 진동 멈춘 후 가까운 옥외대피장소로 대피하며 여진 주의"),
            new Clue(QuakePhase.Prepare, 6f, false, "(부엌 쪽에서 매캐한 냄새가 난다...)", SafetyChecklist.GasValve),
            new Clue(QuakePhase.Prepare, 16f, false, "(천장에서 빗물이 떨어진다. 콘센트에서 '탁' 하고 불꽃이 튀었다...)", SafetyChecklist.Breaker),
            new Clue(QuakePhase.Prepare, 28f, false, "(현관 문틀이 삐걱거리는 소리가 난다...)", SafetyChecklist.Door),
            new Clue(QuakePhase.Prepare, 50f, false, "(바닥 아래에서 낮게 우르릉거리는 소리가 커진다...)"),
            new Clue(QuakePhase.MainShock, 0.2f, false, "(쾅! 훨씬 더 큰 흔들림이 덮쳤다!)"),
            new Clue(QuakePhase.Aftermath, 1.5f, false, "(흔들림이 멈췄다... 이 건물 안은 더 이상 안전하지 않다.)"),
            new Clue(QuakePhase.Aftershock, 0.3f, false, "(또 흔들린다! 여진이다!)"),
            new Clue(QuakePhase.Aftershock, 2f, true,
                "[긴급재난문자] 해안시 동쪽 해역 규모 6.7 지진 발생\n지진해일 경보 발령. 해안가 접근 금지, 즉시 높은 곳으로 대피 바랍니다"),
            new Clue(QuakePhase.Tsunami, 2f, false, "(멀리서 사이렌이 길게 울린다...)"),
            new Clue(QuakePhase.Tsunami, 14f, false, "(바닷물이 이상할 정도로 멀리 빠져나가고 있다...)"),
            new Clue(QuakePhase.Tsunami, 32f, false, "(바다 쪽에서 굉음이 밀려온다!)"),
        };

        private void OnEnable()
        {
            if (director == null || messages == null)
            {
                Debug.LogError("[ClueSchedule] director 또는 messages가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            director.PhaseChanged += OnPhase;
        }

        private void OnDisable()
        {
            if (director != null) director.PhaseChanged -= OnPhase;
            StopAllCoroutines();
        }

        private void OnPhase(QuakePhase phase)
        {
            foreach (var clue in clues)
            {
                if (clue.phase == phase) StartCoroutine(ShowLater(clue));
            }
        }

        private bool IsAlreadyDone(string actionId)
        {
            return checklist != null && !string.IsNullOrEmpty(actionId) && checklist.IsDone(actionId);
        }

        private IEnumerator ShowLater(Clue clue)
        {
            yield return new WaitForSeconds(clue.delay);
            if (director.Phase != clue.phase) yield break;
            // 이미 조치를 끝냈으면 그 조치를 암시하는 단서는 띄우지 않는다(예: 가스를 잠갔으면 냄새가 나지 않음)
            if (IsAlreadyDone(clue.skipIfDone)) yield break;
            if (clue.isAlert) messages.ShowAlert(clue.text);
            else messages.ShowSubtitle(clue.text);
        }
    }
}
