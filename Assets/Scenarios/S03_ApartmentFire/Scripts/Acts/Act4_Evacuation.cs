using System;
using System.Collections;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 4 — 대피 (4:40 ~ 7:00, 루트별 약 2분 20초 이내).
    /// 루트 판정은 RouteResolver 가 "가능한 루트"를 안내하지만 플레이어의 선택은 막지 않는다. 루트는 행동으로 확정된다:
    ///   A  옥상:        계단탑을 걸어 올라간다(10층 위로 오르거나 stairs_up) → 14·17층 열린 방화문 닫기 → 옥상 출입문(20층 위) 열쇠함 유리 깨기 → 옥상 문 → 구조 신호 → 헬기/고가사다리차
    ///   B  경량칸막이:  베란다 짐 치우기 → 칸막이 타격(망치/발차기) → 1003호 할머니 구출 → 1003호 현관 → 계단
    ///   C  대피공간:    방화문 닫기 → 문틈 4면 밀봉 → 창문 환기 → 119 신고 → 구조 대기 → 고가사다리차
    ///   D  1층 하강:    계단탑을 걸어 내려간다(10층 아래로 내려가거나 stairs_down) → 낮은 자세로 벽을 짚고 9~7 / 6~4 / 3~1층 → 1층 출구 → 건물 밖 집결지
    ///   계단탑 좌표는 EvacTower (HouseStairwellBuilder 와 같은 값).
    /// 월드 오브젝트(밀봉 지점, 칸막이, 방화문 등)는 Act 와 무관하게 항상 동작하고, Act 4 는 상태를 읽어 목표·엔딩을 진행한다.
    /// 어떤 루트로도 출발하지 못한 채 act4HardTimeoutSeconds 가 지나면 연기와 열기에 갇힌다(ExitTimeout 사망 + 교육 피드백).
    /// 엔딩/사망은 DeathAndEnding 이 처리하고, 할머니를 동행·구출한 상태면 히든 엔딩으로 바뀐다.
    /// </summary>
    public partial class Act4_Evacuation : EvacActBase
    {
        public enum Route { None = 0, A = 1, B = 2, C = 3, D = 4 }

        [Serializable]
        public struct Spawn
        {
            public string name;
            public Transform point;
        }

        // ───────── 공통 참조 ─────────
        [Header("공통 참조")]
        [SerializeField] private PlayerController player;
        [SerializeField] private GrandmotherNpc grandmother;
        [SerializeField] private YoungManNpc youngMan;
        [SerializeField, Tooltip("루트 B 에서 할머니가 쓰러져 있는 1003호 위치")] private Transform grandmotherFallenPoint;

        [Header("이동 지점")]
        [SerializeField, Tooltip("루트 B: 1003호 현관을 나선 뒤 도착하는 비상계단 입구 (루트 A·D 는 계단탑을 직접 걷는다)")] private Transform stairRoomSpawn;

        [Header("문구")]
        [SerializeField, TextArea(1, 3)] private string chooseText = "이제 결정해야 한다. 연기가 점점 차오른다.";
        [SerializeField, TextArea(1, 2)] private string timeoutText = "망설이는 사이 연기와 열기가 집 안을 완전히 덮었다…";

        // ───────── 상태 ─────────
        private Route route = Route.None;
        private RouteOptions options;
        private float actStart, routeStart;
        private bool cleared;
        private float pollTimer;

        public Route CurrentRoute => route;
        public RouteOptions Options => options;
        public bool IsCleared => cleared;

        // ═════════════════════ 시작 ═════════════════════
        protected override void OnEnter()
        {
            actStart = Now;
            if (Objectives != null) Objectives.RemoveFinished();
            ResolveRefs();
            options = RouteResolver.Evaluate();
            Debug.Log($"[Act4] route options: {options}");
            ScenarioFlags.Set("act4_started", true);

            Add("act4_main");
            Add("act4_choose");
            PhoneSms.Send("119 상황실", "구조대 출동 중입니다. 대피공간이라면 문틈을 막고 119에 정확한 호수(1004호)를 알려주세요. 낮은 자세를 유지하세요.");
            GoToStep("choose");
            EventBus.ShowSubtitle(chooseText, 4f);
            EventBus.ShowScent(RouteResolver.Describe(options));

            // 루트 B 의 전제: 현관으로 못 나가는 판이라면 할머니는 1003호 안에서 쓰러져 있다
            if (ScenarioFlags.GetBool(EvacKeys.FlagHandleHot) && grandmother != null && grandmotherFallenPoint != null)
                grandmother.BeginFallen(grandmotherFallenPoint.position, grandmotherFallenPoint.eulerAngles.y);

            if (Clock != null) Clock.ScheduleAt(actStart + T.act4HardTimeoutSeconds, OnHardTimeout, "act4 hard timeout");
            AdoptExistingProgress();
        }

        protected override void OnExit()
        {
            if (player != null) player.ScenarioSpeedMultiplier = 1f;
        }

        private void OnDestroy()
        {
            if (player != null) player.ScenarioSpeedMultiplier = 1f;
        }

        private void ResolveRefs()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (grandmother == null) grandmother = GrandmotherNpc.Instance;
            if (youngMan == null) youngMan = YoungManNpc.Instance;
            ResolveRouteRefs();
        }

        /// <summary>Act 4 가 시작되기 전에 이미 한 행동(대피공간 문 닫기, 칸막이 타격 등)을 루트 확정으로 이어 받는다</summary>
        private void AdoptExistingProgress()
        {
            if (partition != null && partition.Hits > 0) CommitRoute(Route.B);
            else if (shelterSeal != null && shelterSeal.DoorClosed && PlayerIn(EvacKeys.ZoneShelter)) CommitRoute(Route.C);
        }

        // ═════════════════════ 루트 확정 ═════════════════════
        private void CommitRoute(Route r)
        {
            if (route != Route.None || r == Route.None || cleared) return;
            route = r;
            routeStart = Now;
            bool safe = options.IsAvailable(r.ToString()[0]);
            ScenarioFlags.SetText(EvacKeys.FlagRoute, r.ToString());
            ScenarioFlags.Set(EvacKeys.FlagRouteSafe, safe);
            Record(EvacKeys.RecRouteTime, Now);
            Record("run_route", (float)r);
            TimingLog.Mark($"Act4 route {r}");
            Debug.Log($"[Act4] route committed: {r} (safe={safe}, options={options})");
            Done("act4_choose");
            GoToStep("route_" + r);
            if (!safe) EventBus.ShowScent("이 방법은 지금 상황에서 위험할 수 있다. 조심해서 움직이자.");
            BeginRoute(r);
        }

        private void BeginRoute(Route r)
        {
            switch (r)
            {
                case Route.A: BeginRouteA(); break;
                case Route.B: BeginRouteB(); break;
                case Route.C: BeginRouteC(); break;
                case Route.D: BeginRouteD(); break;
            }
        }

        /// <summary>디버그 패널: 루트를 강제로 확정한다</summary>
        public void DebugForceRoute(Route r) { CommitRoute(r); }

        // ═════════════════════ 이벤트 / 폴링 ═════════════════════
        protected override void OnScenarioEvent(ScenarioEvent e)
        {
            if (cleared || GameFlow.IsEnded) return;
            if (EvacHazards.CheckHotFrontDoor(e)) return;

            // 루트 확정 행동
            if (e.Type == ScenarioEventType.Interacted)
            {
                if (e.SourceId == EvacKeys.StairsUp) CommitRoute(Route.A);
                else if (e.SourceId == EvacKeys.StairsDown) CommitRoute(Route.D);
                else if (e.SourceId == EvacKeys.SealBottom || e.SourceId == EvacKeys.SealLeft || e.SourceId == EvacKeys.SealRight || e.SourceId == EvacKeys.SealTop) CommitRoute(Route.C);
            }
            else if (e.Type == ScenarioEventType.Custom && (e.SourceId == "partition_hit" || e.SourceId == "partition_broken")) CommitRoute(Route.B);
            else if (e.Type == ScenarioEventType.Toggled && e.SourceId == EvacKeys.ShelterFireDoor && !e.State && PlayerIn(EvacKeys.ZoneShelter)) CommitRoute(Route.C);

            switch (route)
            {
                case Route.A: HandleRouteA(e); break;
                case Route.B: HandleRouteB(e); break;
                case Route.C: HandleRouteC(e); break;
                case Route.D: HandleRouteD(e); break;
            }
        }

        private void Update()
        {
            if (!IsActive || cleared || GameFlow.GameplayBlocked) return;

            UpdateAlways();   // 1003호 연기 등 루트와 무관한 환경 갱신
            switch (route)
            {
                case Route.A: UpdateRouteA(); break;
                case Route.B: UpdateRouteB(); break;
                case Route.C: UpdateRouteC(); break;
                case Route.D: UpdateRouteD(); break;
            }

            pollTimer -= Time.deltaTime;
            if (pollTimer > 0f) return;
            pollTimer = 0.25f;
            if (route == Route.None)
            {
                // 이벤트를 놓쳤어도 월드 상태로 확정 (대피공간 문을 닫고 들어가 있는 경우 등)
                if (shelterSeal != null && shelterSeal.DoorClosed && PlayerIn(EvacKeys.ZoneShelter)) CommitRoute(Route.C);
                // 계단탑을 직접 걸어 오르내리면 그 방향으로 루트 확정 (위 = A 옥상, 아래 = D 하강)
                else if (player != null)
                {
                    Vector3 p = player.transform.position;
                    bool tower = EvacTower.Contains(p);
                    if (tower ? p.y > 1.6f : p.y > EvacTower.RoofY - 1f) CommitRoute(Route.A);
                    else if (tower ? p.y < -1.4f : p.y < -20f) CommitRoute(Route.D);
                }
            }
        }

        // ═════════════════════ 종료 ═════════════════════
        private void OnHardTimeout()
        {
            if (cleared || GameFlow.IsEnded || route != Route.None) return;
            EventBus.ShowMessage(timeoutText);
            Die(DeathReason.ExitTimeout);
        }

        private void FinishWithEnding(string endingId)
        {
            if (cleared || GameFlow.IsEnded) return;
            cleared = true;
            if (player != null) player.ScenarioSpeedMultiplier = 1f;
            Done("act4_main");
            Record("act4_clear_time", Now);
            if (grandmother != null && grandmother.State == GrandmaState.FollowingPlayer) grandmother.MarkRescued();   // 동행 구출 → 히든 엔딩 조건 유지
            if (youngMan != null && youngMan.State == YoungManState.Following) youngMan.MarkRescued();
            TimingLog.Mark("Act4 ending " + endingId);
            EvacDirector.Ending(endingId);
        }

        private void MarkFireDoorManaged()
        {
            ScenarioFlags.Set(EvacKeys.FlagFireDoorManaged, true);
            if (Score != null) Score.Increment("fire_doors_closed_act4");
        }

        /// <summary>구조 도착 지연(초): 119 신고 결과에 따라 (정확 0 / 오답 +rescueDelayWrong119 / 미신고 +rescueDelayNo119)</summary>
        private float RescueDelay()
        {
            float res = Score != null ? Score.GetRecord(EvacKeys.RecCall119, 0f) : 0f;
            if (res >= 2f) return 0f;
            return res >= 1f ? T.rescueDelayWrong119 : T.rescueDelayNo119;
        }

        // ═════════════════════ 공통 연출 ═════════════════════
        private void Travel(Transform dest, string caption, float seconds, Action onArrived = null)
        {
            if (dest == null || EvacDirector.Instance == null) { onArrived?.Invoke(); return; }
            EvacDirector.Instance.Travel(dest, caption, seconds, onArrived);
        }


        private static void Hide(GameObject g) { if (g != null) g.SetActive(false); }
        private static void Show(GameObject g) { if (g != null) g.SetActive(true); }
    }
}
