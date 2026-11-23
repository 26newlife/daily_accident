using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 불이 붙을 수 있는 생활 소품(커튼, 이불, 종이, 옷가지 등) 표시용 컴포넌트.
    /// 지금은 동작 로직 없이 "가연물" 이라는 표시와 기존 FireSource 와의 연결 슬롯만 제공한다.
    /// 나중에 FireSource 가 번질 때 주변 Flammable 을 찾아 Ignite() 를 호출하는 식으로 연동하면 된다.
    /// </summary>
    public class Flammable : MonoBehaviour
    {
        /// <summary>Unity Tag 이름 (인테리어 빌더가 자동 등록)</summary>
        public const string TagName = "Flammable";

        [SerializeField, Tooltip("이 가연물이 불타는 장면을 대표하는 FireSource 의 id (선택). 예: 커튼 → fire_curtain. 불은 시나리오 씬에 있으므로 id 로만 연결한다")]
        private string linkedFireId;
        [SerializeField, Range(0f, 1f), Tooltip("불이 옮겨붙기 쉬운 정도 (0 = 거의 안 탐, 1 = 매우 잘 탐)")]
        private float ignitability = 0.5f;
        [SerializeField, Min(1f), Tooltip("완전히 타는 데 걸리는 시간(초) — 추후 연출용")]
        private float burnSeconds = 20f;

        public string LinkedFireId => linkedFireId;
        /// <summary>연결된 FireSource (시나리오가 불을 만들어 두었을 때만 존재)</summary>
        public FireSource LinkedFire
        {
            get
            {
                if (string.IsNullOrEmpty(linkedFireId)) return null;
                foreach (FireSource f in FireSource.All) if (f != null && f.Id == linkedFireId) return f;
                return null;
            }
        }
        public float Ignitability => ignitability;
        public float BurnSeconds => burnSeconds;
        public bool IsBurning { get; private set; }

        /// <summary>불이 붙은 상태로 표시 (현재는 상태 플래그만 바꾼다)</summary>
        public void Ignite() => IsBurning = true;
        public void Extinguish() => IsBurning = false;
    }
}
