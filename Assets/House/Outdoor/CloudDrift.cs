using UnityEngine;

namespace GameGameplay
{
    /// <summary>천천히 흘러가는 구름</summary>
    public class CloudDrift : MonoBehaviour
    {
        [SerializeField] private Vector3 velocity = new Vector3(1.5f, 0f, 0f);
        [SerializeField] private float wrapHalfRange = 1200f;
        private void Update()
        {
            Vector3 p = transform.position + velocity * Time.deltaTime;
            if (p.x > wrapHalfRange) p.x = -wrapHalfRange;
            transform.position = p;
        }
    }
}
