using System.Text;
using QuakeSurvival.Safety;

namespace QuakeSurvival.UI
{
    /// <summary>
    /// EvacuationReport를 등급(S/A/B/C)과 수칙 체크 목록 문자열로 만든다.
    /// 등급은 필수 수칙 점수 비율로 정하고, 보너스는 같은 등급 구간 안에서 한 단계를 올릴 때만 쓴다.
    /// S는 필수 수칙을 모두 지켜야만 받을 수 있다.
    /// 대피지역 덕분에 넘긴 사고(감점)는 필수 수칙 점수에서 빼므로, 하나라도 있으면 S를 받을 수 없다.
    /// </summary>
    public static class ResultFormatter
    {
        public static string Grade(EvacuationReport report)
        {
            float required = RequiredRatio(report);
            if (required >= 1f) return "S";

            // 보너스는 최대 0.1까지만 더한다(필수 수칙을 대신할 수 없음)
            float score = required + System.Math.Min(BonusRatio(report), 1f) * 0.1f;
            if (score >= 0.85f) return "A";
            if (score >= 0.6f) return "B";
            return "C";
        }

        /// <summary>(지킨 필수 수칙 점수 - 감점) / 필수 수칙 만점. 0 아래로는 내려가지 않는다.</summary>
        public static float RequiredRatio(EvacuationReport report)
        {
            int max = MaxPoints(report, bonus: false);
            if (max == 0) return 0f;
            int got = GotPoints(report, bonus: false) - PenaltyPoints(report);
            return System.Math.Max(0f, (float)got / max);
        }

        public static float BonusRatio(EvacuationReport report)
        {
            int max = MaxPoints(report, bonus: true);
            return max == 0 ? 0f : (float)GotPoints(report, bonus: true) / max;
        }

        public static int PenaltyPoints(EvacuationReport report)
        {
            int sum = 0;
            foreach (var p in report.Penalties) sum += p.Points;
            return sum;
        }

        private static int MaxPoints(EvacuationReport report, bool bonus)
        {
            int sum = 0;
            foreach (var r in report.Rules)
            {
                if (r.Bonus == bonus) sum += r.Points;
            }
            return sum;
        }

        private static int GotPoints(EvacuationReport report, bool bonus)
        {
            int sum = 0;
            foreach (var r in report.Rules)
            {
                if (r.Bonus == bonus && r.Kept) sum += r.Points;
            }
            return sum;
        }

        public static string Body(EvacuationReport report)
        {
            var sb = new StringBuilder();
            sb.Append("등급 ").Append(Grade(report));
            int minutes = (int)(report.ElapsedSeconds / 60f);
            int seconds = (int)(report.ElapsedSeconds % 60f);
            sb.Append("    대피 시간 ").Append(minutes).Append("분 ").Append(seconds.ToString("00")).Append("초\n\n");

            foreach (var r in report.Rules)
            {
                if (r.Bonus) continue;
                sb.Append(r.Kept ? "[O] " : "[X] ").Append(r.Text).Append('\n');
            }
            sb.Append('\n');
            foreach (var r in report.Rules)
            {
                if (!r.Bonus) continue;
                sb.Append(r.Kept ? "[+] " : "[ ] ").Append(r.Text).Append('\n');
            }
            AppendPenalties(sb, report);
            return sb.ToString();
        }

        private static void AppendPenalties(StringBuilder sb, EvacuationReport report)
        {
            if (report.Penalties.Count == 0) return;
            sb.Append('\n');
            foreach (var p in report.Penalties)
            {
                sb.Append("[-").Append(p.Points).Append("] ").Append(p.Text).Append('\n');
            }
        }
    }
}
