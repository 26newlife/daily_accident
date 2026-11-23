using System.Collections;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>Act 4 루트별 로직 (A 옥상 / B 경량칸막이 / C 대피공간 / D 계단 하강). 공통 흐름은 Act4_Evacuation.cs</summary>
    public partial class Act4_Evacuation
    {
        // ═════════════════════ 루트 A: 옥상 ═════════════════════
        [Header("루트 A — 옥상")]
        [SerializeField] private ToggleInteractable fireDoor14;
        [SerializeField] private ToggleInteractable fireDoor17;
        [SerializeField] private HazardZone zoneLanding14;
        [SerializeField] private HazardZone zoneLanding17;
        [SerializeField] private HazardZone zoneLanding20;
        [SerializeField, Tooltip("잠긴 옥상 출입문 오브젝트 (열쇠로 열면 렌더러/콜라이더가 꺼진다)")] private GameObject roofDoorObject;
        [SerializeField, Tooltip("비상 열쇠함의 유리 (깨면 숨김)")] private GameObject keyBoxGlass;
        [SerializeField] private ParticleSystem keyShards;
        [SerializeField] private AudioSource glassBreakAudio;
        [SerializeField] private ItemData roofKeyItem;
        [SerializeField] private GameObject helicopter;
        [SerializeField] private Transform heliStart;
        [SerializeField] private Transform heliEnd;
        [SerializeField] private GameObject roofLadder;
        [SerializeField, Tooltip("구조 신호 후 올라오는 고가사다리 (시작 스케일 → 1)")] private Transform roofLadderPivot;
        [SerializeField] private AudioSource rescueArrivalAudio;

        private float arrive14 = -1f, arrive17 = -1f, arrive20 = -1f;
        private float smoke14, smoke17, smoke20;
        private bool keyTaken, roofOpen, signaled;

        private void ResolveRouteRefs()
        {
            if (fireDoor14 == null) fireDoor14 = EvacDirector.FindById<ToggleInteractable>(EvacKeys.FireDoor14);
            if (fireDoor17 == null) fireDoor17 = EvacDirector.FindById<ToggleInteractable>(EvacKeys.FireDoor17);
            if (zoneLanding14 == null) zoneLanding14 = EvacDirector.FindZone(EvacKeys.ZoneLanding14);
            if (zoneLanding17 == null) zoneLanding17 = EvacDirector.FindZone(EvacKeys.ZoneLanding17);
            if (zoneLanding20 == null) zoneLanding20 = EvacDirector.FindZone(EvacKeys.ZoneLanding20);
            if (partition == null) partition = FindFirstObjectByType<PartitionBreakable>(FindObjectsInactive.Include);
            if (zone1003 == null) zone1003 = EvacDirector.FindZone(EvacKeys.Zone1003);
            if (shelterSeal == null) shelterSeal = ShelterSealSystem.Instance;
            if (outdoor == null) outdoor = OutdoorWorldController.Instance;
            if (descentZones == null || descentZones.Length == 0)
                descentZones = new[] { EvacDirector.FindZone(EvacKeys.ZoneDescent1), EvacDirector.FindZone(EvacKeys.ZoneDescent2), EvacDirector.FindZone(EvacKeys.ZoneDescent3) };

            // 계단참/1003호/하강 구간은 집 전체 연기와 별개의 격리 구역
            foreach (HazardZone z in new[] { zoneLanding14, zoneLanding17, zoneLanding20, zone1003 }) if (z != null) z.Isolated = true;
            foreach (HazardZone z in descentZones) if (z != null) z.Isolated = true;
        }

        private void BeginRouteA()
        {
            Add("act4_a_stairs");
            GoToLanding(14, landing14Spawn);
        }

        private static string FloorCaption(int floor) => floor == 14 ? "11층… 12층… 13층… 14층" : floor == 17 ? "15층… 16층… 17층" : "18층… 19층… 20층";

        private void GoToLanding(int floor, Transform spawn)
        {
            Travel(spawn, "계단을 오른다…  " + FloorCaption(floor), StageTravelSeconds, () => ArriveLanding(floor));
        }

        private void ArriveLanding(int floor)
        {
            if (EvacDirector.Instance != null) EvacDirector.Instance.SetAmbientBright(false);
            if (floor == 14)
            {
                arrive14 = Now;
                Done("act4_a_stairs");
                Add("act4_a_close14");
                EventBus.ShowScent("14층 방화문이 열려 있다. 아래에서 올라온 연기가 이쪽으로 따라온다. 닫아야 한다.");
            }
            else if (floor == 17)
            {
                arrive17 = Now;
                Add("act4_a_close17");
                EventBus.ShowScent("17층 방화문도 열려 있다.");
            }
            else
            {
                arrive20 = Now;
                Add("act4_a_key");
                Add("act4_a_roof");
                EventBus.ShowScent("20층. 옥상 출입문이 굳게 잠겨 있다. 옆에 비상 열쇠함이 보인다.");
            }
        }

        private void HandleRouteA(ScenarioEvent e)
        {
            if (e.Type == ScenarioEventType.Toggled && !e.State)
            {
                if (e.SourceId == EvacKeys.FireDoor14) { Done("act4_a_close14"); MarkFireDoorManaged(); EventBus.ShowMessage("14층 방화문을 닫았다. 연기가 막힌다."); }
                else if (e.SourceId == EvacKeys.FireDoor17) { Done("act4_a_close17"); MarkFireDoorManaged(); EventBus.ShowMessage("17층 방화문을 닫았다."); }
            }
            else if (e.Type == ScenarioEventType.Interacted)
            {
                if (e.SourceId == EvacKeys.StairsUp14) GoToLanding(17, landing17Spawn);
                else if (e.SourceId == EvacKeys.StairsUp17) GoToLanding(20, landing20Spawn);
                else if (e.SourceId == EvacKeys.KeyBox) BreakKeyBox();
                else if (e.SourceId == EvacKeys.RoofDoor) TryOpenRoofDoor();
                else if (e.SourceId == EvacKeys.RoofSignal) StartRooftopRescue();
            }
            else if (e.Type == ScenarioEventType.ZoneEntered)
            {
                if (e.SourceId == "zone_roof" && EvacDirector.Instance != null) EvacDirector.Instance.SetAmbientBright(true);
                else if (e.SourceId == EvacKeys.ZoneLanding20 && EvacDirector.Instance != null) EvacDirector.Instance.SetAmbientBright(false);
            }
        }

        private void UpdateRouteA()
        {
            float dt = Time.deltaTime;
            bool open14 = fireDoor14 != null && fireDoor14.IsOn, open17 = fireDoor17 != null && fireDoor17.IsOn;
            float chase = T.fireDoorChaseSeconds;
            // 열려 있는 방화문 → 일정 시간 뒤 연기가 따라 올라와 계단참이 차오른다 (닫으면 가라앉는다)
            smoke14 = Chase(smoke14, open14 && arrive14 >= 0f && Now - arrive14 > chase, dt);
            smoke17 = Chase(smoke17, open17 && arrive17 >= 0f && Now - arrive17 > chase, dt);
            smoke20 = Chase(smoke20, (open14 || open17) && arrive20 >= 0f && Now - arrive20 > chase * 0.6f, dt);
            if (zoneLanding14 != null) zoneLanding14.SetSmoke(smoke14);
            if (zoneLanding17 != null) zoneLanding17.SetSmoke(smoke17);
            if (zoneLanding20 != null) zoneLanding20.SetSmoke(smoke20);
        }

        private static float Chase(float v, bool rising, float dt)
        {
            float rise = GameTimingConfig.Current.landingSmokeRisePerSecond;
            return rising ? Mathf.Min(0.9f, v + rise * dt) : Mathf.Max(0f, v - 0.08f * dt);
        }

        private void BreakKeyBox()
        {
            if (keyTaken) return;
            InventorySystem inv = InventorySystem.Instance;
            if (inv == null || roofKeyItem == null) return;
            if (!inv.TryAdd(roofKeyItem)) { EventBus.ShowMessage("가방이 가득 찼다. 쓰지 않는 물건을 정리하자."); return; }   // ItemAcquired 이벤트 발행
            keyTaken = true;
            ScenarioFlags.Set(EvacKeys.FlagRoofKey, true);
            if (keyShards != null) keyShards.Emit(40);
            if (glassBreakAudio != null && glassBreakAudio.clip != null) glassBreakAudio.Play();
            Hide(keyBoxGlass);
            if (player != null) player.Shake(0.04f, 0.3f);
            Done("act4_a_key");
            EventBus.ShowMessage("유리를 깨고 옥상 열쇠를 꺼냈다.");
            TriggerInteractable box = EvacDirector.FindById<TriggerInteractable>(EvacKeys.KeyBox);
            if (box != null) box.Interactable = false;
        }

        private void TryOpenRoofDoor()
        {
            if (roofOpen) return;
            InventorySystem inv = InventorySystem.Instance;
            ItemData key = inv != null ? inv.Find(EvacKeys.ItemRoofKey) : null;
            if (key == null) { EventBus.ShowMessage("옥상 문이 잠겨 있다. 열쇠가 필요하다. 옆의 비상 열쇠함 유리를 깨자."); return; }
            inv.Remove(key);
            roofOpen = true;
            if (roofDoorObject != null)
            {
                foreach (Renderer r in roofDoorObject.GetComponentsInChildren<Renderer>()) r.enabled = false;
                foreach (Collider c in roofDoorObject.GetComponentsInChildren<Collider>()) c.enabled = false;
            }
            EventBus.ShowMessage("열쇠로 옥상 문을 열었다. 바깥 공기가 밀려든다!");
        }

        private void StartRooftopRescue()
        {
            if (signaled) return;
            signaled = true;
            Done("act4_a_roof");
            EventBus.ShowMessage("두 팔을 크게 흔들어 구조를 요청했다!");
            StartCoroutine(RooftopArrival());
        }

        private IEnumerator RooftopArrival()
        {
            EventBus.ShowSubtitle("멀리서 헬기 소리가 점점 가까워진다.", T.rooftopArrivalSeconds * 0.8f);
            if (rescueArrivalAudio != null && rescueArrivalAudio.clip != null) rescueArrivalAudio.Play();
            Show(helicopter);
            Show(roofLadder);
            float dur = Mathf.Max(0.5f, T.rooftopArrivalSeconds);
            for (float t = 0f; t < 1f; t += Time.deltaTime / dur)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                if (helicopter != null && heliStart != null && heliEnd != null) helicopter.transform.position = Vector3.Lerp(heliStart.position, heliEnd.position, k);
                if (roofLadderPivot != null) roofLadderPivot.localScale = new Vector3(1f, Mathf.Max(0.02f, k), 1f);
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
            FinishWithEnding(EvacKeys.EndingRooftop);
        }

        // ═════════════════════ 루트 B: 경량칸막이 ═════════════════════
        [Header("루트 B — 경량칸막이")]
        [SerializeField] private PartitionBreakable partition;
        [SerializeField, Tooltip("베란다 화분·짐 상자 (id: flower_pot / balcony_pot_2 / balcony_storage_box)")] private RemovableInteractable[] balconyClearables;
        [SerializeField] private HazardZone zone1003;

        private bool bClearDone, bBrokenDone, bNeighborDone, bExitStarted;

        private void BeginRouteB()
        {
            Add("act4_b_clear");
            Add("act4_b_break");
            Add("act4_b_neighbor");
            Add("act4_b_exit");
            if (grandmother != null && grandmother.State == GrandmaState.Hidden && grandmotherFallenPoint != null)
                grandmother.BeginFallen(grandmotherFallenPoint.position, grandmotherFallenPoint.eulerAngles.y);
            EventBus.ShowScent("얇은 석고보드 칸막이 너머는 옆집(1003호) 베란다다. 망치가 있으면 더 빨리 부술 수 있다.");
            UpdateRouteB();
        }

        private void HandleRouteB(ScenarioEvent e)
        {
            if (e.Type == ScenarioEventType.Custom && e.SourceId == "partition_broken")
            {
                Done("act4_b_break");
                EventBus.ShowSubtitle("옆집 안에서 기침 소리가 들린다. 연기가 자욱하다.", 4f);
            }
            else if (e.Type == ScenarioEventType.ZoneEntered && e.SourceId == EvacKeys.Zone1003)
                EventBus.ShowScent("1003호 안은 연기로 가득하다. 거실 바닥에 누군가 쓰러져 있다.");
            else if (e.Type == ScenarioEventType.Interacted && e.SourceId == EvacKeys.Door1003Inside)
                RouteBExit();
        }

        private void UpdateRouteB()
        {
            if (!bClearDone && balconyClearables != null && balconyClearables.Length > 0)
            {
                bool all = true;
                foreach (RemovableInteractable r in balconyClearables) if (r != null && !r.IsRemoved) all = false;
                if (all) { bClearDone = true; Done("act4_b_clear"); EventBus.ShowMessage("베란다가 정리되어 칸막이 앞이 비었다."); }
            }
            if (!bNeighborDone && grandmother != null && grandmother.State == GrandmaState.FollowingPlayer)
            {
                bNeighborDone = true;
                Done("act4_b_neighbor");
            }
        }

        private void RouteBExit()
        {
            if (bExitStarted) return;
            bExitStarted = true;
            Done("act4_b_exit");
            Travel(stairRoomSpawn, "1003호 현관을 열고 복도로 나왔다. 연기가 자욱한 복도를 낮게 숙여 비상계단까지 이동한다…", Mathf.Max(1.5f, T.routeBCorridorRunSeconds), ApplyCorridorRunSmoke);
            StartCoroutine(FinishAfter(Mathf.Max(1.5f, T.routeBCorridorRunSeconds) + 0.6f, EvacKeys.EndingPartition));
        }

        /// <summary>압축된 복도 이동 중 들이마신 연기: 복도 연기 × 수건(0.35) — 젖은 수건 없이 달리면 호흡이 꽤 깎인다</summary>
        private void ApplyCorridorRunSmoke()
        {
            PlayerVitals v = player != null ? player.GetComponent<PlayerVitals>() : null;
            HazardZone corridor = EvacDirector.FindZone(EvacKeys.ZoneCorridor);
            if (v == null || corridor == null) return;
            InventorySystem inv = InventorySystem.Instance;
            bool towel = inv != null && inv.Find(EvacKeys.ItemWetTowel) != null && inv.IsEquipped(inv.Find(EvacKeys.ItemWetTowel));
            float loss = corridor.SmokeLevel * 40f * (towel ? 0.35f : 1f);
            if (loss > 0.5f) v.Modify(-loss);
        }

        // ═════════════════════ 루트 C: 대피공간 농성 ═════════════════════
        [Header("루트 C — 대피공간")]
        [SerializeField] private ShelterSealSystem shelterSeal;
        [SerializeField] private ToggleInteractable shelterWindow;
        [SerializeField] private GameObject rescueLadder;
        [SerializeField] private Transform rescueLadderPivot;

        private bool cDoorDone, cSealDone, cWindowDone, cCallDone, cArrived;
        private int lastHintSecond = 999;

        private void BeginRouteC()
        {
            Add("act4_c_door");
            Add("act4_c_seal");
            Add("act4_c_window");
            Add("act4_c_call");
            Add("act4_c_wait");
            if (shelterWindow == null) shelterWindow = EvacDirector.FindById<ToggleInteractable>(EvacKeys.ShelterWindow);
            EventBus.ShowScent("대피공간은 방화문과 벽으로 막혀 있다. 문틈을 막고, 창문을 열어 환기하고, 119 에 위치를 알리자.");
        }

        private void HandleRouteC(ScenarioEvent e) { }

        private void UpdateRouteC()
        {
            if (shelterSeal == null) return;
            if (!cDoorDone && shelterSeal.DoorClosed && PlayerIn(EvacKeys.ZoneShelter)) { cDoorDone = true; Done("act4_c_door"); MarkFireDoorManaged(); }
            if (!cSealDone && shelterSeal.SealedSides >= 4) { cSealDone = true; Done("act4_c_seal"); EventBus.ShowMessage("문틈을 모두 막았다."); }
            if (!cWindowDone && shelterWindow != null && shelterWindow.IsOn) { cWindowDone = true; Done("act4_c_window"); EventBus.ShowMessage("창문을 열자 신선한 바깥 공기가 들어온다."); }
            if (!cCallDone && Call119Controller.Instance != null && Call119Controller.Instance.HasCalled) { cCallDone = true; Done("act4_c_call"); }
            Record(EvacKeys.RecSealRatio, shelterSeal.SealRatio);

            float eta = routeStart + T.shelterRescueWaitSeconds + RescueDelay();
            float remaining = eta - Now;
            int sec = Mathf.CeilToInt(Mathf.Max(0f, remaining));
            int[] marks = { 30, 15, 5 };
            foreach (int m in marks)
                if (sec <= m && lastHintSecond > m) { lastHintSecond = sec; EventBus.ShowMessage($"구조대 도착까지 약 {m}초. 낮은 자세로 숨을 아끼자."); }

            if (!cArrived && remaining <= 0f)
            {
                cArrived = true;
                Done("act4_c_wait");
                StartCoroutine(LadderArrival());
            }
        }

        private IEnumerator LadderArrival()
        {
            EventBus.ShowSubtitle("창밖에서 사이렌과 함께 고가사다리차가 다가온다!", T.ladderArrivalSeconds);
            if (rescueArrivalAudio != null && rescueArrivalAudio.clip != null) rescueArrivalAudio.Play();
            if (outdoor != null) outdoor.OnFireTruckArrive();
            Show(rescueLadder);
            float dur = Mathf.Max(0.5f, T.ladderArrivalSeconds);
            for (float t = 0f; t < 1f; t += Time.deltaTime / dur)
            {
                if (rescueLadderPivot != null) rescueLadderPivot.localScale = new Vector3(1f, Mathf.Max(0.02f, Mathf.SmoothStep(0f, 1f, t)), 1f);
                yield return null;
            }
            // 대피공간 안에 있어야 구조된다 (밖에 있다면 돌아오도록 안내)
            if (!PlayerIn(EvacKeys.ZoneShelter)) EventBus.ShowMessage("사다리차가 대피공간 창문 앞에 도착했다. 대피공간으로 돌아가자!");
            while (!PlayerIn(EvacKeys.ZoneShelter) && !GameFlow.IsEnded) yield return null;
            ScenarioFlags.Set(EvacKeys.FlagShelterRescue, true);
            FinishWithEnding(EvacKeys.EndingShelter);
        }

        // ═════════════════════ 루트 D: 1층 하강 ═════════════════════
        [Header("루트 D — 계단 하강 / 외부 집결지")]
        [SerializeField, Tooltip("하강 3구간의 연기 구역 (zone_descent_1~3)")] private HazardZone[] descentZones;
        [SerializeField] private GameObject fireTruck;
        [SerializeField] private GameObject ambulance;
        [SerializeField] private OutdoorWorldController outdoor;
        [SerializeField, Tooltip("구간 연기 배율 (아래층일수록 연기가 옅다)")] private float[] stageSmokeFactor = { 1f, 0.85f, 0.55f };

        private int descentStage;       // 0 = 시작 전, 1~3 = 구간, 4 = 집결지
        private float shakeTimer, hintTimer;

        private void BeginRouteD()
        {
            Add("act4_d_down");
            bool smoky = ScenarioFlags.GetBool(EvacKeys.FlagStairDownBlocked);
            EventBus.ShowScent(smoky ? "계단에는 이미 연기가 가득하다. 몸을 낮추고 젖은 수건으로 입을 막아야 한다." : "연기가 올라오기 전이다. 낮은 자세로 벽을 짚으며 내려가자.");
            Travel(descent1Spawn, "9층… 8층… 7층 (연기가 자욱한 층을 지난다)", StageTravelSeconds, () => EnterDescentStage(1));
        }

        private void EnterDescentStage(int stage)
        {
            descentStage = stage;
            if (player != null) player.ScenarioSpeedMultiplier = T.descentSpeedMultiplier;   // 벽을 짚고 천천히
            if (EvacDirector.Instance != null) EvacDirector.Instance.SetAmbientBright(false);
        }

        private void HandleRouteD(ScenarioEvent e)
        {
            if (e.Type != ScenarioEventType.ZoneEntered) return;
            if (e.SourceId == "zone_descent_end_1" && descentStage == 1)
                Travel(descent2Spawn, "6층… 5층… 4층", StageTravelSeconds, () => EnterDescentStage(2));
            else if (e.SourceId == "zone_descent_end_2" && descentStage == 2)
                Travel(descent3Spawn, "3층… 2층…", StageTravelSeconds, () => EnterDescentStage(3));
            else if (e.SourceId == "zone_descent_end_3" && descentStage == 3)
            {
                descentStage = 4;
                Travel(plazaSpawn, "1층. 건물 밖으로 나왔다.", StageTravelSeconds, ArriveAssemblyPoint);
            }
        }

        private void ArriveAssemblyPoint()
        {
            if (player != null) player.ScenarioSpeedMultiplier = 1f;
            if (EvacDirector.Instance != null) EvacDirector.Instance.SetAmbientBright(true);
            Done("act4_d_down");
            Add("act4_d_assembly");
            Show(fireTruck);
            Show(ambulance);
            if (outdoor != null) outdoor.OnFireTruckArrive();   // 창밖 풍경의 소방차 도착 연출과 연동
            EventBus.ShowSubtitle("소방대원: 이쪽으로 오세요! 다치신 곳은 없으세요? 이웃분들도 모두 이쪽에 모여 계십니다.", 6f);
            StartCoroutine(FinishAfter(T.assemblyEndingDelay, EvacKeys.EndingDescent));
        }

        private void UpdateRouteD()
        {
            if (descentStage < 1 || descentStage > 3) return;
            float baseSmoke = StairSmoke(actStart);
            if (ScenarioFlags.GetBool(EvacKeys.FlagStairDownBlocked)) baseSmoke = Mathf.Max(baseSmoke, 0.6f);   // 연기 속을 알고 내려가는 경우
            for (int i = 0; i < descentZones.Length; i++)
            {
                if (descentZones[i] == null) continue;
                float f = stageSmokeFactor != null && i < stageSmokeFactor.Length ? stageSmokeFactor[i] : 1f;
                descentZones[i].SetSmoke(baseSmoke * f);
            }

            // 벽을 짚고 내려가는 느낌: 카메라 미세 흔들림
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0f && player != null) { shakeTimer = 0.7f; player.Shake(T.descentShakeAmplitude, 0.5f); }

            // 웅크리기 유도
            hintTimer -= Time.deltaTime;
            if (hintTimer <= 0f && baseSmoke > 0.25f && player != null && !player.IsCrouching)
            {
                hintTimer = 8f;
                EventBus.ShowScent("연기는 위로 모인다. 몸을 낮추고(Ctrl) 벽을 짚으며 내려가자.");
            }
        }

        // ═════════════════════ 공통 ═════════════════════
        private void UpdateAlways()
        {
            // 쓰러진 할머니가 있는 1003호 연기 (현관으로 못 나가는 판): 시간에 따라 차오른다
            if (zone1003 != null && ScenarioFlags.GetBool(EvacKeys.FlagHandleHot))
                zone1003.SetSmoke(Mathf.Min(0.65f, 0.12f + (Now - actStart) * 0.009f));
        }

        private IEnumerator FinishAfter(float seconds, string endingId)
        {
            yield return new WaitForSeconds(seconds);
            FinishWithEnding(endingId);
        }
    }
}
