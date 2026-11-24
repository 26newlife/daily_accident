using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 2(자연재해 — 지진·지진해일) 시작 훅. 집은 그대로 두고 시나리오 시작 시에만:
    ///   · (비상계단 1~20층·옥상 / 화재 소품 / 창밖 교통 은 ScenarioDefinition 기능 플래그로 ScenarioRunner 가 처리)
    ///   · 시스템 연결 순서: 체크리스트 초기 상태 → 판정·기록 → 재해 연출 → 단서 → 폰 → 분위기 → 엘리베이터 위험 → 타임라인 시작
    ///   · 집 신발장을 "신발 신기"로 바꾼다 (기본 동작 대체, id 그대로)
    /// </summary>
    public class S02Bootstrap : MonoBehaviour, IScenarioBootstrap
    {
        [SerializeField] private QuakeTimeline timeline;
        [SerializeField] private QuakeChecklist checklist;
        [SerializeField] private QuakeOutcome outcome;
        [SerializeField] private QuakeRecord record;
        [SerializeField] private QuakeBuildingExplosion buildingExplosion;
        [SerializeField] private QuakeIndoorCollapse indoorCollapse;
        [SerializeField] private QuakeTsunami tsunami;
        [SerializeField] private QuakeGroundRupture rupture;
        [SerializeField] private QuakeGlassInjury glass;
        [SerializeField] private QuakeUpperCollapse upperCollapse;
        [SerializeField] private QuakeClues clues;
        [SerializeField] private QuakePhonePanel phonePanel;
        [SerializeField] private QuakeAtmosphere atmosphere;
        [SerializeField] private QuakeElevatorHazard elevator;

        private bool shoesOn;

        public void OnScenarioLoaded()
        {
            if (checklist != null) checklist.ReadInitialState();
            if (outcome != null) outcome.Hook();
            if (record != null) record.Hook();
            if (buildingExplosion != null) buildingExplosion.Hook();
            if (indoorCollapse != null) indoorCollapse.Hook();
            if (tsunami != null) tsunami.Init();
            if (rupture != null) rupture.Init();
            if (glass != null) glass.Init();
            if (upperCollapse != null) upperCollapse.Init();
            if (clues != null) clues.Hook();
            if (phonePanel != null) phonePanel.Hook();
            if (atmosphere != null) atmosphere.Begin();
            if (elevator != null) elevator.Register();
            SetupShoes();
            if (timeline != null) timeline.Begin();
            Debug.Log("[S02] 자연재해(지진·지진해일) 시나리오 시작");
        }

        public void OnScenarioUnloading()
        {
            if (elevator != null) elevator.Unregister();
            if (atmosphere != null) atmosphere.StopAll();
            if (timeline != null) timeline.Stop();
            PlayerController p = FindFirstObjectByType<PlayerController>();
            if (p != null) p.ScenarioSpeedMultiplier = 1f;
        }

        private void SetupShoes()
        {
            HouseController h = HouseController.Instance;
            if (h == null) return;
            h.ReplaceInteraction(QuakeKeys.ShoeCabinet, new InteractableObject.InteractionOverride
            {
                prompt = () => shoesOn ? "신발을 신었다" : "신발 신기",
                canInteract = () => !shoesOn,
                hold = () => 0.8f,
                onInteract = _ => { shoesOn = true; EventBus.ShowMessage("운동화를 신고 끈을 단단히 묶었다."); },
            });
        }
    }
}
