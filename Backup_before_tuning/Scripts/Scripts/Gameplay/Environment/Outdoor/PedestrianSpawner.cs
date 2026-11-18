using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 단지 보행자 생성기. 경로 정의(RouteDef)마다 역할/인원/속도를 정하고, 프리팹을 복제해 옷 색·키·나이를 무작위로 입힌다.
    /// 최대 인원(maxCount)을 넘지 않는다 (성능). 프리팹을 실제 모델로 교체해도 PedestrianNPC 의 팔다리 Transform 만 연결하면 된다.
    /// </summary>
    public class PedestrianSpawner : MonoBehaviour
    {
        [Serializable]
        public class RouteDef
        {
            public string name = "route";
            [Tooltip("경로 지점 (월드 위치). 비어 있으면 이 오브젝트의 자식 Transform 을 사용")] public Transform[] points;
            public PedestrianNPC.Role role = PedestrianNPC.Role.Commuter;
            [Min(0)] public int count = 2;
            [Min(0.1f)] public float speed = 1.3f;
            public bool loop = true;
            [Tooltip("개 산책 등 동행 오브젝트(선택)")] public GameObject companionPrefab;
        }

        [SerializeField] private PedestrianNPC prefab;
        [SerializeField] private RouteDef[] routes;
        [SerializeField, Min(0)] private int maxCount = 25;
        [SerializeField] private int seed = 1004;
        [SerializeField, Tooltip("역할별 몸 크기 (어린이/성인/노인)")] private Vector2 childScale = new Vector2(0.62f, 0.72f);
        [SerializeField] private Vector2 adultScale = new Vector2(0.95f, 1.05f);
        [SerializeField] private Vector2 elderScale = new Vector2(0.9f, 0.96f);
        [SerializeField] private Material[] shirtMaterials, pantsMaterials, skinMaterials, hairMaterials;
        [SerializeField] private Renderer[] dummy;   // 직렬화 호환용 (사용 안 함)

        private readonly List<PedestrianNPC> spawned = new List<PedestrianNPC>();
        public IReadOnlyList<PedestrianNPC> Spawned => spawned;

        private void Start() => Spawn();

        public void Spawn()
        {
            if (prefab == null || routes == null) return;
            var rng = new System.Random(seed);
            int total = 0;
            foreach (RouteDef r in routes)
            {
                Vector3[] path = ResolvePath(r);
                for (int i = 0; i < r.count && total < maxCount; i++, total++)
                {
                    PedestrianNPC npc = Instantiate(prefab, transform);
                    npc.name = $"{r.role}_{total}";
                    float scale = PickScale(r.role, rng);
                    npc.Configure(r.role, Offset(path, rng, i), r.speed * Mathf.Lerp(0.85f, 1.15f, (float)rng.NextDouble()), r.loop, scale);
                    Dress(npc, rng, r.role);
                    if (r.companionPrefab != null)
                    {
                        GameObject c = Instantiate(r.companionPrefab, npc.transform.position, Quaternion.identity, transform);
                        var follow = c.GetComponent<DogFollower>();
                        if (follow != null) follow.SetOwner(npc.transform);
                    }
                    spawned.Add(npc);
                }
            }
        }

        private static Vector3[] ResolvePath(RouteDef r)
        {
            var list = new List<Vector3>();
            if (r.points != null) foreach (Transform t in r.points) if (t != null) list.Add(t.position);
            return list.ToArray();
        }

        /// <summary>같은 경로의 보행자들이 한 줄로 겹치지 않게 옆으로 조금씩 어긋나게</summary>
        private static Vector3[] Offset(Vector3[] path, System.Random rng, int i)
        {
            var result = new Vector3[path.Length];
            Vector3 jitter = new Vector3((float)(rng.NextDouble() - 0.5) * 0.8f, 0f, (float)(rng.NextDouble() - 0.5) * 0.8f);
            for (int k = 0; k < path.Length; k++) result[k] = path[k] + jitter;
            return result;
        }

        private float PickScale(PedestrianNPC.Role role, System.Random rng)
        {
            Vector2 range = adultScale;
            if (role == PedestrianNPC.Role.Student) range = childScale;
            else if (role == PedestrianNPC.Role.Sitter || (role == PedestrianNPC.Role.DogWalker && rng.NextDouble() < 0.4)) range = elderScale;
            return Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());
        }

        private void Dress(PedestrianNPC npc, System.Random rng, PedestrianNPC.Role role)
        {
            Pick(npc, "Shirt", shirtMaterials, rng);
            Pick(npc, "Pants", pantsMaterials, rng);
            Pick(npc, "Skin", skinMaterials, rng);
            Pick(npc, "Hair", hairMaterials, rng);
            Transform stroller = npc.transform.Find("Stroller");
            if (stroller != null) stroller.gameObject.SetActive(role == PedestrianNPC.Role.Stroller);
            Transform bag = npc.transform.Find("Bag");
            if (bag != null) bag.gameObject.SetActive(role == PedestrianNPC.Role.Student || role == PedestrianNPC.Role.Commuter || role == PedestrianNPC.Role.Courier);
        }

        private static void Pick(PedestrianNPC npc, string childName, Material[] pool, System.Random rng)
        {
            if (pool == null || pool.Length == 0) return;
            foreach (Renderer r in npc.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith(childName)) r.sharedMaterial = pool[rng.Next(pool.Length)];
        }
    }
}
