using System.Collections.Generic;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 몸을 보호할 수 있는 공간 (탁자 아래, 계단 벽 쪽 등). 시나리오 씬에 두는 범위 표시이며 물리 트리거를 쓰지 않는다
    /// (집 콜라이더·상호작용 레이와 섞이지 않게 좌표 범위로 판정). requireCrouch 면 웅크린 상태여야 보호로 인정한다.
    /// </summary>
    public class QuakeCoverZone : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new Vector3(1f, 1f, 1f);
        [SerializeField] private bool requireCrouch = true;

        private static readonly List<QuakeCoverZone> all = new List<QuakeCoverZone>();
        public static IReadOnlyList<QuakeCoverZone> All => all;
        public bool RequireCrouch => requireCrouch;
        public Bounds Bounds => new Bounds(transform.position, size);

        private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        private void OnDisable() { all.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        private void OnDrawGizmosSelected() { Gizmos.color = new Color(0.2f, 0.8f, 0.3f, 0.4f); Gizmos.DrawWireCube(transform.position, size); }
    }
}
