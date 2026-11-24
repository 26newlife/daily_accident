using System;
using System.Collections;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 떨어지거나 날아오는 잔해(Rigidbody)가 빠른 속도로 플레이어 몸통에 겹치면 한 번 알린다 (원본 DebrisHit 이식).
    /// CharacterController 는 Rigidbody 충돌 콜백을 잘 받지 못하므로 위치로 직접 검사한다. 탁자 등에 막혀 느려진 잔해는 맞은 것으로 치지 않는다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class QuakeDebrisHit : MonoBehaviour
    {
        private Rigidbody body; private Collider shape; private Transform player; private CharacterController pc;
        private float minSpeed, age; private Action onHit;

        public static QuakeDebrisHit Attach(GameObject debris, Transform player, float minSpeed, Action onHit)
        {
            var h = debris.AddComponent<QuakeDebrisHit>();
            h.body = debris.GetComponent<Rigidbody>(); h.shape = debris.GetComponent<Collider>();
            h.player = player; h.pc = player != null ? player.GetComponent<CharacterController>() : null;
            h.minSpeed = minSpeed; h.onHit = onHit;
            return h;
        }

        private void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            if (player == null || pc == null || shape == null || age > 12f) { enabled = false; return; }
            if (body.linearVelocity.sqrMagnitude < minSpeed * minSpeed) return;
            Bounds b = shape.bounds; Vector3 feet = player.position;
            if (b.min.y >= feet.y + pc.height || b.max.y <= feet.y) return;
            Vector3 c = b.ClosestPoint(new Vector3(feet.x, b.center.y, feet.z));
            if (new Vector2(c.x - feet.x, c.z - feet.z).magnitude >= pc.radius) return;
            enabled = false;
            onHit?.Invoke();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (body == null) return;
            body.linearDamping = Mathf.Max(body.linearDamping, 2.5f);
            body.angularDamping = Mathf.Max(body.angularDamping, 2.5f);
        }
    }
}
