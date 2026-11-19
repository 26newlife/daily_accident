using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 3 / Act 4 공통 기반: 목표(ObjectiveData) id 조회, 시계/점수/플래그 헬퍼, 사망·엔딩 호출.
    /// 목표 에셋은 objectivePool 에 담아 두고 id 문자열로 꺼낸다 (Act 마다 필드를 수십 개 만들지 않는다).
    /// 연기·문 상태 같은 월드 상태는 이벤트뿐 아니라 매 프레임 직접 읽어서 판정한다 → Act 가 시작되기 전에 일어난 행동도 놓치지 않는다.
    /// </summary>
    public abstract class EvacActBase : ActBase
    {
        [Header("목표 에셋 풀 (id 로 조회)")]
        [SerializeField] private ObjectiveData[] objectivePool;

        protected static GameTimingConfig T => GameTimingConfig.Current;
        protected GameClock Clock => GameClock.Instance;
        protected ObjectiveSystem Objectives => ObjectiveSystem.Instance;
        protected ScoreSystem Score => ScoreSystem.Instance;
        protected float Now => Clock != null ? Clock.GameSeconds : 0f;

        protected ObjectiveData Obj(string id)
        {
            if (objectivePool == null) return null;
            foreach (ObjectiveData o in objectivePool) if (o != null && o.Id == id) return o;
            return null;
        }

        protected void Add(string id) { ObjectiveData o = Obj(id); if (o != null && Objectives != null) Objectives.Add(o); }
        protected void Done(string id) { if (Objectives != null) Objectives.Complete(id); }
        protected void Drop(string id) { if (Objectives != null) Objectives.Remove(id); }
        protected void Fail(string id) { if (Objectives != null) Objectives.Fail(id); }
        protected void Record(string key, float value) { if (Score != null && !string.IsNullOrEmpty(key)) Score.Record(key, value); }

        /// <summary>폭발 후 경과 시간(초). 폭발 기록이 없으면 Act 시작 기준</summary>
        protected float SinceExplosion(float fallbackActStart)
        {
            float exp = Score != null && Score.HasRecord(EvacKeys.RecExplosionTime) ? Score.GetRecord(EvacKeys.RecExplosionTime) : fallbackActStart;
            return Now - exp;
        }

        /// <summary>계단실 연기 농도 (0~1): 폭발 후 stairSmokeArrivalSeconds 뒤부터 stairSmokeRiseSeconds 동안 차오른다</summary>
        protected float StairSmoke(float fallbackActStart)
        {
            float since = SinceExplosion(fallbackActStart);
            return Mathf.Clamp01((since - T.stairSmokeArrivalSeconds) / Mathf.Max(1f, T.stairSmokeRiseSeconds));
        }

        protected static bool PlayerIn(string zoneId)
        {
            HazardManager hz = HazardManager.Instance;
            if (hz == null) return false;
            foreach (HazardZone z in hz.PlayerZones) if (z != null && z.ZoneId == zoneId) return true;
            return false;
        }

        protected static void Die(DeathReason reason) => EvacDirector.Die(reason);
    }
}
