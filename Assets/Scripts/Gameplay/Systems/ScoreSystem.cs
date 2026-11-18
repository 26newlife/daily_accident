using System;
using System.Collections.Generic;
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
        [Tooltip("낮은 점수부터 높은 점수 순서로 입력")]
        [SerializeField] private Grade[] grades;

        private readonly Dictionary<string, int> earned = new Dictionary<string, int>();

        public static ScoreSystem Instance { get; private set; }
        public IReadOnlyList<ScoreCategoryData> Categories => categories;

        public event Action Changed;

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
            get { int t = 0; if (categories != null) foreach (ScoreCategoryData c in categories) if (c != null) t += c.MaxPoints; return t; }
        }

        public string GetGradeLabel()
        {
            string result = string.Empty;
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
