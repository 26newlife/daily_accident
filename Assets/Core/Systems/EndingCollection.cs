using System.Collections.Generic;
using System.Text;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 엔딩/사망 엔딩 모음 (영구 저장, PlayerPrefs). 판이 바뀌어도 유지되는 "수집 기록"이며 게임 진행 상태와는 무관하다.
    /// 사망 화면과 엔딩 화면이 "발견한 사망 엔딩 n / N" 목록을 보여 줄 때 쓴다.
    /// </summary>
    public static class EndingCollection
    {
        private const string Key = "ending_collection_v1";
        private static HashSet<string> cache;

        private static HashSet<string> Load()
        {
            if (cache != null) return cache;
            cache = new HashSet<string>();
            try
            {
                string raw = PlayerPrefs.GetString(Key, string.Empty);
                foreach (string s in raw.Split(',')) if (!string.IsNullOrEmpty(s)) cache.Add(s);
            }
            catch { /* PlayerPrefs 를 못 읽어도 게임은 진행 */ }
            return cache;
        }

        public static bool Has(string id) => Load().Contains(id);
        public static string DeathId(DeathReason reason) => "death_" + reason;
        public static string EndingId(string endingId) => "ending_" + endingId;

        public static void Add(string id)
        {
            if (string.IsNullOrEmpty(id) || !Load().Add(id)) return;
            try { PlayerPrefs.SetString(Key, string.Join(",", cache)); PlayerPrefs.Save(); } catch { }
        }

        /// <summary>디버그: 수집 기록 지우기</summary>
        public static void ClearAll()
        {
            cache = new HashSet<string>();
            try { PlayerPrefs.DeleteKey(Key); } catch { }
        }

        /// <summary>사망 엔딩/엔딩 모음 문구. 미발견 항목은 ???</summary>
        public static string BuildText(IReadOnlyList<DeathReasonData> deaths, IReadOnlyList<EndingData> endings)
        {
            var sb = new StringBuilder();
            int found = 0, total = 0;
            var lines = new StringBuilder();
            if (deaths != null)
                foreach (DeathReasonData d in deaths)
                {
                    if (d == null || d.Reason == DeathReason.Unknown) continue;
                    total++;
                    bool has = Has(DeathId(d.Reason));
                    if (has) found++;
                    lines.Append(has ? $"· {d.Title}   " : "· ???   ");
                }
            sb.AppendLine($"사망 엔딩 모음  {found} / {total}");
            sb.AppendLine(lines.ToString().TrimEnd());
            if (endings != null && endings.Count > 0)
            {
                int ef = 0, et = 0;
                var el = new StringBuilder();
                foreach (EndingData e in endings)
                {
                    if (e == null || !e.IsSurvival) continue;
                    et++;
                    bool has = Has(EndingId(e.Id));
                    if (has) ef++;
                    el.Append(has ? $"★ {e.EndingName}   " : (e.IsHidden ? "★ ???   " : "☆ ???   "));
                }
                sb.AppendLine();
                sb.AppendLine($"생존 엔딩 모음  {ef} / {et}");
                sb.Append(el.ToString().TrimEnd());
            }
            return sb.ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => cache = null;
    }
}
