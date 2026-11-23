using UnityEngine;

namespace QuakeSurvival.Safety
{
    /// <summary>
    /// 몸을 보호할 수 있는 공간(탁자 아래, 계단 벽쪽 등). Trigger Collider와 함께 쓴다.
    /// requireCrouch가 켜져 있으면 숙인 상태여야 보호로 인정한다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CoverZone : MonoBehaviour
    {
        [SerializeField] private bool requireCrouch = true;

        public bool RequireCrouch => requireCrouch;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }
}
