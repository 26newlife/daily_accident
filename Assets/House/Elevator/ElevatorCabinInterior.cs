using TMPro;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 엘리베이터 내부 세트 (복도와 겹치지 않는 별도 위치의 프리미티브 방). 안쪽에서 보이는 문(좌우 슬라이드), 층 표시 디스플레이,
    /// 천장 조명/붉은 비상등, 연기 파티클, 조작 패널(ElevatorFloorPanel)을 제어한다.
    /// 탑승 중 플레이어는 이 방의 벽 콜라이더 안에서만 움직인다 (물리 경계).
    /// </summary>
    public class ElevatorCabinInterior : MonoBehaviour
    {
        [Header("문 (안쪽에서 보이는 좌우 슬라이드)")]
        [SerializeField] private Transform doorLeft;
        [SerializeField] private Transform doorRight;
        [SerializeField, Tooltip("한쪽 문이 열릴 때 이동하는 거리(m)")] private float openDistance = 0.5f;
        [SerializeField, Min(0.1f)] private float doorSeconds = 0.8f;

        [Header("표시 / 조명 / 연출")]
        [SerializeField] private TMP_Text floorText;
        [SerializeField] private Light mainLight;
        [SerializeField] private Light emergencyLight;
        [SerializeField] private ParticleSystem smokeFx;
        [SerializeField] private HazardZone zone;
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private ElevatorFloorPanel panel;
        [SerializeField] private float mainIntensity = 1.4f;

        private float openness;          // 0 = 닫힘, 1 = 열림
        private bool targetOpen;
        private bool powered = true, flicker;
        private float powerOffT = -1f;   // >=0 이면 꺼지기 전 깜빡이는 중
        private int lastFloor = 10, lastDir;
        private Vector3 leftClosed, rightClosed;
        private bool inited;

        public Transform PlayerSpawn => playerSpawn;
        public ElevatorFloorPanel Panel => panel;
        public bool DoorOpen => targetOpen;
        public bool Powered => powered;

        private void Awake() { Init(); }

        private void Init()
        {
            if (inited) return;
            inited = true;
            if (doorLeft != null) leftClosed = doorLeft.localPosition;
            if (doorRight != null) rightClosed = doorRight.localPosition;
            if (emergencyLight != null) emergencyLight.enabled = false;
        }

        public void SetDoorSeconds(float seconds) { doorSeconds = Mathf.Max(0.1f, seconds); }

        public void SetDoorOpen(bool open, bool instant = false)
        {
            Init();
            targetOpen = open;
            if (instant) { openness = open ? 1f : 0f; ApplyDoor(); }
        }

        public void SetFloor(int floor, int dir)
        {
            lastFloor = floor; lastDir = dir;
            RefreshText();
        }

        private void RefreshText()
        {
            if (floorText == null) return;
            if (!powered && powerOffT < 0f) { floorText.text = string.Empty; return; }
            string arrow = lastDir > 0 ? "^" : lastDir < 0 ? "v" : " ";
            floorText.text = $"{arrow} {lastFloor}";
        }

        /// <summary>false: 층 표시 깜빡임 → 꺼짐, 조명 꺼짐, 붉은 약한 비상등</summary>
        public void SetPower(bool on)
        {
            if (on) { powered = true; powerOffT = -1f; if (mainLight != null) { mainLight.enabled = true; mainLight.intensity = mainIntensity; } if (emergencyLight != null) emergencyLight.enabled = false; RefreshText(); return; }
            powered = false;
            powerOffT = 0f;
            if (emergencyLight != null) emergencyLight.enabled = true;
        }

        public void SetFlicker(bool on) { flicker = on; if (!on && mainLight != null && powered) mainLight.intensity = mainIntensity; }

        public void SetSmoke(bool on)
        {
            if (smokeFx == null) return;
            if (on) smokeFx.Play(); else smokeFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>탑승 직전/하차 후 초기 상태로</summary>
        public void ResetCabin()
        {
            Init();
            SetPower(true);
            SetFlicker(false);
            SetSmoke(false);
            if (zone != null) zone.SetSmoke(0f);
            if (panel != null) panel.ClearLights();
            SetDoorOpen(false, true);
            SetFloor(10, 0);
        }

        private void Update()
        {
            float target = targetOpen ? 1f : 0f;
            if (!Mathf.Approximately(openness, target)) { openness = Mathf.MoveTowards(openness, target, Time.deltaTime / doorSeconds); ApplyDoor(); }

            if (powerOffT >= 0f)
            {
                powerOffT += Time.deltaTime;
                bool on = Mathf.FloorToInt(powerOffT * 12f) % 2 == 0 && powerOffT < 0.8f;   // 깜빡이다 꺼진다
                if (mainLight != null) { mainLight.enabled = on; mainLight.intensity = mainIntensity * 0.6f; }
                if (floorText != null) floorText.text = on ? $"{(lastDir > 0 ? "^" : "v")} {lastFloor}" : string.Empty;
                if (powerOffT >= 0.8f) { powerOffT = -1f; if (mainLight != null) mainLight.enabled = false; if (floorText != null) floorText.text = string.Empty; }
            }
            else if (flicker && powered && mainLight != null)
                mainLight.intensity = mainIntensity * (Random.value < 0.08f ? 0.35f : 1f);   // 조명이 약간 깜빡이는 정도의 미세한 이상
        }

        private void ApplyDoor()
        {
            if (doorLeft != null) doorLeft.localPosition = leftClosed + Vector3.left * (openDistance * openness);
            if (doorRight != null) doorRight.localPosition = rightClosed + Vector3.right * (openDistance * openness);
        }
    }
}
