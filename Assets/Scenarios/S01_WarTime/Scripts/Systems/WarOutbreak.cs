using System.Collections.Generic;
using GameCore;
using GameData;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 전쟁 발발 연출 (창밖). Act 1 의 재난문자 직후 Begin() 이 호출되면:
    ///   ① 하늘(Skybox)·태양·환경광·안개가 평화로운 낮 → 잿빛/붉은빛의 어둡고 긴박한 색감으로 서서히 전환
    ///   ② 먼 도시 외곽에 검은 연기 기둥(파티클)과 간헐적인 포격 섬광(번쩍임 + 시차를 둔 폭음)
    ///   ③ 공습 사이렌·원거리 포격 엠비언트 + 전투기/헬기 편대 비행(WarFlyovers)
    /// Act 4(거짓 평화) 에서는 Calm() 으로 비행·포격을 멈추고(연기 기둥은 남는다), Act 5 에서 Stop() 한다.
    /// 시각 전환(보간)은 deltaTime 기반 연출이고, 포격·사이렌 일정은 GameClock 예약(WarTime.After)이다.
    /// </summary>
    public class WarOutbreak : MonoBehaviour
    {
        [SerializeField] private WarFlyovers flyovers;

        public static WarOutbreak Instance { get; private set; }
        public bool Started { get; private set; }
        public bool Active { get; private set; }
        public int PlumeCount => plumes.Count;
        public int FlashCount { get; private set; }
        public bool SkyTransitionDone { get; private set; }

        private struct Snapshot
        {
            public Vector3 sunEuler; public float sunIntensity; public Color sunColor, ambient, fogColor, skyTint, ground;
            public float fogDensity, atmosphere, exposure; public bool fogOn;
        }

        private readonly List<ParticleSystem> plumes = new List<ParticleSystem>();
        private Transform fxRoot;
        private Light sun, flashLight;
        private Material skyInstance, skyOriginal;
        private Snapshot from, to;
        private float lerpT = 1f, transitionSeconds = 9f;
        private bool transitioning;
        private int token;
        private float flashLevel;
        private Vector3 origin;

        private void Awake() { Instance = this; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (skyOriginal != null) RenderSettings.skybox = skyOriginal;
            if (skyInstance != null) Destroy(skyInstance);
        }

        // ═════════════════════ 시작 / 종료 ═════════════════════
        public void Begin()
        {
            WarConfig c = WarState.Instance != null ? WarState.Instance.Config : null;
            if (Started || c == null) return;
            Started = true; Active = true;
            int t = ++token;
            HouseController house = HouseController.Instance;
            if (house != null) house.ApplyTraffic(false, false, 0f);   // 거리의 사람과 차가 사라진다

            InteractableObject w = InteractableObject.FindById("window_living");
            origin = w != null ? w.transform.position : new Vector3(3.2f, 1.2f, 7.3f);

            CaptureSky(c);
            BuildFx(c);
            transitionSeconds = c.skyTransitionSeconds; lerpT = 0f; transitioning = true;

            WarAudio.Instance?.Play(WarSound.Siren, c.sirenVolume + 0.3f);
            ScheduleSiren(t); ScheduleFlash(t);
            if (flyovers != null) flyovers.StartFlights(c.flyoverStartDelay);
        }

        /// <summary>거짓 평화: 비행·포격·사이렌을 멈춘다 (연기 기둥은 그대로)</summary>
        public void Calm()
        {
            Active = false; token++;
            if (flyovers != null) flyovers.StopFlights();
        }

        public void Stop() { Calm(); }

        // ═════════════════════ 하늘 전환 ═════════════════════
        private void CaptureSky(WarConfig c)
        {
            sun = RenderSettings.sun != null ? RenderSettings.sun : FindDirectionalLight();
            skyOriginal = RenderSettings.skybox;
            if (skyOriginal != null) { skyInstance = new Material(skyOriginal) { name = skyOriginal.name + "_War" }; RenderSettings.skybox = skyInstance; }

            from = new Snapshot
            {
                sunEuler = sun != null ? sun.transform.eulerAngles : Vector3.zero,
                sunIntensity = sun != null ? sun.intensity : 1f,
                sunColor = sun != null ? sun.color : Color.white,
                ambient = RenderSettings.ambientLight,
                fogOn = RenderSettings.fog, fogColor = RenderSettings.fogColor, fogDensity = RenderSettings.fog ? RenderSettings.fogDensity : 0f,
                skyTint = Prop("_SkyTint", new Color(0.5f, 0.5f, 0.5f)), ground = Prop("_GroundColor", new Color(0.4f, 0.4f, 0.4f)),
                atmosphere = Num("_AtmosphereThickness", 1f), exposure = Num("_Exposure", 1.3f),
            };
            WarConfig.SkyProfile p = c.warSky;
            to = new Snapshot
            {
                sunEuler = p.sunEuler, sunIntensity = p.sunIntensity, sunColor = p.sunColor, ambient = p.ambientColor,
                fogOn = true, fogColor = p.fogColor, fogDensity = p.fogDensity, skyTint = p.skyTint, ground = p.groundColor,
                atmosphere = p.atmosphereThickness, exposure = p.exposure,
            };
            if (!from.fogOn) from.fogColor = p.fogColor;
        }

        private static Light FindDirectionalLight()
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l.type == LightType.Directional) return l;
            return null;
        }

        private Color Prop(string n, Color fallback) => skyInstance != null && skyInstance.HasProperty(n) ? skyInstance.GetColor(n) : fallback;
        private float Num(string n, float fallback) => skyInstance != null && skyInstance.HasProperty(n) ? skyInstance.GetFloat(n) : fallback;

        private void Apply(Snapshot a, Snapshot b, float k)
        {
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(a.sunEuler), Quaternion.Euler(b.sunEuler), k);
                sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, k);
                sun.color = Color.Lerp(a.sunColor, b.sunColor, k);
            }
            RenderSettings.ambientLight = Color.Lerp(a.ambient, b.ambient, k);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(a.fogColor, b.fogColor, k);
            RenderSettings.fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, k);
            if (skyInstance != null)
            {
                if (skyInstance.HasProperty("_SkyTint")) skyInstance.SetColor("_SkyTint", Color.Lerp(a.skyTint, b.skyTint, k));
                if (skyInstance.HasProperty("_GroundColor")) skyInstance.SetColor("_GroundColor", Color.Lerp(a.ground, b.ground, k));
                if (skyInstance.HasProperty("_AtmosphereThickness")) skyInstance.SetFloat("_AtmosphereThickness", Mathf.Lerp(a.atmosphere, b.atmosphere, k));
                if (skyInstance.HasProperty("_Exposure")) skyInstance.SetFloat("_Exposure", Mathf.Lerp(a.exposure, b.exposure, k));
                DynamicGI.UpdateEnvironment();
            }
        }

        /// <summary>이후 Act 의 환경 프리셋(밤/새벽/낙진/구조)에 맞춰 하늘 재질을 바꾼다 (프리셋은 태양·환경광·안개만 다루므로)</summary>
        public void MatchSkyToPreset(EnvironmentPreset p)
        {
            if (p == null || skyInstance == null) return;
            transitioning = false;
            Color tint; float exposure, thick;
            if (p.timeOfDay == TimeOfDayKind.Night) { tint = new Color(0.08f, 0.1f, 0.18f); exposure = 0.18f; thick = 0.6f; }
            else if (p.windowView == WindowViewMode.DarkSmoky) { tint = new Color(0.2f, 0.2f, 0.22f); exposure = 0.32f; thick = 3f; }
            else if (p.timeOfDay == TimeOfDayKind.Morning) { tint = new Color(0.62f, 0.5f, 0.45f); exposure = 0.9f; thick = 1.4f; }
            else return;
            if (skyInstance.HasProperty("_SkyTint")) skyInstance.SetColor("_SkyTint", tint);
            if (skyInstance.HasProperty("_Exposure")) skyInstance.SetFloat("_Exposure", exposure);
            if (skyInstance.HasProperty("_AtmosphereThickness")) skyInstance.SetFloat("_AtmosphereThickness", thick);
            DynamicGI.UpdateEnvironment();
        }

        private void Update()
        {
            if (transitioning)
            {
                lerpT += Time.deltaTime / Mathf.Max(0.5f, transitionSeconds);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lerpT));
                Apply(from, to, k);
                if (lerpT >= 1f) { transitioning = false; SkyTransitionDone = true; }
            }
            if (flashLight != null)
            {
                flashLevel = Mathf.MoveTowards(flashLevel, 0f, Time.deltaTime * 2.2f);
                flashLight.intensity = flashLevel * 6f;
                flashLight.enabled = flashLevel > 0.01f;
            }
        }

        // ═════════════════════ 연기 기둥 · 섬광 ═════════════════════
        private void BuildFx(WarConfig c)
        {
            fxRoot = new GameObject("WarOutbreakFx").transform;
            fxRoot.SetParent(transform, false);
            float groundY = origin.y - 28.5f;   // 1층 지면 (집 바닥 y=0 기준 약 28.5m 아래)
            var rng = new System.Random(1004);
            for (int i = 0; i < c.plumeCount; i++)
            {
                float u = (i + 0.5f) / Mathf.Max(1, c.plumeCount);
                float x = origin.x + Mathf.Lerp(-c.plumeSpread, c.plumeSpread, u) + (float)(rng.NextDouble() - 0.5) * 60f;
                float z = origin.z + Mathf.Lerp(c.plumeDistance.x, c.plumeDistance.y, (float)rng.NextDouble());
                plumes.Add(MakePlume(new Vector3(x, groundY, z), c, i));
            }
            // 번쩍임 광원 (섬광 때 잠깐 켜진다)
            var lgo = new GameObject("FlashLight");
            lgo.transform.SetParent(fxRoot, false);
            flashLight = lgo.AddComponent<Light>();
            flashLight.type = LightType.Point; flashLight.range = 400f; flashLight.color = new Color(1f, 0.6f, 0.3f); flashLight.intensity = 0f; flashLight.enabled = false;
            flashLight.shadows = LightShadows.None;
        }

        private ParticleSystem MakePlume(Vector3 pos, WarConfig c, int seed)
        {
            var go = new GameObject("Plume_" + seed);
            go.transform.SetParent(fxRoot, false);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 26f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(c.plumeHeight / 20f, c.plumeHeight / 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(22f, 46f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.05f, 0.05f, 0.06f, 0.75f), new Color(0.16f, 0.14f, 0.14f, 0.6f));
            main.maxParticles = 300;
            var em = ps.emission; em.rateOverTime = c.plumeEmissionRate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 5f; shape.radius = 7f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(5f, 9f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);   // 바람에 기울어진 기둥
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2.1f)));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.8f, 0.8f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = WarFx.Particle(false); rend.renderMode = ParticleSystemRenderMode.Billboard; rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 밑동의 불길
            var fire = new GameObject("Glow");
            fire.transform.SetParent(go.transform, false);
            var fps = fire.AddComponent<ParticleSystem>();
            fps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var fm = fps.main; fm.loop = true; fm.playOnAwake = false; fm.simulationSpace = ParticleSystemSimulationSpace.World;
            fm.startLifetime = 2.2f; fm.startSpeed = 2f; fm.startSize = new ParticleSystem.MinMaxCurve(14f, 26f);
            fm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f, 0.55f), new Color(1f, 0.7f, 0.25f, 0.4f));
            var fe = fps.emission; fe.rateOverTime = 4f;
            var fs = fps.shape; fs.shapeType = ParticleSystemShapeType.Circle; fs.radius = 9f;
            fire.GetComponent<ParticleSystemRenderer>().sharedMaterial = WarFx.Particle(true);
            fps.Play(); ps.Play();
            ps.Simulate(14f, true, false); ps.Play();   // 이미 한참 타오른 기둥처럼 보이게 미리 진행
            return ps;
        }

        private void ScheduleFlash(int t)
        {
            WarConfig c = WarState.Instance != null ? WarState.Instance.Config : null;
            if (c == null) return;
            WarTime.After(Random.Range(c.flashInterval.x, c.flashInterval.y), () =>
            {
                if (token != t || !Active || GameFlow.IsEnded) return;
                DoFlash(c);
                ScheduleFlash(t);
            });
        }

        private void DoFlash(WarConfig c)
        {
            FlashCount++;
            flashLevel = 1f;
            if (flashLight != null && plumes.Count > 0)
            {
                ParticleSystem p = plumes[Random.Range(0, plumes.Count)];
                flashLight.transform.position = p.transform.position + Vector3.up * 30f;
            }
            float delay = Random.Range(c.flashSoundDelay.x, c.flashSoundDelay.y);
            bool shake = Random.value < c.flashShakeChance;
            WarTime.After(delay, () =>
            {
                if (GameFlow.IsEnded) return;
                WarAudio.Instance?.Play(WarSound.Boom, c.distantBoomVolume);
                if (shake) HouseController.Instance?.TriggerStructureShake(StructureShakePreset.Light);
            });
        }

        private void ScheduleSiren(int t)
        {
            WarConfig c = WarState.Instance != null ? WarState.Instance.Config : null;
            if (c == null) return;
            WarTime.After(Random.Range(c.sirenInterval.x, c.sirenInterval.y), () =>
            {
                if (token != t || !Active || GameFlow.IsEnded) return;
                WarAudio.Instance?.Play(WarSound.Siren, c.sirenVolume);
                ScheduleSiren(t);
            });
        }
    }
}
