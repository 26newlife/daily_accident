using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 단지 앞 도로를 사이렌을 울리며 지나가는 구급차 / 소방차 (런타임 생성, 프리미티브 + 합성 사이렌).
    /// 도로를 따라 멀리서 다가와 건물 앞을 지나 사라지거나(stopAtBuilding = false), 건물 앞에 서서 경광등을 켜 둔다(true).
    /// 소리는 3D(거리 감쇠 + 도플러)라 멀리서 작게 들리다가 가까워질수록 커진다.
    /// 만든 쪽(OutdoorWorldController)이 창문 개방 정도에 따라 소리 크기를 조절한다 (SetVolumeScale).
    /// </summary>
    public class EmergencyVehicle : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private EmergencyKind kind;
        private Vector3 start, end, stopPoint;
        private bool stopAtBuilding, stopped;
        private float speed, cruiseSpeed = 17f;
        private AudioSource siren;
        private float sirenBase = 0.55f, volumeScale = 1f;
        private Light redLight, blueLight;
        private Renderer redLamp, blueLamp;
        private float flashT;

        public EmergencyKind Kind => kind;
        public bool IsStopped => stopped;

        /// <summary>차량 하나를 만들어 출발시킨다. roadStart → roadEnd 로 달리며, stopAtBuilding 이면 stopPoint 에서 정차</summary>
        public static EmergencyVehicle Spawn(EmergencyKind kind, Vector3 roadStart, Vector3 roadEnd, Vector3 stopPoint, bool stopAtBuilding, Material template, float cruise)
        {
            var go = new GameObject(kind == EmergencyKind.FireTruck ? "FireTruck_Road" : "Ambulance_Road");
            var v = go.AddComponent<EmergencyVehicle>();
            v.kind = kind; v.start = roadStart; v.end = roadEnd; v.stopPoint = stopPoint; v.stopAtBuilding = stopAtBuilding;
            v.cruiseSpeed = cruise;
            go.transform.position = roadStart;
            go.transform.rotation = Quaternion.LookRotation((roadEnd - roadStart).normalized, Vector3.up);
            if (kind == EmergencyKind.FireTruck) v.BuildFireTruck(template); else v.BuildAmbulance(template);
            v.BuildSiren(kind == EmergencyKind.FireTruck);
            v.speed = cruise;
            return v;
        }

        public void SetVolumeScale(float scale)
        {
            volumeScale = scale;
            if (siren != null) siren.volume = sirenBase * volumeScale;
        }

        // ───────── 이동 ─────────
        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 경광등 깜빡임 (빨강/파랑 번갈아)
            flashT += dt;
            bool redOn = Mathf.Repeat(flashT, 0.5f) < 0.25f;
            if (redLight != null) redLight.enabled = redOn;
            if (blueLight != null) blueLight.enabled = !redOn;
            Tint(redLamp, redOn ? new Color(1f, 0.1f, 0.08f) : new Color(0.25f, 0.02f, 0.02f));
            Tint(blueLamp, redOn ? new Color(0.02f, 0.05f, 0.25f) : new Color(0.15f, 0.35f, 1f));

            if (stopped) return;

            Vector3 target = stopAtBuilding ? stopPoint : end;
            Vector3 to = target - transform.position; to.y = 0f;
            float dist = to.magnitude;

            float desired = cruiseSpeed;
            if (stopAtBuilding) desired = Mathf.Min(cruiseSpeed, Mathf.Max(2.5f, Mathf.Sqrt(2f * 4f * dist)));   // 건물 앞에서 부드럽게 감속
            speed = Mathf.MoveTowards(speed, desired, 6f * dt);

            if (dist < 0.8f)
            {
                if (stopAtBuilding) { stopped = true; speed = 0f; if (siren != null) siren.volume = 0f; if (siren != null) siren.Stop(); }
                else Destroy(gameObject);
                return;
            }
            transform.position += to.normalized * (speed * dt);
        }

        private void Tint(Renderer r, Color c)
        {
            if (r == null) return;
            r.material.SetColor(BaseColorId, c);
            r.material.color = c;
        }

        // ───────── 사이렌 (합성) ─────────
        private void BuildSiren(bool wail)
        {
            siren = gameObject.AddComponent<AudioSource>();
            siren.clip = wail ? MakeWail() : MakeYelp();
            siren.loop = true;
            siren.spatialBlend = 1f;
            siren.rolloffMode = AudioRolloffMode.Logarithmic;
            siren.minDistance = 10f;
            siren.maxDistance = 260f;
            siren.dopplerLevel = 1.2f;
            siren.volume = sirenBase * volumeScale;
            siren.Play();
        }

        /// <summary>소방차: 길게 올라갔다 내려오는 윙 (4초 주기)</summary>
        private static AudioClip MakeWail()
        {
            const int rate = 22050; const float period = 4f;
            int n = (int)(rate * period);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(620f, 1450f, 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f));
                phase += f / rate * Mathf.PI * 2f;
                data[i] = 0.32f * (Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f) + 0.15f * Mathf.Sin(phase * 3f));
            }
            var clip = AudioClip.Create("SirenWail", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>구급차: 두 음을 번갈아 내는 빠른 삑-뽀 (1.2초 주기)</summary>
        private static AudioClip MakeYelp()
        {
            const int rate = 22050; const float period = 1.2f;
            int n = (int)(rate * period);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = t < 0.5f ? 960f : 720f;
                phase += f / rate * Mathf.PI * 2f;
                float env = Mathf.Clamp01(Mathf.Min(Mathf.Repeat(t, 0.5f), 0.5f - Mathf.Repeat(t, 0.5f)) * 60f);   // 음 사이 클릭 방지
                data[i] = 0.34f * env * (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f));
            }
            var clip = AudioClip.Create("SirenYelp", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ───────── 차체 (프리미티브) ─────────
        private Material Mat(Material template, Color c)
        {
            Material m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.SetColor(BaseColorId, c);
            m.color = c;
            return m;
        }

        private Renderer Part(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            GameObject p = GameObject.CreatePrimitive(type);
            p.name = name;
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(transform, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = Quaternion.Euler(euler);
            p.transform.localScale = scale;
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            return r;
        }

        private void Wheels(Material tire, float halfW, float[] zs, float radius)
        {
            foreach (float z in zs)
                foreach (float x in new[] { -halfW, halfW })
                    Part("Wheel", PrimitiveType.Cylinder, new Vector3(x, radius, z), new Vector3(radius * 2f, 0.14f, radius * 2f), tire, new Vector3(0f, 0f, 90f));
        }

        private void Lights(float y, float z, float half)
        {
            Material red = Mat(null, new Color(1f, 0.1f, 0.08f)), blue = Mat(null, new Color(0.15f, 0.35f, 1f));
            redLamp = Part("LightBar_Red", PrimitiveType.Cube, new Vector3(-half, y, z), new Vector3(half * 1.8f, 0.16f, 0.3f), red);
            blueLamp = Part("LightBar_Blue", PrimitiveType.Cube, new Vector3(half, y, z), new Vector3(half * 1.8f, 0.16f, 0.3f), blue);
            redLight = MakeLight(new Vector3(-half, y + 0.3f, z), new Color(1f, 0.1f, 0.08f));
            blueLight = MakeLight(new Vector3(half, y + 0.3f, z), new Color(0.2f, 0.4f, 1f));
        }

        private Light MakeLight(Vector3 pos, Color c)
        {
            var go = new GameObject("Strobe");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = 14f; l.intensity = 6f; l.shadows = LightShadows.None;
            return l;
        }

        private void BuildAmbulance(Material template)
        {
            Material white = Mat(template, new Color(0.95f, 0.96f, 0.97f)), red = Mat(template, new Color(0.86f, 0.15f, 0.15f));
            Material glass = Mat(template, new Color(0.1f, 0.16f, 0.22f)), tire = Mat(template, new Color(0.08f, 0.08f, 0.09f));
            Material head = Mat(template, new Color(1f, 0.96f, 0.8f));
            Part("Body", PrimitiveType.Cube, new Vector3(0f, 1.55f, -0.4f), new Vector3(2.1f, 2.0f, 4.6f), white);
            Part("Hood", PrimitiveType.Cube, new Vector3(0f, 1.0f, 2.4f), new Vector3(2.0f, 1.0f, 1.3f), white);
            Part("Windshield", PrimitiveType.Cube, new Vector3(0f, 1.55f, 1.75f), new Vector3(1.9f, 0.7f, 0.08f), glass);
            Part("StripeL", PrimitiveType.Cube, new Vector3(-1.06f, 1.2f, -0.4f), new Vector3(0.03f, 0.3f, 4.6f), red);
            Part("StripeR", PrimitiveType.Cube, new Vector3(1.06f, 1.2f, -0.4f), new Vector3(0.03f, 0.3f, 4.6f), red);
            Part("CrossL", PrimitiveType.Cube, new Vector3(-1.07f, 1.8f, -0.4f), new Vector3(0.03f, 0.7f, 0.2f), red);
            Part("CrossL2", PrimitiveType.Cube, new Vector3(-1.07f, 1.8f, -0.4f), new Vector3(0.03f, 0.2f, 0.7f), red);
            Part("CrossR", PrimitiveType.Cube, new Vector3(1.07f, 1.8f, -0.4f), new Vector3(0.03f, 0.7f, 0.2f), red);
            Part("CrossR2", PrimitiveType.Cube, new Vector3(1.07f, 1.8f, -0.4f), new Vector3(0.03f, 0.2f, 0.7f), red);
            foreach (float x in new[] { -0.7f, 0.7f }) Part("Headlight", PrimitiveType.Cube, new Vector3(x, 0.85f, 3.07f), new Vector3(0.35f, 0.2f, 0.05f), head);
            Wheels(tire, 1.0f, new[] { -1.7f, 2.0f }, 0.45f);
            Lights(2.65f, 1.4f, 0.55f);
        }

        private void BuildFireTruck(Material template)
        {
            Material red = Mat(template, new Color(0.8f, 0.1f, 0.1f)), white = Mat(template, new Color(0.95f, 0.95f, 0.95f));
            Material glass = Mat(template, new Color(0.1f, 0.16f, 0.22f)), tire = Mat(template, new Color(0.08f, 0.08f, 0.09f));
            Material steel = Mat(template, new Color(0.62f, 0.64f, 0.68f)), head = Mat(template, new Color(1f, 0.96f, 0.8f));
            Part("Body", PrimitiveType.Cube, new Vector3(0f, 1.75f, -0.9f), new Vector3(2.4f, 2.3f, 5.6f), red);
            Part("Cab", PrimitiveType.Cube, new Vector3(0f, 1.6f, 2.55f), new Vector3(2.4f, 2.0f, 1.9f), red);
            Part("Windshield", PrimitiveType.Cube, new Vector3(0f, 2.0f, 3.53f), new Vector3(2.2f, 0.8f, 0.08f), glass);
            Part("StripeL", PrimitiveType.Cube, new Vector3(-1.21f, 1.5f, -0.9f), new Vector3(0.03f, 0.25f, 5.6f), white);
            Part("StripeR", PrimitiveType.Cube, new Vector3(1.21f, 1.5f, -0.9f), new Vector3(0.03f, 0.25f, 5.6f), white);
            Part("LadderL", PrimitiveType.Cube, new Vector3(-0.35f, 3.0f, -1.0f), new Vector3(0.1f, 0.12f, 5.4f), steel);
            Part("LadderR", PrimitiveType.Cube, new Vector3(0.35f, 3.0f, -1.0f), new Vector3(0.1f, 0.12f, 5.4f), steel);
            for (int i = 0; i < 9; i++) Part("Rung", PrimitiveType.Cube, new Vector3(0f, 3.0f, -3.4f + i * 0.6f), new Vector3(0.7f, 0.06f, 0.06f), steel);
            foreach (float x in new[] { -0.8f, 0.8f }) Part("Headlight", PrimitiveType.Cube, new Vector3(x, 1.1f, 3.52f), new Vector3(0.4f, 0.22f, 0.05f), head);
            Wheels(tire, 1.1f, new[] { -2.4f, -0.4f, 2.6f }, 0.55f);
            Lights(2.75f, 2.2f, 0.6f);
        }
    }
}
