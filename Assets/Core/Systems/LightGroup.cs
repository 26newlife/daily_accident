using System.Collections.Generic;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 조명 묶음. 방 단위로 라이트를 묶어 켜기/끄기/밝기 배율을 한 번에 제어한다 (정전·깜빡임 연출용).
    /// LightGroup.SetAllOn(false) 로 전체 정전, Find(id) 로 특정 방만 제어한다.
    /// </summary>
    public class LightGroup : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly List<LightGroup> all = new List<LightGroup>();

        [SerializeField] private string groupId = "light_group";
        [SerializeField] private Light[] lights;
        [Tooltip("켜짐 여부에 따라 발광을 바꿀 전등 외형 (선택)")]
        [SerializeField] private Renderer[] lampRenderers;
        [SerializeField] private Color lampEmission = new Color(1f, 0.93f, 0.75f) * 2f;
        [SerializeField] private bool startOn = true;

        private float[] baseIntensity;
        private float multiplier = 1f;
        private MaterialPropertyBlock block;

        public static IReadOnlyList<LightGroup> All => all;
        public string GroupId => groupId;
        public bool IsOn { get; private set; }

        public static LightGroup Find(string id) => all.Find(g => g.groupId == id);

        public static void SetAllOn(bool on) { foreach (LightGroup g in all) g.SetOn(on); }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            baseIntensity = new float[lights != null ? lights.Length : 0];
            for (int i = 0; i < baseIntensity.Length; i++) if (lights[i] != null) baseIntensity[i] = lights[i].intensity;
            IsOn = startOn;
            Apply();
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        public void SetOn(bool on) { IsOn = on; Apply(); }
        public void Toggle() => SetOn(!IsOn);

        /// <summary>밝기 배율 0~1 (깜빡임/전압 저하 연출용)</summary>
        public void SetIntensityMultiplier(float value) { multiplier = Mathf.Clamp01(value); Apply(); }

        private void Apply()
        {
            if (lights != null)
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] == null) continue;
                    lights[i].enabled = IsOn;
                    lights[i].intensity = baseIntensity[i] * multiplier;
                }
            if (lampRenderers != null)
                foreach (Renderer r in lampRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor(EmissionColorId, IsOn ? lampEmission * multiplier : Color.black);
                    r.SetPropertyBlock(block);
                }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();
    }
}
