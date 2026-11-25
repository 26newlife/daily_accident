using System.Collections.Generic;
using GameCore;
using GameGameplay;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI.EditorTools
{
    /// <summary>Act 3~4 / 신규 시스템 연결 (EvacDirector, Call119Controller, ShelterSealSystem, Act3·Act4 참조, 선택지 UI)</summary>
    public static partial class MainMenuBuilder
    {
        private static ObjectiveData[] EvacObjectivesOf(GameplayAssets d) => d.evacObjectives ?? new ObjectiveData[0];

        /// <summary>BuildSystems 에서 Act 생성 직후 호출</summary>
        private static void ConfigureEvacSystems(Transform systemsRoot, Act3_FireSpreads act3, Act4_Evacuation act4, GameplayAssets d, GameObject player, ScoreSystem score)
        {
            var playerCtrl = player.GetComponent<PlayerController>();
            ToggleInteractable T(string id) => Obj<ToggleInteractable>(id);
            HazardZone Z(string zoneId) { Transform t = s3Zones.Find("Zone_" + zoneId); if (t == null) t = lvZones.Find("Zone_" + zoneId); return t != null ? t.GetComponent<HazardZone>() : null; }

            // ── 연출 총괄 / 119 / 대피공간 밀봉 ──
            var dir = systemsRoot.gameObject.AddComponent<EvacDirector>();
            var dso = SO(dir);
            dso.FindProperty("player").objectReferenceValue = playerCtrl;
            dso.ApplyModifiedPropertiesWithoutUndo();

            var call = scenarioSystemsRoot.gameObject.AddComponent<Call119Controller>();   // 폰/선택지는 런타임에 찾는다
            systemsRoot.gameObject.AddComponent<DebugPanel>();                      // F10 디버그 패널 (에디터/개발 빌드 전용 — 릴리스에서는 빈 컴포넌트)

            HazardZone shelterZone = Z(EvacKeys.ZoneShelter);   // (격리/사망 사유는 S03Bootstrap 이 런타임에 적용)
            var sealSys = scenarioSystemsRoot.gameObject.AddComponent<ShelterSealSystem>();
            var sso = SO(sealSys);
            sso.FindProperty("shelterZone").objectReferenceValue = shelterZone;
            sso.FindProperty("fireDoor").objectReferenceValue = T(Ids.ShelterFireDoor);
            sso.FindProperty("window").objectReferenceValue = T(EvacKeys.ShelterWindow);
            SetObjectArray(sso.FindProperty("sealPoints"), evac.seals);
            sso.ApplyModifiedPropertiesWithoutUndo();

            // ── Act 3 ──
            var a3 = SO(act3);
            a3.FindProperty("player").objectReferenceValue = playerCtrl;
            a3.FindProperty("frontDoor").objectReferenceValue = T(Ids.FrontDoor);
            a3.FindProperty("stairDoor").objectReferenceValue = T(Ids.StairFireDoor);
            a3.FindProperty("grandmother").objectReferenceValue = evac.grandmother;
            if (evac.grandmother != null && evac.grandHome != null)
            {
                var gmSo = SO(evac.grandmother);
                gmSo.FindProperty("homePoint").objectReferenceValue = evac.grandHome;
                gmSo.ApplyModifiedPropertiesWithoutUndo();
            }
            a3.FindProperty("youngMan").objectReferenceValue = evac.youngMan;
            a3.FindProperty("corridorZone").objectReferenceValue = Z(EvacKeys.ZoneCorridor);
            a3.FindProperty("stairZone").objectReferenceValue = Z(EvacKeys.ZoneStairwell);
            a3.FindProperty("stairSmokeFx").objectReferenceValue = evac.stairSmoke;
            a3.FindProperty("stairRumbleAudio").objectReferenceValue = AddAudioUnder(scenarioSystemsRoot, "StairRumbleAudio");
            SetObjectArray(a3.FindProperty("objectivePool"), EvacObjectivesOf(d));
            a3.ApplyModifiedPropertiesWithoutUndo();

            // ── Act 4 ──
            var a4 = SO(act4);
            a4.FindProperty("player").objectReferenceValue = playerCtrl;
            a4.FindProperty("grandmother").objectReferenceValue = evac.grandmother;
            a4.FindProperty("youngMan").objectReferenceValue = evac.youngMan;
            a4.FindProperty("grandmotherFallenPoint").objectReferenceValue = evac.grandFallen;
            a4.FindProperty("stairRoomSpawn").objectReferenceValue = evac.stairRoom;
            // 루트 A
            a4.FindProperty("fireDoor14").objectReferenceValue = T(EvacKeys.FireDoor14);
            a4.FindProperty("fireDoor17").objectReferenceValue = T(EvacKeys.FireDoor17);
            a4.FindProperty("zoneLanding14").objectReferenceValue = Z(EvacKeys.ZoneLanding14);
            a4.FindProperty("zoneLanding17").objectReferenceValue = Z(EvacKeys.ZoneLanding17);
            a4.FindProperty("zoneLanding20").objectReferenceValue = Z(EvacKeys.ZoneLanding20);
            a4.FindProperty("roofDoorObject").objectReferenceValue = evac.roofDoor;
            a4.FindProperty("keyBoxGlass").objectReferenceValue = evac.keyBoxGlass;
            a4.FindProperty("keyShards").objectReferenceValue = evac.keyShards;
            a4.FindProperty("glassBreakAudio").objectReferenceValue = AddAudioUnder(scenarioSystemsRoot, "GlassBreakAudio");
            a4.FindProperty("roofKeyItem").objectReferenceValue = d.roofKey;
            a4.FindProperty("helicopter").objectReferenceValue = evac.helicopter;
            a4.FindProperty("heliStart").objectReferenceValue = evac.heliStart;
            a4.FindProperty("heliEnd").objectReferenceValue = evac.heliEnd;
            a4.FindProperty("roofLadder").objectReferenceValue = evac.roofLadder;
            a4.FindProperty("roofLadderPivot").objectReferenceValue = evac.roofLadderPivot;
            a4.FindProperty("rescueArrivalAudio").objectReferenceValue = AddAudioUnder(scenarioSystemsRoot, "RescueArrivalAudio");
            // 루트 B
            a4.FindProperty("partition").objectReferenceValue = evac.partition;
            SetObjectArray(a4.FindProperty("balconyClearables"), evac.clearables);
            a4.FindProperty("zone1003").objectReferenceValue = Z(EvacKeys.Zone1003);
            // 루트 C
            a4.FindProperty("shelterSeal").objectReferenceValue = sealSys;
            a4.FindProperty("shelterWindow").objectReferenceValue = T(EvacKeys.ShelterWindow);
            a4.FindProperty("rescueLadder").objectReferenceValue = evac.rescueLadder;
            a4.FindProperty("rescueLadderPivot").objectReferenceValue = evac.rescueLadderPivot;
            // 루트 D
            SetObjectArray(a4.FindProperty("descentZones"), new[] { Z(EvacKeys.ZoneDescent1), Z(EvacKeys.ZoneDescent2), Z(EvacKeys.ZoneDescent3) });
            a4.FindProperty("fireTruck").objectReferenceValue = evac.fireTruck;
            a4.FindProperty("ambulance").objectReferenceValue = evac.ambulance;
            a4.FindProperty("outdoor").objectReferenceValue = Object.FindFirstObjectByType<OutdoorWorldController>();
            SerializedProperty factors = a4.FindProperty("stageSmokeFactor");
            float[] f = { 1f, 0.85f, 0.55f };
            factors.arraySize = f.Length;
            for (int i = 0; i < f.Length; i++) factors.GetArrayElementAtIndex(i).floatValue = f[i];
            SetObjectArray(a4.FindProperty("objectivePool"), EvacObjectivesOf(d));
            a4.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioSource AddAudioUnder(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(AudioSource));
            go.transform.SetParent(parent, false);
            var a = go.GetComponent<AudioSource>();
            a.playOnAwake = false; a.spatialBlend = 0f;
            return a;
        }

        // ═════════════════════ 선택지 패널 UI ═════════════════════
        /// <summary>NPC 대화 / 119 위치 선택에 쓰는 범용 선택지 패널 (옵션 버튼 5개)</summary>
        private static ChoicePanel BuildChoicePanel(Transform ct, GenAssets g, TMP_FontAsset font, GameObject buttonPrefab)
        {
            PopupParts p = BuildPopupFrame("ChoicePanel", ct, g, font, "대화", "선택지를 고르세요 (숫자키 1~5 / 클릭 / Esc)", new Vector2(1100, 720), false, 0.45f);
            TMP_Text title = p.window.Find("Title").GetComponent<TMP_Text>();
            TMP_Text body = GameText("Body", p.window, string.Empty, font, 30, TextAlignmentOptions.TopLeft, TextMain, true);
            Place(body.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -160), new Vector2(988, 90));

            const int count = 5;
            var buttons = new Button[count];
            var labels = new TMP_Text[count];
            for (int i = 0; i < count; i++)
            {
                GameObject b = InstantiateButtonRaw(buttonPrefab, p.window, $"Btn_Choice_{i + 1}", $"{i + 1}.", new Vector2(988, 72), 28);
                Anchor((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -280 - i * 84));
                buttons[i] = b.GetComponent<Button>();
                labels[i] = b.transform.Find("Label").GetComponent<TMP_Text>();
                labels[i].alignment = TextAlignmentOptions.MidlineLeft;
            }

            var panel = p.root.gameObject.AddComponent<ChoicePanel>();
            var so = SO(panel);
            so.FindProperty("group").objectReferenceValue = p.root;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("bodyText").objectReferenceValue = body;
            SetObjectArray(so.FindProperty("optionButtons"), buttons);
            SetObjectArray(so.FindProperty("optionLabels"), labels);
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        /// <summary>사망/엔딩 화면에 모음·피드백 텍스트를 추가해 연결한다</summary>
        private static void AddEndingScreenExtras(DeathScreen death, EndingScreen ending, TMP_FontAsset font)
        {
            // 사망 화면: 하단 왼쪽에 사망 엔딩 모음
            RectTransform dw = (RectTransform)death.transform.Find("Window");
            TMP_Text dc = GameText("CollectionText", dw, string.Empty, font, 22, TextAlignmentOptions.BottomLeft, TextDim, true);
            Place(dc.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(56, 28), new Vector2(760, 130));
            var dso = SO(death);
            dso.FindProperty("collectionText").objectReferenceValue = dc;
            dso.ApplyModifiedPropertiesWithoutUndo();

            // 엔딩 화면: 오른쪽 열에 잘한 점/아쉬운 점, 아래에 엔딩 모음
            RectTransform ew = (RectTransform)ending.transform.Find("Window");
            TMP_Text fb = GameText("FeedbackText", ew, string.Empty, font, 24, TextAlignmentOptions.TopLeft, TextMain, true);
            fb.richText = true;
            Place(fb.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -470), new Vector2(430, 300));
            TMP_Text ec = GameText("CollectionText", ew, string.Empty, font, 20, TextAlignmentOptions.BottomLeft, TextDim, true);
            Place(ec.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(56, 28), new Vector2(760, 120));
            var eso = SO(ending);
            eso.FindProperty("feedbackText").objectReferenceValue = fb;
            eso.FindProperty("collectionText").objectReferenceValue = ec;
            eso.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
