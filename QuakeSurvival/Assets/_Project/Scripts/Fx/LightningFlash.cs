using System.Collections;
using UnityEngine;

namespace QuakeSurvival.Fx
{
    /// <summary>
    /// 폭풍 번개: 불규칙한 간격으로 차가운 섬광을 한두 번 번쩍인다.
    /// 해 질 녘 키 라이트의 세기를 잠깐 올렸다가 되돌린다.
    /// </summary>
    public sealed class LightningFlash : MonoBehaviour
    {
        [SerializeField] private Light sky;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashIntensity = 3.5f;
        [SerializeField] private Vector2 intervalRange = new Vector2(7f, 18f);

        /// <summary>번개가 칠 때마다(한 번의 섬광 묶음 시작) 알린다. 천둥 소리가 이어 받는다.</summary>
        public event System.Action Flashed;

        private float baseIntensity;
        private Color baseColor;

        private void Awake()
        {
            if (sky == null)
            {
                Debug.LogError("[LightningFlash] sky 조명이 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            baseIntensity = sky.intensity;
            baseColor = sky.color;
        }

        private void OnEnable()
        {
            if (sky != null) StartCoroutine(Loop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (sky == null) return;
            sky.intensity = baseIntensity;
            sky.color = baseColor;
        }

        private IEnumerator Loop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(intervalRange.x, intervalRange.y));
                int flashes = Random.value < 0.5f ? 1 : 2;
                Flashed?.Invoke();
                for (int i = 0; i < flashes; i++)
                {
                    yield return Flash(Random.Range(0.06f, 0.12f));
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
                }
            }
        }

        private IEnumerator Flash(float seconds)
        {
            sky.color = flashColor;
            sky.intensity = flashIntensity;
            yield return new WaitForSeconds(seconds);
            sky.color = baseColor;
            sky.intensity = baseIntensity;
        }
    }
}
