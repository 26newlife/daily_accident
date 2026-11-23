using System;
using UnityEngine;

namespace QuakeSurvival.Player
{
    /// <summary>
    /// 달리기 체력. 달리는 동안 줄고, 달리지 않으면 잠깐 쉰 뒤 회복된다.
    /// 다 쓰면 지쳐서(Exhausted) 일정 비율까지 회복될 때까지 다시 달릴 수 없다.
    /// </summary>
    [Serializable]
    public sealed class Stamina
    {
        [SerializeField] private float runSeconds = 5f;
        [SerializeField] private float recoverSeconds = 6f;
        [SerializeField] private float recoverDelay = 0.8f;
        [SerializeField, Range(0f, 1f)] private float exhaustedUntil = 0.3f;

        private float value = 1f;
        private float restTimer;

        /// <summary>남은 체력 비율(0~1).</summary>
        public float Ratio => value;

        /// <summary>체력을 다 써서 다시 달릴 수 없는 상태.</summary>
        public bool Exhausted { get; private set; }

        public bool CanRun => !Exhausted && value > 0f;

        public void Tick(bool running, float deltaTime)
        {
            if (running)
            {
                restTimer = 0f;
                value = Mathf.Max(0f, value - deltaTime / Mathf.Max(0.01f, runSeconds));
                if (value <= 0f) Exhausted = true;
                return;
            }

            restTimer += deltaTime;
            if (restTimer < recoverDelay) return;
            value = Mathf.Min(1f, value + deltaTime / Mathf.Max(0.01f, recoverSeconds));
            if (Exhausted && value >= exhaustedUntil) Exhausted = false;
        }
    }
}
