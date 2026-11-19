using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>목표 정의. Create > GameGameplay > Objective Data</summary>
    [CreateAssetMenu(fileName = "Objective_New", menuName = "GameGameplay/Objective Data", order = 4)]
    public class ObjectiveData : ScriptableObject
    {
        [SerializeField] private string id = "objective_id";
        [SerializeField] private string title = "목표";
        [SerializeField, TextArea(1, 3)] private string description;
        [Tooltip("메인 목표인가 (아니면 서브 목표)")]
        [SerializeField] private bool isMain = true;

        public string Id => id;
        public string Title => title;
        public string Description => description;
        public bool IsMain => isMain;
    }
}
