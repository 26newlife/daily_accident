using System;
using GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// Easy 난이도 전용 목표 방향 표시. 진행 중인 목표 중 위치가 등록된 첫 항목을 가리키는 화살표와 거리(m)를 화면 가장자리에 그린다.
    /// 대상이 화면 안에 있으면 그 위에 작은 화살표가 뜨고, 화면 밖이면 가장자리로 붙어서 방향을 알려 준다.
    /// </summary>
    public class ObjectiveMarker : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("ObjectiveData 의 id (예: act1_main_valve)")] public string objectiveId;
            [Tooltip("가리킬 오브젝트 (상호작용 오브젝트 등)")] public Transform target;
        }

        [SerializeField] private ObjectiveSystem objectives;
        [SerializeField] private Transform player;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Entry[] entries;
        [SerializeField, Min(0f)] private float edgeMargin = 90f;
        [SerializeField] private string distanceFormat = "{0:0}m";
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.2f, 0f);

        private Camera cam;

        private void Awake() => SetVisible(false);

        private void LateUpdate()
        {
            Transform target = FindTarget();
            bool on = GameSettings.HintLevel == HintLevel.Easy && target != null && !GameFlow.GameplayBlocked && !GameFlow.IsPhoneOpen;
            if (!on) { SetVisible(false); return; }
            if (cam == null) cam = Camera.main;
            if (cam == null || canvas == null || arrow == null) return;

            Vector3 world = target.position + targetOffset;
            Vector3 sp = cam.WorldToScreenPoint(world);
            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 pos = new Vector2(sp.x, sp.y);
            if (sp.z < 0f) pos = center + (center - pos);          // 뒤쪽이면 반대편으로 투영해 가장자리에 붙인다

            Vector2 dir = pos - center;
            bool inside = sp.z > 0f && Mathf.Abs(dir.x) < center.x - edgeMargin && Mathf.Abs(dir.y) < center.y - edgeMargin;
            float angle;
            if (inside)
            {
                angle = 180f;                                       // 대상 위에서 아래를 가리킴
                pos += Vector2.up * 60f;
            }
            else
            {
                // 화면 중심에서 대상 방향으로 쏜 선이 가장자리 사각형과 만나는 점
                Vector2 half = center - Vector2.one * edgeMargin;
                float k = Mathf.Min(half.x / Mathf.Max(0.001f, Mathf.Abs(dir.x)), half.y / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
                pos = center + dir * k;
                angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            }

            SetVisible(true);
            Vector2 canvasPos = (pos - center) / Mathf.Max(0.01f, canvas.scaleFactor);
            arrow.anchoredPosition = canvasPos;
            arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (distanceText != null)
            {
                distanceText.text = string.Format(distanceFormat, player != null ? Vector3.Distance(player.position, target.position) : 0f);
                ((RectTransform)distanceText.transform).anchoredPosition = canvasPos + Vector2.down * 54f;
            }
        }

        private Transform FindTarget()
        {
            if (objectives == null || entries == null) return null;
            foreach (ObjectiveSystem.Entry e in objectives.Entries)
            {
                if (e.State != ObjectiveState.Active || e.Data == null) continue;
                foreach (Entry m in entries)
                    if (m.target != null && m.objectiveId == e.Data.Id) return m.target;
            }
            return null;
        }

        private void SetVisible(bool v)
        {
            if (arrow != null && arrow.gameObject.activeSelf != v) arrow.gameObject.SetActive(v);
            if (distanceText != null && distanceText.gameObject.activeSelf != v) distanceText.gameObject.SetActive(v);
        }
    }
}
