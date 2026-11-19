using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>HUD 인벤토리 바의 슬롯 1칸 표시. 아이콘이 없으면 아이템 색 + 이름 첫 글자로 대체한다.</summary>
    public class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text numberLabel;
        [SerializeField] private TMP_Text initialLabel;
        [SerializeField] private GameObject selectedMark;
        [SerializeField] private GameObject equippedMark;
        [SerializeField] private Color emptyColor = new Color(0f, 0f, 0f, 0.35f);
        [SerializeField] private Color filledColor = new Color(0.05f, 0.08f, 0.12f, 0.75f);

        public void Show(int index, ItemData item, bool selected, bool equipped)
        {
            if (numberLabel != null) numberLabel.text = (index + 1).ToString();
            if (background != null) background.color = item != null ? filledColor : emptyColor;
            if (selectedMark != null) selectedMark.SetActive(selected);
            if (equippedMark != null) equippedMark.SetActive(item != null && equipped);

            bool has = item != null;
            if (icon != null)
            {
                icon.enabled = has;
                if (has)
                {
                    icon.sprite = item.Icon;
                    icon.color = item.Icon != null ? Color.white : item.FallbackColor;
                }
            }
            if (initialLabel != null)
                initialLabel.text = has && item.Icon == null && !string.IsNullOrEmpty(item.DisplayName) ? item.DisplayName.Substring(0, 1) : string.Empty;
        }
    }
}
