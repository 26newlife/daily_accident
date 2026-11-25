using System.Collections;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 3 — 번지는 화재: 복도·이웃·비상계단 판단 (약 3:10 ~ 4:40, 약 90초).
    ///
    /// ① 손잡이가 미지근했던 경우(복도 대피 가능)
    ///    복도로 나가 현관문을 닫는다(열어 두면 복도 연기 가속) → 엘리베이터 앞의 1003호 할머니(기침, 호출 버튼 반복)와
    ///    수건 없이 계단으로 뛰어가려는 1002호 청년을 돕는다(설득 / 젖은 수건 / 동행) → 비상계단 방화문을 열어 아래층 상황을 확인한다.
    ///    · 방화문을 열었을 때 폭발 후 stairSmokeArrivalSeconds 이전 + 현관문을 닫아 둔 상태면 "초기 방화문 관리 성공"(루트 D 가능),
    ///      이후면 검은 연기·열기가 올라오고 하강 불가(stairwellDownBlocked) → 루트 A(옥상) 또는 C.
    /// ② 손잡이가 뜨거웠던 경우(act3_front_door_blocked)
    ///    복도로 나갈 수 없다(열면 백드래프트 사망). 짧은 "상황 판단" 구간 후 Act 4 로 — 루트 B(경량칸막이) 또는 C(대피공간).
    ///
    /// 모든 시간 값은 GameTimingConfig. 결과는 EvacKeys 의 ScenarioFlags / ScoreSystem.Records 로 남긴다.
    /// </summary>
    public class Act3_FireSpreads : EvacActBase
    {
        [Header("참조")]
        [SerializeField] private PlayerController player;
        [SerializeField] private ToggleInteractable frontDoor;
        [SerializeField] private ToggleInteractable stairDoor;
        [SerializeField] private GrandmotherNpc grandmother;
        [SerializeField] private YoungManNpc youngMan;
        [SerializeField] private HazardZone corridorZone;
        [SerializeField] private HazardZone stairZone;

        [Header("연출 (오디오 클립/파티클은 비워도 동작)")]
        [SerializeField, Tooltip("계단 방화문을 열었을 때 올라오는 검은 연기")] private ParticleSystem stairSmokeFx;
        [SerializeField] private AudioSource stairRumbleAudio;
        [SerializeField, Tooltip("복도 문구멍(DoorPeephole)에서 보이는 NPC 실루엣 플래그 (PeepholeCameraController.corridorNpcs.flagKey)")]
        private string[] peepholeNpcFlags = { "act3_npc_corridor_1003", "act3_npc_corridor_1002" };

        [Header("문구")]
        [SerializeField, TextArea(1, 2)] private string corridorEnterText = "복도는 어둡고 매캐하다. 엘리베이터 쪽에서 기침 소리와 버튼 누르는 소리가 들린다.";
        [SerializeField, TextArea(1, 2)] private string frontClosedText = "현관문을 닫았다. 집 안의 연기가 복도로 새어 나오지 않는다.";
        [SerializeField, TextArea(1, 2)] private string stairClearText = "방화문을 열자 계단은 아직 맑다. 연기가 올라오기 전에 내려갈 수 있다!";
        [SerializeField, TextArea(1, 2)] private string stairSmokeText = "방화문을 열자 검은 연기와 뜨거운 열기가 계단을 타고 올라온다! 아래로는 내려갈 수 없다.";
        [SerializeField, TextArea(1, 2)] private string stairNpcOpenedText = "누군가 계단 방화문을 열어 둔 채 내려갔다. 열린 방화문으로 연기가 새어 나온다.";
        [SerializeField, TextArea(1, 2)] private string blockedText = "현관문이 뜨겁다. 복도로는 나갈 수 없다. 이웃집 비명과 소방 사이렌이 들린다. 집 안에서 버틸 방법을 찾자.";

        // ───────── 상태 ─────────
        private bool blocked, inCorridor, stairChecked, cleared, npcOpenedStairDoor;
        private float actStart, corridorSmoke;

        protected override void OnEnter()
        {
            actStart = Now;
            if (Objectives != null) Objectives.RemoveFinished();
            ResolveRefs();
            if (stairZone != null) stairZone.Isolated = true;   // 계단실은 집 전체 연기와 별개
            blocked = ScenarioFlags.GetBool(EvacKeys.FlagHandleHot);
            ScenarioFlags.Set(EvacKeys.FlagInCorridor, false);
            ScenarioFlags.Set(EvacKeys.FlagStairDownBlocked, false);
            ScenarioFlags.Set(EvacKeys.FlagEarlyFireDoor, false);
            if (Score != null && !Score.HasRecord(EvacKeys.RecExplosionTime)) Score.Record(EvacKeys.RecExplosionTime, Now);   // 디버그 점프 대비

            Add("act3_main");
            PhoneSms.Send("아들(할머니 가족)", "1003호에 어머니 계신데 전화를 안 받으세요. 엘리베이터 말고 계단으로 모시고 가주세요. 부탁드립니다.");
            if (blocked)
            {
                GoToStep("blocked");
                Add("act3_assess");
                EventBus.ShowScent(blockedText);
                EventBus.RaiseEmergencyVehicle(EmergencyKind.FireTruck);   // 대사의 소방 사이렌: 실제로 소방차가 도로를 지나간다
                SetPeepholeFlags(true);   // 문구멍으로 복도 이웃이 보인다
                if (Clock != null) Clock.ScheduleAt(Now + T.act3BlockedSeconds, FinishBlocked, "act3 blocked end");
            }
            else
            {
                GoToStep("corridor");
                Add("act3_corridor");
                Add("act3_neighbors");
                Add("act3_stairs");
                if (Clock != null) Clock.ScheduleAt(Now + T.act3HardTimeoutSeconds, OnTimeout, "act3 hard timeout");
            }
        }

        protected override void OnExit()
        {
            if (stairSmokeFx != null) stairSmokeFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (stairRumbleAudio != null) stairRumbleAudio.Stop();
        }

        private void ResolveRefs()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (frontDoor == null) frontDoor = EvacDirector.FindById<ToggleInteractable>(EvacKeys.FrontDoor);
            if (stairDoor == null) stairDoor = EvacDirector.FindById<ToggleInteractable>(EvacKeys.StairFireDoor);
            if (grandmother == null) grandmother = GrandmotherNpc.Instance;
            if (youngMan == null) youngMan = YoungManNpc.Instance;
            if (corridorZone == null) corridorZone = EvacDirector.FindZone(EvacKeys.ZoneCorridor);
            if (stairZone == null) stairZone = EvacDirector.FindZone(EvacKeys.ZoneStairwell);
        }

        private void SetPeepholeFlags(bool on)
        {
            if (peepholeNpcFlags != null) foreach (string f in peepholeNpcFlags) if (!string.IsNullOrEmpty(f)) ScenarioFlags.Set(f, on);
        }

        // ═════════════════════ 복도 / 계단실 연기 ═════════════════════
        private void Update()
        {
            if (!IsActive || GameFlow.GameplayBlocked) return;

            float stair = StairSmoke(actStart);
            if (stairZone != null) stairZone.SetSmoke(stair);

            // 복도 연기: 불이 붙은 복도(FireManager 가 구동)가 아니면 시간에 따라 차오른다. 현관문/계단 방화문이 열려 있으면 가속
            FireSource cf = EvacHazards.CorridorFire();
            if (corridorZone != null && (cf == null || !cf.IsLit))
            {
                float t = Now - actStart - T.corridorSmokeStartDelay;
                float rise01 = Mathf.Clamp01(t / Mathf.Max(1f, T.corridorSmokeRiseSeconds));
                float mult = frontDoor != null && frontDoor.IsOn ? T.corridorOpenDoorSmokeMultiplier : 1f;
                float target = Mathf.Clamp01(T.corridorSmokeMax * rise01 * mult + (stairDoor != null && stairDoor.IsOn ? stair * 0.5f : 0f));
                corridorSmoke = Mathf.MoveTowards(corridorSmoke, target, 0.05f * Time.deltaTime);
                corridorZone.SetSmoke(corridorSmoke);
            }

            // 열린 방화문 + 계단 연기 → 검은 연기 파티클
            if (stairSmokeFx != null)
            {
                bool emit = stairDoor != null && stairDoor.IsOn && stair > 0.05f;
                var em = stairSmokeFx.emission;
                em.rateOverTime = emit ? 20f + 60f * stair : 0f;
                if (emit && !stairSmokeFx.isPlaying) stairSmokeFx.Play();
            }
        }

        // ═════════════════════ 이벤트 ═════════════════════
        protected override void OnScenarioEvent(ScenarioEvent e)
        {
            if (cleared || GameFlow.IsEnded) return;
            if (EvacHazards.CheckHotFrontDoor(e)) return;   // 뜨거운 현관문을 열면 백드래프트 (막지 않는다)

            if (e.Type == ScenarioEventType.ZoneEntered && e.SourceId == EvacKeys.ZoneCorridor && !blocked) OnEnterCorridor();
            // 이미 열려 있는 방화문(청년이 열어 둠) 쪽 계단실로 들어가면 아래층 상태를 본 것으로 판정한다
            if (e.Type == ScenarioEventType.ZoneEntered && e.SourceId == EvacKeys.ZoneStairwell && !blocked && !stairChecked && stairDoor != null && stairDoor.IsOn) OnStairDoorOpened();

            if (e.Type == ScenarioEventType.Toggled)
            {
                if (e.SourceId == EvacKeys.FrontDoor && !e.State && PlayerIn(EvacKeys.ZoneCorridor) && !blocked) OnFrontDoorClosedFromCorridor();
                else if (e.SourceId == EvacKeys.StairFireDoor && e.State) OnStairDoorOpened();
            }
        }

        private void OnEnterCorridor()
        {
            if (inCorridor) return;
            inCorridor = true;
            ScenarioFlags.Set(EvacKeys.FlagInCorridor, true);
            Record("act3_corridor_enter_time", Now);
            SetPeepholeFlags(true);
            EventBus.ShowScent(corridorEnterText);
            Done("act3_corridor");
            PhoneSms.Send("소방청", "고층 화재: 아래층 연기가 심하면 내려가지 말고 방화문을 닫고 옥상 또는 대피공간으로 이동하세요.");
            if (grandmother != null) grandmother.BeginAtElevator();
            if (youngMan != null) youngMan.BeginWaiting();
            GoToStep("neighbors");
        }

        private void OnFrontDoorClosedFromCorridor()
        {
            if (ScenarioFlags.GetBool(EvacKeys.FlagFrontDoorClosed)) return;
            ScenarioFlags.Set(EvacKeys.FlagFrontDoorClosed, true);
            Record("act3_front_door_closed_time", Now);
            EventBus.ShowMessage(frontClosedText);
        }

        private void OnStairDoorOpened()
        {
            if (stairChecked) return;
            // 청년이 달려가며 열어 둔 경우: 플레이어가 확인한 것이 아니다 (문만 열려 있다)
            bool playerNear = player != null && stairDoor != null && Vector3.Distance(player.transform.position, stairDoor.transform.position) < 3.6f;
            if (!playerNear)
            {
                if (!npcOpenedStairDoor) { npcOpenedStairDoor = true; EventBus.ShowScent(stairNpcOpenedText); }
                return;
            }
            EvaluateStairs(playEffects: true);
            ClearAct();
        }

        /// <summary>방화문을 연 순간(또는 시간 초과 시점)의 계단 상태를 판정해 플래그로 남긴다</summary>
        private void EvaluateStairs(bool playEffects)
        {
            stairChecked = true;
            float smoke = StairSmoke(actStart);
            bool frontClosed = frontDoor == null || !frontDoor.IsOn;
            bool downBlocked = smoke >= T.stairBlockedThreshold;
            bool early = SinceExplosion(actStart) < T.stairSmokeArrivalSeconds && smoke < 0.05f && frontClosed;

            ScenarioFlags.Set(EvacKeys.FlagStairDownBlocked, downBlocked);
            ScenarioFlags.Set(EvacKeys.FlagEarlyFireDoor, early);
            ScenarioFlags.Set(EvacKeys.FlagFireDoorManaged, frontClosed);
            Record(EvacKeys.RecStairSmokeAtOpen, smoke);
            Record("act3_stair_check_time", Now);
            Done("act3_stairs");
            Debug.Log($"[Act3] stair check: smoke={smoke:0.00} downBlocked={downBlocked} early={early} frontClosed={frontClosed}");

            if (!playEffects) return;
            if (downBlocked)
            {
                // 검은 연기 · 열기 비네트 · 소리 (비그래픽)
                if (stairSmokeFx != null) stairSmokeFx.Play();
                if (stairRumbleAudio != null && stairRumbleAudio.clip != null) stairRumbleAudio.Play();
                if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(new Color(1f, 0.45f, 0.12f), 0.4f, 1.4f);
                if (player != null) player.Shake(0.05f, 1.2f);
                EventBus.ShowMessage(stairSmokeText);
            }
            else EventBus.ShowMessage(early ? stairClearText : stairClearText);
        }

        // ═════════════════════ 종료 ═════════════════════
        private void OnTimeout()
        {
            if (cleared || GameFlow.IsEnded) return;
            if (!stairChecked) EvaluateStairs(playEffects: false);
            ClearAct();
        }

        private void FinishBlocked()
        {
            if (cleared || GameFlow.IsEnded) return;
            Done("act3_assess");
            ClearAct();
        }

        private void ClearAct()
        {
            if (cleared) return;
            cleared = true;
            TimingLog.Mark("Act3 clear");
            bool saved = ScenarioFlags.GetBool(EvacKeys.FlagGrandmotherWith) || ScenarioFlags.GetBool(EvacKeys.FlagYoungManSaved);
            ScenarioFlags.Set(EvacKeys.FlagNeighborRescued, saved);
            if (saved) Done("act3_neighbors");
            Done("act3_main");
            Record("act3_clear_time", Now);
            StartCoroutine(GoNext());
        }

        private IEnumerator GoNext()
        {
            yield return new WaitForSeconds(T.act3ClearDelay);   // timeScale 0(일시정지)에서는 멈춘다
            CompleteAct();   // → Act 4
        }
    }
}
