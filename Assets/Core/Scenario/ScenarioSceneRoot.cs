using System;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>시나리오 씬의 컴포넌트가 가진, 집(베이스 씬) 오브젝트에 대한 참조 1건. 씬 사이 참조는 저장할 수 없어 "경로 + 타입"으로 기록해 두었다가 로드 후 되살린다</summary>
    [Serializable]
    public struct ScenarioBinding
    {
        [Tooltip("시나리오 씬 안의 소유 오브젝트 경로 (ScenarioPaths 형식)")] public string ownerPath;
        public string ownerType;
        public int ownerIndex;
        [Tooltip("SerializedProperty 경로 (예: windows.Array.data[1])")] public string propertyPath;
        [Tooltip("베이스 씬 안의 대상 오브젝트 경로")] public string targetPath;
        [Tooltip("비어 있으면 GameObject 참조, 아니면 이 컴포넌트 타입")] public string targetType;
    }

    /// <summary>시나리오가 시작/종료될 때 호출받는 컴포넌트 (시나리오 씬 안에 둔다)</summary>
    public interface IScenarioBootstrap
    {
        void OnScenarioLoaded();
        void OnScenarioUnloading();
    }

    /// <summary>
    /// 시나리오 씬의 루트 컴포넌트 (씬마다 1개). 내용물(contentRoot)은 평소 비활성으로 저장되어 있다가,
    /// ScenarioRunner 가 집 참조 바인딩을 복원한 뒤 활성화한다 → Awake/OnEnable 시점에 집 참조가 비어 있는 문제가 없다.
    /// </summary>
    public class ScenarioSceneRoot : MonoBehaviour
    {
        [SerializeField] private GameObject contentRoot;
        [SerializeField, Tooltip("순서 있는 Act 목록")] private ActBase[] acts;
        [SerializeField] private ScenarioBinding[] bindings;
        [SerializeField, Tooltip("IScenarioBootstrap 을 구현한 컴포넌트")] private MonoBehaviour[] bootstraps;

        public GameObject ContentRoot => contentRoot;
        public ActBase[] Acts => acts;
        public ScenarioBinding[] Bindings => bindings;
        public MonoBehaviour[] Bootstraps => bootstraps;
    }
}
