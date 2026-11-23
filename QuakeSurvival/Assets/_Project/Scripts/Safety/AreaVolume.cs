using UnityEngine;

namespace QuakeSurvival.Safety
{
    /// <summary>
    /// 구역을 표시하는 Trigger 박스. "Ignore Raycast" 레이어에 둔다.
    /// 구역이 겹치면 priority가 높은 쪽이 이긴다(예: 넓은 '건물 밖' 안에 있는 '옥외대피장소').
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class AreaVolume : MonoBehaviour
    {
        [SerializeField] private PlayerArea area = PlayerArea.Apartment;
        [SerializeField] private int priority;

        public PlayerArea Area => area;
        public int Priority => priority;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }
}
