using GameUI.EditorTools.Interior;
using UnityEngine;
using OM = GameUI.EditorTools.Outdoor.OutdoorMaterials;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 그레이박스 주민 모습 (프리미티브 조립, 콜라이더 없음 — 충돌은 NPC 의 CharacterController 가 맡는다).
    /// 다리·신발·몸통(또는 치마)·팔·손·목·머리·머리카락·눈. 정면 = +z.
    /// 반환값은 "Visual" 루트 (NpcBase.visualRoot). reachingArm 이면 오른팔이 "Arm" 이라는 이름으로 앞으로 뻗어 있다
    /// (GrandmotherNpc 가 Arm 의 localPosition.z 를 움직여 엘리베이터 버튼을 누른다).
    /// </summary>
    public static class NpcLook
    {
        public struct Style
        {
            public float height;
            public string top, bottom, hair, skin, shoes;
            public bool skirt, hood, bun, hunch, reachingArm;
        }

        public static Style Grandmother => new Style { height = 1.45f, top = "8E6C8A", bottom = "3F3A4A", hair = "CFCFCF", skin = "E6C2A4", shoes = "3A2E2A", skirt = true, bun = true, hunch = true, reachingArm = true };
        public static Style YoungMan => new Style { height = 1.75f, top = "2F4A7A", bottom = "3B4E6B", hair = "2A2420", skin = "DDB896", shoes = "E5E7EB", hood = true };

        /// <summary>집결지 주민 i 번 (옷·머리색·키가 조금씩 다르다)</summary>
        public static Style Resident(int i)
        {
            string[] tops = { "8B5E3C", "5B7FA6", "9A6B8E", "6E8B5B", "B0813E", "57606B", "A64B4B", "4B7F7A" };
            string[] bottoms = { "2F3440", "4A4A52", "3B4E6B", "5A4636", "2F3440", "6B6358", "3F3A4A", "33415C" };
            string[] hairs = { "2A2420", "4A3426", "CFCFCF", "1E1B18", "6B4A2E", "2A2420", "8A8A8A", "3A2A1E" };
            string[] skins = { "E6C2A4", "DDB896", "D9AE8C", "E8C9AE" };
            bool child = i % 5 == 3;
            return new Style
            {
                height = child ? 1.2f : 1.55f + 0.06f * (i % 4),
                top = tops[i % tops.Length], bottom = bottoms[i % bottoms.Length], hair = hairs[i % hairs.Length],
                skin = skins[i % skins.Length], shoes = i % 2 == 0 ? "2B2B2B" : "E5E7EB",
                skirt = i % 4 == 2, hood = i % 3 == 1, bun = hairs[i % hairs.Length] == "CFCFCF", hunch = hairs[i % hairs.Length] == "CFCFCF",
            };
        }

        private static Material M(string hex) => OM.Lit("Npc_" + hex, OM.Hex(hex), 0.15f);

        public static GameObject Build(Transform parent, Style s)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);
            Transform v = visual.transform;
            float h = s.height, k = h / 1.7f;
            Material top = M(s.top), bottom = M(s.bottom), hair = M(s.hair), skin = M(s.skin), shoes = M(s.shoes), eye = M("1A1A1A");

            float hip = 0.47f * h, shoulder = 0.80f * h;
            // 다리 + 신발
            foreach (float x in new[] { -0.075f * k, 0.075f * k })
            {
                Prim.Box(v, "Leg", new Vector3(x, hip * 0.5f + 0.03f, 0f), new Vector3(0.11f * k, hip - 0.04f, 0.13f * k), bottom);
                Prim.Box(v, "Shoe", new Vector3(x, 0.035f, 0.03f * k), new Vector3(0.12f * k, 0.07f, 0.22f * k), shoes);
            }
            if (s.skirt) Prim.Cyl(v, "Skirt", new Vector3(0f, hip - 0.1f * h, 0f), 0.18f * k, 0.3f * h, bottom);

            // 몸통 (굽은 등은 위쪽만 살짝 앞으로)
            var torso = new GameObject("Torso").transform;
            torso.SetParent(v, false);
            torso.localPosition = new Vector3(0f, hip, 0f);
            torso.localRotation = Quaternion.Euler(s.hunch ? 12f : 0f, 0f, 0f);
            float th = shoulder - hip;
            Prim.Box(torso, "Chest", new Vector3(0f, th * 0.5f, 0f), new Vector3(0.36f * k, th, 0.21f * k), top);
            if (s.hood) Prim.Box(torso, "Hood", new Vector3(0f, th - 0.02f, -0.11f * k), new Vector3(0.26f * k, 0.12f * k, 0.1f * k), top);
            // 목 + 머리 + 머리카락 + 눈
            float headR = 0.115f * k;
            Prim.Cyl(torso, "Neck", new Vector3(0f, th + 0.035f * k, 0f), 0.045f * k, 0.08f * k, skin);
            Vector3 head = new Vector3(0f, th + 0.07f * k + headR, 0.01f);
            Prim.Sph(torso, "Head", head, Vector3.one * headR * 2f, skin);
            Prim.Sph(torso, "Hair", head + new Vector3(0f, 0.03f * k, -0.025f * k), new Vector3(headR * 2.08f, headR * 1.7f, headR * 2.0f), hair);
            if (s.bun) Prim.Sph(torso, "HairBun", head + new Vector3(0f, headR * 0.55f, -headR * 0.85f), Vector3.one * headR * 0.85f, hair);
            foreach (float x in new[] { -0.04f * k, 0.04f * k })
                Prim.Box(torso, "Eye", head + new Vector3(x, 0.01f * k, headR * 0.93f), new Vector3(0.022f * k, 0.022f * k, 0.01f), eye);

            // 팔: 왼팔은 늘어뜨림, 오른팔은 늘어뜨리거나(기본) 앞으로 뻗음(Arm)
            float armLen = 0.36f * h;
            float ax = 0.18f * k + 0.05f * k;
            Prim.Box(torso, "Arm_L", new Vector3(-ax, th - armLen * 0.5f, 0f), new Vector3(0.09f * k, armLen, 0.1f * k), top, new Vector3(0f, 0f, -4f));
            Prim.Box(torso, "Hand_L", new Vector3(-ax - 0.01f, th - armLen - 0.03f * k, 0f), new Vector3(0.07f * k, 0.08f * k, 0.08f * k), skin);
            if (s.reachingArm)
            {
                // GrandmotherNpc 가 z 를 0.18 ~ 0.30 으로 움직인다 — 루트 기준 위치 (몸통 회전과 무관하게)
                var arm = new GameObject("Arm").transform;
                arm.SetParent(v, false);
                arm.localPosition = new Vector3(ax, shoulder - 0.06f * k, 0.18f);
                Prim.Box(arm, "ArmBox", new Vector3(0f, 0f, -0.05f), new Vector3(0.09f * k, 0.09f * k, 0.36f), top);
                Prim.Box(arm, "Hand", new Vector3(0f, 0f, 0.16f), new Vector3(0.07f * k, 0.08f * k, 0.08f * k), skin);
            }
            else
            {
                Prim.Box(torso, "Arm_R", new Vector3(ax, th - armLen * 0.5f, 0f), new Vector3(0.09f * k, armLen, 0.1f * k), top, new Vector3(0f, 0f, 4f));
                Prim.Box(torso, "Hand_R", new Vector3(ax + 0.01f, th - armLen - 0.03f * k, 0f), new Vector3(0.07f * k, 0.08f * k, 0.08f * k), skin);
            }
            return visual;
        }
    }
}
