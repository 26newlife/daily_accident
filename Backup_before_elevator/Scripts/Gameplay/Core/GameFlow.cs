using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 게임 진행 상태 플래그(일시정지 / 종료 / 폰 열림). 플레이어·UI가 이 값을 읽어 입력을 제한한다.
    /// </summary>
    public static class GameFlow
    {
        public static bool IsPaused { get; private set; }
        public static bool IsEnded { get; private set; }
        public static bool IsPhoneOpen { get; private set; }
        /// <summary>대화/선택지(ChoicePanel) 같은 모달 UI 가 열려 있는가 — 커서를 풀고 이동·시점을 멈춘다 (시간은 계속 흐른다)</summary>
        public static bool IsModalOpen { get; private set; }

        /// <summary>이동/시점/상호작용을 막아야 하는가</summary>
        public static bool GameplayBlocked => IsPaused || IsEnded;
        /// <summary>마우스 커서를 풀어줘야 하는가 (메뉴·폰·결과 화면)</summary>
        public static bool CursorFree => IsPaused || IsEnded || IsPhoneOpen || IsModalOpen;

        public static void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;   // 일시정지 중에는 효과음/환경음/알람도 멈춘다 (UI 소리는 ignoreListenerPause 로 예외 지정 가능)
        }

        public static void SetEnded() { IsEnded = true; }
        public static void SetPhoneOpen(bool open) { IsPhoneOpen = open; }
        public static void SetModalOpen(bool open) { IsModalOpen = open; }

        /// <summary>씬 시작 시 호출: 상태 초기화 + 시간 정상화</summary>
        public static void Reset()
        {
            IsPaused = false; IsEnded = false; IsPhoneOpen = false; IsModalOpen = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsPaused = false; IsEnded = false; IsPhoneOpen = false; IsModalOpen = false; }
    }
}
