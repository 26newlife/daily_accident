using QuakeSurvival.Fx;
using QuakeSurvival.Interaction;
using QuakeSurvival.Npc;
using QuakeSurvival.Player;
using QuakeSurvival.Quake;
using QuakeSurvival.Safety;
using QuakeSurvival.UI;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>게임 진행 시스템(타임라인, 판정, 재해 연출, HUD, 선택 요소, 결과 기록)을 만들고 서로 연결한다.</summary>
    internal static class SystemsBuilder
    {
        public sealed class Input
        {
            public ApartmentBuilder.Parts Apartment;
            public HallwayBuilder.Parts Hallway;
            public PlayerBuilder.Parts Player;
            public ExteriorBuilder.Parts Exterior;
            public Light Sun;
            public GameObject UpperCollapseHazard;
        }

        private readonly struct OptionalParts
        {
            public readonly GlassInjury Injury;
            public readonly PhoneAction Phone;

            public OptionalParts(GlassInjury injury, PhoneAction phone)
            {
                Injury = injury;
                Phone = phone;
            }
        }

        /// <summary>본진 때 꺼지는 조명: 집 전등 + 복도 등 + 가로등(도시 정전). 비상등은 남는다.</summary>
        private static Object[] OutageLights(Input w)
        {
            var lights = new System.Collections.Generic.List<Object>(w.Apartment.Lamps) { w.Hallway.Lamp };
            lights.AddRange(w.Exterior.StreetLights);
            return lights.ToArray();
        }

        public static void Build(Input w)
        {
            var sys = new GameObject("QuakeSystems");
            var director = sys.AddComponent<QuakeDirector>();
            SetRef(w.Player.Shake, "director", director);

            var outage = sys.AddComponent<PowerOutage>();
            SetArray(outage, "lights", OutageLights(w));

            var glassActivator = sys.AddComponent<PhaseActivator>();
            SetRef(glassActivator, "director", director);
            SetArray(glassActivator, "targets", w.Apartment.GlassShards);
            foreach (var shard in w.Apartment.GlassShards) shard.SetActive(false);

            var checklist = sys.AddComponent<SafetyChecklist>();
            SetArray(checklist, "switches", w.Apartment.Switches);

            var cover = sys.AddComponent<CoverTracker>();
            SetRef(cover, "director", director);
            SetRef(cover, "player", w.Player.Controller);

            var zones = sys.AddComponent<ZoneSensor>();
            SetRef(zones, "player", w.Player.Controller.transform);

            FxLibrary fx = FxBuilder.BuildLibrary(sys);
            ChainDisaster chain = DisasterBuilder.BuildChain(sys, fx, w.Player, zones);
            TsunamiController tsunami = DisasterBuilder.BuildTsunami(sys, director, w.Player, w.Exterior.Sea);
            DisasterBuilder.BuildRain(w.Player, zones);

            var evaluator = sys.AddComponent<OutcomeEvaluator>();
            SetRef(evaluator, "director", director);
            SetRef(evaluator, "checklist", checklist);
            SetRef(evaluator, "cover", cover);
            SetRef(evaluator, "zones", zones);
            SetRef(evaluator, "chainDisaster", chain);
            SetRef(evaluator, "tsunami", tsunami);

            HudBuilder.Parts hud = HudBuilder.Build(w.Player.Controller, w.Player.Interactor, evaluator);
            CatastropheBuilder.Build(new CatastropheBuilder.Deps
            {
                Host = sys,
                Director = director,
                Zones = zones,
                Cover = cover,
                Evaluator = evaluator,
                Tsunami = tsunami,
                Fx = fx,
                Messages = hud.Messages,
                Player = w.Player,
                Exterior = w.Exterior,
            });
            var clues = sys.AddComponent<ClueSchedule>();
            SetRef(clues, "director", director);
            SetRef(clues, "messages", hud.Messages);
            SetRef(clues, "checklist", checklist);

            var shakeRule = sys.AddComponent<ShakeMovementRule>();
            SetRef(shakeRule, "director", director);
            SetRef(shakeRule, "player", w.Player.Controller);
            SetRef(shakeRule, "evaluator", evaluator);

            // 위층 붕괴 위험 구역은 지진(전진)이 시작돼야 켜진다. 그 전에는 11~12층에 올라가도 안전하다
            var upperCollapse = sys.AddComponent<PhaseActivator>();
            SetRef(upperCollapse, "director", director);
            SetArray(upperCollapse, "targets", new Object[] { w.UpperCollapseHazard });

            SetRef(w.Hallway.Elevator, "director", director);
            SetRef(w.Hallway.Elevator, "messages", hud.Messages);

            OptionalParts optional = BuildOptionalElements(sys, w, director, checklist, cover, zones, evaluator, hud);

            AudioSynth.AudioBuilder.Build(new AudioSynth.AudioBuilder.Targets
            {
                SystemsHost = sys,
                Director = director,
                Zones = zones,
                Tsunami = tsunami,
                Messages = hud.Messages,
                Lightning = w.Sun != null ? w.Sun.GetComponent<LightningFlash>() : null,
                Evaluator = evaluator,
                Player = w.Player.Controller,
                Switches = w.Apartment.Switches,
                Elevator = w.Hallway.Elevator,
                Fx = fx,
                Injury = optional.Injury,
                Phone = optional.Phone,
                GameOver = hud.GameOver,
            });
        }

        private static OptionalParts BuildOptionalElements(GameObject sys, Input w, QuakeDirector director, SafetyChecklist checklist,
            CoverTracker cover, ZoneSensor zones, OutcomeEvaluator evaluator, HudBuilder.Parts hud)
        {
            FirstPersonController controller = w.Player.Controller;

            var flashlight = controller.gameObject.AddComponent<Flashlight>();
            SetRef(flashlight, "checklist", checklist);
            SetRef(flashlight, "spot", w.Player.FlashlightSpot);

            var injury = sys.AddComponent<GlassInjury>();
            SetRef(injury, "player", controller);
            SetRef(injury, "checklist", checklist);
            SetRef(injury, "messages", hud.Messages);

            var phone = sys.AddComponent<PhoneAction>();
            SetRef(phone, "director", director);
            SetRef(phone, "zones", zones);
            SetRef(phone, "player", controller);
            SetRef(phone, "messages", hud.Messages);
            SetRef(phone, "evaluator", evaluator);
            PhoneMapUI phoneMap = PhoneMapBuilder.Build(hud.Messages.transform, controller.transform);
            SetRef(phone, "map", phoneMap);
            InjuredNeighbor neighbor = NeighborBuilder.Build(controller, hud.Messages);

            var record = sys.AddComponent<EvacuationRecord>();
            SetRef(record, "director", director);
            SetRef(record, "checklist", checklist);
            SetRef(record, "cover", cover);
            SetRef(record, "elevator", w.Hallway.Elevator);
            SetRef(record, "injury", injury);
            SetRef(record, "neighbor", neighbor);
            SetRef(record, "phone", phone);
            SetRef(record, "evaluator", evaluator);
            SetRef(record, "zones", zones);
            SetRef(hud.GameOver, "record", record);
            return new OptionalParts(injury, phone);
        }
    }
}
