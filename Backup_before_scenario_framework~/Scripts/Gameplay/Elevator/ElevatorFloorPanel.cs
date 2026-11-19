using UnityEngine;

namespace GameGameplay
{
    /// <summary>오른쪽 벽 조작 패널: 층 버튼 1~20 + 열림/닫힘/비상 호출/하차 버튼의 불 켜기·끄기를 관리한다</summary>
    public class ElevatorFloorPanel : MonoBehaviour
    {
        [SerializeField] private ElevatorButton[] buttons;

        private ElevatorButton alarm;
        private float blinkUntil;

        public void Init(ElevatorButton[] all) { buttons = all; }

        /// <summary>해당 층 버튼만 불을 켠다 (마지막 선택이 우선)</summary>
        public void LightFloor(int floor)
        {
            if (buttons == null) return;
            foreach (ElevatorButton b in buttons)
                if (b != null && b.Kind == ElevatorButton.ButtonKind.Floor) b.SetLit(b.Floor == floor);
        }

        public void ClearLights()
        {
            if (buttons == null) return;
            foreach (ElevatorButton b in buttons) if (b != null) b.SetLit(false);
        }

        /// <summary>비상 호출 버튼을 잠깐 깜빡인다 ("연결 중…" 연출, 안내 문구 없음)</summary>
        public void BlinkAlarm()
        {
            blinkUntil = Time.time + 3f;
            if (alarm == null && buttons != null)
                foreach (ElevatorButton b in buttons) if (b != null && b.Kind == ElevatorButton.ButtonKind.Alarm) alarm = b;
        }

        private void Update()
        {
            if (alarm == null) return;
            if (Time.time < blinkUntil) alarm.SetLit(Mathf.FloorToInt(Time.time * 4f) % 2 == 0);
            else if (alarm.IsLit) alarm.SetLit(false);
        }
    }
}
