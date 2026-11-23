using UnityEngine;

namespace QuakeSurvival.Fx
{
    /// <summary>씬에 하나 두는 효과용 재질 모음(빌더가 연결).</summary>
    public sealed class FxLibrary : MonoBehaviour
    {
        [SerializeField] private Material fire;
        [SerializeField] private Material smoke;
        [SerializeField] private Material fireball;
        [SerializeField] private AudioClip explosionSound;
        [SerializeField] private AudioClip fireLoopSound;

        public Material Fire => fire;
        public Material Smoke => smoke;
        public Material Fireball => fireball;
        public AudioClip ExplosionSound => explosionSound;
        public AudioClip FireLoopSound => fireLoopSound;

        public bool IsValid => fire != null && smoke != null && fireball != null;
    }
}
