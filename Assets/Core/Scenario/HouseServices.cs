using GameCore;
using GameData;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>집 에셋이 Core(ScenarioRunner)에 제공하는 서비스 인터페이스 — Core 는 House 를 직접 참조하지 않는다</summary>
    public interface IHouseEnvironment
    {
        void ApplyEnvironmentPreset(EnvironmentPreset preset);
        /// <summary>시나리오 기능 플래그 중 집(베이스)이 처리하는 것 (화재 소품·도어 스코프·창밖 보행자/차량)</summary>
        void ApplyScenarioFeatures(ScenarioDefinition definition);
    }

    public interface ISpawnProvider
    {
        bool TryGetSpawn(string spawnId, out Vector3 position, out float yaw);
    }

    /// <summary>집 에셋(HouseController)이 등록하고 ScenarioRunner 가 쓰는 서비스 레지스트리. 판이 끝나면 비워진다</summary>
    public static class HouseServices
    {
        public static IHouseEnvironment Environment { get; private set; }
        public static ISpawnProvider Spawns { get; private set; }

        public static void Register(IHouseEnvironment env, ISpawnProvider spawns)
        {
            Environment = env; Spawns = spawns;
            RunCleanup.Register(Clear);
        }

        public static void Clear() { Environment = null; Spawns = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
