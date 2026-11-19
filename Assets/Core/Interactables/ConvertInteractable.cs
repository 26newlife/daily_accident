using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 아이템 변환 상호작용 (예: 화장실 세면대에서 수건 → 젖은 수건). requiredItem 을 소모하고 resultItem 을 인벤토리에 넣는다.
    /// 필요한 아이템이 없으면 안내 문구만 띄운다. 길게 누르기(holdSeconds)는 아이템이 있을 때만 적용된다.
    /// </summary>
    public class ConvertInteractable : InteractableObject
    {
        [SerializeField] private ItemData requiredItem;
        [SerializeField] private ItemData resultItem;
        [SerializeField] private string missingMessage = "적실 수건이 필요하다.";
        [SerializeField] private string doneMessage = "수건을 물에 흠뻑 적셨다.";
        [SerializeField] private string inventoryFullMessage = "가방이 가득 찼다.";

        private bool HasRequired => InventorySystem.Instance != null && requiredItem != null && InventorySystem.Instance.Has(requiredItem.Id);

        public override float HoldSeconds => HasRequired ? base.HoldSeconds : 0f;

        protected override void OnInteract(PlayerInteractor interactor)
        {
            InventorySystem inv = InventorySystem.Instance;
            if (inv == null || requiredItem == null || resultItem == null) return;
            if (!HasRequired) { EventBus.ShowMessage(missingMessage); return; }

            inv.Remove(requiredItem);
            if (!inv.TryAdd(resultItem))
            {
                inv.TryAdd(requiredItem, silent: true);   // 실패 시 원상 복구
                EventBus.ShowMessage(inventoryFullMessage);
                return;
            }
            EventBus.ShowMessage(doneMessage);
            Publish(ScenarioEventType.ItemUsed, true);
        }
    }
}
