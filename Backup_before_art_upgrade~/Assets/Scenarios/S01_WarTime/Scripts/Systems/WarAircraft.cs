using System;
using UnityEngine;

namespace GameGameplay
{
    public enum WarAircraftKind { Jet = 0, Helicopter = 1 }

    /// <summary>
    /// 창밖을 지나가는 전투기/헬기 1기. 웨이포인트를 따라 이동하고(순수 연출 — 게임플레이 타이밍과 무관),
    /// 3D 사운드(도플러 효과)를 재생한다. 프리팹(S01_Fighter / S01_Helicopter)에 붙어 있다.
    /// </summary>
    public class WarAircraft : MonoBehaviour
    {
        [SerializeField] private WarAircraftKind kind;
        [SerializeField] private AudioSource engineAudio;
        [SerializeField, Tooltip("도플러 강도")] private float dopplerLevel = 4f;
        [SerializeField] private float maxAudibleDistance = 700f;
        [SerializeField] private ParticleSystem afterburner;
        [SerializeField, Tooltip("선회 시 기울임(도)")] private float bankDegrees = 22f;

        public WarAircraftKind Kind => kind;
        public bool IsFlying { get; private set; }

        private Vector3[] path;
        private int next;
        private float speed, delay, volume;
        private Action<WarAircraft> done;
        private float bank;
        private bool started;

        public void Fly(Vector3[] worldPath, float metersPerSecond, float startDelay, float vol, AudioClip clip, Action<WarAircraft> onDone)
        {
            path = worldPath; speed = metersPerSecond; delay = startDelay; volume = vol; done = onDone; next = 1;
            transform.position = path[0];
            if (path.Length > 1) transform.rotation = Quaternion.LookRotation((path[1] - path[0]).normalized, Vector3.up);
            IsFlying = true;
            gameObject.SetActive(true);
            if (engineAudio != null)
            {
                engineAudio.clip = clip; engineAudio.loop = kind == WarAircraftKind.Helicopter;
                engineAudio.spatialBlend = 1f; engineAudio.dopplerLevel = dopplerLevel;
                engineAudio.rolloffMode = AudioRolloffMode.Linear; engineAudio.minDistance = 25f; engineAudio.maxDistance = maxAudibleDistance;
                engineAudio.volume = volume; engineAudio.playOnAwake = false;
            }
            started = false;
            SetVisible(false);
        }

        private void SetVisible(bool v)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = v;
            if (afterburner != null) { if (v) afterburner.Play(); else afterburner.Stop(); }
        }

        private void Update()
        {
            if (!IsFlying) return;
            if (!started)
            {
                delay -= Time.deltaTime;
                if (delay > 0f) return;
                started = true; SetVisible(true);
                if (engineAudio != null && engineAudio.clip != null) engineAudio.Play();
            }
            if (next >= path.Length) { Finish(); return; }
            Vector3 to = path[next] - transform.position;
            float step = speed * Time.deltaTime;
            if (to.magnitude <= step) { transform.position = path[next]; next++; return; }
            Vector3 dir = to.normalized;
            transform.position += dir * step;
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            float turn = Vector3.SignedAngle(transform.forward, dir, Vector3.up);   // 진행 방향 변화에 맞춘 기울임
            bank = Mathf.Lerp(bank, Mathf.Clamp(-turn * 4f, -bankDegrees, bankDegrees), 3f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, look * Quaternion.Euler(0f, 0f, bank), (kind == WarAircraftKind.Jet ? 6f : 2.5f) * Time.deltaTime);
        }

        private void Finish()
        {
            IsFlying = false;
            if (engineAudio != null) engineAudio.Stop();
            SetVisible(false);
            gameObject.SetActive(false);
            done?.Invoke(this);
        }

        public void Abort() { if (IsFlying) Finish(); }
    }
}
