using GameUI.EditorTools.Interior;
using UnityEngine;
using OM = GameUI.EditorTools.Outdoor.OutdoorMaterials;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 하이퍼 캐주얼 카툰풍 주민 모습 (프리미티브 조립, 콜라이더 없음 — 충돌은 NPC 의 CharacterController 가 맡는다).
    /// 큰 머리·통통한 몸통·짧은 다리·동글한 손. 정면 = +z.
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

        private static Material M(string hex) => OM.Lit("Npc_" + hex, OM.Hex(hex), 0.1f);

        /// <summary>
        /// 하이퍼 캐주얼 카툰 체형: 큰 둥근 머리(키의 약 40%), 통통한 알 모양 몸통, 짧은 다리, 동글한 손.
        /// 단색 · 단순한 점 눈 · 작은 입. (에셋 스토어 "Hyper Casual Cartoon People" 풍으로 프리미티브만 조립)
        /// </summary>
        public static GameObject Build(Transform parent, Style s)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);
            Transform v = visual.transform;
            float h = s.height;
            Material top = M(s.top), bottom = M(s.bottom), hair = M(s.hair), skin = M(s.skin), shoes = M(s.shoes);
            Material eye = M("1A1A1A"), white = M("F8F8F8"), cheek = M("F2A7A0"), mouth = M("7A3B3B");

            float legH = 0.20f * h, hip = legH;
            // 짧은 다리 + 동글한 신발
            foreach (float x in new[] { -0.075f * h, 0.075f * h })
            {
                Prim.Cyl(v, "Leg", new Vector3(x, legH * 0.5f + 0.02f * h, 0f), 0.05f * h, legH, bottom);
                Prim.Sph(v, "Shoe", new Vector3(x, 0.03f * h, 0.03f * h), new Vector3(0.12f * h, 0.07f * h, 0.17f * h), shoes);
            }
            if (s.skirt) Prim.Cyl(v, "Skirt", new Vector3(0f, hip + 0.01f * h, 0f), 0.16f * h, 0.2f * h, bottom);

            // 몸통 (엉덩이 위 기준 회전 — 굽은 등)
            var torso = new GameObject("Torso").transform;
            torso.SetParent(v, false);
            torso.localPosition = new Vector3(0f, hip, 0f);
            torso.localRotation = Quaternion.Euler(s.hunch ? 12f : 0f, 0f, 0f);
            if (!s.skirt) Prim.Sph(torso, "Hips", new Vector3(0f, 0.04f * h, 0f), new Vector3(0.30f * h, 0.17f * h, 0.22f * h), bottom);
            Prim.Sph(torso, "Chest", new Vector3(0f, 0.17f * h, 0f), new Vector3(0.34f * h, 0.34f * h, 0.25f * h), top);
            if (s.hood) Prim.Sph(torso, "Hood", new Vector3(0f, 0.31f * h, -0.06f * h), new Vector3(0.28f * h, 0.12f * h, 0.16f * h), top);

            // 큰 머리 + 머리카락 + 눈/볼/입
            float headD = 0.40f * h, headR = headD * 0.5f;
            Vector3 head = new Vector3(0f, 0.34f * h + headR * 0.85f, 0.01f * h);
            Prim.Sph(torso, "Head", head, Vector3.one * headD, skin);
            Prim.Sph(torso, "Hair", head + new Vector3(0f, headR * 0.22f, -headR * 0.14f), new Vector3(headD * 1.04f, headD * 0.82f, headD * 1.02f), hair);
            if (s.bun) Prim.Sph(torso, "HairBun", head + new Vector3(0f, headR * 0.85f, -headR * 0.55f), Vector3.one * headD * 0.36f, hair);
            foreach (float x in new[] { -0.07f * h, 0.07f * h })
            {
                Prim.Sph(torso, "EyeWhite", head + new Vector3(x, -0.01f * h, headR * 0.86f), new Vector3(0.07f * h, 0.085f * h, 0.03f * h), white);
                Prim.Sph(torso, "Eye", head + new Vector3(x, -0.012f * h, headR * 0.90f), new Vector3(0.04f * h, 0.055f * h, 0.03f * h), eye);
                Prim.Sph(torso, "Cheek", head + new Vector3(x * 1.45f, -0.07f * h, headR * 0.80f), new Vector3(0.05f * h, 0.03f * h, 0.02f * h), cheek);
            }
            Prim.Sph(torso, "Mouth", head + new Vector3(0f, -0.085f * h, headR * 0.92f), new Vector3(0.045f * h, 0.02f * h, 0.015f * h), mouth);

            // 팔: 짧고 통통한 소매 + 동글한 손. 왼팔은 늘어뜨림, 오른팔은 늘어뜨리거나(기본) 앞으로 뻗음(Arm)
            float ax = 0.20f * h;
            float shoulderY = 0.28f * h;
            Prim.Cyl(torso, "Arm_L", new Vector3(-ax, shoulderY - 0.09f * h, 0f), 0.04f * h, 0.20f * h, top, new Vector3(0f, 0f, -6f));
            Prim.Sph(torso, "Hand_L", new Vector3(-ax - 0.012f * h, shoulderY - 0.21f * h, 0f), Vector3.one * 0.075f * h, skin);
            if (s.reachingArm)
            {
                // GrandmotherNpc 가 z 를 0.18 ~ 0.30 으로 움직인다 — 루트 기준 위치 (몸통 회전과 무관하게)
                var arm = new GameObject("Arm").transform;
                arm.SetParent(v, false);
                arm.localPosition = new Vector3(ax, hip + shoulderY - 0.02f * h, 0.18f);
                Prim.Sph(arm, "ArmBox", new Vector3(0f, 0f, -0.05f), new Vector3(0.08f * h, 0.08f * h, 0.36f), top);
                Prim.Sph(arm, "Hand", new Vector3(0f, 0f, 0.15f), Vector3.one * 0.075f * h, skin);
            }
            else
            {
                Prim.Cyl(torso, "Arm_R", new Vector3(ax, shoulderY - 0.09f * h, 0f), 0.04f * h, 0.20f * h, top, new Vector3(0f, 0f, 6f));
                Prim.Sph(torso, "Hand_R", new Vector3(ax + 0.012f * h, shoulderY - 0.21f * h, 0f), Vector3.one * 0.075f * h, skin);
            }
            return visual;
        }
    }
}
