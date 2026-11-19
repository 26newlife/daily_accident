using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>아이템 획득 오브젝트. 인벤토리에 넣으면 사라진다 (인벤토리가 가득 차면 안내만).</summary>
    public class PickupInteractable : InteractableObject
    {
        [SerializeField] private ItemData item;
        [SerializeField, Tooltip("{0} = 아이템 이름")] private string promptFormat = "{0} 줍기";
        [SerializeField, Tooltip("{0} = 아이템 이름")] private string acquiredFormat = "{0}을(를) 획득했다.";
        [SerializeField] private string inventoryFullMessage = "가방이 가득 찼다.";

        public ItemData Item => item;

        public override string GetPrompt() => item != null ? string.Format(promptFormat, item.DisplayName) : base.GetPrompt();

        protected override void OnInteract(PlayerInteractor interactor)
        {
            InventorySystem inv = InventorySystem.Instance;
            if (item == null || inv == null) return;

            if (!inv.TryAdd(item)) // 성공 시 InventorySystem 이 ItemAcquired 이벤트 발행
            {
                EventBus.ShowMessage(inventoryFullMessage);
                return;
            }
            EventBus.ShowMessage(string.Format(acquiredFormat, item.DisplayName));
            gameObject.SetActive(false);
        }
    }
}
