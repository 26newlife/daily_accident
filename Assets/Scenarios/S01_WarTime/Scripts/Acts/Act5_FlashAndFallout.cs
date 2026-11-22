using GameCore;
using GameData;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 5. 섬광과 낙진 (클라이맥스)
    ///   ① 백색 섬광(화이트아웃). 이때 밖(복도)에 있으면 즉사 (NuclearFlash)
    ///   ② 충격파까지 몇 초의 여유: 창에서 떨어져 몸을 낮춰야 한다. 창가에 서 있으면 유리 파편에 즉사 (ShockwaveDebris)
    ///   ③ 모든 창이 깨지고 검은 비 + 한파가 시작된다: 방독면·요오드·안쪽 방(창에서 먼 곳)·보온으로 피폭과 체온을 관리하며 구조를 기다린다
    /// 피폭·체온·식량 틱은 WarState 가 담당한다 (여기서는 단계 전환과 연출만).
    /// </summary>
    public class Act5_FlashAndFallout : WarActBase
    {
        [SerializeField] private ObjectiveData objectiveSurvive, objectiveProtect;
        [SerializeField] private EnvironmentPreset falloutPreset;

        protected override void OnEnter()
        {
            WarConfig c = Cfg;
            if (War == null || c == null) return;
            AddObjective(objectiveSurvive);
            War.PushRecords();
            WarOutbreak.Instance?.Stop();

            // ① 섬광
            WarHud.Instance?.Whiteout(c.whiteoutSeconds * 0.5f, c.whiteoutSeconds);
            HouseController.Instance?.SetBlackout(false);
            EventBus.ShowSubtitle("…창밖이 하얗게 변했다. 창에서 떨어져 몸을 낮춰라!", c.shockwaveDelaySeconds - 1f);

            if (PlayerIsOutside(c.act4DoorOutsideZ))
            {
                War.Kill(DeathReason.NuclearFlash);
                return;
            }
            At(c.shockwaveDelaySeconds, Shockwave);
        }

        private void Shockwave()
        {
            WarConfig c = Cfg;
            WarAudio.Instance?.Play(WarSound.Boom, 1f);
            HouseController house = HouseController.Instance;
            if (house != null) { house.ShakeCamera(1.2f, 3.5f); house.TriggerStructureShake(StructureShakePreset.Heavy); }

            // ② 창가 판정
            PlayerController pl = FindFirstObjectByType<PlayerController>();
            bool nearWindow = pl != null && War.DistanceToNearestWindow(pl.transform.position) < c.windowDangerRadius;
            bool covered = pl != null && c.crouchSavesNearWindow && pl.IsCrouching;
            WarWindows.BreakAll();
            if (nearWindow && !covered)
            {
                War.Kill(DeathReason.ShockwaveDebris);
                return;
            }
            ScenarioFlags.Set(WarKeys.FlagSurvivedFlash, true);
            War.MarkContaminated();
            EventBus.ShowMessage("충격파가 지나갔다. 깨진 창으로 바깥 공기가 스며든다.");
            At(5f, StartFallout);
        }

        private void StartFallout()
        {
            WarConfig c = Cfg;
            War.SetPhase(WarPhase.Fallout);
            ApplyPreset(falloutPreset);
            AddObjective(objectiveProtect);
            WarAudio.Instance?.Play(WarSound.Rumble, 0.5f);
            EventBus.ShowSubtitle("검은 비가 내리기 시작한다. 기온이 곤두박질친다.", 5f);
            At(4f, () => EventBus.ShowMessage("방독면을 착용하고(사용 키), 깨진 창에서 먼 안쪽 방으로 물러나 보온하며 버텨야 한다."));
            At(c.falloutSeconds * 0.5f, () => EventBus.ShowSubtitle("라디오는 치직거리기만 한다. 멀리서 정체 모를 폭음이 계속된다.", 5f));
            At(c.falloutSeconds, () => CompleteAct(), "war.act5.end");
            At(20f, ProtectCheck);
        }

        private void ProtectCheck()
        {
            if (War == null || !IsActive) return;
            if (War.GasMaskWorn && War.PlayerDeepInside) CompleteObjective(objectiveProtect);
            At(10f, ProtectCheck);
        }
    }
}
