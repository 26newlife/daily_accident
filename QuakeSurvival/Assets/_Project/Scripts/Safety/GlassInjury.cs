using QuakeSurvival.Audio;
using System;
using QuakeSurvival.Player;
using QuakeSurvival.UI;
using UnityEngine;

namespace QuakeSurvival.Safety
{
    /// <summary>
    /// 신발 없이 깨진 유리(GlassShards)를 밟으면 발을 다쳐 이동 속도가 느려진다(한 번만).
    /// </summary>
    public sealed class GlassInjury : MonoBehaviour
    {
        private const string Reason = "injury";

        [SerializeField] private FirstPersonController player;
        [SerializeField] private SafetyChecklist checklist;
        [SerializeField] private MessageUI messages;
        [SerializeField] private LayerMask shardMask = 1 << 2; // Ignore Raycast
        [SerializeField, Range(0.1f, 1f)] private float injuredSpeed = 0.6f;
        [SerializeField] private AudioClip crunch;

        private readonly Collider[] hits = new Collider[8];

        public event Action Injured;
        public bool IsInjured { get; private set; }

        private void Awake()
        {
            if (player == null || checklist == null || messages == null)
            {
                Debug.LogError("[GlassInjury] 참조가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (IsInjured || checklist.IsDone(SafetyChecklist.Shoes)) return;
            if (!IsOnShards()) return;

            IsInjured = true;
            player.SetSpeedModifier(Reason, injuredSpeed);
            SoundPlayer.Play2D(crunch, AudioCategory.Sfx, 0.8f);
            messages.ShowSubtitle("(악! 맨발로 깨진 유리를 밟았다... 발이 욱신거려 빨리 걸을 수 없다.)");
            Injured?.Invoke();
        }

        private bool IsOnShards()
        {
            Vector3 probe = player.transform.position + Vector3.up * 0.1f;
            int count = Physics.OverlapSphereNonAlloc(probe, 0.25f, hits, shardMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].TryGetComponent(out GlassShards _)) return true;
            }
            return false;
        }
    }
}
