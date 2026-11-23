using UnityEngine;

namespace GameGameplay
{
    /// <summary>화재 후 엘리베이터 시나리오(A/B)를 ElevatorHazardConfig 가중치로 한 번 뽑는다 (결정과 보관은 ElevatorController 가 한다)</summary>
    public static class ElevatorScenarioSelector
    {
        public static ElevatorScenario Roll(ElevatorHazardConfig config)
        {
            float a = config != null ? config.weightScenarioA : 50f;
            float b = config != null ? config.weightScenarioB : 50f;
            float total = a + b;
            if (total <= 0f) return ElevatorScenario.A;
            return Random.value * total < a ? ElevatorScenario.A : ElevatorScenario.B;
        }
    }
}
