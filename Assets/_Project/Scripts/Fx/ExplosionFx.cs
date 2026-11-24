using QuakeSurvival.Audio;
using QuakeSurvival.Art;
using UnityEngine;

namespace QuakeSurvival.Fx
{
    /// <summary>
    /// 한 번 터지는 폭발: 섬광 조명 + 커지며 사라지는 불덩이 + 불꽃/연기 파티클.
    /// Spawn으로 만들고 수명이 끝나면 스스로 사라진다.
    /// </summary>
    public sealed class ExplosionFx : MonoBehaviour
    {
        private const float Lifetime = 3f;
        private const float FireballSeconds = 0.6f;

        private Light flash;
        private Transform fireball;
        private float scale;
        private float age;

        public static ExplosionFx Spawn(FxLibrary lib, Vector3 position, float scale)
        {
            var go = new GameObject("Explosion");
            go.transform.position = position;
            var fx = go.AddComponent<ExplosionFx>();
            fx.Init(lib, scale);
            return fx;
        }

        private void Init(FxLibrary lib, float size)
        {
            scale = size;
            flash = gameObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = Palette.Fire;
            flash.range = 12f * size;
            flash.intensity = 25f;

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.name = "Fireball";
            ball.transform.SetParent(transform, false);
            ball.GetComponent<MeshRenderer>().sharedMaterial = lib.Fireball;
            fireball = ball.transform;

            var sparks = new ParticleFactory.Spec(Palette.Fire, 1.2f, 9f * size, 0.25f * size, 1f, 0.3f * size);
            var smoke = new ParticleFactory.Spec(Palette.Smoke, 3f, 2.5f * size, 2.2f * size, -0.05f, 0.8f * size);
            ParticleFactory.Burst(transform, lib.Fire, sparks, 80);
            ParticleFactory.Burst(transform, lib.Smoke, smoke, 25);
            SoundPlayer.PlayAt(lib.ExplosionSound, transform.position, AudioCategory.Sfx, Mathf.Clamp01(0.7f * size), 0.12f, 90f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / FireballSeconds);
            fireball.localScale = Vector3.one * Mathf.Lerp(0.5f, 4f * scale, t) * (1f - t * 0.9f);
            fireball.gameObject.SetActive(t < 1f);
            flash.intensity = Mathf.Lerp(25f, 0f, Mathf.Clamp01(age / 0.8f));
            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
