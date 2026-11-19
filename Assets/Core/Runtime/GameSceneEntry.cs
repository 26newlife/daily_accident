using GameCore;
using GameData;
using TMPro;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// HomeBase(GameScene) 진입 관문. 새 게임 / 다시 시작 / 메뉴 왕복 후 재진입 모두 여기서 GameRunContext.Begin 으로 판을 정리한다.
    /// 시나리오 선택 검사와 시나리오 씬 로드는 ScenarioRunner 가 맡는다 (이전의 "준비 중 → 메인 메뉴 복귀"는 제거됨).
    /// 아래 필드는 기존 씬과의 직렬화 호환을 위해 남겨 두었다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameSceneEntry : MonoBehaviour
    {
        [Header("(호환) 기존 진입 관문 설정 — 현재는 사용하지 않음")]
        [SerializeField] private ScenarioData requiredScenario;
        [SerializeField] private bool allowDirectPlayInEditor = true;
        [SerializeField] private GameObject[] gameplayRoots;
        [SerializeField] private GameObject noticeRoot;
        [SerializeField] private TMP_Text noticeText;

        private void Awake()
        {
            GameRunContext.Begin("scene-enter");   // 새 게임 / 다시 시작 / 메뉴 왕복 후 재진입 모두 여기를 거친다
            if (noticeRoot != null) noticeRoot.SetActive(false);
        }
    }
}
