using QuakeSurvival.Audio;
using UnityEngine;
using UnityEngine.Events;

namespace QuakeSurvival.Interaction
{
    /// <summary>
    /// 켜짐/꺼짐 두 상태를 가진 조작 물건(가스 밸브, 차단기, 현관문 등).
    /// 조작하면 visual을 회전시키고 (actionId, 새 상태) 이벤트를 보낸다.
    /// </summary>
    public sealed class SwitchInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string actionId = "gas_valve";
        [SerializeField] private string promptWhenOff = "조작하기";
        [SerializeField] private string promptWhenOn = "되돌리기";
        [SerializeField] private bool oneWay = true;
        [SerializeField] private bool startOn;

        [Header("Visual")]
        [SerializeField] private Transform visual;
        [SerializeField] private Vector3 offEuler = Vector3.zero;
        [SerializeField] private Vector3 onEuler = new Vector3(0f, 0f, 90f);
        [Tooltip("켜면 조작 시 visual을 숨긴다(신발, 가방처럼 집어 드는 물건).")]
        [SerializeField] private bool hideVisualWhenOn;

        [Header("Sound")]
        [SerializeField] private AudioClip interactSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.8f;

        [SerializeField] private UnityEvent<string, bool> onChanged = new UnityEvent<string, bool>();

        public string ActionId => actionId;
        public bool IsOn { get; private set; }

        public string Prompt => IsOn ? promptWhenOn : promptWhenOff;
        public bool CanInteract => !Locked && !(oneWay && IsOn) && Time.time >= nextInteractTime;

        /// <summary>true면 더 이상 조작할 수 없다(판정이 끝난 가스 밸브, 차단기 등).</summary>
        public bool Locked { get; set; }

        // 키를 연타해 실수로 되돌리지 않도록 짧은 대기 시간
        private const float InteractCooldown = 0.4f;
        private float nextInteractTime;

        /// <summary>상태가 바뀔 때마다 (actionId, IsOn)으로 호출된다.</summary>
        public UnityEvent<string, bool> OnChanged => onChanged;

        public void Interact()
        {
            if (!CanInteract) return;
            IsOn = !IsOn;
            nextInteractTime = Time.time + InteractCooldown;
            ApplyVisual();
            SoundPlayer.PlayAt(interactSound, transform.position, AudioCategory.Sfx, soundVolume);
            onChanged.Invoke(actionId, IsOn);
        }

        private void Awake()
        {
            IsOn = startOn;
        }

        private void Start()
        {
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (visual == null) return;
            if (hideVisualWhenOn)
            {
                visual.gameObject.SetActive(!IsOn);
                return;
            }
            visual.localRotation = Quaternion.Euler(IsOn ? onEuler : offEuler);
        }
    }
}
