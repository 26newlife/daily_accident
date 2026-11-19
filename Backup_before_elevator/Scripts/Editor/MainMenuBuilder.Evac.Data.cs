using System;
using GameCore;
using GameGameplay;
using UnityEditor;
using UnityEngine;

namespace GameUI.EditorTools
{
    /// <summary>
    /// Act 3~4(복도·탈출) 데이터 에셋 생성: 아이템(망치/옥상 열쇠), 새 사망 사유 교육 피드백, 엔딩 문구(+히든), 목표, ScoreRules.
    /// 모두 "없을 때만 생성" 규칙을 따른다 (이미 있으면 사용자가 고친 값 보존). 예외: 엔딩 설명이 "(작성 예정)" 자리표시인 경우에만 문구를 채워 넣는다.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string ScoreRulesPath = "Assets/Data/ScoreRules.asset";

        private static void EnsureEvacData(ref GameplayAssets d)
        {
            // ── 아이템 ──
            d.hammer = EnsureItem("Item_Hammer", EvacKeys.ItemHammer, "망치", "다용도실 공구함의 망치. 석고보드 경량칸막이를 부수는 데 쓴다.", false, "B91C1C");
            d.roofKey = EnsureItem("Item_RoofKey", EvacKeys.ItemRoofKey, "옥상 열쇠", "20층 비상 열쇠함에 들어 있던 옥상 출입문 열쇠.", false, "FACC15");
            // 청테이프는 2회 사용 (문틈 두 면). 기존 에셋이 1회라면 2회로 올린다
            var tapeSo = new SerializedObject(d.tape);
            SerializedProperty uses = tapeSo.FindProperty("maxUses");
            if (uses != null && uses.intValue < 2) { uses.intValue = 2; tapeSo.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(d.tape); }

            // ── 새 사망 사유 (DeathReason 과 같이 추가) ──
            var deaths = new System.Collections.Generic.List<DeathReasonData>(d.deaths)
            {
                EnsureDeath("Death_StairwellSmoke", DeathReason.StairwellSmoke, "계단 연기 질식",
                    "화재가 난 층의 연기와 열기는 비상계단(굴뚝처럼 작동)을 타고 아주 빠르게 위로 올라옵니다. 연기가 찬 계단을 서서 내려가며 숨을 들이마셔 의식을 잃었습니다.",
                    "방화문을 열기 전에 손등으로 온도를 확인하고, 아래층에서 연기가 올라오면 내려가지 말고 위층(옥상)이나 대피공간으로 이동하세요. 부득이하게 내려갈 때는 몸을 낮추고 젖은 수건으로 코와 입을 막은 채 벽을 짚고 이동합니다."),
                EnsureDeath("Death_ShelterSmoke", DeathReason.ShelterSmoke, "대피공간 연기 유입",
                    "대피공간의 방화문 틈을 막지 않으면 연기가 문틈으로 스며듭니다. 문틈을 밀봉하지도, 환기하지도 않은 채 오래 머물러 연기를 마셨습니다.",
                    "대피공간에 들어가 방화문을 닫고, 젖은 수건과 청테이프로 문틈 4면(아래·양옆·위)을 막으세요. 창문을 열어 신선한 공기를 확보하고, 119에 \"1004호 대피공간\"처럼 정확한 위치를 알린 뒤 낮은 자세로 구조를 기다립니다."),
                EnsureDeath("Death_ExitTimeout", DeathReason.ExitTimeout, "대피 시기를 놓침",
                    "어떤 경로로 대피할지 정하지 못하고 시간을 보내는 사이 연기와 열기가 집 안을 가득 채웠습니다.",
                    "화재의 골든타임은 매우 짧습니다. 현관 손잡이 온도와 계단 연기를 확인해 가능한 경로를 빨리 정하세요. 마땅한 경로가 없다면 대피공간에서 문틈을 막고 구조를 기다리는 것이 가장 안전합니다."),
            };
            d.deaths = deaths.ToArray();

            // ── 엔딩: 자리표시 문구 교체 + 히든 엔딩 ──
            PatchEnding("Ending_Rooftop", "옥상 대피", "루트 A · 옥상",
                "아래층 연기가 올라오는 것을 확인하고 위쪽으로 향했다. 14층과 17층의 방화문을 닫아 연기를 늦추고, 20층 비상 열쇠함의 유리를 깨 옥상 열쇠를 얻어 옥상으로 나왔다. 두 팔을 흔들어 보낸 신호를 본 헬기와 고가사다리차가 도착해 구조되었다.");
            PatchEnding("Ending_Partition", "경량칸막이 대피", "루트 B · 경량칸막이",
                "현관이 불길에 막힌 것을 손등으로 확인하고 베란다로 향했다. 얇은 석고보드 칸막이를 부수고 옆집으로 넘어가 연기 속에 쓰러진 이웃을 부축해, 옆집 현관을 통해 계단으로 탈출했다.");
            PatchEnding("Ending_Shelter", "대피공간 농성", "루트 C · 대피공간",
                "방화문을 닫고 젖은 수건과 청테이프로 문틈을 막았다. 창문을 열어 신선한 공기를 확보하고 119에 정확한 위치를 알린 뒤 낮은 자세로 버텼고, 고가사다리차가 창문 앞에 도착해 구조되었다.");
            PatchEnding("Ending_Descent", "1층 하강 탈출", "루트 D · 계단 하강",
                "방화문을 닫아 둔 덕에 연기가 계단에 오르기 전에 움직일 수 있었다. 몸을 낮추고 벽을 짚으며 1층까지 내려와 외부 집결지에서 소방대원과 이웃들을 만났다.");
            EndingData hidden = Ensure<EndingData>($"{EndingDir}/Ending_HiddenGrandmother.asset", so =>
            {
                so.FindProperty("id").stringValue = EvacKeys.EndingHidden;
                so.FindProperty("endingName").stringValue = "함께 살아남다";
                so.FindProperty("description").stringValue = "혼자 움직였다면 더 빨랐을지도 모른다. 하지만 당신은 이웃 할머니의 손을 놓지 않았다. 느린 걸음에 보폭을 맞추고, 젖은 수건을 나누고, 끝까지 함께 대피했다. 모두가 살아남았다.";
                so.FindProperty("isSurvival").boolValue = true;
                so.FindProperty("isHidden").boolValue = true;
                so.FindProperty("routeLabel").stringValue = "히든 · 이웃 동행 구조";
            });
            var ends = new System.Collections.Generic.List<EndingData>(d.endings);
            if (!ends.Contains(hidden)) ends.Add(hidden);
            d.endings = ends.ToArray();
            d.hiddenEnding = hidden;

            // ── 목표 (Act 3 / Act 4) ──
            var list = new System.Collections.Generic.List<ObjectiveData>();
            ObjectiveData Ob(string id, string title, bool main = false) { var o = EnsureObjective("Evac_" + id, id, title, main); list.Add(o); return o; }
            Ob("act3_main", "비상 대피 경로 판단하기", true);
            Ob("act3_corridor", "복도로 나가 현관문 닫기");
            Ob("act3_neighbors", "복도의 이웃 돕기 (엘리베이터는 타지 않기)");
            Ob("act3_stairs", "비상계단 방화문을 열어 아래층 상황 확인하기");
            Ob("act3_assess", "대피공간과 베란다 상황 살펴보기");
            Ob("act4_main", "탈출 루트를 정하고 끝까지 실행하기", true);
            Ob("act4_choose", "대피 방법 결정하기 (옥상 / 칸막이 / 대피공간 / 계단 하강)");
            Ob("act4_a_stairs", "[옥상] 비상계단으로 위층까지 올라가기");
            Ob("act4_a_close14", "[옥상] 14층 열린 방화문 닫기");
            Ob("act4_a_close17", "[옥상] 17층 열린 방화문 닫기");
            Ob("act4_a_key", "[옥상] 20층 비상 열쇠함 유리를 깨고 열쇠 꺼내기");
            Ob("act4_a_roof", "[옥상] 옥상 문을 열고 구조 신호 보내기");
            Ob("act4_b_clear", "[칸막이] 베란다 화분·짐 상자 치우기");
            Ob("act4_b_break", "[칸막이] 경량칸막이 부수기 (망치 또는 발차기)");
            Ob("act4_b_neighbor", "[칸막이] 1003호의 할머니 부축해 함께 이동하기");
            Ob("act4_b_exit", "[칸막이] 1003호 현관으로 나가 계단으로 탈출하기");
            Ob("act4_c_door", "[대피공간] 방화문 닫기");
            Ob("act4_c_seal", "[대피공간] 문틈 4면 막기 (젖은 수건·청테이프)");
            Ob("act4_c_window", "[대피공간] 창문을 열어 환기하기");
            Ob("act4_c_call", "[대피공간] 119 에 정확한 위치 신고하기");
            Ob("act4_c_wait", "[대피공간] 낮은 자세로 구조대 기다리기");
            Ob("act4_d_down", "[하강] 낮은 자세로 벽을 짚고 계단 내려가기");
            Ob("act4_d_assembly", "[하강] 외부 집결지로 이동하기");
            d.evacObjectives = list.ToArray();

            // ── 점수 규칙 ──
            d.scoreRules = EnsureScoreRules();
        }

        private static void PatchEnding(string file, string name, string routeLabel, string desc)
        {
            var e = AssetDatabase.LoadAssetAtPath<EndingData>($"{EndingDir}/{file}.asset");
            if (e == null) return;
            var so = new SerializedObject(e);
            SerializedProperty d = so.FindProperty("description");
            bool placeholder = string.IsNullOrEmpty(d.stringValue) || d.stringValue.Contains("작성 예정");
            if (placeholder) d.stringValue = desc;
            SerializedProperty rl = so.FindProperty("routeLabel");
            if (rl != null && string.IsNullOrEmpty(rl.stringValue)) rl.stringValue = routeLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(e);
        }

        private static ScoreRules EnsureScoreRules()
        {
            return Ensure<ScoreRules>(ScoreRulesPath, so =>
            {
                var rules = new[]
                {
                    R("valve_order", "가스 차단 순서", 10, ScoreRules.Kind.RecordEquals, "act1_fuse_close_order", 1f, 5, "act1_main_close_order", 0f,
                      "주방 퓨즈콕부터 잠가 가스 유입을 먼저 막았다.", "가스는 주방 중간 밸브(퓨즈콕)를 먼저, 이어서 다용도실 메인 밸브를 잠그는 순서가 안전하다."),
                    R("ventilation", "환기 완료", 10, ScoreRules.Kind.RecordPresent, "act1_ventilation_done_time", 0f, 0, "", 0f,
                      "창문 3개를 열어 맞통풍으로 가스를 빼냈다.", "가스 냄새가 나면 창문을 열어 환기해야 한다. 잠금장치를 풀고 거실·주방·안방 창문을 모두 열자."),
                    R("multitap", "멀티탭 처리", 10, ScoreRules.Kind.RecordPresent, "act1_multitap_handled_correctly", 0f, 0, "", 0f,
                      "주변 가연물을 치우고 코드 본체를 뽑아 과열된 멀티탭을 처리했다.", "과열된 멀티탭은 전원 스위치를 누르지 말고 주변 가연물을 치운 뒤 플러그 본체를 잡고 뽑아야 한다."),
                    R("no_danger", "방송 후 위험 행동 없음", 10, ScoreRules.Kind.RecordZero, "act1_danger_attempts_after_broadcast", 0f, 0, "", 0f,
                      "긴급 방송 뒤 전등·가전 스위치를 건드리지 않았다.", "가스 누출 방송 뒤에는 전등·가전·화기 조작을 하면 안 된다. 작은 불꽃도 폭발로 이어진다."),
                    R("peephole", "문구멍 확인", 5, ScoreRules.Kind.RecordPresent, "peephole_checked", 0f, 0, "", 0f,
                      "문을 열기 전에 문구멍으로 복도를 살폈다.", "문을 열기 전에 문구멍으로 복도 상황(연기·불빛)을 먼저 살피자."),
                    R("handle_check", "손잡이 온도 확인", 10, ScoreRules.Kind.RecordPresent, "act2_handle_check_time", 0f, 0, "", 0f,
                      "손등으로 현관 손잡이 온도를 확인한 뒤 행동했다.", "문을 열기 전에는 반드시 손등으로 손잡이 온도를 확인해야 한다. 뜨거우면 열지 않는다."),
                    R("towel", "젖은 수건 사용", 10, ScoreRules.Kind.RecordPresent, "act2_wet_towel_time", 0f, 0, "", 0f,
                      "젖은 수건으로 코와 입을 막아 연기를 막았다.", "연기가 나면 젖은 수건으로 코와 입을 막아야 한다. 화장실에서 수건을 적셔 두자."),
                    R("neighbor_grandmother", "이웃 구조 (할머니)", 10, ScoreRules.Kind.FlagTrue, EvacKeys.FlagGrandmotherWith, 0f, 0, "", 0f,
                      "1003호 할머니를 설득해 끝까지 함께 대피했다.", "엘리베이터 앞의 할머니를 설득하고 젖은 수건을 건네 함께 계단으로 대피할 수 있었다."),
                    R("neighbor_youngman", "이웃 구조 (청년)", 5, ScoreRules.Kind.FlagTrue, EvacKeys.FlagYoungManSaved, 0f, 0, "", 0f,
                      "수건 없이 계단으로 달려가던 청년을 설득해 함께 대피했다.", "수건 없이 연기 찬 계단으로 달려가는 이웃은 말려야 한다. 수건을 나눠 주고 함께 움직이자."),
                    R("fire_doors", "방화문 닫기", 10, ScoreRules.Kind.FlagTrue, EvacKeys.FlagFireDoorManaged, 0f, 0, "", 0f,
                      "현관문과 방화문을 닫아 연기 확산을 늦췄다.", "지나온 현관문·방화문은 닫아야 연기가 따라오지 못한다. 열린 방화문은 연기의 통로가 된다."),
                    R("call119", "119 신고 정확도", 10, ScoreRules.Kind.Call119, EvacKeys.RecCall119, 0f, 3, "", 0f,
                      "119 에 \"1004호 대피공간\"처럼 정확한 위치를 알렸다.", "119 에는 호수·층·현재 있는 장소를 정확히 말해야 구조가 빨라진다. 위치를 잘못 말하거나 신고하지 않으면 구조가 늦어진다."),
                    R("no_elevator", "엘리베이터 미사용", 5, ScoreRules.Kind.RecordZero, "risk_elevator_attempts", 0f, 0, "", 0f,
                      "화재 중 엘리베이터를 사용하지 않았다.", "화재 중 엘리베이터는 정전·승강로 연기로 매우 위험하다. 절대 이용하지 말고 계단을 쓰자."),
                    R("route_safe", "안전한 경로 선택", 10, ScoreRules.Kind.FlagTrue, EvacKeys.FlagRouteSafe, 0f, 0, "", 0f,
                      "손잡이 온도·계단 연기·방화문 상태를 종합해 안전한 경로를 골랐다.", "손잡이 온도와 계단 연기를 확인해 그 상황에서 가능한 경로를 골라야 한다. 불가능한 경로는 큰 위험이 따른다."),
                    R("time", "소요 시간", 5, ScoreRules.Kind.ElapsedAtMost, "", 420f, 2, "", 540f,
                      "7분 이내에 탈출했다.", "화재에서는 판단과 행동이 빠를수록 안전하다. 망설이는 시간을 줄여 보자."),
                };

                SerializedProperty arr = so.FindProperty("rules");
                arr.arraySize = rules.Length;
                for (int i = 0; i < rules.Length; i++)
                {
                    SerializedProperty e = arr.GetArrayElementAtIndex(i);
                    ScoreRules.Rule r = rules[i];
                    e.FindPropertyRelative("id").stringValue = r.id;
                    e.FindPropertyRelative("label").stringValue = r.label;
                    e.FindPropertyRelative("maxPoints").intValue = r.maxPoints;
                    e.FindPropertyRelative("kind").enumValueIndex = (int)r.kind;
                    e.FindPropertyRelative("key").stringValue = r.key;
                    e.FindPropertyRelative("threshold").floatValue = r.threshold;
                    e.FindPropertyRelative("partialPoints").intValue = r.partialPoints;
                    e.FindPropertyRelative("partialKey").stringValue = r.partialKey;
                    e.FindPropertyRelative("partialThreshold").floatValue = r.partialThreshold;
                    e.FindPropertyRelative("goodText").stringValue = r.goodText;
                    e.FindPropertyRelative("badText").stringValue = r.badText;
                }
                // 등급: C(0%) / B(55%) / A(75%) / S(90%)
                SerializedProperty gr = so.FindProperty("grades");
                string[] gl = { "C", "B", "A", "S" };
                float[] gm = { 0f, 0.55f, 0.75f, 0.9f };
                gr.arraySize = gl.Length;
                for (int i = 0; i < gl.Length; i++)
                {
                    SerializedProperty e = gr.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("minRatio").floatValue = gm[i];
                    e.FindPropertyRelative("label").stringValue = gl[i];
                }
            });
        }

        private static ScoreRules.Rule R(string id, string label, int max, ScoreRules.Kind kind, string key, float thr, int partial, string partialKey, float partialThr, string good, string bad)
            => new ScoreRules.Rule { id = id, label = label, maxPoints = max, kind = kind, key = key, threshold = thr, partialPoints = partial, partialKey = partialKey, partialThreshold = partialThr, goodText = good, badText = bad };
    }
}
