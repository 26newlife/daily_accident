using UnityEngine;

namespace GameGameplay
{
    /// <summary>산책하는 개: 주인을 느슨하게 따라다니며 종종거리는 동작</summary>
    public class DogFollower : MonoBehaviour
    {
        [SerializeField] private Transform owner;
        [SerializeField] private float distance = 1.1f;
        [SerializeField] private float speed = 2.2f;
        public void SetOwner(Transform t) => owner = t;

        private void Update()
        {
            if (owner == null) return;
            Vector3 target = owner.position - owner.forward * distance + owner.right * 0.3f;
            Vector3 to = target - transform.position; to.y = 0f;
            if (to.magnitude > 0.15f)
            {
                transform.position += to.normalized * (Mathf.Min(speed * 1.6f, to.magnitude * 3f) * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), Time.deltaTime * 8f);
            }
            transform.position += Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.01f);
        }
    }
}
