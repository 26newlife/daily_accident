using System;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>목표의 진행 상태</summary>
    public enum ObjectiveState { Active, Completed, Failed }

    /// <summary>
    /// 현재 목표(메인/서브) 목록 관리. HUD 가 Changed 이벤트로 목록을 그리고 알림 배너를 띄운다.
    /// Act 로직에서 Add / Complete / Fail 을 호출한다.
    /// </summary>
    public class ObjectiveSystem : MonoBehaviour
    {
        /// <summary>목록 변화의 종류 (HUD 알림 문구 선택용)</summary>
        public enum ChangeKind { Added, Completed, Failed }

        public class Entry
        {
            public ObjectiveData Data;
            public ObjectiveState State;
        }

        [SerializeField] private ObjectiveData[] initialObjectives;

        private readonly List<Entry> entries = new List<Entry>();

        public static ObjectiveSystem Instance { get; private set; }
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>목록이 바뀔 때 (항목, 종류)</summary>
        public event Action<Entry, ChangeKind> Changed;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            if (initialObjectives != null)
                foreach (ObjectiveData data in initialObjectives) Add(data);
        }

        public Entry Find(string id) => entries.Find(e => e.Data != null && e.Data.Id == id);

        public void Add(ObjectiveData data)
        {
            if (data == null || Find(data.Id) != null) return;
            var entry = new Entry { Data = data, State = ObjectiveState.Active };
            entries.Add(entry);
            Changed?.Invoke(entry, ChangeKind.Added);
            EventBus.RaiseObjectiveAdded(data.Id);
        }

        public void Complete(string id) => SetState(id, ObjectiveState.Completed, ChangeKind.Completed);
        public void Fail(string id) => SetState(id, ObjectiveState.Failed, ChangeKind.Failed);

        private void SetState(string id, ObjectiveState state, ChangeKind kind)
        {
            Entry e = Find(id);
            if (e == null || e.State != ObjectiveState.Active) return;
            e.State = state;
            Changed?.Invoke(e, kind);
            if (kind == ChangeKind.Completed) EventBus.RaiseObjectiveCompleted(id);
            else EventBus.RaiseObjectiveFailed(id);
        }

        /// <summary>목표를 알림 없이 목록에서 제거 (더 이상 의미 없어진 목표 정리용)</summary>
        public void Remove(string id)
        {
            if (entries.RemoveAll(e => e.Data != null && e.Data.Id == id) > 0) Changed?.Invoke(null, ChangeKind.Added);
        }

        /// <summary>완료/실패한 목표를 목록에서 제거 (HUD 정리용)</summary>
        public void RemoveFinished()
        {
            if (entries.RemoveAll(e => e.State != ObjectiveState.Active) > 0) Changed?.Invoke(null, ChangeKind.Added);
        }
    }
}
