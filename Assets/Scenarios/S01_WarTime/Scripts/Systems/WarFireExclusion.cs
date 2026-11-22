using System.Collections.Generic;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 1 에서 시나리오 3(아파트 화재) 소품을 완전히 제외한다.
    /// 집 에셋(공유)에는 가스 밸브·멀티탭·소화기·수건 같은 화재용 상호작용 오브젝트가 들어 있다 — 집을 지우거나 고치지 않고,
    /// 시나리오 1 이 시작될 때 그 오브젝트를 비활성화(숨김 + 상호작용 불가)한다. 시나리오 3 을 실행하면 이 코드는 돌지 않으므로 그대로 동작한다.
    /// (가스 누출·화재 확산·연기·FireSource·Act 로직은 시나리오 3 씬에만 있어 시나리오 1 에는 로드되지 않는다.)
    /// </summary>
    public static class WarFireExclusion
    {
        private static readonly string[] IdTokens =
        {
            "gas_", "fuse", "multitap", "extinguisher", "towel", "frying", "handle_check", "duct_tape", "water_pot", "range_hood",
            "fire_curtain", "fire_sofa", "fire_pan", "fire_multitap", "fire_corridor", "clutter_", "smoke",
        };
        private static readonly string[] NameTokens = { "Extinguisher", "Hydrant", "FireAlarm" };

        public static int LastExcluded { get; private set; }

        public static bool IsFireId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (string t in IdTokens) if (id.Contains(t)) return true;
            return false;
        }

        public static void Apply()
        {
            var hide = new List<GameObject>();
            foreach (InteractableObject o in Object.FindObjectsByType<InteractableObject>(FindObjectsInactive.Include))
                if (IsFireId(o.Id)) hide.Add(o.gameObject);
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                foreach (string n in NameTokens) if (t.name.Contains(n)) { hide.Add(t.gameObject); break; }
            // 시나리오 3 시스템이 혹시 로드되어 있으면 함께 끈다 (정상이라면 0개)
            foreach (FireSource f in Object.FindObjectsByType<FireSource>(FindObjectsInactive.Include)) hide.Add(f.gameObject);
            foreach (GasLeakController g in Object.FindObjectsByType<GasLeakController>(FindObjectsInactive.Include)) g.enabled = false;
            foreach (FireManager m in Object.FindObjectsByType<FireManager>(FindObjectsInactive.Include)) m.enabled = false;

            int n2 = 0;
            foreach (GameObject g in hide) if (g != null && g.activeSelf) { g.SetActive(false); n2++; }
            LastExcluded = n2;
            Debug.Log($"[S01] 시나리오 3(화재) 소품 {n2}개를 제외했습니다.");
        }
    }
}
