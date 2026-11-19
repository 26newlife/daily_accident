using System;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 슬롯 방식 인벤토리(기본 8칸). 플레이어 프리팹에 붙어 있다.
    /// 슬롯 사용: 착용 가능한 아이템은 착용/해제 토글, 그 외에는 ItemUsed 이벤트만 발행한다(효과는 Act 로직이 결정).
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        [SerializeField, Range(1, 16)] private int maxSlots = 8;
        [SerializeField] private ItemData[] startingItems;
        [SerializeField, Tooltip("비워두면 같은 오브젝트의 GameInput 을 사용")] private GameInput input;

        private ItemData[] slots;
        private readonly HashSet<ItemData> equipped = new HashSet<ItemData>();
        private readonly Dictionary<ItemData, int> usesSpent = new Dictionary<ItemData, int>();

        public static InventorySystem Instance { get; private set; }

        public int MaxSlots => maxSlots;
        public int SelectedIndex { get; private set; }

        /// <summary>슬롯 내용 / 선택 / 착용 상태가 바뀔 때</summary>
        public event Action Changed;

        private void Awake()
        {
            Instance = this;
            slots = new ItemData[maxSlots];
            if (startingItems != null)
                foreach (ItemData item in startingItems) TryAdd(item, silent: true);
        }

        private void OnEnable()
        {
            if (input == null) input = GetComponent<GameInput>();
            if (input == null) return;
            input.InventorySlotPressed += OnSlotPressed;
            input.InventoryCycle += OnCycle;
            input.InventoryUsePressed += OnUsePressed;
        }

        private void OnDisable()
        {
            if (input == null) return;
            input.InventorySlotPressed -= OnSlotPressed;
            input.InventoryCycle -= OnCycle;
            input.InventoryUsePressed -= OnUsePressed;
        }

        // 숫자키: 슬롯 선택 + 사용 / [ ] 및 십자키: 선택 이동 / Q, X버튼: 선택 슬롯 사용
        private void OnSlotPressed(int index) { if (GameFlow.GameplayBlocked || slots == null || index >= slots.Length) return; Select(index); Use(index); }
        private void OnCycle(int direction) { if (!GameFlow.GameplayBlocked) CycleSelection(direction); }
        private void OnUsePressed() { if (!GameFlow.GameplayBlocked) UseSelected(); }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public ItemData GetItem(int index) => index >= 0 && index < slots.Length ? slots[index] : null;
        public bool IsFull => FindEmptySlot() < 0;
        public bool IsEquipped(ItemData item) => item != null && equipped.Contains(item);

        public bool Has(string itemId)
        {
            foreach (ItemData s in slots) if (s != null && s.Id == itemId) return true;
            return false;
        }

        public bool TryAdd(ItemData item, bool silent = false)
        {
            if (item == null) return false;
            int index = FindEmptySlot();
            if (index < 0) return false;
            slots[index] = item;
            Changed?.Invoke();
            if (!silent) EventBus.Raise(ScenarioEventType.ItemAcquired, item.Id);
            return true;
        }

        /// <summary>소모성 아이템 1회 사용. 남은 횟수가 0 이 되면 인벤토리에서 제거한다. 가지고 있지 않으면 false</summary>
        public bool ConsumeUse(string itemId)
        {
            ItemData item = Find(itemId);
            if (item == null) return false;
            usesSpent.TryGetValue(item, out int spent);
            spent++;
            if (spent >= item.MaxUses) { usesSpent.Remove(item); Remove(item); }
            else { usesSpent[item] = spent; Changed?.Invoke(); }
            return true;
        }

        public ItemData Find(string itemId)
        {
            foreach (ItemData s in slots) if (s != null && s.Id == itemId) return s;
            return null;
        }

        /// <summary>아이템 id 의 남은 사용 횟수 (없으면 0)</summary>
        public int UsesLeft(string itemId)
        {
            ItemData item = Find(itemId);
            if (item == null) return 0;
            usesSpent.TryGetValue(item, out int spent);
            return item.MaxUses - spent;
        }

        public bool Remove(ItemData item)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != item) continue;
                slots[i] = null;
                equipped.Remove(item);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public void Select(int index)
        {
            SelectedIndex = Mathf.Clamp(index, 0, slots.Length - 1);
            Changed?.Invoke();
        }

        public void CycleSelection(int direction) => Select((SelectedIndex + direction + slots.Length) % slots.Length);

        /// <summary>선택 슬롯 사용 (착용 토글 또는 사용 이벤트)</summary>
        public void UseSelected() => Use(SelectedIndex);

        public void Use(int index)
        {
            ItemData item = GetItem(index);
            if (item == null) return;

            if (item.Equippable)
            {
                bool nowEquipped = !equipped.Contains(item);
                if (nowEquipped) equipped.Add(item); else equipped.Remove(item);
                Changed?.Invoke();
                EventBus.Raise(ScenarioEventType.ItemEquipped, item.Id, nowEquipped);
            }
            else
            {
                EventBus.Raise(ScenarioEventType.ItemUsed, item.Id);
            }
        }

        private int FindEmptySlot()
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i] == null) return i;
            return -1;
        }
    }
}
