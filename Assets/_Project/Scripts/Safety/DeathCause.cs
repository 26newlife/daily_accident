using System.Collections.Generic;

namespace QuakeSurvival.Safety
{
    /// <summary>사망 원인. 게임오버 화면은 정답이 아니라 힌트만 보여준다.</summary>
    public enum DeathCause
    {
        None,
        FallingObjects,  // 본진 중 몸을 보호하지 않음
        GasExplosion,    // 가스 밸브를 잠그지 않음
        ElectricFire,    // 차단기를 내리지 않음(빗물 누수 + 누전)
        TrappedInside,   // 문을 열어 출구를 확보하지 않음
        StairFall,       // 계단에서 본진 중 자세를 낮추지 않음
        ElevatorTrapped, // 엘리베이터 이용
        FacadeDebris,    // 여진 때 건물 외벽 근처에 있음
        Tsunami,         // 지진해일에 휩쓸림
        BuildingExplosion, // 본진 뒤 건물 폭발 때 아직 건물 안에 있음
        ExplosionDebris, // 폭발 잔해에 맞음(건물 가까이에 있음)
        Sinkhole,        // 땅이 꺼진 싱크홀에 빠짐
        UpperFloorCollapse, // 10층에서 위층(11층)으로 올라감 - 무너지는 위층에 깔림
        FellWhileShaking,   // 땅이 흔들리는 동안 서서 돌아다님
        GroundCrack         // 갈라진 땅 틈을 밟음
    }

    /// <summary>클리어 방식.</summary>
    public enum ClearKind
    {
        None,
        Shelter,   // 고지대 대피소 도착
        Vertical,  // 대피소에 못 갔지만 물이 닿지 않는 높은 곳에서 버팀
        Protected  // 대피지역(무적 구역) 안이라 죽을 위기를 넘김(결과에서 감점)
    }

    public readonly struct DeathInfo
    {
        public readonly string Title;
        public readonly string Hint;

        public DeathInfo(string title, string hint)
        {
            Title = title;
            Hint = hint;
        }
    }

    public static class DeathCatalog
    {
        private static readonly IReadOnlyDictionary<DeathCause, DeathInfo> Entries =
            new Dictionary<DeathCause, DeathInfo>
            {
                [DeathCause.FallingObjects] = new DeathInfo(
                    "떨어진 물건에 맞았습니다",
                    "흔들리는 동안, 머리 위를 막아 줄 튼튼한 무언가가 필요했다..."),
                [DeathCause.GasExplosion] = new DeathInfo(
                    "가스 폭발이 일어났습니다",
                    "첫 흔들림이 멈췄을 때, 부엌에서 나던 냄새를 그냥 두지 말았어야 했다..."),
                [DeathCause.ElectricFire] = new DeathInfo(
                    "누전으로 불이 났습니다",
                    "천장에서 새는 빗물과 집 안의 전기... 더 큰 흔들림이 오기 전에 막을 방법이 있었다..."),
                [DeathCause.TrappedInside] = new DeathInfo(
                    "집 안에 갇혔습니다",
                    "건물이 뒤틀리면 닫힌 것은 다시 열리지 않는다. 나갈 길을 미리 만들어 뒀어야 했다..."),
                [DeathCause.StairFall] = new DeathInfo(
                    "계단에서 굴러 떨어졌습니다",
                    "흔들리는 계단 위에 그대로 서 있었다... 몸을 낮추고 버텼어야 했다..."),
                [DeathCause.ElevatorTrapped] = new DeathInfo(
                    "엘리베이터에 갇혔습니다",
                    "지진이 나면 가장 먼저 끊기는 것은 전기다. 그 전기로 움직이는 상자에 몸을 맡기면 안 됐다..."),
                [DeathCause.FacadeDebris] = new DeathInfo(
                    "떨어진 외벽 조각에 맞았습니다",
                    "건물 바로 옆은 흔들릴 때 가장 위험하다. 위에서 떨어지는 것이 닿지 않는 곳이 필요했다..."),
                [DeathCause.Tsunami] = new DeathInfo(
                    "지진해일에 휩쓸렸습니다",
                    "바다가 이상하게 멀리 물러나 있었다... 물이 닿지 않을 곳을 찾아야 했다..."),
                [DeathCause.BuildingExplosion] = new DeathInfo(
                    "건물 폭발에 휘말렸습니다",
                    "흔들림이 멈춘 뒤에도 무너져 가는 건물 안에 너무 오래 머물렀다... 서둘러 밖으로 나가야 했다..."),
                [DeathCause.ExplosionDebris] = new DeathInfo(
                    "날아온 잔해에 맞았습니다",
                    "건물 가까이에 머물렀다... 무슨 일이 생겨도 닿지 않을 만큼 멀리, 넓은 곳으로 가야 했다..."),
                [DeathCause.Sinkhole] = new DeathInfo(
                    "꺼진 땅속으로 떨어졌습니다",
                    "발밑에서 땅이 갈라지는 소리가 들렸다... 금이 가기 시작한 곳에서는 바로 벗어났어야 했다..."),
                [DeathCause.UpperFloorCollapse] = new DeathInfo(
                    "무너지는 위층에 깔렸습니다",
                    "크게 흔들린 건물의 위층은 언제 무너질지 모른다... 대피는 언제나 아래로, 밖으로 향해야 했다..."),
                [DeathCause.FellWhileShaking] = new DeathInfo(
                    "흔들리는 중에 넘어졌습니다",
                    "땅이 울리는 동안 선 채로 돌아다녔다... 그 자리에서 몸을 낮추고 흔들림이 멈추기를 기다렸어야 했다..."),
                [DeathCause.GroundCrack] = new DeathInfo(
                    "갈라진 땅 틈에 빠졌습니다",
                    "검게 벌어진 틈을 그대로 밟았다... 돌아가거나 뛰어넘었어야 했다..."),
            };

        public static DeathInfo Get(DeathCause cause)
        {
            return Entries.TryGetValue(cause, out var info)
                ? info
                : new DeathInfo("사망", string.Empty);
        }
    }
}
