using System;
using System.Collections.Generic;

namespace GameCore
{
    /// <summary>
    /// 한 판이 끝날 때(다시 시작 / 엔딩 후 다시 하기 / 메인 메뉴 복귀) 정리해야 하는 static 상태의 등록소.
    /// 씬 오브젝트(MonoBehaviour)는 씬 재로드로 사라지므로 등록할 필요가 없고,
    /// "씬을 넘어 살아남는" static 필드·static 이벤트 구독만 Register 로 정리 루틴을 등록한다.
    /// GameRunContext.Cleanup 이 Run() 을 호출한다. 등록은 판 시작 시 다시 하므로 한 번 실행한 뒤 비운다.
    /// </summary>
    public static class RunCleanup
    {
        private static readonly List<Action> actions = new List<Action>();

        /// <summary>정리 루틴 등록 (같은 delegate 중복 등록은 무시)</summary>
        public static void Register(Action cleanup)
        {
            if (cleanup != null && !actions.Contains(cleanup)) actions.Add(cleanup);
        }

        /// <summary>현재 등록된 정리 루틴 수 (디버그 패널 표시용)</summary>
        public static int PendingCount => actions.Count;

        public static void Run()
        {
            var copy = new List<Action>(actions);
            actions.Clear();
            foreach (Action a in copy)
            {
                try { a(); }
                catch (Exception e) { UnityEngine.Debug.LogException(e); }
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => actions.Clear();
    }
}
