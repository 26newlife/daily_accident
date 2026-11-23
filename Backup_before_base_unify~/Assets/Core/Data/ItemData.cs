using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>인벤토리 아이템 정의. Create > GameGameplay > Item Data</summary>
    [CreateAssetMenu(fileName = "Item_New", menuName = "GameGameplay/Item Data", order = 0)]
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string id = "item_id";
        [SerializeField] private string displayName = "아이템";
        [SerializeField, TextArea(1, 3)] private string description;
        [SerializeField] private Sprite icon;
        [Tooltip("착용/장비 가능한 아이템인가 (예: 젖은 수건). 사용키로 착용·해제가 토글된다.")]
        [SerializeField] private bool equippable;
        [Tooltip("아이콘이 없을 때 슬롯에 표시할 색 / 머리글자")]
        [SerializeField] private Color fallbackColor = new Color(0.45f, 0.6f, 0.8f, 1f);
        [Tooltip("소모성 아이템의 사용 가능 횟수 (예: 청테이프 2회). 1 = 한 번 쓰면 사라짐. InventorySystem.ConsumeUse 가 사용")]
        [SerializeField, Min(1)] private int maxUses = 1;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public bool Equippable => equippable;
        public Color FallbackColor => fallbackColor;
        public int MaxUses => maxUses;
    }

}
