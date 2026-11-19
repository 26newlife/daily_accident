using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>사망 원인 종류. 새 원인은 값을 추가하고 DeathReasonData 에셋을 만들어 DeathAndEnding 목록에 등록한다.</summary>
    public enum DeathReason
    {
        Unknown = 0,
        Suffocation = 1,    // 연기 질식
        GasExplosion = 2,   // 가스 폭발
        Burn = 3,           // 화상
        Fall = 4,           // 추락
        Poisoning = 5,      // 가스 중독
        GasIgnition = 6,    // 가스 누출 중 전기/화기 조작으로 폭발
        MultitapFire = 7,   // 과열된 멀티탭 스위치 조작으로 점화
        OilFireExplosion = 8, // 기름불에 물을 부어 불기둥 폭발
        ElectricShock = 9,  // 전기가 흐르는 멀티탭 화재에 물을 부어 감전
        DoorBackdraft = 10, // 뜨거운 현관문을 확인 없이 열어 복도 화염 유입
        ElevatorFireball = 11, // 화재 중 엘리베이터가 도착해 문이 열리는 순간 화염이 덮침
        ElevatorSmoke = 12,    // 엘리베이터 문이 열리며 유독가스 / 탑승 후 승강로 연기로 질식
        BurnInjury = 13,       // 화상 수치(Burn)가 한도를 넘음 (뜨거운 버튼, 불길 근접 등)
        FallFromHeight = 14,   // 베란다/창밖으로 뛰어내려 추락
        StairwellSmoke = 15,   // 연기가 올라오는 비상계단으로 내려가다 질식 (계단은 굴뚝)
        ShelterSmoke = 16,     // 밀봉하지 않은 대피공간에서 연기 유입으로 질식
        ExitTimeout = 17,      // 대피 판단 시간 초과 — 연기와 열기에 갇힘
    }

    /// <summary>ScenarioEvent 의 종류</summary>
    public enum ScenarioEventType
    {
        Interacted = 0,     // 일반 상호작용
        Toggled = 1,        // 스위치/문/창문 상태 변경 (State = 켜짐/열림)
        ValveChanged = 2,   // 밸브 (State = 열림)
        LockChanged = 3,    // 잠금장치 (State = 잠김)
        ItemAcquired = 4,   // 아이템 획득
        ItemUsed = 5,       // 아이템 사용
        ItemEquipped = 6,   // 아이템 착용/해제 (State = 착용)
        ZoneEntered = 7,    // 구역 진입
        ZoneExited = 8,     // 구역 이탈
        PhoneAction = 9,    // 스마트폰 조작 (SourceId = 동작 id)
        Custom = 100,
    }

    /// <summary>
    /// 시나리오 로직이 구독하는 이벤트 1건. "무엇이(SourceId) 어떻게 됐는가"만 담는다.
    /// Act 로직은 이 이벤트를 받아 목표/위험/엔딩을 진행시킨다.
    /// </summary>
    public readonly struct ScenarioEvent
    {
        public readonly ScenarioEventType Type;
        public readonly string SourceId;
        public readonly bool State;

        public ScenarioEvent(ScenarioEventType type, string sourceId, bool state = false)
        {
            Type = type;
            SourceId = sourceId;
            State = state;
        }

        public bool Is(ScenarioEventType type, string id) => Type == type && SourceId == id;
        public override string ToString() => $"{Type}:{SourceId}:{State}";
    }

    /// <summary>
    /// 간단한 정적 이벤트 허브. 시스템끼리 직접 참조하지 않고 이 곳을 통해 통신한다.
    /// 플레이 시작 시 구독자를 비워 이전 플레이의 잔여 구독을 막는다.
    /// </summary>
    public static class EventBus
    {
        /// <summary>모든 시나리오 이벤트 (Act 로직용)</summary>
        public static event Action<ScenarioEvent> ScenarioEventRaised;

        // 편의용 개별 이벤트
        public static event Action<string> Interacted;          // OnInteracted(id)
        public static event Action<string> ItemAcquired;        // OnItemAcquired(id)
        public static event Action<DeathReason> PlayerDied;     // OnPlayerDied(reason)
        public static event Action<string> ObjectiveAdded;
        public static event Action<string> ObjectiveCompleted;  // OnObjectiveCompleted(id)
        public static event Action<string> ObjectiveFailed;
        public static event Action<string> EndingReached;       // 엔딩 id
        public static event Action<int> ActChanged;             // 새 Act 번호
        public static event Action<string> MessageRequested;    // HUD 안내 문구
        public static event Action<string> ScentRequested;      // 냄새 등 감각 묘사 (옅은 노란 글씨)
        public static event Action<string, float> SubtitleRequested; // 자막 (문구, 표시 시간)
        public static event Action Explosion;                   // 아래층 가스 폭발 발생 (창밖 연출/가이드 구독)

        public static void Raise(ScenarioEvent e)
        {
            ScenarioEventRaised?.Invoke(e);
            switch (e.Type)
            {
                case ScenarioEventType.Interacted:
                case ScenarioEventType.Toggled:
                case ScenarioEventType.ValveChanged:
                case ScenarioEventType.LockChanged:
                    Interacted?.Invoke(e.SourceId);
                    break;
                case ScenarioEventType.ItemAcquired:
                    ItemAcquired?.Invoke(e.SourceId);
                    break;
            }
        }

        public static void Raise(ScenarioEventType type, string sourceId, bool state = false) => Raise(new ScenarioEvent(type, sourceId, state));
        public static void RaiseExplosion() => Explosion?.Invoke();
        public static void RaisePlayerDied(DeathReason reason) => PlayerDied?.Invoke(reason);
        public static void RaiseObjectiveAdded(string id) => ObjectiveAdded?.Invoke(id);
        public static void RaiseObjectiveCompleted(string id) => ObjectiveCompleted?.Invoke(id);
        public static void RaiseObjectiveFailed(string id) => ObjectiveFailed?.Invoke(id);
        public static void RaiseEnding(string endingId) => EndingReached?.Invoke(endingId);
        public static void RaiseActChanged(int actNumber) => ActChanged?.Invoke(actNumber);
        public static void ShowMessage(string text) { if (!string.IsNullOrEmpty(text)) MessageRequested?.Invoke(text); }
        public static void ShowScent(string text) { if (!string.IsNullOrEmpty(text)) ScentRequested?.Invoke(text); }
        public static void ShowSubtitle(string text, float seconds) { if (!string.IsNullOrEmpty(text)) SubtitleRequested?.Invoke(text, seconds); }

        /// <summary>현재 EventBus 에 걸린 구독 수 (재시작 후 값이 처음과 같은지 확인하는 디버그용)</summary>
        public static int SubscriptionCount()
        {
            int n = 0;
            n += Count(ScenarioEventRaised); n += Count(Interacted); n += Count(ItemAcquired); n += Count(PlayerDied);
            n += Count(ObjectiveAdded); n += Count(ObjectiveCompleted); n += Count(ObjectiveFailed); n += Count(EndingReached);
            n += Count(ActChanged); n += Count(MessageRequested); n += Count(ScentRequested); n += Count(SubtitleRequested); n += Count(Explosion);
            return n;
        }

        private static int Count(Delegate d) => d == null ? 0 : d.GetInvocationList().Length;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ScenarioEventRaised = null; Interacted = null; ItemAcquired = null; PlayerDied = null;
            ObjectiveAdded = null; ObjectiveCompleted = null; ObjectiveFailed = null;
            EndingReached = null; ActChanged = null; MessageRequested = null; ScentRequested = null; SubtitleRequested = null; Explosion = null;
        }
    }
}
