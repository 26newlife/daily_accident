using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// S03 상황 변화에 맞춰 스마트폰(PhoneUI)에 문자를 보낸다. 폰을 열지 않아도 도착 알림이 화면에 뜬다.
    /// 같은 문자는 한 판에 한 번만 보낸다 (키 = 보낸 사람 + 내용).
    /// </summary>
    public static class PhoneSms
    {
        private static readonly System.Collections.Generic.HashSet<string> sent = new System.Collections.Generic.HashSet<string>();

        public static void Send(string sender, string body)
        {
            if (!sent.Add(sender + "|" + body)) return;

            PhoneUI phone = Object.FindFirstObjectByType<PhoneUI>(FindObjectsInactive.Include);
            if (phone != null) phone.AddMessage(sender, body);
            EventBus.ShowMessage($"문자 도착 — {sender}");
        }

        /// <summary>새 판 시작 시 호출 (Act 1 OnEnter)</summary>
        public static void Reset() { sent.Clear(); }
    }
}
