using System;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 구조 점수 집계. 점수 항목은 ScoreCategoryData 에셋으로 정의하고 Award(id, 점수)로 가산한다.
    /// 새 항목을 추가하려면 에셋을 만들어 categories 목록에 넣으면 엔딩 화면에 자동으로 표시된다.
    /// </summary>
    public class ScoreSystem : MonoBehaviour
    {
        [Serializable]
        public struct Grade
        {
            [Tooltip("이 점수 이상이면 이 등급")] public int minScore;
            public string label;
        }

        [SerializeField] private ScoreCategoryData[] categories;
        [Tooltip("엔딩 시점 환산 규칙. 지정하면 categories 대신 이 규칙으로 점수·등급·피드백을 계산한다")]
        [SerializeField] private ScoreRules rules;
        [Tooltip("낮은 점수부터 높은 점수 순서로 입력")]
        [SerializeField] private Grade[] grades;

        private readonly Dictionary<string, int> earned = new Dictionary<string, int>();

        public static ScoreSystem Instance { get; private set; }

        /// <summary>시나리오 정의의 점수 규칙으로 교체한다</summary>
        public void SetRules(ScoreRules r) { if (r != null) rules = r; }
        public IReadOnlyList<ScoreCategoryData> Categories => categories;

        public event Action Changed;

        // ───────── 최종 환산 (ScoreRules) ─────────
        public struct Line
        {
            public string id, label, note;
            public int earned, max;
        }

        private readonly List<Line> breakdown = new List<Line>();
        private readonly List<string> goodPoints = new List<string>();
        private readonly List<string> badPoints = new List<string>();
        public bool HasRules => rules != null && rules.rules != null && rules.rules.Length > 0;
        public bool IsFinalized { get; private set; }
        public IReadOnlyList<Line> Breakdown => breakdown;
        public IReadOnlyList<string> GoodPoints => goodPoints;
        public IReadOnlyList<string> BadPoints => badPoints;

        /// <summary>엔딩 시점에 Records / ScenarioFlags 를 ScoreRules 로 환산한다 (여러 번 불러도 결과는 같다)</summary>
        public void FinalizeScore(float elapsedSeconds)
        {
            breakdown.Clear(); goodPoints.Clear(); badPoints.Clear();
            earned.Clear();
            IsFinalized = true;
            if (!HasRules) return;

            foreach (ScoreRules.Rule rule in rules.rules)
            {
                int pts = EvaluateRule(rule, elapsedSeconds);
                earned[rule.id] = pts;
                breakdown.Add(new Line { id = rule.id, label = rule.label, earned = pts, max = rule.maxPoints });
                if (pts >= rule.maxPoints) { if (!string.IsNullOrEmpty(rule.goodText)) goodPoints.Add(rule.goodText); }
                else if (!string.IsNullOrEmpty(rule.badText)) badPoints.Add(rule.badText);
            }
            Changed?.Invoke();
        }

        private int EvaluateRule(ScoreRules.Rule r, float elapsed)
        {
            float v = GetRecord(r.key);
            switch (r.kind)
            {
                case ScoreRules.Kind.FlagTrue: return ScenarioFlags.GetBool(r.key) ? r.maxPoints : 0;
                case ScoreRules.Kind.RecordPresent: return HasRecord(r.key) && v > 0f ? r.maxPoints : 0;
                case ScoreRules.Kind.RecordEquals:
                    if (HasRecord(r.key) && Mathf.Approximately(v, r.threshold)) return r.maxPoints;
                    return !string.IsNullOrEmpty(r.partialKey) && HasRecord(r.partialKey) ? r.partialPoints : 0;
                case ScoreRules.Kind.RecordAtMost:
                    if (!HasRecord(r.key)) return 0;
                    return v <= r.threshold ? r.maxPoints : r.partialPoints;
                case ScoreRules.Kind.RecordZero: return !HasRecord(r.key) || v <= 0f ? r.maxPoints : 0;
                case ScoreRules.Kind.ElapsedAtMost:
                    if (elapsed <= r.threshold) return r.maxPoints;
                    return elapsed <= r.partialThreshold ? r.partialPoints : 0;
                case ScoreRules.Kind.Call119:
                    float res = GetRecord(r.key, 0f);
                    return res >= 2f ? r.maxPoints : (res >= 1f ? r.partialPoints : 0);
            }
            return 0;
        }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>항목 점수를 가산 (항목 최대치를 넘지 않음)</summary>
        public void Award(string categoryId, int points)
        {
            ScoreCategoryData cat = Find(categoryId);
            int max = cat != null ? cat.MaxPoints : int.MaxValue;
            earned.TryGetValue(categoryId, out int cur);
            earned[categoryId] = Mathf.Clamp(cur + points, 0, max);
            Changed?.Invoke();
        }

        // ───────── 행동 기록 (최종 점수 계산 전 원자료. Act 4 에서 이 값을 읽어 점수로 환산한다) ─────────
        private readonly Dictionary<string, float> records = new Dictionary<string, float>();
        public IReadOnlyDictionary<string, float> Records => records;

        public void Record(string key, float value) => records[key] = value;
        public void Increment(string key, float amount = 1f) { records.TryGetValue(key, out float v); records[key] = v + amount; }
        public bool HasRecord(string key) => records.ContainsKey(key);
        public float GetRecord(string key, float fallback = 0f) => records.TryGetValue(key, out float v) ? v : fallback;

        public int GetEarned(string categoryId) => earned.TryGetValue(categoryId, out int v) ? v : 0;

        public int Total
        {
            get { int t = 0; foreach (int v in earned.Values) t += v; return t; }
        }

        public int MaxTotal
        {
            get { int t = 0; if (HasRules) { foreach (ScoreRules.Rule rl in rules.rules) t += rl.maxPoints; return t; } if (categories != null) foreach (ScoreCategoryData c in categories) if (c != null) t += c.MaxPoints; return t; }
        }

        public string GetGradeLabel()
        {
            string result = string.Empty;
            if (HasRules && rules.grades != null)
            {
                float ratio = MaxTotal > 0 ? (float)Total / MaxTotal : 0f;
                foreach (ScoreRules.GradeStep gs in rules.grades) if (ratio >= gs.minRatio) result = gs.label;
                return result;
            }
            if (grades == null) return result;
            int total = Total;
            foreach (Grade g in grades) if (total >= g.minScore) result = g.label;
            return result;
        }

        private ScoreCategoryData Find(string id)
        {
            if (categories == null) return null;
            foreach (ScoreCategoryData c in categories) if (c != null && c.Id == id) return c;
            return null;
        }
    }
}
