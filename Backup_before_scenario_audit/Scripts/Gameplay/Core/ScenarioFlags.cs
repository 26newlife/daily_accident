using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 시나리오 진행 플래그 저장소. Act 가 결과(루트 분기, 막힌 출구 등)를 기록하면 이후 Act 가 읽어서 분기한다.
    /// 예) Act 2 가 "act3_front_door_blocked" 를 true 로 두면 Act 3 는 현관 대피 대신 농성 루트(3-B)로 진행.
    /// </summary>
    public static class ScenarioFlags
    {
        private static readonly Dictionary<string, float> numbers = new Dictionary<string, float>();
        private static readonly Dictionary<string, string> texts = new Dictionary<string, string>();

        /// <summary>플래그가 바뀔 때 (키)</summary>
        public static event Action<string> Changed;

        public static void Set(string key, bool value) => SetNumber(key, value ? 1f : 0f);
        public static void SetNumber(string key, float value) { numbers[key] = value; Changed?.Invoke(key); }
        public static void SetText(string key, string value) { texts[key] = value; Changed?.Invoke(key); }

        public static bool GetBool(string key) => numbers.TryGetValue(key, out float v) && v > 0.5f;
        public static float GetNumber(string key, float fallback = 0f) => numbers.TryGetValue(key, out float v) ? v : fallback;
        public static string GetText(string key, string fallback = "") => texts.TryGetValue(key, out string v) ? v : fallback;
        public static bool Has(string key) => numbers.ContainsKey(key) || texts.ContainsKey(key);

        public static void Clear() { numbers.Clear(); texts.Clear(); }

        /// <summary>새 판 시작/재시작 시: 값과 변경 이벤트 구독을 모두 지운다 (GameRunContext)</summary>
        public static void ResetRun() { numbers.Clear(); texts.Clear(); Changed = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { numbers.Clear(); texts.Clear(); Changed = null; }
    }
}
