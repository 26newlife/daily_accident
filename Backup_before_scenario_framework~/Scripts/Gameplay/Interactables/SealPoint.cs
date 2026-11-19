using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 대피공간 방화문 문틈 한 면(아래 / 왼쪽 / 오른쪽 / 위) 밀봉 지점. 길게 눌러 청테이프(가장 확실) 또는 젖은 수건(덜 확실)으로 막는다.
    /// 면마다 밀봉률(sealRatio, 0~1)이 따로 있고, ShelterSealSystem 이 4면 평균으로 연기 유입량을 계산한다.
    /// 재료는 인벤토리 아이템으로 소모된다 (청테이프는 ItemData.MaxUses 만큼 여러 면에 쓴다).
    /// </summary>
    public class SealPoint : InteractableObject
    {
        public enum Side { Bottom = 0, Left = 1, Right = 2, Top = 3 }

        [Header("밀봉 지점")]
        [SerializeField] private Side side = Side.Bottom;
        [SerializeField, Tooltip("밀봉 전 틈(밝은 선) — 밀봉하면 숨긴다")] private GameObject gapVisual;
        [SerializeField, Tooltip("밀봉 후 보이는 테이프/수건")] private GameObject sealedVisual;
        [SerializeField] private AudioSource sealAudio;

        public Side SealSide => side;
        public float SealRatio { get; private set; }
        public bool UsedTape { get; private set; }

        public override float HoldSeconds => GameTimingConfig.Current.sealHoldSeconds;

        public override string GetPrompt()
        {
            string name = side == Side.Bottom ? "아래" : side == Side.Top ? "위" : side == Side.Left ? "왼쪽" : "오른쪽";
            if (SealRatio <= 0f) return $"문틈({name}) 막기";
            if (!UsedTape && InventorySystem.Instance != null && InventorySystem.Instance.Has(EvacKeys.ItemTape)) return $"문틈({name}) 청테이프로 덧붙이기";
            return $"문틈({name}) 밀봉 완료";
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (!base.CanInteract(interactor)) return false;
            if (SealRatio <= 0f) return true;
            // 이미 막은 면은 수건→테이프로 보강할 때만 다시 상호작용
            return !UsedTape && InventorySystem.Instance != null && InventorySystem.Instance.Has(EvacKeys.ItemTape);
        }

        protected override void OnInteract(PlayerInteractor interactor)
        {
            InventorySystem inv = InventorySystem.Instance;
            GameTimingConfig t = GameTimingConfig.Current;
            if (inv == null) return;

            if (inv.Has(EvacKeys.ItemTape))
            {
                inv.ConsumeUse(EvacKeys.ItemTape);
                Apply(t.sealTapeRatio, true);
                EventBus.ShowMessage("청테이프로 문틈을 빈틈없이 붙였다.");
            }
            else if (inv.Has(EvacKeys.ItemWetTowel))
            {
                ItemData towel = inv.Find(EvacKeys.ItemWetTowel);
                inv.Remove(towel);   // 착용 중이던 수건이면 함께 해제된다
                Apply(Mathf.Max(SealRatio, t.sealTowelRatio), false);
                EventBus.ShowMessage("젖은 수건을 문틈에 밀어 넣었다. 완전히 막히진 않는다.");
            }
            else
            {
                EventBus.ShowMessage("문틈을 막을 재료가 없다. 청테이프나 젖은 수건이 필요하다.");
                return;
            }
            Publish(ScenarioEventType.Interacted, true);
        }

        /// <summary>밀봉률을 직접 지정 (디버그/테스트)</summary>
        public void Apply(float ratio, bool tape)
        {
            SealRatio = Mathf.Clamp01(ratio);
            UsedTape |= tape;
            if (gapVisual != null) gapVisual.SetActive(false);
            if (sealedVisual != null) sealedVisual.SetActive(true);
            if (sealAudio != null && sealAudio.clip != null) sealAudio.Play();
        }
    }
}
