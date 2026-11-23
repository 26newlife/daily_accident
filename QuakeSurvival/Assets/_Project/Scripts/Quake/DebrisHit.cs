using System;
using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>
    /// 떨어지거나 날아오는 잔해(Rigidbody)가 빠른 속도로 플레이어 몸통에 겹치면 한 번 알린다.
    /// CharacterController는 Rigidbody 충돌 콜백을 잘 받지 못하므로 위치로 직접 검사한다.
    /// 탁자 같은 물체에 막혀 멈춘 잔해는 속도가 줄어서 맞은 것으로 치지 않는다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DebrisHit : MonoBehaviour
    {
        private const float MaxLifetime = 12f;
        private const float LandedDamping = 2.5f;

        private Rigidbody body;
        private Collider shape;
        private Transform player;
        private CharacterController playerBody;
        private float minSpeed;
        private Action onHit;
        private float age;

        /// <summary>잔해(Rigidbody + Collider)에 붙여 판정을 시작한다.</summary>
        public static DebrisHit Attach(GameObject debris, Transform player, float minSpeed, Action onHit)
        {
            var hit = debris.AddComponent<DebrisHit>();
            hit.body = debris.GetComponent<Rigidbody>();
            hit.shape = debris.GetComponent<Collider>();
            hit.player = player;
            hit.playerBody = player != null ? player.GetComponent<CharacterController>() : null;
            hit.minSpeed = minSpeed;
            hit.onHit = onHit;
            return hit;
        }

        private void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            if (player == null || playerBody == null || shape == null || age > MaxLifetime)
            {
                enabled = false;
                return;
            }
            if (body.linearVelocity.sqrMagnitude < minSpeed * minSpeed) return;
            if (!Overlaps(shape.bounds)) return;

            enabled = false;
            onHit?.Invoke();
        }

        // 땅이나 벽에 처음 닿으면 미끄러져 멀리 굴러가지 않도록 감속시킨다
        private void OnCollisionEnter(Collision collision)
        {
            if (body == null) return;
            body.linearDamping = Mathf.Max(body.linearDamping, LandedDamping);
            body.angularDamping = Mathf.Max(body.angularDamping, LandedDamping);
        }

        /// <summary>잔해의 실제 범위(AABB)가 플레이어 몸통(원기둥)과 겹치는지.</summary>
        private bool Overlaps(Bounds b)
        {
            Vector3 feet = player.position;
            if (b.min.y >= feet.y + playerBody.height || b.max.y <= feet.y) return false;
            Vector3 closest = b.ClosestPoint(new Vector3(feet.x, b.center.y, feet.z));
            var flat = new Vector2(closest.x - feet.x, closest.z - feet.z);
            return flat.magnitude < playerBody.radius;
        }
    }
}
