using System.Collections;
using QuakeSurvival.Fx;
using QuakeSurvival.Safety;
using QuakeSurvival.UI;
using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>
    /// 본진이 끝나고 일정 시간 뒤, 아파트 여러 층의 가스관이 터지며 건물이 폭발한다(플레이어 조치와 무관).
    /// - 먼저 아래층에서 연기와 불길이 보이고(단서), 그 다음 아래층부터 위층으로 연쇄 폭발.
    /// - 그때 건물 안(집/복도/계단)에 있으면 사망.
    /// - 콘크리트 잔해가 사방으로 날아가고(최대 약 40m), 잔해에 맞으면 사망.
    /// </summary>
    public sealed class BuildingExplosion : MonoBehaviour
    {
        [SerializeField] private QuakeDirector director;
        [SerializeField] private FxLibrary fx;
        [SerializeField] private CameraShake shake;
        [SerializeField] private Transform player;
        [SerializeField] private ZoneSensor zones;
        [SerializeField] private OutcomeEvaluator evaluator;
        [SerializeField] private MessageUI messages;
        [SerializeField] private Material debrisMaterial;

        [Header("시간(본진이 끝난 뒤, 초)")]
        [SerializeField] private float warningDelay = 10f;
        [SerializeField] private float explosionDelay = 45f;

        [Header("위치(빌더가 채움)")]
        [SerializeField] private Vector3 buildingCenter;
        [SerializeField] private Vector3[] warningFires = new Vector3[0];
        [SerializeField] private Vector3[] blastPoints = new Vector3[0]; // 아래층 -> 위층 순서

        [Header("잔해")]
        [SerializeField] private int debrisCount = 40;
        [SerializeField] private Vector2 debrisSpeed = new Vector2(6f, 13f);
        [SerializeField] private Vector2 debrisLift = new Vector2(3f, 9f);
        [SerializeField] private float debrisMaxHeight = 21f;
        [SerializeField] private float hitSpeed = 4f;

        private const float DebrisLifetime = 30f;

        public bool HasExploded { get; private set; }

        private void Awake()
        {
            if (director == null || fx == null || !fx.IsValid || shake == null || player == null
                || zones == null || evaluator == null || messages == null || debrisMaterial == null)
            {
                Debug.LogError("[BuildingExplosion] 참조가 연결되지 않았습니다.", this);
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

        // 이미 결과가 났으면(사망/클리어) 결과 화면 뒤에서 폭발을 재생하지 않는다
        private void OnDecided(DeathCause cause)
        {
            if (!HasExploded) StopAllCoroutines();
        }

        private void OnPhase(QuakePhase phase)
        {
            if (phase == QuakePhase.Aftermath) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(warningDelay);
            foreach (var p in warningFires) FireFx.Spawn(fx, p, 1.4f);
            messages.ShowSubtitle("(건물 아래층 어딘가에서 불길과 연기가 치솟는다... 가스 냄새가 진동한다!)");
            shake.AddImpulse(0.2f);

            yield return new WaitForSeconds(Mathf.Max(0f, explosionDelay - warningDelay));
            yield return Explode();
        }

        private IEnumerator Explode()
        {
            HasExploded = true;
            Debug.Log($"[BuildingExplosion] explode, player area={zones.CurrentArea}");
            bool inside = IsInsideBuilding(zones.CurrentArea);

            for (int i = 0; i < blastPoints.Length; i++)
            {
                Vector3 p = blastPoints[i];
                ExplosionFx.Spawn(fx, p, 3f);
                if (i % 2 == 0) FireFx.Spawn(fx, p, 2f); // 실시간 조명 수를 줄이려고 두 번에 한 번만 불을 남긴다
                float distance = Vector3.Distance(p, player.position);
                shake.AddImpulse(Mathf.Clamp01(1f - distance / 120f));
                if (i == 0) SpawnDebris();
                if (inside && i == 1) evaluator.Kill(DeathCause.BuildingExplosion);
                yield return new WaitForSeconds(0.18f);
            }
            if (inside) evaluator.Kill(DeathCause.BuildingExplosion);
        }

        private static bool IsInsideBuilding(PlayerArea area)
        {
            return area == PlayerArea.Apartment || area == PlayerArea.Hallway || area == PlayerArea.Stairwell;
        }

        private void SpawnDebris()
        {
            for (int i = 0; i < debrisCount; i++)
            {
                // 각도와 높이를 고르게 나눠 잔해끼리 겹쳐 생기지 않게 한다(겹치면 서로 튕겨 멀리 날아감)
                float angle = (i + Random.Range(0f, 0.6f)) / debrisCount * Mathf.PI * 2f;
                float height = Mathf.Lerp(3f, debrisMaxHeight, (i * 7 % debrisCount) / (float)debrisCount);
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 origin = buildingCenter + dir * 8.5f + Vector3.up * height;
                Vector3 velocity = dir * Random.Range(debrisSpeed.x, debrisSpeed.y) + Vector3.up * Random.Range(debrisLift.x, debrisLift.y);
                SpawnChunk(origin, velocity);
            }
        }

        private void SpawnChunk(Vector3 origin, Vector3 velocity)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ExplosionDebris";
            go.transform.position = origin;
            go.transform.rotation = Random.rotation;
            float size = Random.Range(0.5f, 1.3f);
            go.transform.localScale = new Vector3(size, size * Random.Range(0.4f, 0.9f), size * Random.Range(0.6f, 1.2f));
            go.GetComponent<MeshRenderer>().sharedMaterial = debrisMaterial;

            var body = go.AddComponent<Rigidbody>();
            body.mass = 80f * size;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxDepenetrationVelocity = 2f;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 4f;
            DebrisHit.Attach(go, player, hitSpeed, () => evaluator.Kill(DeathCause.ExplosionDebris));
            // 일부 잔해는 불이 붙은 채 날아간다(조명 없이 불꽃/연기만)
            if (Random.value < 0.25f) FireFx.Spawn(fx, origin, 0.5f, false).transform.SetParent(go.transform, true);
            Destroy(go, DebrisLifetime);
        }
    }
}
