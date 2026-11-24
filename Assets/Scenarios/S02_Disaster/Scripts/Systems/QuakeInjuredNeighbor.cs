using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 계단에서 만나는 다친 이웃 (원본 InjuredNeighbor 이식). 부축하면 플레이어가 지나간 길을 따라 뒤에서 따라온다.
    /// 함께 걷는 동안 플레이어 이동 속도가 줄어든다. 공통 상호작용(E)으로 부축한다.
    /// </summary>
    public class QuakeInjuredNeighbor : InteractableObject
    {
        [SerializeField] private Transform body;
        [SerializeField] private Vector3 standLocalPosition = new Vector3(0f, 0.9f, 0f);
        [SerializeField] private Vector3 standLocalScale = new Vector3(0.5f, 0.9f, 0.5f);
        [SerializeField] private float followGap = 1.2f, crumbSpacing = 0.3f, moveSpeed = 4f;

        private readonly Queue<Vector3> crumbs = new Queue<Vector3>();
        private Vector3 lastCrumb;
        public bool IsFollowing { get; private set; }

        public override string GetPrompt() => "다친 이웃 부축하기";
        public override bool CanInteract(PlayerInteractor interactor) => base.CanInteract(interactor) && !IsFollowing;

        protected override void OnInteract(PlayerInteractor interactor)
        {
            if (IsFollowing) return;
            IsFollowing = true;
            if (QuakeRecord.Instance != null) { QuakeRecord.Instance.NeighborFollowing = true; QuakeRecord.Instance.ApplySpeed(); }
            if (body != null) { body.localPosition = standLocalPosition; body.localScale = standLocalScale; }   // 주저앉아 있던 자세에서 일어선다
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            lastCrumb = Player.position;
            EventBus.ShowSubtitle("(이웃: \"고마워요... 다리를 다쳐서 혼자서는 못 내려가겠어요.\")", 4f);
            Publish(ScenarioEventType.Interacted, true);
        }

        private Transform Player => QuakeTimeline.Instance.Player.transform;

        private void Update()
        {
            if (!IsFollowing || QuakeTimeline.Instance == null) return;
            Vector3 p = Player.position;
            if (Vector3.Distance(p, lastCrumb) >= crumbSpacing) { crumbs.Enqueue(p); lastCrumb = p; }
            while (crumbs.Count > 0 && Vector3.Distance(crumbs.Peek(), p) > followGap && Vector3.Distance(transform.position, crumbs.Peek()) < 0.05f) crumbs.Dequeue();
            if (crumbs.Count == 0) return;
            Vector3 target = crumbs.Peek();
            if (Vector3.Distance(target, p) < followGap) return;
            Vector3 flat = new Vector3(target.x - transform.position.x, 0f, target.z - transform.position.z);
            if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat);
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        }
    }
}
