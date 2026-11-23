using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>시나리오 1 물자 줍기: 아이템(선택) + 자원 수치(현금·귀중품·식량·물·약품·담요)를 얻는다. 한 번 줍으면 사라진다.</summary>
    public class WarPickup : InteractableObject
    {
        [SerializeField] private ItemData item;
        [SerializeField] private int food, water, medicine;
        [SerializeField, Tooltip("비상 무기 (약탈자 방어에 쓴다)")] private bool weapon;
        [SerializeField, Tooltip("보온 용품 쾌적도")] private float comfort;
        [SerializeField] private bool blanket;
        [SerializeField, Tooltip("0 이면 행동력을 쓰지 않는다 (Act 1 파밍 등)")] private int actionCost;
        [SerializeField, TextArea(1, 2)] private string message = "물자를 챙겼다.";

        protected override void OnInteract(PlayerInteractor interactor)
        {
            WarState s = WarState.Instance;
            if (s == null) return;
            if (item != null && InventorySystem.Instance != null && InventorySystem.Instance.IsFull)
            { EventBus.ShowMessage("가방이 가득 찼다."); return; }
            if (!s.SpendAction(actionCost)) return;

            if (item != null && InventorySystem.Instance != null) InventorySystem.Instance.TryAdd(item);
            if (food != 0) s.AddFood(food);
            if (water != 0) s.AddWater(water);
            if (medicine != 0) s.AddMedicine(medicine);
            if (blanket) s.AddBlanket();
            if (comfort > 0f) s.AddComfort(comfort);
            if (weapon) s.SetHasWeapon();
            EventBus.ShowMessage(message);
            Publish(ScenarioEventType.ItemAcquired);
            gameObject.SetActive(false);
        }
    }
}
