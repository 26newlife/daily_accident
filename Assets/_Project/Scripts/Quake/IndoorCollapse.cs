using System;
using System.Collections;
using QuakeSurvival.Safety;
using QuakeSurvival.UI;
using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>
    /// 집 안 구조물 붕괴 연출.
    /// - 정해진 시각에 가구/조명/천장 조각(처음엔 고정된 Rigidbody)을 풀어 넘어뜨리거나 떨어뜨린다.
    /// - 본진 동안 플레이어가 집/복도에 있으면 머리 위 천장에서 조각이 떨어진다.
    ///   탁자 아래에 숨어 있으면 조각은 탁자 위에 떨어지고, 서 있으면 몸에 맞는다.
    /// - 한 번 맞으면 경고, 두 번 맞으면 사망(떨어진 물건).
    /// </summary>
    public sealed class IndoorCollapse : MonoBehaviour
    {
        [Serializable]
        public struct Faller
        {
            public Rigidbody body;
            public QuakePhase phase;
            public float delay;
            [Tooltip("넘어뜨릴 때 물체 윗부분에 주는 힘(충격량). 0이면 그냥 떨어진다.")]
            public Vector3 push;
        }

        [SerializeField] private QuakeDirector director;
        [SerializeField] private ZoneSensor zones;
        [SerializeField] private CoverTracker cover;
        [SerializeField] private Transform player;
        [SerializeField] private CameraShake shake;
        [SerializeField] private MessageUI messages;
        [SerializeField] private OutcomeEvaluator evaluator;
        [SerializeField] private Material debrisMaterial;
        [SerializeField] private Faller[] fallers = new Faller[0];

        [Header("머리 위 낙하물")]
        [SerializeField] private float ceilingHeight = 2.6f;
        [SerializeField] private float aimedInterval = 2.2f;
        [SerializeField] private float aimedJitter = 0.15f;
        [SerializeField] private float hitSpeed = 2.5f;
        [SerializeField] private float debrisLifetime = 40f;

        private int hits;

        private void Awake()
        {
            if (director == null || zones == null || cover == null || player == null || shake == null
                || messages == null || evaluator == null || debrisMaterial == null)
            {
                Debug.LogError("[IndoorCollapse] 참조가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (director != null) director.PhaseChanged += OnPhase;
            if (evaluator != null) evaluator.Decided += OnDecided;
        }

        private void OnDisable()
        {
            if (director != null) director.PhaseChanged -= OnPhase;
            if (evaluator != null) evaluator.Decided -= OnDecided;
            StopAllCoroutines();
        }

        // 결과가 나면 더 떨어뜨리지 않는다(결과 화면 위로 자막/흔들림이 겹치지 않게)
        private void OnDecided(DeathCause cause) => enabled = false;

        private void OnPhase(QuakePhase phase)
        {
            foreach (var faller in fallers)
            {
                if (faller.body != null && faller.phase == phase) StartCoroutine(Release(faller));
            }
            if (phase == QuakePhase.MainShock) StartCoroutine(OverheadDebris());
        }

        private IEnumerator Release(Faller faller)
        {
            yield return new WaitForSeconds(faller.delay);
            Rigidbody body = faller.body;
            body.isKinematic = false;
            body.WakeUp();
            if (faller.push != Vector3.zero)
            {
                Vector3 top = body.worldCenterOfMass + Vector3.up * 0.8f;
                body.AddForceAtPosition(faller.push, top, ForceMode.Impulse);
            }
            DebrisHit.Attach(body.gameObject, player, hitSpeed, OnHit);
        }

        private IEnumerator OverheadDebris()
        {
            var wait = new WaitForSeconds(aimedInterval);
            while (director.Phase == QuakePhase.MainShock)
            {
                yield return wait;
                PlayerArea area = zones.CurrentArea;
                if (area != PlayerArea.Apartment && area != PlayerArea.Hallway) continue;

                // 바로 머리 위 하나 + 주변 하나. 숨어 있으면 머리 위 조각은 탁자 위에 떨어져 맞지 않는다.
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * aimedJitter;
                SpawnChunk(new Vector3(jitter.x, 0f, jitter.y), !cover.IsCovered);
                Vector2 around = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1f, 2.2f);
                SpawnChunk(new Vector3(around.x, 0f, around.y), true);
                shake.AddImpulse(0.15f);
            }
        }

        private void SpawnChunk(Vector3 offset, bool canHit)
        {
            Vector3 feet = player.position;
            var position = new Vector3(feet.x + offset.x, feet.y + ceilingHeight - 0.35f, feet.z + offset.z);
            float size = UnityEngine.Random.Range(0.25f, 0.4f);
            // 벽 속이나 건물 밖(빈 공간)에 생기지 않게: 그 자리가 막혀 있거나 아래에 바닥이 없으면 건너뛴다
            if (Physics.CheckBox(position, Vector3.one * (size * 0.6f), Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            if (!Physics.Raycast(position, Vector3.down, ceilingHeight + 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CeilingChunk";
            go.transform.position = position;
            go.transform.rotation = UnityEngine.Random.rotation;
            go.transform.localScale = new Vector3(size, UnityEngine.Random.Range(0.06f, 0.12f), size * UnityEngine.Random.Range(0.7f, 1.3f));
            go.GetComponent<MeshRenderer>().sharedMaterial = debrisMaterial;

            var body = go.AddComponent<Rigidbody>();
            body.mass = 6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (canHit) DebrisHit.Attach(go, player, hitSpeed, OnHit);
            Destroy(go, debrisLifetime);
        }

        private void OnHit()
        {
            // 결과가 난 뒤, 또는 본진이 아닐 때(전진 때 떨어지는 책 등)는 세지 않는다
            if (!enabled || director.Phase != QuakePhase.MainShock) return;
            hits++;
            shake.AddImpulse(0.7f);
            if (hits == 1)
            {
                messages.ShowSubtitle("(쿵! 떨어진 조각이 몸을 때렸다... 이대로 있으면 위험하다!)");
                return;
            }
            evaluator.Kill(DeathCause.FallingObjects);
        }
    }
}
