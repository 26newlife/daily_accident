using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 신발 없이 깨진 유리(QuakeGlassShards)를 밟으면 발을 다쳐 이동 속도가 느려진다 (한 번만, 원본 GlassInjury 이식).
    /// 유리 조각은 전진 때 나타난다 (현관 거울이 깨져 흩어짐).
    /// </summary>
    public class QuakeGlassInjury : MonoBehaviour
    {
        [SerializeField] private GameObject[] shards = new GameObject[0];
        [SerializeField] private Vector3[] shardAreaCenters = new Vector3[0];
        [SerializeField] private Vector3[] shardAreaSizes = new Vector3[0];
        [SerializeField] private AudioClip crunch;
        [SerializeField] private QuakePhase appearAt = QuakePhase.Foreshock;

        public bool IsInjured { get; private set; }
        private bool visible;

        public void Init()
        {
            foreach (GameObject s in shards) if (s != null) s.SetActive(false);
            if (QuakeTimeline.Instance != null) { QuakeTimeline.Instance.PhaseChanged -= OnPhase; QuakeTimeline.Instance.PhaseChanged += OnPhase; }
        }

        private void OnDestroy() { if (QuakeTimeline.Instance != null) QuakeTimeline.Instance.PhaseChanged -= OnPhase; }

        private void OnPhase(QuakePhase p)
        {
            if (p != appearAt) return;
            visible = true;
            foreach (GameObject s in shards) if (s != null) s.SetActive(true);
            if (shardAreaCenters.Length > 0) QuakeFx.PlayAt(crunch, shardAreaCenters[0], 0.9f, 0.1f);
        }

        private void Update()
        {
            if (!visible || IsInjured || QuakeTimeline.Instance == null) return;
            if (QuakeChecklist.Instance != null && QuakeChecklist.Instance.IsDone(QuakeKeys.ActShoes)) return;
            Vector3 feet = QuakeTimeline.Instance.Player.transform.position + Vector3.up * 0.1f;
            bool on = false;
            for (int i = 0; i < shardAreaCenters.Length && i < shardAreaSizes.Length; i++)
                if (new Bounds(shardAreaCenters[i], shardAreaSizes[i]).Contains(feet)) { on = true; break; }
            if (!on) return;
            IsInjured = true;
            if (QuakeRecord.Instance != null) { QuakeRecord.Instance.Injured = true; QuakeRecord.Instance.ApplySpeed(); }
            QuakeFx.Play2D(crunch, 0.8f);
            EventBus.ShowSubtitle("(악! 맨발로 깨진 유리를 밟았다... 발이 욱신거려 빨리 걸을 수 없다.)", 4f);
        }
    }
}
