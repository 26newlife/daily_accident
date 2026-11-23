using System;
using System.Collections;
using System.Collections.Generic;
using GameCore;
using GameData;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameGameplay
{
    /// <summary>
    /// HomeBase(GameScene) 의 시나리오 실행기. 시작 시:
    ///   ① GameSession.SelectedScenario → ScenarioDefinition 확인 (에디터에서 바로 재생하면 defaultDefinition)
    ///   ② 시나리오 씬을 Additive 로 로드 → 집 참조 바인딩 복원(ScenarioBinder) → 사망/엔딩/점수/힌트 데이터 등록
    ///   ③ 환경 프리셋 적용 → 플레이어를 startSpawnId 에 배치 → 내용물 활성화 → Act 목록 전달 후 첫 Act 시작
    /// 정의·씬·루트 누락 시 명확한 에러 로그 + 안내 후 메인 메뉴 복귀. 사망/재시작/메인 메뉴 이동은 씬 Single 로드로
    /// 시나리오 씬이 함께 언로드되며, 시나리오 컴포넌트는 OnDisable/RunCleanup 으로 정리된다.
    /// </summary>
    public class ScenarioRunner : MonoBehaviour
    {
        [SerializeField, Tooltip("에디터에서 GameScene 을 바로 재생할 때 쓰는 정의 (빌드에서는 메인 메뉴 선택이 필요)")] private ScenarioDefinition defaultDefinition;
        [SerializeField] private ScenarioStateMachine machine;
        [SerializeField] private DeathAndEnding deathAndEnding;
        [SerializeField] private ScoreSystem scoreSystem;
        [SerializeField] private PlayerController player;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField, Min(0.5f)] private float errorSeconds = 3f;

        public static ScenarioRunner Instance { get; private set; }
        public ScenarioDefinition Definition { get; private set; }
        public ScenarioSceneRoot Root { get; private set; }
        public Scene ScenarioScene { get; private set; }
        public bool IsReady { get; private set; }
        public string LastError { get; private set; }

        public event Action Ready;

        private readonly List<IScenarioBootstrap> bootstraps = new List<IScenarioBootstrap>();

        private void Awake() { Instance = this; }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (IScenarioBootstrap b in bootstraps) { try { b.OnScenarioUnloading(); } catch (Exception e) { Debug.LogException(e); } }
            bootstraps.Clear();
        }

        private void Start() { StartCoroutine(Load()); }

        private ScenarioDefinition Resolve()
        {
            ScenarioData selected = GameSession.SelectedScenario;
            if (selected != null)
            {
                if (selected.Definition == null) { Fail($"시나리오 '{selected.DisplayName}'({selected.Id}) 에 ScenarioDefinition 이 연결되어 있지 않습니다."); return null; }
                return selected.Definition;
            }
            if (Application.isEditor && defaultDefinition != null) return defaultDefinition;   // 에디터에서 GameScene 을 바로 재생
            Fail("선택된 시나리오가 없습니다. 메인 메뉴에서 시나리오를 선택하세요.");
            return null;
        }

        private IEnumerator Load()
        {
            Definition = Resolve();
            if (Definition == null) yield break;
            string sceneName = Definition.ScenarioSceneName;
            if (!Definition.IsPlayable) { Fail($"시나리오 '{Definition.DisplayName}'은(는) 아직 플레이할 수 없습니다 (isPlayable = false)."); yield break; }
            if (string.IsNullOrEmpty(sceneName)) { Fail($"시나리오 '{Definition.Id}' 의 scenarioSceneName 이 비어 있습니다."); yield break; }
            if (!Application.CanStreamedLevelBeLoaded(sceneName)) { Fail($"시나리오 씬 '{sceneName}' 을(를) 로드할 수 없습니다. Build Settings 에 등록했는지 확인하세요."); yield break; }

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (op != null && !op.isDone) yield return null;
            ScenarioScene = SceneManager.GetSceneByName(sceneName);
            if (!ScenarioScene.IsValid() || !ScenarioScene.isLoaded) { Fail($"시나리오 씬 '{sceneName}' 로드에 실패했습니다."); yield break; }

            foreach (GameObject r in ScenarioScene.GetRootGameObjects())
            {
                Root = r.GetComponentInChildren<ScenarioSceneRoot>(true);
                if (Root != null) break;
            }
            if (Root == null) { Fail($"시나리오 씬 '{sceneName}' 에 ScenarioSceneRoot 가 없습니다."); yield break; }

            // 집 참조 복원
            var errors = new List<string>();
            int ok = ScenarioBinder.Apply(ScenarioScene, gameObject.scene, Root.Bindings, errors);
            Debug.Log($"[Scenario] '{Definition.Id}' 바인딩 {ok}/{(Root.Bindings != null ? Root.Bindings.Length : 0)} 복원");
            foreach (string e in errors) Debug.LogError("[Scenario] 바인딩 오류: " + e);

            // 시나리오 데이터 등록 (사망 사유·엔딩·점수·힌트)
            if (deathAndEnding != null) deathAndEnding.RegisterScenarioData(Definition.DeathReasons, Definition.FallbackDeath, Definition.Endings, Definition.HiddenEndingId, Definition.HiddenEndingFlag);
            if (scoreSystem != null) scoreSystem.SetRules(Definition.ScoreRules);
            if (HintSystem.Instance != null) HintSystem.Instance.AddScenarioHints(Definition.Hints);

            // 기능 플래그 (호흡기 게이지·가스·화재 소품·도어 스코프·창밖 교통)
            ScenarioFeatures.Apply(Definition);

            // 환경 프리셋 / 시작 위치
            if (HouseServices.Environment != null && Definition.EnvironmentPreset != null) HouseServices.Environment.ApplyEnvironmentPreset(Definition.EnvironmentPreset);
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player != null && HouseServices.Spawns != null && !string.IsNullOrEmpty(Definition.StartSpawnId))
            {
                if (HouseServices.Spawns.TryGetSpawn(Definition.StartSpawnId, out Vector3 pos, out float yaw))
                {
                    player.Teleport(pos, yaw);
                    if (HazardManager.Instance != null) HazardManager.Instance.ResyncPlayerZones(pos + Vector3.up);
                }
                else Debug.LogWarning($"[Scenario] 시작 스폰 '{Definition.StartSpawnId}' 을(를) 집에서 찾지 못했습니다. 기본 위치에서 시작합니다.");
            }

            // 활성화 → 부트스트랩 → Act 시작
            if (Root.ContentRoot != null) Root.ContentRoot.SetActive(true);
            if (Root.Bootstraps != null)
                foreach (MonoBehaviour m in Root.Bootstraps)
                    if (m is IScenarioBootstrap b) { bootstraps.Add(b); b.OnScenarioLoaded(); }
            if (machine != null)
            {
                machine.SetActs(Root.Acts);
                if (Root.Acts != null && Root.Acts.Length > 0) machine.StartAct(0);
                else Debug.LogWarning($"[Scenario] '{Definition.Id}' 에 Act 가 없습니다.");
            }
            IsReady = true;
            Ready?.Invoke();
        }

        private void Fail(string message)
        {
            LastError = message;
            Debug.LogError("[Scenario] " + message);
            EventBus.ShowMessage(message);
            StartCoroutine(BackToMenu());
        }

        private IEnumerator BackToMenu()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return new WaitForSecondsRealtime(errorSeconds);
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
