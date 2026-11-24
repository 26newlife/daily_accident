using System;
using System.Collections;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 집 안 구조물 붕괴 연출 (원본 IndoorCollapse 이식).
    ///   · 정해진 시각에 시나리오 소품(처음엔 고정된 Rigidbody: 책·액자·조명)을 풀어 넘어뜨리거나 떨어뜨린다
    ///   · 본진 동안 집/복도에 있으면 머리 위 천장에서 조각이 떨어진다. 탁자 아래에서 웅크리고 있으면 맞지 않는다
    ///   · 한 번 맞으면 경고, 두 번 맞으면 사망 (떨어진 물건)
    /// </summary>
    public class QuakeIndoorCollapse : MonoBehaviour
    {
        [Serializable]
        public struct Faller
        {
            public Rigidbody body;
            public QuakePhase phase;
            public float delay;
            [Tooltip("넘어뜨릴 때 물체 윗부분에 주는 충격량. 0 이면 그냥 떨어진다")] public Vector3 push;
        }

        [SerializeField] private Faller[] fallers = new Faller[0];
        [SerializeField] private float ceilingHeight = 2.4f, aimedInterval = 2.2f, aimedJitter = 0.15f, hitSpeed = 2.5f, debrisLifetime = 40f;
        private int hits;

        public void Hook()
        {
            if (QuakeTimeline.Instance != null) { QuakeTimeline.Instance.PhaseChanged -= OnPhase; QuakeTimeline.Instance.PhaseChanged += OnPhase; }
            if (QuakeOutcome.Instance != null) { QuakeOutcome.Instance.Decided -= OnDecided; QuakeOutcome.Instance.Decided += OnDecided; }
            foreach (Faller f in fallers) if (f.body != null) f.body.isKinematic = true;
        }

        private void OnDestroy()
        {
            if (QuakeTimeline.Instance != null) QuakeTimeline.Instance.PhaseChanged -= OnPhase;
            if (QuakeOutcome.Instance != null) QuakeOutcome.Instance.Decided -= OnDecided;
        }

        private void OnDecided(DeathReason _) { StopAllCoroutines(); enabled = false; }

        private void OnPhase(QuakePhase phase)
        {
            if (!enabled) return;
            foreach (Faller f in fallers) if (f.body != null && f.phase == phase) StartCoroutine(Release(f));
            if (phase == QuakePhase.MainShock) StartCoroutine(Overhead());
        }

        private IEnumerator Release(Faller f)
        {
            yield return new WaitForSeconds(f.delay);
            f.body.isKinematic = false; f.body.WakeUp();
            if (f.push != Vector3.zero) f.body.AddForceAtPosition(f.push, f.body.worldCenterOfMass + Vector3.up * 0.8f, ForceMode.Impulse);
            QuakeDebrisHit.Attach(f.body.gameObject, QuakeTimeline.Instance.Player.transform, hitSpeed, OnHit);
            QuakeFx.PlayAt(QuakeAtmosphere.Instance != null ? QuakeAtmosphere.Instance.Clatter : null, f.body.position, 0.7f, 0.15f);
        }

        private IEnumerator Overhead()
        {
            var wait = new WaitForSeconds(aimedInterval);
            while (QuakeTimeline.Instance != null && QuakeTimeline.Instance.Phase == QuakePhase.MainShock)
            {
                yield return wait;
                QuakeArea area = QuakeAreas.Instance.Current;
                if (area != QuakeArea.Apartment && area != QuakeArea.Hallway) continue;
                Vector2 j = UnityEngine.Random.insideUnitCircle * aimedJitter;
                bool covered = QuakeCoverTracker.Instance != null && QuakeCoverTracker.Instance.IsCovered;
                SpawnChunk(new Vector3(j.x, 0f, j.y), !covered);
                Vector2 around = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1f, 2.2f);
                SpawnChunk(new Vector3(around.x, 0f, around.y), true);
                QuakeTimeline.Instance.AddImpulse(0.15f);
            }
        }

        private void SpawnChunk(Vector3 offset, bool canHit)
        {
            Transform player = QuakeTimeline.Instance.Player.transform;
            Vector3 feet = player.position;
            var pos = new Vector3(feet.x + offset.x, feet.y + ceilingHeight - 0.35f, feet.z + offset.z);
            float size = UnityEngine.Random.Range(0.25f, 0.4f);
            // 벽 속이나 집 밖에 생기지 않게: 자리가 막혀 있거나 아래에 바닥이 없으면 건너뛴다
            if (Physics.CheckBox(pos, Vector3.one * (size * 0.6f), Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            if (!Physics.Raycast(pos, Vector3.down, ceilingHeight + 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CeilingChunk";
            go.transform.position = pos; go.transform.rotation = UnityEngine.Random.rotation;
            go.transform.localScale = new Vector3(size, UnityEngine.Random.Range(0.06f, 0.12f), size * UnityEngine.Random.Range(0.7f, 1.3f));
            if (QuakeFx.Instance != null && QuakeFx.Instance.Debris != null) go.GetComponent<MeshRenderer>().sharedMaterial = QuakeFx.Instance.Debris;
            var body = go.AddComponent<Rigidbody>();
            body.mass = 6f; body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (canHit) QuakeDebrisHit.Attach(go, player, hitSpeed, OnHit);
            Destroy(go, debrisLifetime);
        }

        private void OnHit()
        {
            if (!enabled || QuakeTimeline.Instance.Phase != QuakePhase.MainShock) return;   // 전진 때 떨어지는 책 등은 세지 않는다
            hits++;
            QuakeTimeline.Instance.AddImpulse(0.7f);
            if (hits == 1) { EventBus.ShowSubtitle("(쿵! 떨어진 조각이 몸을 때렸다... 이대로 있으면 위험하다!)", 3.5f); return; }
            QuakeOutcome.Instance.Kill(DeathReason.QuakeFallingObjects);
        }
    }
}
