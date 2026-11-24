using UnityEngine;

namespace QuakeSurvival.Fx
{
    /// <summary>에셋 없이 코드로 파티클 시스템을 만든다(불꽃, 연기, 파편, 비).</summary>
    public static class ParticleFactory
    {
        public readonly struct Spec
        {
            public readonly Color Color;
            public readonly float Lifetime;
            public readonly float Speed;
            public readonly float Size;
            public readonly float Gravity;
            public readonly float ShapeRadius;

            public Spec(Color color, float lifetime, float speed, float size, float gravity, float shapeRadius)
            {
                Color = color;
                Lifetime = lifetime;
                Speed = speed;
                Size = size;
                Gravity = gravity;
                ShapeRadius = shapeRadius;
            }
        }

        public static ParticleSystem Burst(Transform parent, Material material, Spec spec, int count)
        {
            ParticleSystem ps = Create(parent, "Burst", material, spec, loop: false);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            ps.Play();
            return ps;
        }

        public static ParticleSystem Loop(Transform parent, Material material, Spec spec, float rate)
        {
            ParticleSystem ps = Create(parent, "Loop", material, spec, loop: true);
            var emission = ps.emission;
            emission.rateOverTime = rate;
            ps.Play();
            return ps;
        }

        private static ParticleSystem Create(Transform parent, string name, Material material, Spec spec, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = loop;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(spec.Lifetime * 0.6f, spec.Lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(spec.Speed * 0.5f, spec.Speed);
            main.startSize = new ParticleSystem.MinMaxCurve(spec.Size * 0.6f, spec.Size);
            main.startColor = spec.Color;
            main.gravityModifier = spec.Gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = spec.ShapeRadius;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            return ps;
        }
    }
}
