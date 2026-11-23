using System.Collections.Generic;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 베이스 집 안의 "화재 시나리오용" 소품·상호작용(가스 밸브, 멀티탭, 소화기, 수건, 프라이팬 …)에 붙는 태그.
    /// 오브젝트·id 는 그대로 두고, ScenarioDefinition.useFireProps 가 꺼진 시나리오에서는 HouseController 가 이 태그가 붙은 오브젝트를 통째로 숨긴다.
    /// (예전에는 시나리오 1 이 id 문자열 일부(Contains)로 찾아 숨겼다 — 태그로 대체)
    /// 시나리오 로직은 없다. 단순 표식이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireHazardProp : MonoBehaviour
    {
        private static readonly List<FireHazardProp> all = new List<FireHazardProp>();
        public static IReadOnlyList<FireHazardProp> All => all;

        private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        private void OnDisable() { all.Remove(this); }

        /// <summary>비활성 오브젝트 포함 전체 (OnEnable 이 돌지 않은 것도 찾는다)</summary>
        public static FireHazardProp[] FindAllIncludingInactive() => FindObjectsByType<FireHazardProp>(FindObjectsInactive.Include);
    }
}
