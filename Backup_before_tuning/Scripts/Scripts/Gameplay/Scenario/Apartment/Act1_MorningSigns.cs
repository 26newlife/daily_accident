using System;
using System.Collections;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 1 — 아침의 징후 (00:00 ~ 03:00, 평온한 아침과 전조 증상).
    ///
    /// 타임라인(게임 시간 기준, GameClock):
    ///   00:00 알람과 함께 기상 → 알람 끄기 → 일상 행동(세수 / 물 / 스마트폰) 튜토리얼
    ///   01:30 가스 냄새 + 누출음 + 멀티탭 스파크 시작 (가스 농도 상승 시작)
    ///   02:30 긴급 안내방송(자막) → 생존 행동 목표 제시
    ///
    /// 사망: 가스 농도가 기준 이상일 때 전기/화기 조작(전등 스위치, 커피포트, 전자레인지, 후드, 가스레인지) → 가스 폭발
    ///       스파크가 나는 멀티탭의 전원 스위치 조작 → 점화
    /// 생존(순서 무관, 모두 완료 시 클리어): 손전등 / 퓨즈콕 잠금 / 메인 밸브 잠금 / 창문 3개 환기 / 멀티탭 처리
    /// 점수는 계산하지 않고 ScoreSystem 에 행동 기록만 남긴다 (최종 계산은 Act 4).
    /// </summary>
    public class Act1_MorningSigns : ActBase
    {
        [Serializable]
        public struct SubtitleCue
        {
            [TextArea(1, 3)] public string text;
            [Min(0.5f)] public float seconds;
        }

        // ───────── 타임라인 ─────────
        [Header("타임라인 (게임 시간, 초)")]
        [SerializeField, Min(0f)] private float signsTime = 90f;       // 01:30 이상 징후
        [SerializeField, Min(0f)] private float broadcastTime = 150f;  // 02:30 긴급 방송
        [SerializeField, Min(0f)] private float nextActDelay = 3f;     // 클리어 후 Act 2 로 넘어가기까지 (실시간 초)

        // ───────── 참조 ─────────
        [Header("시스템/오브젝트 참조")]
        [SerializeField] private GasLeakController gas;
        [SerializeField] private ValveInteractable fuseCock;
        [SerializeField] private ValveInteractable mainValve;
        [SerializeField] private ToggleInteractable[] windows;

        // ───────── 상호작용 id (빌더가 배치한 오브젝트 id) ─────────
        [Header("상호작용 id")]
        [SerializeField] private string alarmId;
        [SerializeField] private string washId;
        [SerializeField] private string drinkId;
        [SerializeField] private string multitapSwitchId;
        [SerializeField] private string multitapPlugId;
        [SerializeField] private string[] clutterIds;
        [Tooltip("가스 누출 중 조작하면 폭발하는 전기/화기 오브젝트 id")]
        [SerializeField] private string[] dangerIds;
        [SerializeField] private string phoneOpenActionId = "phone_open";
        [SerializeField] private string flashlightActionId = "flashlight";

        // ───────── 목표 ─────────
        [Header("목표")]
        [SerializeField] private ObjectiveData objMorning;
        [SerializeField] private ObjectiveData objAlarm;
        [SerializeField] private ObjectiveData objWash;
        [SerializeField] private ObjectiveData objDrink;
        [SerializeField] private ObjectiveData objPhone;
        [SerializeField] private ObjectiveData objSmell;
        [SerializeField] private ObjectiveData objGasResponse;
        [SerializeField] private ObjectiveData objFlashlight;
        [SerializeField] private ObjectiveData objFuse;
        [SerializeField] private ObjectiveData objMainValve;
        [SerializeField] private ObjectiveData objVentilate;
        [SerializeField] private ObjectiveData objMultitap;

        // ───────── 연출 슬롯 ─────────
        [Header("연출 (오디오 클립은 비워둠)")]
        [SerializeField] private AudioSource alarmSource;
        [SerializeField] private AudioSource sparkAudio;
        [SerializeField] private AudioSource announcementSource;
        [SerializeField] private ParticleSystem sparkParticles;

        // ───────── 문구 ─────────
        [Header("문구")]
        [SerializeField, TextArea(1, 2)] private string smellText;
        [SerializeField, TextArea(1, 2)] private string smellStrongText;
        [SerializeField, Min(5f)] private float smellRepeatSeconds = 25f;
        [SerializeField, Range(0f, 1f)] private float smellStrongGas = 0.3f;
        [SerializeField] private SubtitleCue[] announcementCues;
        [SerializeField] private string clearMessage = "Act 1 클리어";

        // ───────── 사망 ─────────
        [Header("사망 원인")]
        [SerializeField] private DeathReason ignitionDeath = DeathReason.GasIgnition;
        [SerializeField] private DeathReason multitapDeath = DeathReason.MultitapFire;

        // ───────── 점수 기록 키 (ScoreSystem.Records) ─────────
        [Header("점수 기록 키")]
        [SerializeField] private string recFuseOrder = "act1_fuse_close_order";
        [SerializeField] private string recMainOrder = "act1_main_close_order";
        [SerializeField] private string recFuseTime = "act1_fuse_close_time";
        [SerializeField] private string recMainTime = "act1_main_close_time";
        [SerializeField] private string recVentTime = "act1_ventilation_done_time";
        [SerializeField] private string recMultitapOk = "act1_multitap_handled_correctly";
        [SerializeField] private string recMultitapTime = "act1_multitap_done_time";
        [SerializeField] private string recDangerAfterBroadcast = "act1_danger_attempts_after_broadcast";
        [SerializeField] private string recDangerCount = "act1_danger_attempts_total";
        [SerializeField] private string recFlashlightTime = "act1_flashlight_on_time";
        [SerializeField] private string recMorningTime = "act1_morning_routine_done_time";
        [SerializeField] private string recClearTime = "act1_clear_time";
        [SerializeField] private string recDeathAt = "act1_death_time";
        [SerializeField] private string recGasAtClear = "act1_gas_level_at_clear";

        // ───────── 상태 ─────────
        private bool alarmOff, washDone, drinkDone, phoneDone, morningDone;
        private bool signsStarted, broadcastDone, cleared;
        private bool flashlightLit;
        private bool plugged = true, sparksActive;
        private int clutterRemoved, valveCloseCount;
        private bool fuseRecorded, mainRecorded, ventRecorded, multitapRecorded;
        private float scentTimer;

        private GameClock Clock => GameClock.Instance;
        private ObjectiveSystem Objectives => ObjectiveSystem.Instance;
        private ScoreSystem Score => ScoreSystem.Instance;
        private float Now => Clock != null ? Clock.GameSeconds : 0f;

        // ═════════════════════════ 진행 ═════════════════════════
        protected override void OnEnter()
        {
            if (Clock != null)
            {
                Clock.ScheduleAt(signsTime, StartSigns);
                Clock.ScheduleAt(broadcastTime, StartBroadcast);
            }

            // 00:00 기상: 알람이 울린다
            if (alarmSource != null && alarmSource.clip != null) alarmSource.Play();
            Add(objMorning);
            Add(objAlarm);
        }

        protected override void OnExit()
        {
            if (alarmSource != null) alarmSource.Stop();
            if (sparkAudio != null) sparkAudio.Stop();
            if (sparkParticles != null) sparkParticles.Stop();
        }

        private void Update()
        {
            // 냄새 묘사를 주기적으로 반복 (농도가 짙을수록 강한 문구)
            if (!signsStarted || cleared || GameFlow.GameplayBlocked) return;
            scentTimer -= Time.deltaTime;
            if (scentTimer > 0f) return;
            scentTimer = smellRepeatSeconds;
            if (gas != null && gas.GasLevel > smellStrongGas) EventBus.ShowScent(smellStrongText);
            else if (gas != null && gas.GasLevel > 0.02f) EventBus.ShowScent(smellText);
        }

        /// <summary>01:30 이상 징후: 가스 냄새, 누출음, 멀티탭 스파크</summary>
        private void StartSigns()
        {
            if (signsStarted) return;
            signsStarted = true;
            sparksActive = plugged;
            GoToStep("signs");

            if (gas != null) gas.StartLeak();
            if (sparksActive)
            {
                if (sparkParticles != null) sparkParticles.Play();
                if (sparkAudio != null && sparkAudio.clip != null) sparkAudio.Play();
            }

            scentTimer = smellRepeatSeconds;
            EventBus.ShowScent(smellText);
            Add(objSmell);
        }

        /// <summary>02:30 긴급 방송: 자막 + 오디오, 목표 갱신</summary>
        private void StartBroadcast()
        {
            if (broadcastDone) return;
            if (!signsStarted) StartSigns();
            broadcastDone = true;
            GoToStep("broadcast");

            if (announcementSource != null && announcementSource.clip != null) announcementSource.Play();
            if (announcementCues != null)
                foreach (SubtitleCue cue in announcementCues) EventBus.ShowSubtitle(cue.text, cue.seconds);

            // 목표 갱신: 냄새 원인 찾기 → 가스 누출 대응
            Complete(objSmell);
            foreach (ObjectiveData o in new[] { objAlarm, objWash, objDrink, objPhone, objMorning }) Remove(o);
            Add(objGasResponse);
            Add(objFlashlight);
            Add(objFuse);
            Add(objMainValve);
            Add(objVentilate);
            Add(objMultitap);

            GoToStep("response");
            Evaluate();
        }

        // ═════════════════════════ 이벤트 ═════════════════════════
        protected override void OnScenarioEvent(ScenarioEvent e)
        {
            if (cleared || GameFlow.IsEnded) return;

            // 1) 사망 판정이 가장 먼저
            if (CheckDeath(e)) return;

            // 2) 일상 / 생존 행동
            switch (e.Type)
            {
                case ScenarioEventType.Interacted:
                    if (e.SourceId == alarmId) OnAlarmOff();
                    else if (e.SourceId == washId) washDone = true;
                    else if (e.SourceId == drinkId) drinkDone = true;
                    else if (e.SourceId == multitapPlugId) OnPlugRemoved();
                    else if (clutterIds != null && Array.IndexOf(clutterIds, e.SourceId) >= 0) clutterRemoved++;
                    break;

                case ScenarioEventType.PhoneAction:
                    if (e.SourceId == phoneOpenActionId) phoneDone = true;
                    else if (e.SourceId == flashlightActionId && e.State && !flashlightLit)
                    {
                        flashlightLit = true;
                        Record(recFlashlightTime, Now);
                    }
                    break;

                case ScenarioEventType.ValveChanged:
                    OnValveChanged(e);
                    break;
            }

            Evaluate();
        }

        private bool CheckDeath(ScenarioEvent e)
        {
            bool operated = e.Type == ScenarioEventType.Toggled || e.Type == ScenarioEventType.Interacted;
            if (!operated) return false;

            // 스파크가 나는 멀티탭의 전원 스위치 → 점화
            if (e.SourceId == multitapSwitchId && sparksActive && plugged)
            {
                RecordDanger();
                Die(multitapDeath);
                return true;
            }

            // 전기/화기 조작
            if (dangerIds != null && Array.IndexOf(dangerIds, e.SourceId) >= 0)
            {
                RecordDanger();
                if (gas != null && gas.IsExplosive)
                {
                    Die(ignitionDeath);
                    return true;
                }
            }
            return false;
        }

        private void RecordDanger()
        {
            if (Score == null) return;
            Score.Increment(recDangerCount);
            if (broadcastDone) Score.Increment(recDangerAfterBroadcast);
        }

        private void Die(DeathReason reason)
        {
            Record(recDeathAt, Now);
            if (DeathAndEnding.Instance != null) DeathAndEnding.Instance.Die(reason);
            else EventBus.RaisePlayerDied(reason);
        }

        private void OnAlarmOff()
        {
            if (alarmOff) return;
            alarmOff = true;
            if (alarmSource != null) alarmSource.Stop();
            GoToStep("tutorial");
            Complete(objAlarm);
            Add(objWash);
            Add(objDrink);
            Add(objPhone);
        }

        private void OnPlugRemoved()
        {
            plugged = false;
            sparksActive = false;
            if (sparkParticles != null) sparkParticles.Stop();
            if (sparkAudio != null) sparkAudio.Stop();
        }

        private void OnValveChanged(ScenarioEvent e)
        {
            bool closed = !e.State; // State = 열림
            if (!closed) return;

            valveCloseCount++;
            if (fuseCock != null && e.SourceId == fuseCock.Id && !fuseRecorded)
            {
                fuseRecorded = true;
                Record(recFuseOrder, valveCloseCount);
                Record(recFuseTime, Now);
            }
            else if (mainValve != null && e.SourceId == mainValve.Id && !mainRecorded)
            {
                mainRecorded = true;
                Record(recMainOrder, valveCloseCount);
                Record(recMainTime, Now);
            }
        }

        // ═════════════════════════ 목표 평가 / 클리어 ═════════════════════════
        private bool FuseClosed => fuseCock != null && !fuseCock.IsOpen;
        private bool MainClosed => mainValve != null && !mainValve.IsOpen;
        private bool AllWindowsOpen
        {
            get
            {
                if (windows == null || windows.Length == 0) return false;
                foreach (ToggleInteractable w in windows) if (w == null || !w.IsOn) return false;
                return true;
            }
        }
        private bool MultitapHandled => !plugged && clutterRemoved >= (clutterIds != null ? clutterIds.Length : 0);

        private void Evaluate()
        {
            // 아침 준비 (튜토리얼)
            if (washDone) Complete(objWash);
            if (drinkDone) Complete(objDrink);
            if (phoneDone) Complete(objPhone);
            if (!morningDone && alarmOff && washDone && drinkDone && phoneDone)
            {
                morningDone = true;
                Complete(objMorning);
                Record(recMorningTime, Now);
            }

            if (!broadcastDone) return;

            // 생존 행동 목표
            if (flashlightLit) Complete(objFlashlight);
            if (FuseClosed) Complete(objFuse);
            if (MainClosed) Complete(objMainValve);
            if (AllWindowsOpen)
            {
                Complete(objVentilate);
                if (!ventRecorded) { ventRecorded = true; Record(recVentTime, Now); }
            }
            if (MultitapHandled)
            {
                Complete(objMultitap);
                if (!multitapRecorded)
                {
                    multitapRecorded = true;
                    Record(recMultitapOk, 1f);
                    Record(recMultitapTime, Now);
                }
            }

            if (flashlightLit && FuseClosed && MainClosed && AllWindowsOpen && MultitapHandled) ClearAct();
        }

        private void ClearAct()
        {
            if (cleared) return;
            cleared = true;
            Complete(objGasResponse);
            Record(recClearTime, Now);
            Record(recGasAtClear, gas != null ? gas.GasLevel : 0f);
            EventBus.ShowMessage(clearMessage);   // 임시 "Act 1 클리어" 알림
            StartCoroutine(GoNext());
        }

        private IEnumerator GoNext()
        {
            yield return new WaitForSecondsRealtime(nextActDelay);
            CompleteAct();   // → ScenarioStateMachine.NextAct() (Act 2 로)
        }

        // ═════════════════════════ 헬퍼 ═════════════════════════
        private void Add(ObjectiveData o) { if (o != null && Objectives != null) Objectives.Add(o); }
        private void Complete(ObjectiveData o) { if (o != null && Objectives != null) Objectives.Complete(o.Id); }
        private void Remove(ObjectiveData o) { if (o != null && Objectives != null) Objectives.Remove(o.Id); }
        private void Record(string key, float value) { if (Score != null && !string.IsNullOrEmpty(key)) Score.Record(key, value); }
    }
}
