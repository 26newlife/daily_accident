using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 가까운(기본 3m) 상호작용 대상을 은은하게 밝혀서 "무엇이 상호작용 되는지" 알려 준다.
    /// 셰이더를 쓰지 않고 MaterialPropertyBlock 으로 _BaseColor 를 살짝 밝게 하는 가벼운 방식이며,
    /// 지금 조준 중인 대상은 더 밝게 천천히 맥동한다. 설정(GameSettings.HighlightEnabled)에서 끌 수 있다.
    /// </summary>
    public class InteractableHighlight : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField, Min(0.5f)] private float range = 3f;
        [SerializeField, Range(0f, 1f)] private float nearStrength = 0.12f;
        [SerializeField, Range(0f, 1f)] private float targetStrength = 0.3f;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private Color tint = new Color(0.75f, 0.9f, 1f);
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.1f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private sealed class Item
        {
            public InteractableObject io;
            public Renderer[] renderers;
            public Color[] baseColors;
            public float applied;
        }

        private readonly List<Item> items = new List<Item>();
        private MaterialPropertyBlock block;
        private float nextRefresh;
        private bool built;

        private void Awake() => block = new MaterialPropertyBlock();

        private void OnDisable() { foreach (Item it in items) Apply(it, 0f); }

        private void Update()
        {
            if (!built) Build();
            bool enabledNow = GameSettings.HighlightEnabled && !GameFlow.GameplayBlocked;
            if (Time.unscaledTime < nextRefresh && enabledNow) return;
            nextRefresh = Time.unscaledTime + refreshInterval;

            Vector3 p = transform.position;
            InteractableObject current = interactor != null ? interactor.Current : null;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            foreach (Item it in items)
            {
                if (it.io == null) continue;
                float strength = 0f;
                if (enabledNow && it.io.isActiveAndEnabled && it.io.Interactable && !(it.io is FireSource))
                {
                    float d = Vector3.Distance(p, it.io.transform.position);
                    if (d <= range)
                    {
                        float fade = Mathf.Clamp01(1f - (d - range * 0.5f) / (range * 0.5f));  // 멀어질수록 서서히 사라짐
                        strength = nearStrength * fade;
                        if (it.io == current) strength = Mathf.Lerp(nearStrength, targetStrength, pulse);
                    }
                }
                if (Mathf.Abs(strength - it.applied) > 0.01f || (strength == 0f && it.applied != 0f)) Apply(it, strength);
            }
        }

        private void Build()
        {
            built = true;
            foreach (InteractableObject io in FindObjectsByType<InteractableObject>(FindObjectsSortMode.None))
            {
                if (io is FireSource) continue;
                var list = new List<Renderer>();
                foreach (Renderer r in io.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (r.GetComponentInParent<InteractableObject>() != io) continue;   // 자식 상호작용(예: 도어락)은 따로 처리
                    list.Add(r);
                }
                if (list.Count == 0) continue;
                var it = new Item { io = io, renderers = list.ToArray(), baseColors = new Color[list.Count] };
                for (int i = 0; i < list.Count; i++)
                {
                    Material m = list[i].sharedMaterial;
                    it.baseColors[i] = m == null ? Color.white : m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
                }
                items.Add(it);
            }
        }

        private void Apply(Item it, float strength)
        {
            it.applied = strength;
            for (int i = 0; i < it.renderers.Length; i++)
            {
                Renderer r = it.renderers[i];
                if (r == null) continue;
                if (strength <= 0.001f) { r.SetPropertyBlock(null); continue; }
                Color c = Color.Lerp(it.baseColors[i], tint, strength) + new Color(strength, strength, strength, 0f) * 0.25f;
                c.a = it.baseColors[i].a;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                block.SetColor(ColorId, c);
                r.SetPropertyBlock(block);
            }
        }
    }
}
