using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Nebula.Roles.Abilities;

public class SpectatorsAbility : IGameEntity
{
    static ISpriteLoader spectatorSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SpectatorButton.png", 115f);
    static ISpriteLoader spectatorChangeSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SpectatorChangeButton.png", 115f);

    GamePlayer? currentTarget = null;
    bool spectatorsMode = false;
    bool isFirst = true;

    GamePlayer[] AvailableTargets => NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.IsDead || p.AmOwner).OrderBy(p => p.AmOwner ? -1 : p.PlayerId).ToArray();
    
    void OnChangeTarget()
    {
        if (!spectatorsMode) return;
        var target = currentTarget?.Unbox().MyControl;

        if (target != null)
            AmongUsUtil.SetCamTarget(target);
        else
            SetSpectatorMode(false);
    }

    void RefreshTarget()
    {
        var targets = AvailableTargets;

        if(currentTarget == null)
        {
            currentTarget = targets[0];
            return;
        }

        int index = Array.IndexOf(targets, currentTarget);
        if(index == -1)
        {
            currentTarget = targets[0];
            return;
        }
    }

    void ChangeTarget(bool increament)
    {
        var targets = AvailableTargets;

        if (targets.Length == 0)
        {
            SetSpectatorMode(false);
            return;
        }

        int index = Array.IndexOf(targets, currentTarget);
        if(index == -1)
        {
            currentTarget = targets[0];
        }
        else
        {
            index += increament ? 1 : -1;
            if (index < 0) index = targets.Length - 1;
            else if (index >= targets.Length) index = 0;

            currentTarget = targets[index];
        }

        OnChangeTarget();
    }

    void SwitchSpectatorMode() => SetSpectatorMode(!spectatorsMode);

    void SetSpectatorMode(bool on)
    {
        if (spectatorsMode == on) return;

        spectatorsMode = on;
        if (on)
        {
            if (AvailableTargets.Length == 0) SetSpectatorMode(false);
            RefreshTarget();
            OnChangeTarget();

            //NebulaGameManager.Instance?.WideCamera.SetDrawShadow(false);

            if (isFirst)
            {
                NebulaGameManager.Instance!.WideCamera.TargetRate = 1.5f;
                isFirst = false;
            }
        }
        else
        {
            AmongUsUtil.SetCamTarget();
            NebulaGameManager.Instance?.WideCamera.Inactivate();
        }
        
    }

    void IGameEntity.HudUpdate()
    {
        if (spectatorsMode)
        {
            float axis = Input.GetAxis("Mouse ScrollWheel");

            float rate = NebulaGameManager.Instance!.WideCamera.TargetRate;
            if (axis < 0f) rate -= 0.25f;
            if (axis > 0f) rate += 0.25f;
            rate = Mathf.Clamp(rate, 1f, 6f);

            NebulaGameManager.Instance!.WideCamera.TargetRate = rate;
        }
    }

    public SpectatorsAbility()
    {
        var spectatorButton = new ModAbilityButton(true).KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Spectator));
        spectatorButton.SetSprite(spectatorSprite.GetSprite());
        spectatorButton.Availability = button => true;
        spectatorButton.Visibility = button => true;
        spectatorButton.OnClick = (button) => SwitchSpectatorMode();
        spectatorButton.SetLabel("spectator");

        var spectatorChangeButton = new ModAbilityButton(true)
            .KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.SpectatorRight)).SubKeyBind(Virial.Compat.VirtualKeyInput.SpectatorLeft);
        spectatorChangeButton.SetSprite(spectatorChangeSprite.GetSprite());
        spectatorChangeButton.Availability = button => true;
        spectatorChangeButton.Visibility = button => !(NebulaGameManager.Instance?.LocalPlayerInfo.Tasks.IsCrewmateTask ?? false) || (NebulaGameManager.Instance?.LocalPlayerInfo.Tasks.IsCompletedCurrentTasks ?? true);
        spectatorChangeButton.OnClick = (button) =>
        {
            if (!spectatorsMode) SetSpectatorMode(true);
            ChangeTarget(true);
        };
        spectatorChangeButton.OnSubAction = (button) =>
        {
            if (!spectatorsMode) SetSpectatorMode(true);
            ChangeTarget(false);
        };
        spectatorChangeButton.SetLabel("spectatorChange");
    }

    //プレイヤーの死亡時、そのプレイヤーを観戦していたら近くのプレイヤーに視点を変える
    void IGameEntity.OnPlayerDead(Virial.Game.Player dead)
    {
        if(spectatorsMode && currentTarget == dead)
        {
            var nearest = AvailableTargets.OrderBy(p => p.VanillaPlayer.transform.position.Distance(dead.VanillaPlayer.transform.position));
            currentTarget = nearest.FirstOrDefault();
            OnChangeTarget();
        }
    }

    void IGameEntity.OnPlayerMurdered(Virial.Game.Player dead, Virial.Game.Player murderer)
    {
        if (spectatorsMode)
        {
            if (currentTarget == dead) new StaticAchievementToken("spectator.dead");
            if (currentTarget == murderer) new StaticAchievementToken("spectator.murderer");
        }
    }
}
