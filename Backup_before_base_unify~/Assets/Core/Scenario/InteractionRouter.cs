using System;
using System.Collections.Generic;
using GameCore;

namespace GameGameplay
{
    /// <summary>
    /// 상호작용 라우터: 집의 상호작용 오브젝트(기존 id)가 발행하는 ScenarioEvent 를 id 별로 구독하게 해 주는 공용 API.
    ///   · 집 오브젝트는 "눌림/열림/잠김…" 이벤트만 발행한다 (로직 없음).
    ///   · 시나리오는 Subscribe(id, handler) 로 동작을 정의한다. Act 의 OnScenarioEvent 도 같은 이벤트를 받는다.
    ///   · 아무도 구독하지 않은 오브젝트는 컴포넌트의 기본 동작(문 열고 닫기, 스위치, 창문, 아이템 줍기 …)이 막힘 없이 수행된다.
    ///   · 기본 동작 자체를 바꾸려면 HouseController.ReplaceInteractionBehavior(id, handler).
    /// 구독은 IDisposable 로 해제하며, 판이 끝나면(RunCleanup) 모두 해제된다.
    /// </summary>
    public static class InteractionRouter
    {
        private static readonly Dictionary<string, List<Action<ScenarioEvent>>> handlers = new Dictionary<string, List<Action<ScenarioEvent>>>();
        private static bool hooked;

        private sealed class Subscription : IDisposable
        {
            private readonly string id; private readonly Action<ScenarioEvent> handler;
            public Subscription(string id, Action<ScenarioEvent> h) { this.id = id; handler = h; }
            public void Dispose() { Unsubscribe(id, handler); }
        }

        public static IDisposable Subscribe(string id, Action<ScenarioEvent> handler)
        {
            if (string.IsNullOrEmpty(id) || handler == null) return null;
            if (!hooked) { EventBus.ScenarioEventRaised += Dispatch; hooked = true; RunCleanup.Register(Clear); }
            if (!handlers.TryGetValue(id, out var list)) handlers[id] = list = new List<Action<ScenarioEvent>>();
            list.Add(handler);
            return new Subscription(id, handler);
        }

        public static void Unsubscribe(string id, Action<ScenarioEvent> handler)
        {
            if (handlers.TryGetValue(id, out var list)) list.Remove(handler);
        }

        public static bool HasSubscribers(string id) => handlers.TryGetValue(id, out var l) && l.Count > 0;
        public static int SubscriptionCount { get { int n = 0; foreach (var l in handlers.Values) n += l.Count; return n; } }

        private static void Dispatch(ScenarioEvent e)
        {
            if (e.SourceId == null || !handlers.TryGetValue(e.SourceId, out var list)) return;
            foreach (var h in list.ToArray()) h(e);
        }

        public static void Clear()
        {
            handlers.Clear();
            if (hooked) { EventBus.ScenarioEventRaised -= Dispatch; hooked = false; }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { handlers.Clear(); hooked = false; }
    }
}
