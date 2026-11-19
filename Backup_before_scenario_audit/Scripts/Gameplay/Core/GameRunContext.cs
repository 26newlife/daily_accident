using GameGameplay;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 한 판(Run)의 수명 관리. 새 게임 시작 / 다시 시작 / 메인 메뉴 복귀는 모두 이 클래스의 정리 루틴(Cleanup)을 거친다.
    ///
    /// 씬 오브젝트(GameClock 의 타이머, 목표, 인벤토리, 점수, 가스/연기/화재, NPC 등)는 씬 재로드로 새로 만들어지므로
    /// 여기서는 "씬을 넘어 살아남는 static 상태"만 초기화한다:
    ///   ScenarioFlags(진행 플래그), TimingLog(타이밍 기록), FireSource/Peephole 의 static 값, GameFlow(일시정지/종료), timeScale, 오디오 일시정지.
    /// RunId 는 판마다 증가하며, GameClock 의 예약 타이머는 자신이 만들어진 RunId 와 다르면 실행되지 않는다.
    /// </summary>
    public static class GameRunContext
    {
        /// <summary>현재 판의 번호 (게임 씬 진입마다 +1)</summary>
        public static int RunId { get; private set; }

        /// <summary>새 판 시작 (게임 씬 Awake 가장 앞에서 호출). 이전 판의 잔여 static 상태를 모두 지운다.</summary>
        public static void Begin(string reason)
        {
            Cleanup(reason);
            RunId++;
            Debug.Log($"[Run] begin #{RunId} ({reason})");
        }

        /// <summary>판 종료/전환 시 정리. 여러 번 호출해도 안전하다.</summary>
        public static void Cleanup(string reason)
        {
            GameFlow.Reset();                // 일시정지/종료/폰 상태, Time.timeScale = 1, AudioListener.pause = false
            ScenarioFlags.ResetRun();        // 진행 플래그 + 변경 이벤트 구독
            TimingLog.ResetRun();
            FireSource.ResetRunState();
            PeepholeCameraController.ResetRunState();
            ElevatorHazardSystem.ResetRunState();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => RunId = 0;
    }
}
