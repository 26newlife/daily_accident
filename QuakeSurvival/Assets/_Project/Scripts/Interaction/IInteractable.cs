namespace QuakeSurvival.Interaction
{
    /// <summary>F키로 조작할 수 있는 모든 물건이 구현하는 인터페이스.</summary>
    public interface IInteractable
    {
        /// <summary>화면에 띄울 안내 문구. 예: "가스 밸브 잠그기".</summary>
        string Prompt { get; }

        /// <summary>지금 조작 가능한지 여부.</summary>
        bool CanInteract { get; }

        void Interact();
    }
}
