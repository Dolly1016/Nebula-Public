using Virial;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Abilities;

public class SpectatorsAbility : IGameOperator
{
    static Image spectatorExitSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SpectatorExitButton.png", 115f);
    static Image spectatorChangeSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SpectatorChangeButton.png", 115f);

    GamePlayer? currentTarget = null;

    GamePlayer[] AvailableTargets => NebulaGameManager.Instance!.AllPlayerInfo.Where(p => !p.IsDead || p.AmOwner).OrderBy(p => p.AmOwner ? -1 : p.PlayerId).ToArray();
    
    void OnChangeTarget()
    {
        var target = currentTarget?.Unbox().MyControl;

        AmongUsUtil.SetCamTarget(target);
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

    void SetSpectatorMode(bool on)
    {
        if (on)
        {
            if (AvailableTargets.Length == 0) SetSpectatorMode(false);
            RefreshTarget();
            OnChangeTarget();
        }
        else
        {
            AmongUsUtil.SetCamTarget();
        }
        
    }

    void HudUpdate(GameHudUpdateEvent ev)
    {
        if (!Utilities.NebulaInput.SomeUiIsActive)
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

        var spectatorChangeButton = new ModAbilityButtonImpl(true)
            .KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.SpectatorRight)).SubKeyBind(Virial.Compat.VirtualKeyInput.SpectatorLeft).Register(NebulaAPI.CurrentGame);
        spectatorChangeButton.SetSprite(spectatorChangeSprite.GetSprite());
        spectatorChangeButton.Availability = button => true;
        spectatorChangeButton.Visibility = button => !(GamePlayer.LocalPlayer?.Tasks.IsCrewmateTask ?? false) || (GamePlayer.LocalPlayer?.Tasks.IsCompletedCurrentTasks ?? true);
        spectatorChangeButton.OnClick = (button) =>
        {
            ChangeTarget(true);
        };
        spectatorChangeButton.OnSubAction = (button) =>
        {
            ChangeTarget(false);
        };
        spectatorChangeButton.SetLabel("spectatorChange");


        
        var spectatorButton = new ModAbilityButtonImpl(true).KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Spectator)).Register(NebulaAPI.CurrentGame);
        spectatorButton.SetSprite(spectatorExitSprite.GetSprite());
        spectatorButton.Availability = button => true;
        spectatorButton.Visibility = button => !(currentTarget?.AmOwner ?? true);
        spectatorButton.OnClick = (button) =>
        {
            currentTarget = null;
            OnChangeTarget();
        };
        spectatorButton.SetLabel("spectatorExit");


        SetSpectatorMode(true);
    }

    //プレイヤーの死亡時、そのプレイヤーを観戦していたら近くのプレイヤーに視点を変える
    void OnPlayerDead(PlayerDieEvent ev)
    {
        if(currentTarget == ev.Player)
        {
            var nearest = AvailableTargets.OrderBy(p => p.VanillaPlayer.transform.position.Distance(ev.Player.VanillaPlayer.transform.position));
            currentTarget = nearest.FirstOrDefault();
            OnChangeTarget();
        }
    }

    void OnPlayerMurdered(PlayerMurderedEvent ev)
    {
        if (currentTarget == ev.Dead) new StaticAchievementToken("spectator.dead");
        if (currentTarget == ev.Murderer) new StaticAchievementToken("spectator.murderer");
    }
}
