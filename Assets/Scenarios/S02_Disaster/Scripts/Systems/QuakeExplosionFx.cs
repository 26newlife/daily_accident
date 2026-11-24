using GameCore;
using UnityEngine;
using UnityEngine.Audio;

namespace GameGameplay
{
    public class QuakeExplosionFx : MonoBehaviour
    {
        private Light flash; private Transform ball; private float scale, age;

        public void Init(QuakeFx lib, float size)
        {
            scale = size;
            flash = gameObject.AddComponent<Light>();
            flash.type = LightType.Point; flash.color = QuakeFx.FireColor; flash.range = 12f * size; flash.intensity = 25f;
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(b.GetComponent<Collider>());
            b.name = "Fireball"; b.transform.SetParent(transform, false);
            b.GetComponent<MeshRenderer>().sharedMaterial = lib.Fireball;
            ball = b.transform;
            QuakeFx.Burst(transform, lib.Fire, QuakeFx.FireColor, 1.2f, 9f * size, 0.25f * size, 1f, 0.3f * size, 80);
            QuakeFx.Burst(transform, lib.Smoke, QuakeFx.SmokeColor, 3f, 2.5f * size, 2.2f * size, -0.05f, 0.8f * size, 25);
            QuakeFx.PlayAt(lib.ExplosionSound, transform.position, Mathf.Clamp01(0.7f * size), 0.12f, 90f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / 0.6f);
            ball.localScale = Vector3.one * Mathf.Lerp(0.5f, 4f * scale, t) * (1f - t * 0.9f);
            ball.gameObject.SetActive(t < 1f);
            flash.intensity = Mathf.Lerp(25f, 0f, Mathf.Clamp01(age / 0.8f));
            if (age >= 3f) Destroy(gameObject);
        }
    }
}
