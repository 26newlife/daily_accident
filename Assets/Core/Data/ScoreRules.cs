using System;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 구조 점수 규칙 모음 (ScriptableObject). 항목별 배점·판정 방식·피드백 문구를 Inspector 에서 조절한다.
    /// ScoreSystem.FinalizeScore 가 엔딩 시점에 ScoreSystem.Records / ScenarioFlags 를 읽어 이 규칙으로 환산한다.
    /// Create > GameGameplay > Score Rules  (기본 에셋: Assets/Data/ScoreRules.asset — 빌더가 생성)
    /// </summary>
    [CreateAssetMenu(fileName = "ScoreRules", menuName = "GameGameplay/Score Rules", order = 5)]
    public class ScoreRules : ScriptableObject
    {
        public enum Kind
        {
            FlagTrue = 0,        // ScenarioFlags[key] 가 true 면 만점
            RecordPresent = 1,   // ScoreSystem.Records 에 key 가 있고 0 보다 크면 만점
            RecordEquals = 2,    // Records[key] == threshold 면 만점 (partialKey 가 기록돼 있으면 부분 점수)
            RecordAtMost = 3,    // Records[key] 가 있고 <= threshold 면 만점 (있지만 초과면 부분 점수)
            RecordZero = 4,      // Records[key] 가 없거나 0 이면 만점 (위험 행동을 안 했는가) — 횟수가 있으면 0점
            ElapsedAtMost = 5,   // 소요 시간(실시간 초) <= threshold 면 만점, <= partialThreshold 면 부분 점수
            Call119 = 6,         // Records["call119_result"]: 2 = 정확한 위치 만점, 1 = 잘못 말함 부분 점수, 0/없음 = 0
        }

        [Serializable]
        public struct Rule
        {
            public string id;
            public string label;
            [Min(0)] public int maxPoints;
            public Kind kind;
            [Tooltip("ScenarioFlags 키 또는 ScoreSystem.Records 키")] public string key;
            public float threshold;
            [Tooltip("부분 점수 (0 이면 부분 점수 없음)")] [Min(0)] public int partialPoints;
            [Tooltip("RecordEquals: 이 기록이 있으면 부분 점수 / ElapsedAtMost: 부분 점수 시간 한도")] public string partialKey;
            public float partialThreshold;
            [TextArea(1, 2), Tooltip("만점일 때 '잘한 점' 문구")] public string goodText;
            [TextArea(1, 2), Tooltip("만점이 아닐 때 '아쉬운 점' 교육 문구")] public string badText;
        }

        [Serializable]
        public struct GradeStep
        {
            [Range(0f, 1f), Tooltip("만점 대비 비율 이상이면 이 등급")] public float minRatio;
            public string label;
        }

        public Rule[] rules;
        [Tooltip("낮은 비율부터 높은 비율 순서로")] public GradeStep[] grades =
        {
            new GradeStep { minRatio = 0f, label = "C" },
            new GradeStep { minRatio = 0.55f, label = "B" },
            new GradeStep { minRatio = 0.75f, label = "A" },
            new GradeStep { minRatio = 0.9f, label = "S" },
        };
    }
}
