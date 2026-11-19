using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 베란다 석고보드 경량칸막이 (id: light_partition). 망치(item: hammer)로 여러 번 내리치거나 발로 여러 번 걷어차면 균열이 커지다가 부서진다.
    /// 망치가 있으면 적은 횟수(GameTimingConfig.partitionHitsHammer), 없으면 발차기(partitionHitsKick)가 필요하다.
    /// 타격마다 파편 파티클 + 균열 단계 연출이 나오고, 부서지면 콜라이더가 꺼져 옆집(1003호) 베란다로 통과할 수 있다.
    /// 이벤트: Custom "partition_hit" (State = false) / "partition_broken" (State = true). 항상 상호작용 가능 — 막지 않는다.
    /// </summary>
    public class PartitionBreakable : InteractableObject
    {
        [Header("칸막이")]
        [SerializeField] private Transform visualRoot;
        [SerializeField, Tooltip("균열 단계별로 하나씩 켜지는 오브젝트 (약한 쪽에서 강한 쪽 순서)")] private GameObject[] crackStages;
        [SerializeField, Tooltip("부서질 때 함께 사라지는 장식 (테두리, 안내판 등)")] private GameObject[] hideOnBreak;
        [SerializeField] private ParticleSystem debris;
        [SerializeField] private AudioSource hitAudio;
        [SerializeField] private string hammerItemId = EvacKeys.ItemHammer;
        [SerializeField] private string hammerPrompt = "망치로 칸막이 부수기";
        [SerializeField] private string kickPrompt = "칸막이 걷어차기";
        [SerializeField] private string hammerMessage = "망치로 석고보드를 내리쳤다.";
        [SerializeField] private string kickMessage = "힘껏 걷어찼다. 석고보드에 금이 간다.";
        [SerializeField] private string brokenMessage = "칸막이가 부서졌다! 옆집 베란다로 통하는 구멍이 생겼다.";

        private float damage01;
        private float lastHitAt = -9f;
        private int hits;

        public bool IsBroken { get; private set; }
        public int Hits => hits;
        public float Damage01 => damage01;

        private bool HasHammer => InventorySystem.Instance != null && InventorySystem.Instance.Has(hammerItemId);

        public override string GetPrompt() => HasHammer ? hammerPrompt : kickPrompt;
        public override bool CanInteract(PlayerInteractor interactor) => base.CanInteract(interactor) && !IsBroken;

        protected override void OnInteract(PlayerInteractor interactor)
        {
            Hit(HasHammer);
        }

        /// <summary>한 번 타격 (디버그/테스트에서도 호출)</summary>
        public void Hit(bool withHammer)
        {
            if (IsBroken) return;
            GameTimingConfig t = GameTimingConfig.Current;
            if (Time.time - lastHitAt < t.partitionHitCooldown) return;
            lastHitAt = Time.time;
            hits++;

            int need = withHammer ? t.partitionHitsHammer : t.partitionHitsKick;
            damage01 = Mathf.Min(1f, damage01 + 1f / Mathf.Max(1, need));
            EventBus.ShowMessage(withHammer ? hammerMessage : kickMessage);
            if (hitAudio != null && hitAudio.clip != null) hitAudio.Play();
            if (debris != null) debris.Emit(withHammer ? 14 : 8);
            PlayerController p = FindFirstObjectByType<PlayerController>();
            if (p != null) p.Shake(withHammer ? 0.04f : 0.05f, 0.25f);
            UpdateCracks();
            EventBus.Raise(ScenarioEventType.Custom, "partition_hit", false);
            if (ScoreSystem.Instance != null) ScoreSystem.Instance.Increment(withHammer ? "partition_hits_hammer" : "partition_hits_kick");

            if (damage01 >= 0.999f) Break();
        }

        private void UpdateCracks()
        {
            if (crackStages == null || crackStages.Length == 0) return;
            int stage = Mathf.Clamp(Mathf.FloorToInt(damage01 * crackStages.Length), 0, crackStages.Length);
            for (int i = 0; i < crackStages.Length; i++) if (crackStages[i] != null) crackStages[i].SetActive(i < stage);
        }

        private void Break()
        {
            IsBroken = true;
            Interactable = false;
            if (debris != null) debris.Emit(60);
            if (visualRoot != null)
            {
                foreach (Renderer r in visualRoot.GetComponentsInChildren<Renderer>()) r.enabled = false;
            }
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (crackStages != null) foreach (GameObject g in crackStages) if (g != null) g.SetActive(false);
            if (hideOnBreak != null) foreach (GameObject g in hideOnBreak) if (g != null) g.SetActive(false);
            ScenarioFlags.Set(EvacKeys.FlagPartitionOpen, true);
            EventBus.ShowMessage(brokenMessage);
            EventBus.Raise(ScenarioEventType.Custom, "partition_broken", true);
            Publish(ScenarioEventType.Interacted, true);
        }
    }
}
