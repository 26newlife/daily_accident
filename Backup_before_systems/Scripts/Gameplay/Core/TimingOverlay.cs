using UnityEngine;
using UnityEngine.InputSystem;

namespace GameCore
{
    /// <summary>
    /// F9 로 켜고 끄는 개발용 타이밍 오버레이 (경과 시간 / 현재 Act / 최근 사건 / 목표 종료 시각).
    /// 에디터와 Development Build 에서만 그려지고, 릴리스 빌드에서는 아무것도 하지 않는다. 기본은 꺼짐.
    /// </summary>
    public class TimingOverlay : MonoBehaviour
    {
        [SerializeField, Tooltip("표시를 토글하는 키 (새 Input System)")] private Key toggleKey = Key.F9;
        [SerializeField, Min(1)] private int visibleEntries = 8;

        private bool visible;
        private GUIStyle style;

        private void Awake() { visible = GameTimingConfig.Current.overlayVisibleAtStart; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (style == null) style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 16, normal = { textColor = Color.white } };

            GameTimingConfig cfg = GameTimingConfig.Current;
            float t = TimingLog.Now;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Timing]  {GameClock.Format(t)} / 목표 {GameClock.Format(cfg.targetEnd)}   ({TimingLog.CurrentAct})");
            var list = TimingLog.Entries;
            for (int i = Mathf.Max(0, list.Count - visibleEntries); i < list.Count; i++)
                sb.AppendLine($"  {GameClock.Format(list[i].seconds)}  {list[i].label}");
            GUI.Box(new Rect(12, 12, 420, 28 + 22 * (Mathf.Min(visibleEntries, list.Count) + 1)), sb.ToString(), style);
        }
#endif
    }
}
