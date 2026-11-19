using System;
using System.Collections;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 3~4 연출 총괄 (씬에 1개, 항상 활성).
    ///  · 압축 이동: 페이드 → 순간이동 → 안내 문구. 층수·긴 복도를 짧은 연출로 줄인다 (플레이어 + 동행 NPC).
    ///  · 방화문/현관문 개폐 기록(ScoreSystem.Records "door_open_&lt;id&gt;" / "door_close_&lt;id&gt;") — 방화문 관리 판정과 점수에 쓴다.
    ///  · 실내외 주변광 전환(옥상·집결지는 밝게, 실내는 정전 상태 유지).
    /// 모든 코루틴은 이 컴포넌트에서 돌리므로 씬 재로드/다시 시작 시 함께 사라진다 (임시 코루틴이 남지 않는다).
    /// </summary>
    public class EvacDirector : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Color travelFadeColor = Color.black;
        [SerializeField, Range(0.2f, 1f)] private float teleportAtFraction = 0.3f;
        [SerializeField, Min(0.2f)] private float minTravelSeconds = 0.8f;

        public static EvacDirector Instance { get; private set; }
        public bool IsTraveling { get; private set; }
        public PlayerController Player => player != null ? player : (player = FindFirstObjectByType<PlayerController>());

        private Color savedAmbient;
        private bool ambientSaved;

        private void Awake() { Instance = this; }
        private void OnEnable() => EventBus.ScenarioEventRaised += OnScenarioEvent;
        private void OnDisable() => EventBus.ScenarioEventRaised -= OnScenarioEvent;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Player != null) Player.ControlSuspended = false;
        }

        // ───────── 문 기록 ─────────
        private void OnScenarioEvent(ScenarioEvent e)
        {
            if (e.Type != ScenarioEventType.Toggled || ScoreSystem.Instance == null) return;
            if (!e.SourceId.StartsWith("door_front") && !e.SourceId.StartsWith("fire_door")) return;
            ScoreSystem.Instance.Increment((e.State ? "door_open_" : "door_close_") + e.SourceId);
        }

        // ───────── 압축 이동 ─────────
        /// <summary>
        /// 화면을 어둡게 했다가 dest 로 이동한다. companions 는 같이 이동하는 NPC(동행 중인 경우).
        /// onArrived 는 이동 직후(화면이 아직 어두운 동안) 호출된다.
        /// </summary>
        public void Travel(Vector3 position, float yaw, string caption, float seconds, Action onArrived = null, bool warpCompanions = true)
        {
            if (IsTraveling || Player == null || GameFlow.IsEnded) return;
            StartCoroutine(TravelRoutine(position, yaw, caption, Mathf.Max(minTravelSeconds, seconds), onArrived, warpCompanions));
        }

        public void Travel(Transform dest, string caption, float seconds, Action onArrived = null)
            => Travel(dest.position, dest.eulerAngles.y, caption, seconds, onArrived);

        private IEnumerator TravelRoutine(Vector3 position, float yaw, string caption, float seconds, Action onArrived, bool warpCompanions)
        {
            IsTraveling = true;
            Player.ControlSuspended = true;
            if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(travelFadeColor, 1f, seconds);
            if (!string.IsNullOrEmpty(caption)) EventBus.ShowMessage(caption);

            yield return new WaitForSeconds(seconds * teleportAtFraction);
            if (GameFlow.IsEnded) { IsTraveling = false; yield break; }

            Player.Teleport(position, yaw);
            if (HazardManager.Instance != null) HazardManager.Instance.ResyncPlayerZones(position + Vector3.up);
            if (warpCompanions) WarpCompanions(position, yaw);
            onArrived?.Invoke();

            yield return new WaitForSeconds(seconds * (1f - teleportAtFraction));
            Player.ControlSuspended = false;
            IsTraveling = false;
        }

        /// <summary>동행 중인 NPC 를 플레이어 뒤로 옮긴다</summary>
        public void WarpCompanions(Vector3 playerPos, float yaw)
        {
            Vector3 back = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            int i = 0;
            foreach (NpcBase npc in NpcBase.All)
            {
                if (npc == null || !npc.IsFollowing) continue;
                npc.Warp(playerPos + back * (1.3f + 0.7f * i), yaw);
                i++;
            }
        }

        // ───────── 주변광 ─────────
        public void SetAmbientBright(bool bright, float level = 0.55f)
        {
            if (!ambientSaved) { savedAmbient = RenderSettings.ambientLight; ambientSaved = true; }
            RenderSettings.ambientLight = bright ? new Color(level, level, level * 1.05f) : savedAmbient;
        }

        // ───────── 헬퍼 ─────────
        public static T FindById<T>(string id) where T : InteractableObject
        {
            foreach (T t in FindObjectsByType<T>(FindObjectsInactive.Include))
                if (t.Id == id) return t;
            return null;
        }

        public static HazardZone FindZone(string zoneId) => HazardManager.Instance != null ? HazardManager.Instance.FindZone(zoneId) : null;

        public static void Die(DeathReason reason)
        {
            if (DeathAndEnding.Instance != null) DeathAndEnding.Instance.Die(reason);
            else EventBus.RaisePlayerDied(reason);
        }

        public static void Ending(string endingId)
        {
            if (DeathAndEnding.Instance != null) DeathAndEnding.Instance.TriggerEnding(endingId);
        }
    }
}
