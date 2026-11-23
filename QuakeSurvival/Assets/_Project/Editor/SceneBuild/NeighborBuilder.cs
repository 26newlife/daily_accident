using QuakeSurvival.Art;
using QuakeSurvival.Npc;
using QuakeSurvival.Player;
using QuakeSurvival.UI;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>6층 계단 층계참 구석에 주저앉은 다친 이웃(임시 캡슐).</summary>
    internal static class NeighborBuilder
    {
        private const int NeighborFloor = 6;

        public static InjuredNeighbor Build(FirstPersonController player, MessageUI messages)
        {
            var root = new GameObject("InjuredNeighbor");
            root.transform.position = new Vector3(0.3f, FloorY(NeighborFloor), -4.35f);
            root.transform.rotation = Quaternion.Euler(0f, 135f, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);   // 주저앉은 자세
            body.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            body.GetComponent<MeshRenderer>().sharedMaterial = Mat("Neighbor", Palette.FabricWarm, 0.15f);

            var neighbor = root.AddComponent<InjuredNeighbor>();
            SetRef(neighbor, "player", player);
            SetRef(neighbor, "messages", messages);
            SetRef(neighbor, "body", body.transform);
            return neighbor;
        }
    }
}
