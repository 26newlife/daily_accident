using System;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    public enum WarPhase { None = 0, Prep = 1, Ark = 2, Peace = 3, Fallout = 4, Rescue = 5, Over = 6 }

    /// <summary>
    /// 시나리오 1 의 공유 상태 (자원·피폭·방어도·단서·동행자). 모든 수치 계수는 WarConfig 에 있다.
    /// 플레이 한 판마다 시나리오 씬이 새로 로드되므로 상태는 자동으로 초기화된다 (재시작 안전).
    /// 시간 흐름(식량 소비, 피폭, 체온)은 GameClock 의 게임 시간을 기준으로 틱한다 — 코루틴/Invoke 없음.
    /// </summary>
    public class WarState : MonoBehaviour
    {
        [SerializeField] private WarConfig config;

        public static WarState Instance { get; private set; }
        public WarConfig Config => config;
        public WarPhase Phase { get; private set; }

        // ───────── 설계한 변수 ─────────
        public float Radiation { get; private set; }          // radiationLevel (0~100)
        public float Comfort { get; private set; }            // 쾌적도 (0~100)
        public float Sanity { get; private set; } = 100f;     // 정신력
        public float BodyTemp { get; private set; } = 100f;   // 체온 (0 = 동사)
        public int Food { get; private set; }
        public int Water { get; private set; }
        public int Medicine { get; private set; }
        public int ActionPoints { get; private set; }
        public bool HasRadio { get; private set; }
        public bool HasWeapon { get; private set; }
        public bool IodineTaken { get; private set; }
        public bool IodineBeforeFallout { get; private set; }
        public int RadioFrequency { get; private set; }      // radioFrequency : 현재 채널 번호
        public bool HasGasMask => InventorySystem.Instance != null && InventorySystem.Instance.Find(WarKeys.ItemGasMask) != null;
        public bool GasMaskWorn { get { var inv = InventorySystem.Instance; if (inv == null) return false; ItemData m = inv.Find(WarKeys.ItemGasMask); return m != null && inv.IsEquipped(m); } }
        public bool HasIodine => InventorySystem.Instance != null && InventorySystem.Instance.Find(WarKeys.ItemIodine) != null;

        public int DeceptionCluesCount => clues.Count;        // deceptionCluesCount
        public bool CluesEnough => config != null && clues.Count >= config.deceptionClueThreshold;
        public int Joined { get; private set; }
        public int Lost { get; private set; }
        public int Refused { get; private set; }
        public bool SoldierJoined { get; private set; }
        public bool ChildJoined { get; private set; }
        public bool NeighborJoined { get; private set; }
        public int BlanketCount { get; private set; }
        public int BrokenWindows { get; private set; }
        public bool ConsumablesContaminated { get; private set; }

        public event Action Changed;
        public event Action<string> ClueFound;

        private readonly HashSet<string> clues = new HashSet<string>();
        private readonly List<Transform> windowTransforms = new List<Transform>();
        private float lastClock = -1f;
        private float nextFood = -1f, nextWater = -1f, nextMedicine = -1f;
        private int hunger;
        private bool dead;

        private void Awake() { Instance = this; if (config != null) ActionPoints = config.actionPoints; Comfort = 0f; }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        public void Poke() => Changed?.Invoke();

        public void SetPhase(WarPhase p)
        {
            Phase = p;
            GameClock c = GameClock.Instance;
            float now = c != null ? c.GameSeconds : 0f;
            if (p == WarPhase.Ark || p == WarPhase.Peace) { ScheduleConsume(now); }
            if (p == WarPhase.Fallout) { ScheduleConsume(now); nextMedicine = now + (config != null ? config.medicineIntervalSeconds : 30f); }
            Changed?.Invoke();
        }

        private void ScheduleConsume(float now)
        {
            if (config == null) return;
            float shorten = Joined * config.consumeIntervalPerSurvivor;
            float fi = Phase == WarPhase.Fallout ? config.falloutConsumeIntervalSeconds : config.foodConsumeIntervalSeconds;
            float wi = Phase == WarPhase.Fallout ? config.falloutConsumeIntervalSeconds : config.waterConsumeIntervalSeconds;
            nextFood = now + Mathf.Max(8f, fi - shorten);
            nextWater = now + Mathf.Max(8f, wi - shorten);
        }

        // ───────── 자원 ─────────
                public void AddFood(int n) { Food = Mathf.Max(0, Food + n); Changed?.Invoke(); }
        public void AddWater(int n) { Water = Mathf.Max(0, Water + n); Changed?.Invoke(); }
        public void AddMedicine(int n) { Medicine = Mathf.Max(0, Medicine + n); Changed?.Invoke(); }
        public void AddComfort(float v) { Comfort = Mathf.Clamp(Comfort + v, 0f, 100f); Changed?.Invoke(); }
        public void AddSanity(float v) { Sanity = Mathf.Clamp(Sanity + v, 0f, 100f); Changed?.Invoke(); }
        public void AddBlanket() { BlanketCount++; AddComfort(config != null ? config.comfortBlanket : 20f); }
        public void SetHasRadio() { HasRadio = true; ScenarioFlags.Set(WarKeys.FlagHasRadio, true); Changed?.Invoke(); }
        public void SetHasWeapon() { HasWeapon = true; Changed?.Invoke(); }
        public void SetRadioFrequency(int i) { RadioFrequency = i; Changed?.Invoke(); }
        public bool SpendAction(int n)
        {
            if (n <= 0) return true;
            if (ActionPoints < n) { EventBus.ShowMessage("지금은 더 움직일 힘이 없다. (행동력 부족)"); return false; }
            ActionPoints -= n; Changed?.Invoke(); return true;
        }

        // ───────── 단서 ─────────
        public bool AddClue(string id)
        {
            if (string.IsNullOrEmpty(id) || !clues.Add(id)) return false;
            ScoreSystem.Instance?.Record(WarKeys.RecClues, clues.Count);
            ScenarioFlags.Set(WarKeys.FlagCluesEnough, CluesEnough);
            ClueFound?.Invoke(id);
            Changed?.Invoke();
            return true;
        }
        public bool HasClue(string id) => clues.Contains(id);

        // ───────── 창문 / 위치 ─────────
        /// <summary>충격파: 모든 창이 깨진 것으로 기록 (낙진·한파가 안으로 들어온다)</summary>
        public void MarkAllWindowsBroken() { BrokenWindows = WarKeys.Windows.Length; Changed?.Invoke(); }

        private void CacheWindows()
        {
            if (windowTransforms.Count > 0) return;
            foreach (string id in WarKeys.Windows)
            {
                InteractableObject w = InteractableObject.FindById(id);
                if (w != null) windowTransforms.Add(w.transform);
            }
        }

        /// <summary>플레이어(또는 지점)에서 가장 가까운 창까지의 수평 거리 (창을 못 찾으면 큰 값)</summary>
        public float DistanceToNearestWindow(Vector3 p)
        {
            CacheWindows();
            float best = 999f;
            foreach (Transform t in windowTransforms)
            {
                Vector3 d = t.position - p; d.y = 0f;
                best = Mathf.Min(best, d.magnitude);
            }
            return best;
        }

        public bool PlayerDeepInside
        {
            get
            {
                PlayerController pl = FindFirstObjectByType<PlayerController>();
                return pl != null && config != null && DistanceToNearestWindow(pl.transform.position) >= config.deepInteriorDistance;
            }
        }

        // ───────── 동행자 ─────────
        public void Join(string who)
        {
            if (who == WarKeys.SurvivorSoldier) { SoldierJoined = true; AddMedicine(config.soldierMedicine); }
            else if (who == WarKeys.SurvivorChild) { ChildJoined = true; AddSanity(config.childSanityBonus); }
            else if (who == WarKeys.SurvivorNeighbor) { NeighborJoined = true; AddFood(config.neighborFood); AddWater(config.neighborWater); }
            Joined++;
            ScoreSystem.Instance?.Record(WarKeys.RecJoined, Joined);
            Changed?.Invoke();
        }
        public void RefuseVisitor() { Refused++; ScoreSystem.Instance?.Record(WarKeys.RecRefused, Refused); AddSanity(-config.refuseSanityLoss); }
        public void LoseSurvivor() { Lost++; if (Joined > 0) { /* 이탈한 동행자는 소비에서 제외 */ } ScoreSystem.Instance?.Record(WarKeys.RecLost, Lost); Changed?.Invoke(); }
        public int AliveSurvivors => Mathf.Max(0, Joined - Lost);

        // ───────── 요오드 / 방독면 ─────────
        public void TakeIodine()
        {
            IodineTaken = true;
            IodineBeforeFallout = Phase != WarPhase.Fallout && Phase != WarPhase.Rescue;
            ScenarioFlags.Set(WarKeys.FlagIodineBefore, IodineBeforeFallout);
            Changed?.Invoke();
        }
        public void MarkContaminated() { ConsumablesContaminated = true; }
        public void SetRadiation(float v) { Radiation = Mathf.Clamp(v, 0f, 200f); Changed?.Invoke(); }

        // ───────── 틱 ─────────
        private void Update()
        {
            GameClock clock = GameClock.Instance;
            if (clock == null || GameFlow.IsEnded || dead || config == null) return;
            float now = clock.GameSeconds;
            if (lastClock < 0f) { lastClock = now; return; }
            float dt = now - lastClock;
            lastClock = now;
            if (dt <= 0f || !clock.Running) return;

            if (Phase == WarPhase.Ark || Phase == WarPhase.Peace) TickShelter(now, dt);
            else if (Phase == WarPhase.Fallout) TickFallout(now, dt);
        }

        private void TickShelter(float now, float dt)
        {
            Consume(now, false);
            float delta = Comfort >= config.comfortForRecovery ? config.sanityRecoverPerSecond : -config.sanityDrainPerSecond;
            Sanity = Mathf.Clamp(Sanity + delta * dt, 0f, 100f);
        }

        private void Consume(float now, bool fallout)
        {
            if (now >= nextFood && nextFood > 0f)
            {
                nextFood = now + NextInterval(fallout ? config.falloutConsumeIntervalSeconds : config.foodConsumeIntervalSeconds);
                if (Food > 0)
                {
                    Food--;
                    if (fallout && ConsumablesContaminated) Radiation += config.contaminatedFoodRadiation;
                    hunger = Mathf.Max(0, hunger - 1);
                }
                else StarveHit(fallout);
                Changed?.Invoke();
            }
            if (now >= nextWater && nextWater > 0f)
            {
                nextWater = now + NextInterval(fallout ? config.falloutConsumeIntervalSeconds : config.waterConsumeIntervalSeconds);
                if (Water > 0) Water--; else StarveHit(fallout);
                Changed?.Invoke();
            }
        }

        private float NextInterval(float baseSeconds) => Mathf.Max(8f, baseSeconds - Joined * config.consumeIntervalPerSurvivor);

        private void StarveHit(bool fallout)
        {
            Sanity = Mathf.Max(0f, Sanity - config.starveSanityLoss);
            hunger++;
            EventBus.ShowMessage(Food <= 0 && Water <= 0 ? "먹을 것도 마실 것도 없다. 속이 뒤틀린다." : "식량이나 물이 바닥났다. 기운이 빠진다.");
            if (fallout && hunger >= config.starvationLimit) Kill(DeathReason.Starvation);
        }

        private void TickFallout(float now, float dt)
        {
            Consume(now, true);

            // 피폭: 창 상태 × 방독면 × 요오드
            float exposure = 1f;
            bool deep = PlayerDeepInside;
            if (deep) exposure *= config.deepInteriorExposure;
            if (GasMaskWorn) exposure *= config.gasMaskExposure;
            if (IodineTaken && (IodineBeforeFallout || !config.iodineMustBeBeforeFallout)) exposure *= config.iodineExposure;
            Radiation += config.baseRadiationPerSecond * exposure * dt;

            // 비상 약품이 피폭 증상을 덜어준다
            if (Medicine > 0 && now >= nextMedicine)
            {
                nextMedicine = now + config.medicineIntervalSeconds;
                Medicine--; Radiation = Mathf.Max(0f, Radiation - config.medicineRadiationRelief);
            }

            // 핵겨울 체온
            float cold = deep ? config.deepInteriorColdFactor : 1f;
            float warm = Mathf.Clamp01(Comfort * config.comfortWarmth);
            BodyTemp -= config.coldPerSecond * cold * (1f - Mathf.Min(0.7f, warm)) * dt;
            BodyTemp = Mathf.Clamp(BodyTemp, 0f, 100f);

            Changed?.Invoke();
            if (Radiation >= config.radiationDeathLevel) Kill(DeathReason.RadiationSickness);
            else if (BodyTemp <= config.bodyTempDeathLevel) Kill(DeathReason.Hypothermia);
        }

        public void Kill(DeathReason reason)
        {
            if (dead) return;
            dead = true;
            PushRecords();
            if (DeathAndEnding.Instance != null) DeathAndEnding.Instance.Die(reason);
        }

        /// <summary>점수 계산용 최종 기록 (엔딩/사망 직전)</summary>
        public void PushRecords()
        {
            ScoreSystem s = ScoreSystem.Instance;
            if (s == null) return;
            s.Record(WarKeys.RecClues, clues.Count);
            s.Record(WarKeys.RecRadiation, Radiation);
            s.Record(WarKeys.RecJoined, Joined);
            s.Record(WarKeys.RecLost, Lost);
            s.Record(WarKeys.RecFood, Food + Water);
            s.Record(WarKeys.RecComfort, Comfort);
        }
    }
}
