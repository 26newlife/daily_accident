using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 집(베이스)의 "걸어서 오르내리는 비상계단" 확장 (1~20층 + 옥상, 1층 출구 + 건물 밖 지면).
    /// 10층 계단실 입구(블록아웃 계단 8단 + 서쪽 벽)를 그대로 두고, 그 서쪽 건물 옆에 계단탑을 덧붙인 구조다.
    ///   · 켜기(SetWalkable(true)): 확장 루트를 켜고, 입구를 막고 있던 블록아웃 오브젝트(stubObjects)를 끈다.
    ///   · 끄기: 원래 입구 상태 그대로 (S01·S03 이 검증한 상태). 기본값은 꺼짐.
    /// 시나리오가 ScenarioDefinition.useWalkableStairwell 로 정한다 (HouseController.ApplyScenarioFeatures).
    /// 계단 조명은 가까운 층만 켜고(성능), 집의 "stairwell" 조명 그룹이 꺼지면(정전) 비상등 밝기로 낮춘다.
    /// 지오메트리는 에디터 빌더(HouseStairwellBuilder)가 만든다 — 직접 고치지 말고 빌더를 다시 실행할 것.
    /// </summary>
    public class StairwellExtension : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField, Tooltip("확장 지오메트리 루트 (평소 비활성)")] private GameObject extensionRoot;
        [SerializeField, Tooltip("확장을 켤 때 숨기는 기존 입구 오브젝트 (서쪽 벽, 블록아웃 계단, 걸레받이)")] private GameObject[] stubObjects;
        [SerializeField] private Light[] shaftLights;
        [SerializeField] private Renderer[] lampRenderers;
        [SerializeField] private Color lampEmission = new Color(1f, 0.95f, 0.85f) * 1.6f;
        [SerializeField, Tooltip("이 높이 차(m) 안의 조명만 켠다")] private float activeRange = 7.5f;
        [SerializeField, Range(0f, 1f), Tooltip("정전 중 비상등 밝기 배율")] private float emergencyLevel = 0.3f;
        [SerializeField] private Color emergencyTint = new Color(1f, 0.72f, 0.5f);

        [Header("층 정보 (빌더가 채움)")]
        [SerializeField] private float floorHeight = 3f;
        [SerializeField] private int homeFloor = 10;
        [SerializeField] private int topFloor = 20;
        [SerializeField, Tooltip("1층 출구(지면) 높이")] private float groundY = -28.5f;
        [SerializeField, Tooltip("옥상 바닥 높이")] private float roofY = 33f;

        private float[] baseIntensity;
        private Color[] baseColor;
        private MaterialPropertyBlock block;
        private Transform player;
        private float nextRefresh;
        private int lastMode = -1;

        public bool IsWalkable { get; private set; }
        public float FloorHeight => floorHeight;
        public int HomeFloor => homeFloor;
        public int TopFloor => topFloor;
        public float GroundY => groundY;
        public float RoofY => roofY;

        /// <summary>층 바닥 높이 (10층 = 0)</summary>
        public float FloorY(int floor) => (floor - homeFloor) * floorHeight;

        /// <summary>높이로 층 번호 (지면 근처는 1층, 옥상은 topFloor + 1)</summary>
        public int FloorAt(float y) => Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / floorHeight) + homeFloor, 1, topFloor + 1);

        public static StairwellExtension Find() => FindFirstObjectByType<StairwellExtension>(FindObjectsInactive.Include);

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            int n = shaftLights != null ? shaftLights.Length : 0;
            baseIntensity = new float[n];
            baseColor = new Color[n];
            for (int i = 0; i < n; i++)
            {
                if (shaftLights[i] == null) continue;
                baseIntensity[i] = shaftLights[i].intensity;
                baseColor[i] = shaftLights[i].color;
            }
            IsWalkable = extensionRoot != null && extensionRoot.activeSelf;
        }

        /// <summary>걸어서 오르내리는 계단 켜기/끄기 (켜면 입구 블록아웃이 사라지고 계단탑이 열린다)</summary>
        public void SetWalkable(bool on)
        {
            IsWalkable = on;
            if (extensionRoot != null) extensionRoot.SetActive(on);
            if (stubObjects != null) foreach (GameObject g in stubObjects) if (g != null) g.SetActive(!on);
            lastMode = -1;
            nextRefresh = 0f;
            Physics.SyncTransforms();
        }

        private void Update()
        {
            if (!IsWalkable || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            if (player == null)
            {
                PlayerController p = FindFirstObjectByType<PlayerController>();
                if (p == null) return;
                player = p.transform;
            }
            RefreshLights();
        }

        // 0 = 꺼짐, 1 = 비상등, 2 = 정상
        private int PowerMode()
        {
            LightGroup main = LightGroup.Find("stairwell");
            if (main == null || main.IsOn) return 2;
            LightGroup exit = LightGroup.Find("exit");
            return exit != null && exit.IsOn ? 1 : 0;
        }

        private void RefreshLights()
        {
            int mode = PowerMode();
            float py = player.position.y;
            for (int i = 0; i < baseIntensity.Length; i++)
            {
                Light l = shaftLights[i];
                if (l == null) continue;
                bool near = Mathf.Abs(l.transform.position.y - py) < activeRange;
                l.enabled = mode > 0 && near;
                l.intensity = baseIntensity[i] * (mode == 2 ? 1f : emergencyLevel);
                l.color = mode == 2 ? baseColor[i] : emergencyTint;
            }
            if (mode == lastMode || lampRenderers == null) return;
            lastMode = mode;
            Color e = mode == 2 ? lampEmission : mode == 1 ? emergencyTint * emergencyLevel : Color.black;
            foreach (Renderer r in lampRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, e);
                r.SetPropertyBlock(block);
            }
        }
    }
}
