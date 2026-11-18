using System.Collections;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 2 — 8층의 폭발과 연쇄 화재 (03:00 ~ 07:00).
    ///
    /// 타임라인 (Act 1 이 늦게 끝나면 explosionGameTime 대신 "Act 시작 + minDelay" 를 기준으로 상대 진행):
    ///   03:30 아래층 대형 폭발 → 카메라 흔들림 / 섬광 / 베란다 유리 파손 / 전체 정전(비상구 유도등만 점등)
    ///   04:00 (+30s) 거실 커튼과 주방 식용유 팬에 불이 옮겨붙음 → 연기가 차오름 (FireManager)
    ///   05:30 (+120s) 복도에도 불길이 번짐 → 현관문 손잡이가 뜨거워진다
    ///   07:00 (+210s) 시간 초과 시 현재 상태로 Act 2 종료
    ///
    /// 분기: 2-A 소화기로 불을 모두 진압 (이후 연기 거의 없음) / 2-B 수건을 적셔 입막음 후 방어 (불은 계속 번져 연기 짙음)
    /// 탈출 직전 체크: 현관문 손잡이 온도 확인 → 뜨거우면 현관 탈출 포기(Act 3 농성 루트 플래그), 확인 없이 열면 사망.
    /// 사망: 기름불/멀티탭 화재에 물 붓기(FireSource), 열기 접근(FireManager), 뜨거운 문 개방, 질식(PlayerVitals).
    /// 결과는 ScenarioFlags 와 ScoreSystem.Records 에 남긴다 (Act 3 / Act 4 가 사용).
    /// </summary>
    public class Act2_BlastAndSmoke : ActBase
    {
        // ───────── 타임라인 ─────────
        [Header("타임라인 (게임 시간, 초)")]
        [SerializeField, Min(0f)] private float explosionGameTime = 210f;     // 03:30
        [SerializeField, Min(0f), Tooltip("Act 1 이 03:30 이후에 끝난 경우, 시작 후 이만큼 뒤에 폭발")] private float minDelayAfterAct1 = 6f;
        [SerializeField, Min(0f)] private float fireDelayAfterExplosion = 30f;     // 04:00
        [SerializeField, Min(0f)] private float corridorFireAfterExplosion = 120f; // 05:30
        [SerializeField, Min(0f)] private float endAfterExplosion = 210f;          // 07:00
        [SerializeField, Min(0f)] private float nextActDelay = 3f;

        // ───────── 참조 ─────────
        [Header("참조")]
        [SerializeField] private PlayerController player;
        [SerializeField] private GasLeakController gas;
        [SerializeField] private FireManager fireManager;
        [SerializeField] private FireSource[] initialFires;
        [SerializeField] private FireSource corridorFire;
        [SerializeField] private ToggleInteractable frontDoor;

        [Header("폭발 연출")]
        [SerializeField] private GameObject[] glassToShatter;
        [SerializeField] private ToggleInteractable[] brokenOpenings;
        [SerializeField] private ParticleSystem glassShards;
        [SerializeField] private AudioSource explosionAudio;
        [SerializeField] private string exitLightGroupId = "exit";
        [SerializeField, Range(0f, 0.3f)] private float blackoutAmbient = 0.03f;
        [SerializeField] private float shakeAmplitude = 0.14f;
        [SerializeField] private float shakeSeconds = 3f;

        // ───────── id ─────────
        [Header("id")]
        [SerializeField] private string handleCheckId;
        [SerializeField] private string frontDoorId;
        [SerializeField] private string extinguisherItemId = "extinguisher";
        [SerializeField] private string wetTowelItemId = "wet_towel";
        [SerializeField] private string spareTowelItemId = "towel";
        [SerializeField] private string waterBottleItemId = "water_bottle";
        [SerializeField] private string pinPulledEventId = "extinguisher_pin_pulled";
        [SerializeField] private string fireAllOutEventId = "fire_all_out";

        // ───────── 목표 ─────────
        [Header("목표")]
        [SerializeField] private ObjectiveData objMain;
        [SerializeField] private ObjectiveData objExtinguisher;
        [SerializeField] private ObjectiveData objSuppress;
        [SerializeField] private ObjectiveData objTowel;
        [SerializeField] private ObjectiveData objBottle;
        [SerializeField] private ObjectiveData objHandle;

        // ───────── 문구 ─────────
        [Header("문구")]
        [SerializeField, TextArea(1, 2)] private string explosionText;
        [SerializeField, TextArea(1, 2)] private string fireText;
        [SerializeField, TextArea(1, 2)] private string smokeText;
        [SerializeField, TextArea(1, 2)] private string allOutText;
        [SerializeField, TextArea(1, 2)] private string corridorFireText;
        [SerializeField, TextArea(1, 2)] private string handleHotText;
        [SerializeField, TextArea(1, 2)] private string handleCoolText;
        [SerializeField, TextArea(1, 2)] private string hotDoorLockedMessage;
        [SerializeField] private string clearMessage = "Act 2 클리어";

        // ───────── 연기 결과 ─────────
        [Header("결과가 연기에 미치는 영향")]
        [SerializeField, Range(0f, 1f), Tooltip("2-A(전부 진압) 후 남는 연기 농도")] private float smokeFloorRouteA = 0.04f;
        [SerializeField, Range(0f, 1f), Tooltip("2-B(진압 포기) 상태에서 최소 연기 농도 — 불이 계속 번져 연기가 짙다")] private float smokeFloorRouteB = 0.25f;

        // ───────── 사망 ─────────
        [Header("사망 원인")]
        [SerializeField] private DeathReason doorBackdraftDeath = DeathReason.DoorBackdraft;

        // ───────── 플래그 / 기록 키 ─────────
        [Header("플래그 키 (Act 3 가 읽는다)")]
        [SerializeField] private string flagRoute = "act2_route";                       // 텍스트 "A" / "B"
        [SerializeField] private string flagExtinguished = "act2_fires_extinguished";
        [SerializeField] private string flagWetTowel = "act2_wet_towel";
        [SerializeField] private string flagSpareTowel = "act2_spare_towel";
        [SerializeField] private string flagBottle = "act2_water_bottle";
        [SerializeField] private string flagCorridorHot = "act2_corridor_hot";
        [SerializeField] private string flagFrontBlocked = "act3_front_door_blocked";   // true → 현관 포기, 3-B 농성 루트
        [SerializeField] private string flagHandleChecked = "act2_handle_checked";
        [SerializeField] private string flagTimeout = "act2_timeout";
        [SerializeField] private string flagSmoke = "act2_smoke_level_at_end";

        [Header("점수 기록 키")]
        [SerializeField] private string recExplosionAt = "act2_explosion_time";
        [SerializeField] private string recFireAt = "act2_fire_start_time";
        [SerializeField] private string recPinAt = "act2_pin_pulled_time";
        [SerializeField] private string recAllOutAt = "act2_fires_out_time";
        [SerializeField] private string recTowelAt = "act2_wet_towel_time";
        [SerializeField] private string recHandleAt = "act2_handle_check_time";
        [SerializeField] private string recClearAt = "act2_clear_time";

        // ───────── 상태 ─────────
        private float explosionAt;
        private bool exploded, firesStarted, allOut, wetTowelOn, handleChecked, handleHot, cleared;
        private bool pinRecorded;

        private GameClock Clock => GameClock.Instance;
        private ObjectiveSystem Objectives => ObjectiveSystem.Instance;
        private ScoreSystem Score => ScoreSystem.Instance;
        private float Now => Clock != null ? Clock.GameSeconds : 0f;

        // ═════════════════════════ 진행 ═════════════════════════
        protected override void OnEnter()
        {
            if (Objectives != null) Objectives.RemoveFinished();   // Act 1 의 끝난 목표 정리
            if (gas != null) gas.StopLeak();

            explosionAt = Mathf.Max(explosionGameTime, Now + minDelayAfterAct1);
            if (Clock != null)
            {
                Clock.ScheduleAt(explosionAt, Explode);
                Clock.ScheduleAt(explosionAt + fireDelayAfterExplosion, StartFires);
                Clock.ScheduleAt(explosionAt + corridorFireAfterExplosion, IgniteCorridor);
                Clock.ScheduleAt(explosionAt + endAfterExplosion, OnTimeout);
            }
            GoToStep("wait");
        }

        /// <summary>폭발: 흔들림 + 섬광 + 유리 파손 + 전체 정전</summary>
        private void Explode()
        {
            if (exploded) return;
            exploded = true;
            GoToStep("blast");

            if (explosionAudio != null && explosionAudio.clip != null) explosionAudio.Play();
            if (player != null) player.Shake(shakeAmplitude, shakeSeconds);
            if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(new Color(1f, 0.85f, 0.6f), 0.9f, 1.6f);

            // 베란다 유리 파손
            if (glassToShatter != null)
                foreach (GameObject g in glassToShatter)
                {
                    if (g == null) continue;
                    foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) r.enabled = false;
                    foreach (Collider c in g.GetComponentsInChildren<Collider>()) c.enabled = false;
                }
            if (brokenOpenings != null)
                foreach (ToggleInteractable t in brokenOpenings) if (t != null) t.Interactable = false;
            if (glassShards != null) glassShards.Play();

            // 전체 정전: 모든 조명 소등 → 비상구 유도등만 점등
            LightGroup.SetAllOn(false);
            LightGroup exit = LightGroup.Find(exitLightGroupId);
            if (exit != null) exit.SetOn(true);
            RenderSettings.ambientLight = new Color(blackoutAmbient, blackoutAmbient, blackoutAmbient * 1.1f);

            if (HazardManager.Instance != null) HazardManager.Instance.SetGas(0f);
            EventBus.ShowMessage(explosionText);
            Record(recExplosionAt, Now);
        }

        /// <summary>화염과 파편이 커튼/식용유 팬으로 옮겨붙는다</summary>
        private void StartFires()
        {
            if (firesStarted) return;
            if (!exploded) Explode();
            firesStarted = true;
            GoToStep("fire");

            if (initialFires != null) foreach (FireSource f in initialFires) if (f != null) f.Ignite();

            Add(objMain); Add(objExtinguisher); Add(objSuppress); Add(objTowel); Add(objBottle); Add(objHandle);
            EventBus.ShowMessage(fireText);
            EventBus.ShowScent(smokeText);
            Record(recFireAt, Now);
            Evaluate();
        }

        private void IgniteCorridor()
        {
            if (corridorFire == null || corridorFire.IsLit) return;
            corridorFire.Ignite();
            ScenarioFlags.Set(flagCorridorHot, true);
            if (exploded) EventBus.ShowScent(corridorFireText);
        }

        private void OnTimeout()
        {
            if (cleared) return;
            ScenarioFlags.Set(flagTimeout, true);
            if (!handleChecked) ResolveDoor(announce: false);
            ClearAct();
        }

        // ═════════════════════════ 이벤트 ═════════════════════════
        protected override void OnScenarioEvent(ScenarioEvent e)
        {
            if (cleared || GameFlow.IsEnded) return;

            switch (e.Type)
            {
                case ScenarioEventType.ItemEquipped:
                    if (e.SourceId == wetTowelItemId)
                    {
                        wetTowelOn = e.State;
                        if (e.State) { Complete(objTowel); Record(recTowelAt, Now); }
                    }
                    break;

                case ScenarioEventType.Custom:
                    if (e.SourceId == pinPulledEventId && !pinRecorded) { pinRecorded = true; Complete(objExtinguisher); Record(recPinAt, Now); }
                    else if (e.SourceId == fireAllOutEventId && firesStarted && !allOut) OnAllOut();
                    break;

                case ScenarioEventType.Interacted:
                    if (e.SourceId == handleCheckId) ResolveDoor(announce: true);
                    break;

                case ScenarioEventType.Toggled:
                    // 뜨거운(= 복도에 불길이 있는) 현관문을 확인 없이 열면 불길이 밀려든다
                    if (e.SourceId == frontDoorId && e.State && corridorFire != null && corridorFire.IsLit) Die(doorBackdraftDeath);
                    break;
            }
            Evaluate();
        }

        private void OnAllOut()
        {
            allOut = true;
            Complete(objSuppress);
            Complete(objExtinguisher);
            if (fireManager != null) fireManager.SmokeFloor = smokeFloorRouteA;
            EventBus.ShowMessage(allOutText);
            Record(recAllOutAt, Now);
        }

        /// <summary>현관문 손잡이 온도 확인 (손등 대기)</summary>
        private void ResolveDoor(bool announce)
        {
            handleChecked = true;
            handleHot = corridorFire != null && corridorFire.IsLit;
            Complete(objHandle);
            Record(recHandleAt, Now);
            ScenarioFlags.Set(flagHandleChecked, true);

            if (handleHot)
            {
                if (frontDoor != null)
                {
                    frontDoor.SetLocked(true);
                    frontDoor.SetLockedMessage(hotDoorLockedMessage);
                }
                if (announce) EventBus.ShowMessage(handleHotText);
            }
            else if (announce) EventBus.ShowMessage(handleCoolText);
        }

        // ═════════════════════════ 평가 / 클리어 ═════════════════════════
        private void Evaluate()
        {
            InventorySystem inv = InventorySystem.Instance;
            if (inv != null && inv.Has(waterBottleItemId)) Complete(objBottle);

            if (!firesStarted) return;

            // 진압 포기(2-B) 상태에서는 불이 계속 번져 연기가 짙게 남는다
            if (fireManager != null && !allOut && fireManager.SmokeFloor < smokeFloorRouteB) fireManager.SmokeFloor = smokeFloorRouteB;

            bool prepared = allOut || wetTowelOn;
            if (handleChecked && prepared) ClearAct();
        }

        private void ClearAct()
        {
            if (cleared) return;
            cleared = true;

            InventorySystem inv = InventorySystem.Instance;
            bool routeA = allOut;
            ScenarioFlags.SetText(flagRoute, routeA ? "A" : "B");
            ScenarioFlags.Set(flagExtinguished, routeA);
            ScenarioFlags.Set(flagWetTowel, wetTowelOn);
            ScenarioFlags.Set(flagSpareTowel, inv != null && inv.Has(spareTowelItemId));
            ScenarioFlags.Set(flagBottle, inv != null && inv.Has(waterBottleItemId));
            ScenarioFlags.Set(flagFrontBlocked, handleHot);   // 뜨거움 → Act 3 대신 루트 B/C(농성) 전환
            ScenarioFlags.SetNumber(flagSmoke, HazardManager.Instance != null ? HazardManager.Instance.PlayerSmoke : 0f);

            Complete(objMain);
            Record(recClearAt, Now);
            EventBus.ShowMessage(clearMessage);
            StartCoroutine(GoNext());
        }

        private IEnumerator GoNext()
        {
            yield return new WaitForSecondsRealtime(nextActDelay);
            CompleteAct();   // → Act 3 (flagFrontBlocked 로 분기)
        }

        // ═════════════════════════ 헬퍼 ═════════════════════════
        private void Die(DeathReason reason)
        {
            if (DeathAndEnding.Instance != null) DeathAndEnding.Instance.Die(reason);
            else EventBus.RaisePlayerDied(reason);
        }

        private void Add(ObjectiveData o) { if (o != null && Objectives != null) Objectives.Add(o); }
        private void Complete(ObjectiveData o) { if (o != null && Objectives != null) Objectives.Complete(o.Id); }
        private void Record(string key, float value) { if (Score != null && !string.IsNullOrEmpty(key)) Score.Record(key, value); }
    }
}
