using System.Collections.Generic;
using QuakeSurvival.Interaction;
using QuakeSurvival.Player;
using QuakeSurvival.UI;
using UnityEngine;

namespace QuakeSurvival.Npc
{
    /// <summary>
    /// 계단에서 만나는 다친 이웃. 부축하면 플레이어가 지나간 길을 따라 뒤에서 따라온다.
    /// 함께 걷는 동안 플레이어 이동 속도가 줄어든다.
    /// </summary>
    public sealed class InjuredNeighbor : MonoBehaviour, IInteractable
    {
        private const string Reason = "neighbor";

        [SerializeField] private FirstPersonController player;
        [SerializeField] private MessageUI messages;
        [SerializeField] private Transform body;
        [SerializeField] private Vector3 standLocalPosition = new Vector3(0f, 0.9f, 0f);
        [SerializeField] private Vector3 standLocalScale = new Vector3(0.5f, 0.9f, 0.5f);
        [SerializeField, Range(0.1f, 1f)] private float carrySpeed = 0.75f;
        [SerializeField] private float followGap = 1.2f;
        [SerializeField] private float crumbSpacing = 0.3f;
        [SerializeField] private float moveSpeed = 4f;

        private readonly Queue<Vector3> crumbs = new Queue<Vector3>();
        private Vector3 lastCrumb;

        public bool IsFollowing { get; private set; }

        public string Prompt => "다친 이웃 부축하기";
        public bool CanInteract => !IsFollowing;

        private void Awake()
        {
            if (player == null || messages == null || body == null)
            {
                Debug.LogError("[InjuredNeighbor] 참조가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        public void Interact()
        {
            if (!enabled || IsFollowing) return;
            IsFollowing = true;
            player.SetSpeedModifier(Reason, carrySpeed);
            // 주저앉아 있던 자세에서 일어선다
            body.localPosition = standLocalPosition;
            body.localScale = standLocalScale;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            lastCrumb = player.transform.position;
            messages.ShowSubtitle("(이웃: \"고마워요... 다리를 다쳐서 혼자서는 못 내려가겠어요.\")");
        }

        private void Update()
        {
            if (!IsFollowing) return;
            RecordCrumb();
            FollowCrumbs();
        }

        private void RecordCrumb()
        {
            Vector3 p = player.transform.position;
            if (Vector3.Distance(p, lastCrumb) < crumbSpacing) return;
            crumbs.Enqueue(p);
            lastCrumb = p;
        }

        private void FollowCrumbs()
        {
            Vector3 playerPos = player.transform.position;
            while (crumbs.Count > 0 && Vector3.Distance(crumbs.Peek(), playerPos) > followGap
                   && Vector3.Distance(transform.position, crumbs.Peek()) < 0.05f)
            {
                crumbs.Dequeue();
            }
            if (crumbs.Count == 0) return;

            Vector3 target = crumbs.Peek();
            if (Vector3.Distance(target, playerPos) < followGap) return;
            Vector3 next = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            Vector3 flat = new Vector3(target.x - transform.position.x, 0f, target.z - transform.position.z);
            if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat);
            transform.position = next;
        }
    }
}
