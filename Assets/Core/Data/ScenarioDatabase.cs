using System.Collections.Generic;
using UnityEngine;

namespace GameData
{
    /// <summary>
    /// 시나리오 목록. 시나리오 선택 팝업은 이 목록을 읽어 슬롯을 자동 생성한다.
    /// 목록 순서 = 슬롯 번호 순서.
    /// </summary>
    [CreateAssetMenu(fileName = "ScenarioDatabase", menuName = "GameData/Scenario Database", order = 1)]
    public class ScenarioDatabase : ScriptableObject
    {
        [SerializeField] private List<ScenarioData> scenarios = new List<ScenarioData>();

        public IReadOnlyList<ScenarioData> Scenarios => scenarios;
        public int Count => scenarios.Count;

        /// <summary>id로 시나리오를 찾는다. 없으면 null.</summary>
        public ScenarioData GetById(string id)
        {
            foreach (var s in scenarios)
                if (s != null && s.Id == id) return s;
            return null;
        }
    }
}
