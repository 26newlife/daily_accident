using System.Text;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>가능한 탈출 루트 계산 결과 (A 옥상 / B 경량칸막이 / C 대피공간 농성 / D 계단 하강)</summary>
    public struct RouteOptions
    {
        public bool a, b, c, d;
        public bool hasTowel, hasTape, hasHammer;

        public bool IsAvailable(char route)
        {
            switch (char.ToUpperInvariant(route))
            {
                case 'A': return a;
                case 'B': return b;
                case 'C': return c;
                case 'D': return d;
            }
            return false;
        }

        public override string ToString() => $"A={a} B={b} C={c} D={d} (towel={hasTowel} tape={hasTape} hammer={hasHammer})";
    }

    /// <summary>
    /// 루트 선택 판정. Act 1~3 의 기록(손잡이 온도, 계단 연기, 방화문 관리 상태)과 아이템 보유(수건·청테이프·망치)로
    /// "지금 안전하게 갈 수 있는 루트"를 계산한다. 플레이어의 선택은 막지 않는다 —
    /// 계산 결과는 안내(목표 문구)와 점수(안전한 경로 선택)에만 쓰이고, 불가능한 루트를 고르면 실제 연기·열기 결과가 나온다.
    ///
    ///   손잡이가 뜨겁다                         → B(경량칸막이) 또는 C(대피공간)
    ///   계단 연기로 하강 불가 + 복도 진입 성공  → A(옥상) 또는 C(복귀 후 농성)
    ///   초기 방화문 관리 성공 + 연기 유입 전    → D(1층 하강)
    ///   C 는 언제나 마지막 수단으로 가능하다 (대피공간이 있는 세대)
    /// </summary>
    public static class RouteResolver
    {
        public static RouteOptions Evaluate()
        {
            var o = new RouteOptions();
            bool hot = ScenarioFlags.GetBool(EvacKeys.FlagHandleHot);
            bool corridor = ScenarioFlags.GetBool(EvacKeys.FlagInCorridor);
            bool stairBlocked = ScenarioFlags.GetBool(EvacKeys.FlagStairDownBlocked);
            bool early = ScenarioFlags.GetBool(EvacKeys.FlagEarlyFireDoor);

            o.b = hot;
            o.a = !hot && corridor && stairBlocked;
            o.d = !hot && corridor && early && !stairBlocked;
            o.c = true;
            // 복도에 나갔지만 계단 상태를 확인하지 못한 경우(시간 초과 등): 위로 올라가는 A 는 가능하다고 본다
            if (!hot && corridor && !stairBlocked && !early) o.a = true;

            InventorySystem inv = InventorySystem.Instance;
            if (inv != null)
            {
                o.hasTowel = inv.Has(EvacKeys.ItemWetTowel);
                o.hasTape = inv.Has(EvacKeys.ItemTape);
                o.hasHammer = inv.Has(EvacKeys.ItemHammer);
            }
            return o;
        }

        /// <summary>목표/안내 문구용 요약</summary>
        public static string Describe(RouteOptions o)
        {
            var sb = new StringBuilder();
            if (o.d) sb.Append("계단에 연기가 올라오기 전이다 — 낮은 자세로 1층까지 내려갈 수 있다(루트 D). ");
            if (o.a) sb.Append("아래층 연기가 올라온다 — 위로 올라가 옥상에서 구조를 기다린다(루트 A). ");
            if (o.b) sb.Append("현관으로는 나갈 수 없다 — 베란다 경량칸막이를 부수고 옆집으로 대피한다(루트 B). ");
            sb.Append(o.hasTape || o.hasTowel ? "대피공간에서 문틈을 막고 구조를 기다릴 수도 있다(루트 C)." : "대피공간(루트 C)은 문틈을 막을 청테이프·젖은 수건이 필요하다.");
            return sb.ToString();
        }
    }

    /// <summary>Act 3·4 가 공통으로 쓰는 위험 판정</summary>
    public static class EvacHazards
    {
        public static FireSource CorridorFire()
        {
            foreach (FireSource f in FireSource.All) if (f != null && f.Id == "fire_corridor") return f;
            return null;
        }

        /// <summary>
        /// 복도에 불이 붙은 상태에서 현관문을 열면 백드래프트 사망 (Act 2 와 동일한 판정 — 막지 않고 결과로 이어진다).
        /// 사망 처리했으면 true.
        /// </summary>
        public static bool CheckHotFrontDoor(ScenarioEvent e)
        {
            if (e.Type != ScenarioEventType.Toggled || e.SourceId != EvacKeys.FrontDoor || !e.State) return false;
            FireSource f = CorridorFire();
            if (f == null || !f.IsLit) return false;
            if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(new Color(1f, 0.5f, 0.1f), 1f, 1.5f);
            if (ScoreSystem.Instance != null) { ScoreSystem.Instance.Increment("risk_backdraft_attempts"); ScoreSystem.Instance.Increment("risk_attempts_total"); }
            Debug.Log("[Risk] hot front door opened (Act 3/4) → backdraft");
            EvacDirector.Die(DeathReason.DoorBackdraft);
            return true;
        }
    }
}
