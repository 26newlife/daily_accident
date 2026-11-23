using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 생존 상태 패널 (화면 우측 상단 전용). 시나리오가 "행(stat)"과 "줄(line)"을 등록하면 세로로 정렬해 보여준다.
    ///   · 행 = 라벨 + 막대 + 수치 (호흡기, 화상, 체온, 방사능, 쾌적도 …). 숨김 조건(visible)이 거짓이면 그 행은 접힌다.
    ///   · 줄 = 막대 없는 한 줄 문구 (자원 요약 등).
    ///   · 보이는 행이 하나도 없으면 패널 전체가 페이드 아웃된다 ("평소엔 숨기고 위험할 때만" 동작).
    /// 행 간격·정렬은 VerticalLayoutGroup 이 맡으므로 행이 늘어나도 겹치지 않는다.
    /// 시나리오 1(WarHud)과 공통 호흡기(HUD)가 같은 패널을 쓴다. 판이 끝나면(씬 재로드) 함께 사라지므로 등록 해제는 필요 없다.
    /// </summary>
    public class SurvivalStatsPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text titleText;
        [SerializeField, Tooltip("행 견본 (비활성). 자식: Label, Bar(Fill), Value")] private RectTransform rowTemplate;
        [SerializeField, Tooltip("줄 견본 (비활성). TMP_Text")] private TMP_Text lineTemplate;
        [SerializeField] private Image barTemplateFill;
        [SerializeField] private float fadeSpeed = 5f;
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

        public static SurvivalStatsPanel Instance { get; private set; }

        private sealed class Entry
        {
            public string id;
            public GameObject go;
            public Func<bool> visible;
            // 행
            public Func<float> normalized;
            public Func<string> value;
            public Image fill;
            public TMP_Text valueText;
            public Color good, bad;
            public bool highIsBad;
            // 줄
            public Func<string> line;
            public TMP_Text lineText;
        }

        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private readonly List<Entry> order = new List<Entry>();
        private float timer;

        public int VisibleCount { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (lineTemplate != null) lineTemplate.gameObject.SetActive(false);
            if (group != null) group.alpha = 0f;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>막대 행 등록/교체. highIsBad = 수치가 높을수록 위험(방사능·화상) → 가득 찰수록 빨갛게</summary>
        public void SetStat(string id, string label, Func<float> normalized, Func<string> value, Func<bool> visible, Color good, Color bad, bool highIsBad = false)
        {
            if (rowTemplate == null || content == null) return;
            Remove(id);
            var e = new Entry { id = id, normalized = normalized, value = value, visible = visible, good = good, bad = bad, highIsBad = highIsBad };
            e.go = Instantiate(rowTemplate.gameObject, content, false);
            e.go.name = "Stat_" + id;
            Transform t = e.go.transform;
            Transform lab = t.Find("Label"), bar = t.Find("Bar"), val = t.Find("Value");
            if (lab != null) { TMP_Text lt = lab.GetComponent<TMP_Text>(); if (lt != null) lt.text = label; }
            if (bar != null) { Transform f = bar.Find("Fill"); if (f != null) e.fill = f.GetComponent<Image>(); }
            if (val != null) e.valueText = val.GetComponent<TMP_Text>();
            Add(e);
        }

        /// <summary>막대 없는 한 줄 문구 등록/교체</summary>
        public void SetLine(string id, Func<string> text, Func<bool> visible = null)
        {
            if (lineTemplate == null || content == null) return;
            Remove(id);
            var e = new Entry { id = id, line = text, visible = visible };
            GameObject go = Instantiate(lineTemplate.gameObject, content, false);
            go.name = "Line_" + id;
            e.go = go; e.lineText = go.GetComponent<TMP_Text>();
            Add(e);
        }

        public void Remove(string id)
        {
            if (!entries.TryGetValue(id, out Entry e)) return;
            if (e.go != null) Destroy(e.go);
            entries.Remove(id); order.Remove(e);
        }

        public void Clear() { foreach (Entry e in order) if (e.go != null) Destroy(e.go); entries.Clear(); order.Clear(); }

        private void Add(Entry e)
        {
            entries[e.id] = e; order.Add(e);
            e.go.SetActive(false);   // 다음 갱신에서 조건에 맞으면 켠다
            timer = 0f;
        }

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f) { timer = refreshInterval; Refresh(); }
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, VisibleCount > 0 ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
        }

        private void Refresh()
        {
            int shown = 0;
            foreach (Entry e in order)
            {
                bool show = e.visible == null || e.visible();
                if (e.go.activeSelf != show) e.go.SetActive(show);
                if (!show) continue;
                shown++;
                if (e.line != null) { if (e.lineText != null) e.lineText.text = e.line(); continue; }
                float n = Mathf.Clamp01(e.normalized());
                if (e.fill != null) { e.fill.fillAmount = n; e.fill.color = Color.Lerp(e.bad, e.good, e.highIsBad ? 1f - n : n); }
                if (e.valueText != null && e.value != null) e.valueText.text = e.value();
            }
            VisibleCount = shown;
            if (titleText != null) titleText.gameObject.SetActive(shown > 0);
        }
    }
}
