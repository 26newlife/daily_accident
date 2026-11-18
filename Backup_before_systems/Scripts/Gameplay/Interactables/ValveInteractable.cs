using GameCore;

namespace GameGameplay
{
    /// <summary>
    /// 밸브 (가스 메인 밸브, 퓨즈콕 등). ToggleInteractable 과 같지만 ValveChanged 이벤트를 발행한다.
    /// IsOn = 열림. 프롬프트 문구는 Inspector 에서 "잠그기 / 열기" 로 지정한다.
    /// </summary>
    public class ValveInteractable : ToggleInteractable
    {
        protected override ScenarioEventType ChangeEventType => ScenarioEventType.ValveChanged;

        /// <summary>밸브가 열려 있는가</summary>
        public bool IsOpen => IsOn;
    }
}
