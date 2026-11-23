using QuakeSurvival.Audio;
using QuakeSurvival.Fx;
using QuakeSurvival.Interaction;
using QuakeSurvival.Player;
using QuakeSurvival.Quake;
using QuakeSurvival.Safety;
using QuakeSurvival.UI;
using UnityEditor;
using UnityEngine;
using static QuakeSurvival.EditorTools.AudioSynth.SynthAudioAssets;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools.AudioSynth
{
    /// <summary>합성 효과음을 만들고(없을 때만) 씬의 각 컴포넌트에 연결한다.</summary>
    internal static class AudioBuilder
    {
        public sealed class Targets
        {
            public GameObject SystemsHost;
            public QuakeDirector Director;
            public ZoneSensor Zones;
            public TsunamiController Tsunami;
            public MessageUI Messages;
            public LightningFlash Lightning;
            public OutcomeEvaluator Evaluator;
            public FirstPersonController Player;
            public SwitchInteractable[] Switches;
            public ElevatorCall Elevator;
            public FxLibrary Fx;
            public GlassInjury Injury;
            public PhoneAction Phone;
            public GameOverUI GameOver;
        }

        public static void Build(Targets t)
        {
            BuildSoundscape(t);
            WireInteractions(t);
            WireEffects(t);

            var steps = t.Player.gameObject.AddComponent<Footsteps>();
            SetRef(steps, "player", t.Player);
            SetRef(steps, "step", Get("footstep", SynthEffects.Footstep, false));
        }

        private static void BuildSoundscape(Targets t)
        {
            var scape = t.SystemsHost.AddComponent<QuakeSoundscape>();
            SetRef(scape, "director", t.Director);
            SetRef(scape, "zones", t.Zones);
            SetRef(scape, "tsunami", t.Tsunami);
            SetRef(scape, "messages", t.Messages);
            SetRef(scape, "lightning", t.Lightning);
            SetRef(scape, "evaluator", t.Evaluator);
            SetRef(scape, "listener", t.Player.transform);

            SetRef(scape, "rainOutside", Get("rain_outside_loop", SynthAmbience.RainOutside, true));
            SetRef(scape, "rainInside", Get("rain_inside_loop", SynthAmbience.RainInside, true));
            SetRef(scape, "rumble", Get("quake_rumble_loop", SynthAmbience.Rumble, true));
            SetRef(scape, "siren", Get("tsunami_siren_loop", SynthAmbience.Siren, true));
            SetRef(scape, "oceanRoar", Get("ocean_roar_loop", SynthAmbience.OceanRoar, true));
            SetRef(scape, "creak", Get("building_creak", SynthEffects.Creak, false));
            SetRef(scape, "clatter", Get("object_clatter", SynthEffects.Clatter, false));
            SetRef(scape, "thunder", Get("thunder", SynthEffects.Thunder, false));
            SetRef(scape, "alert", Get("disaster_alert", SynthEffects.Alert, false));
        }

        private static void WireInteractions(Targets t)
        {
            foreach (var sw in t.Switches)
            {
                SetRef(sw, "interactSound", SwitchSound(sw.ActionId));
            }
            SetRef(t.Elevator, "arriveDing", Get("elevator_ding", SynthEffects.ElevatorDing, false));
            SetRef(t.Elevator, "buttonClick", Get("button_click", SynthEffects.ButtonClick, false));
            SetRef(t.Phone, "badCallLoop", Get("phone_bad_call_loop", SynthAmbience.BadCall, true));
            SetRef(t.Phone, "textSent", Get("phone_text_sent", SynthEffects.TextSent, false));
        }

        private static AudioClip SwitchSound(string actionId)
        {
            switch (actionId)
            {
                case SafetyChecklist.GasValve: return Get("gas_valve_turn", SynthEffects.Valve, false);
                case SafetyChecklist.Breaker: return Get("breaker_switch", SynthEffects.Breaker, false);
                case SafetyChecklist.Door: return Get("door_creak", SynthEffects.Door, false);
                default: return Get("item_pickup", SynthEffects.Pickup, false);
            }
        }

        private static void WireEffects(Targets t)
        {
            SetRef(t.Fx, "explosionSound", Get("explosion", SynthEffects.Explosion, false));
            SetRef(t.Fx, "fireLoopSound", Get("fire_crackle_loop", SynthAmbience.FireLoop, true));
            SetRef(t.Injury, "crunch", Get("glass_crunch", SynthEffects.GlassCrunch, false));
            SetRef(t.GameOver, "deathSting", Get("sting_death", SynthEffects.DeathSting, false));
            SetRef(t.GameOver, "clearSting", Get("sting_clear", SynthEffects.ClearSting, false));
            AssetDatabase.SaveAssets();
        }
    }
}
