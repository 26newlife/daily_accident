using QuakeSurvival.Audio;
using System.Collections;
using QuakeSurvival.Quake;
using QuakeSurvival.UI;
using UnityEngine;

namespace QuakeSurvival.Interaction
{
    /// <summary>
    /// 엘리베이터 호출 버튼. 본진 전에는 엘리베이터가 올라와 문이 열린다(타면 갇혀 사망).
    /// 본진이 시작된 뒤에는 정전으로 반응하지 않는다.
    /// </summary>
    public sealed class ElevatorCall : MonoBehaviour, IInteractable
    {
        [SerializeField] private QuakeDirector director;
        [SerializeField] private MessageUI messages;
        [SerializeField] private Transform doorPanel;
        [SerializeField] private Vector3 doorOpenOffset = new Vector3(-0.95f, 0f, 0f);
        [SerializeField] private GameObject carHazard;
        [SerializeField] private float arriveSeconds = 4f;
        [SerializeField] private AudioClip arriveDing;
        [SerializeField] private AudioClip buttonClick;

        private bool called;
        private bool opened;

        /// <summary>한 번이라도 엘리베이터를 불렀는지(결과 화면 감점용).</summary>
        public bool WasCalled => called;

        public string Prompt => "엘리베이터 호출 버튼 누르기";
        public bool CanInteract => !opened;

        private void Awake()
        {
            if (director == null || messages == null || doorPanel == null || carHazard == null)
            {
                Debug.LogError("[ElevatorCall] 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            carHazard.SetActive(false);
        }

        public void Interact()
        {
            if (!enabled) return;
            SoundPlayer.PlayAt(buttonClick, transform.position, AudioCategory.Sfx, 0.6f);
            if (director.Phase >= QuakePhase.MainShock)
            {
                messages.ShowSubtitle("(버튼을 눌러도 아무 반응이 없다...)");
                return;
            }
            messages.ShowSubtitle("(웅- 엘리베이터가 올라오는 소리가 난다...)");
            if (called) return;
            called = true;
            StartCoroutine(Arrive());
        }

        private IEnumerator Arrive()
        {
            yield return new WaitForSeconds(arriveSeconds);
            if (director.Phase >= QuakePhase.MainShock) yield break;

            opened = true;
            doorPanel.localPosition += doorOpenOffset;
            carHazard.SetActive(true);
            SoundPlayer.PlayAt(arriveDing, doorPanel.position + Vector3.up * 2f, AudioCategory.Sfx, 0.8f, 0f);
            messages.ShowSubtitle("(띵- 엘리베이터 문이 열렸다.)");
        }
    }
}
