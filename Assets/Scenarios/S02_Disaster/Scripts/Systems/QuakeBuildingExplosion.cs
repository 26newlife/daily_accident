using System;
using System.Collections;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 본진이 끝나고 일정 시간 뒤 아파트 여러 층의 가스관이 터지며 건물이 폭발한다 (플레이어 조치와 무관, 원본 BuildingExplosion 이식).
    /// 먼저 아래층에서 불길과 연기(단서), 그 다음 아래층부터 위층으로 연쇄 폭발. 그때 건물 안(집/복도/계단/옥상)에 있으면 사망,
    /// 사방으로 날아가는 콘크리트 잔해에 맞아도 사망.
    /// </summary>
    public class QuakeBuildingExplosion : MonoBehaviour
    {
        [SerializeField] private Vector3[] warningFires = new Vector3[0];
        [SerializeField, Tooltip("아래층 → 위층")] private Vector3[] blastPoints = new Vector3[0];
        [SerializeField] private Vector3 buildingCenter = new Vector3(-4f, -14f, 2.7f);
        [SerializeField] private float debrisRing = 16f;
        [SerializeField] private int debrisCount = 40;
        [SerializeField] private Vector2 debrisSpeed = new Vector2(6f, 13f), debrisLift = new Vector2(3f, 9f);
        [SerializeField] private float debrisMinY = -27f, debrisMaxY = 6f, hitSpeed = 4f;

        public bool HasExploded { get; private set; }

        public void Hook()
        {
            if (QuakeTimeline.Instance != null) { QuakeTimeline.Instance.PhaseChanged -= OnPhase; QuakeTimeline.Instance.PhaseChanged += OnPhase; }
            if (QuakeOutcome.Instance != null) { QuakeOutcome.Instance.Decided -= OnDecided; QuakeOutcome.Instance.Decided += OnDecided; }
        }

        private void OnDestroy()
        {
            if (QuakeTimeline.Instance != null) QuakeTimeline.Instance.PhaseChanged -= OnPhase;
            if (QuakeOutcome.Instance != null) QuakeOutcome.Instance.Decided -= OnDecided;
        }

        private void OnDecided(DeathReason _) { if (!HasExploded) StopAllCoroutines(); }
        private void OnPhase(QuakePhase p) { if (p == QuakePhase.Aftermath) StartCoroutine(Run()); }

        private IEnumerator Run()
        {
            QuakeConfig c = QuakeTimeline.Instance.Config;
            yield return new WaitForSeconds(c.buildingWarningDelay);
            foreach (Vector3 p in warningFires) QuakeFx.FireAt(p, 1.4f);
            EventBus.ShowSubtitle("(건물 아래층 어딘가에서 불길과 연기가 치솟는다... 가스 냄새가 진동한다!)", 5f);
            QuakeTimeline.Instance.AddImpulse(0.2f);
            yield return new WaitForSeconds(Mathf.Max(0f, c.buildingExplosionDelay - c.buildingWarningDelay));
            yield return Explode();
        }

        private IEnumerator Explode()
        {
            HasExploded = true;
            Transform player = QuakeTimeline.Instance.Player.transform;
            bool inside = QuakeAreas.Instance != null && QuakeAreas.Instance.Current.IsInsideBuilding();
            // 5점대(중진): 폭발은 아래층 가스관이라 옥상은 버틴다 (수직대피). 6점대는 옥상도 함께 무너진다
            if (inside && QuakeAreas.Instance.Current == QuakeArea.Rooftop && !QuakeTimeline.Instance.IsStrong) inside = false;
            Debug.Log($"[Quake] 건물 폭발 (플레이어 구역 {QuakeAreas.Instance?.Current})");
            for (int i = 0; i < blastPoints.Length; i++)
            {
                Vector3 p = blastPoints[i];
                QuakeFx.Explosion(p, 3f);
                if (i % 2 == 0) QuakeFx.FireAt(p, 2f, i % 4 == 0);
                QuakeTimeline.Instance.AddImpulse(Mathf.Clamp01(1f - Vector3.Distance(p, player.position) / 120f));
                if (i == 0) SpawnDebris(player);
                if (inside && i == 1) QuakeOutcome.Instance.Kill(DeathReason.QuakeBuildingExplosion);
                yield return new WaitForSeconds(0.18f);
            }
            if (inside) QuakeOutcome.Instance.Kill(DeathReason.QuakeBuildingExplosion);
        }

        private void SpawnDebris(Transform player)
        {
            QuakeFx fx = QuakeFx.Instance;
            for (int i = 0; i < debrisCount; i++)
            {
                float a = (i + UnityEngine.Random.Range(0f, 0.6f)) / debrisCount * Mathf.PI * 2f;
                float h = Mathf.Lerp(debrisMinY, debrisMaxY, (i * 7 % debrisCount) / (float)debrisCount);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 origin = new Vector3(buildingCenter.x, h, buildingCenter.z) + dir * debrisRing;
                Vector3 v = dir * UnityEngine.Random.Range(debrisSpeed.x, debrisSpeed.y) + Vector3.up * UnityEngine.Random.Range(debrisLift.x, debrisLift.y);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "ExplosionDebris";
                go.transform.position = origin; go.transform.rotation = UnityEngine.Random.rotation;
                float s = UnityEngine.Random.Range(0.5f, 1.3f);
                go.transform.localScale = new Vector3(s, s * UnityEngine.Random.Range(0.4f, 0.9f), s * UnityEngine.Random.Range(0.6f, 1.2f));
                if (fx != null && fx.Debris != null) go.GetComponent<MeshRenderer>().sharedMaterial = fx.Debris;
                var body = go.AddComponent<Rigidbody>();
                body.mass = 80f * s; body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.maxDepenetrationVelocity = 2f;
                body.linearVelocity = v; body.angularVelocity = UnityEngine.Random.insideUnitSphere * 4f;
                QuakeDebrisHit.Attach(go, player, hitSpeed, () => QuakeOutcome.Instance.Kill(DeathReason.QuakeExplosionDebris));
                if (UnityEngine.Random.value < 0.25f) { GameObject f = QuakeFx.FireAt(origin, 0.5f, false); if (f != null) f.transform.SetParent(go.transform, true); }
                Destroy(go, 30f);
            }
        }

        public void SetPoints(Vector3[] warnings, Vector3[] blasts) { warningFires = warnings; blastPoints = blasts; }
    }
}
