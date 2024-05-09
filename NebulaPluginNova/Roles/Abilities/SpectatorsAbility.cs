using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles.Abilities;

public class SpectatorsAbility : IGameOperator
{
    static ISpriteLoader spectatorSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SpectatorButton.png", 115f);
    static ISpriteLoader spectatorChangeSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SpectatorChangeButton.png", 115f);

    GamePlayer? currentTarget = null;

    GamePlayer[] AvailableTargets => NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.IsDead || p.AmOwner).OrderBy(p => p.AmOwner ? -1 : p.PlayerId).ToArray();
    
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
        float axis = Input.GetAxis("Mouse ScrollWheel");

        float rate = NebulaGameManager.Instance!.WideCamera.TargetRate;
        if (axis < 0f) rate -= 0.25f;
        if (axis > 0f) rate += 0.25f;
        rate = Mathf.Clamp(rate, 1f, 6f);

        NebulaGameManager.Instance!.WideCamera.TargetRate = rate;
    }

    public SpectatorsAbility()
    {
        /*
        var spectatorButton = new ModAbilityButton(true).KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Spectator));
        spectatorButton.SetSprite(spectatorSprite.GetSprite());
        spectatorButton.Availability = button => true;
        spectatorButton.Visibility = button => true;
        spectatorButton.OnClick = (button) => SwitchSpectatorMode();
        spectatorButton.SetLabel("spectator");
        */

        var spectatorChangeButton = new ModAbilityButton(true)
            .KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.SpectatorRight)).SubKeyBind(Virial.Compat.VirtualKeyInput.SpectatorLeft);
        spectatorChangeButton.SetSprite(spectatorChangeSprite.GetSprite());
        spectatorChangeButton.Availability = button => true;
        spectatorChangeButton.Visibility = button => !(NebulaGameManager.Instance?.LocalPlayerInfo.Tasks.IsCrewmateTask ?? false) || (NebulaGameManager.Instance?.LocalPlayerInfo.Tasks.IsCompletedCurrentTasks ?? true);
        spectatorChangeButton.OnClick = (button) =>
        {
            ChangeTarget(true);
        };
        spectatorChangeButton.OnSubAction = (button) =>
        {
            ChangeTarget(false);
        };
        spectatorChangeButton.SetLabel("spectatorChange");

        SetSpectatorMode(true);
    }

    //プレイヤーの死亡時、そのプレイヤーを観戦していたら近くのプレイヤーに視点を変える
    void IGameOperator.OnPlayerDead(Virial.Game.Player dead)
    {
        if(currentTarget == dead)
        {
            var nearest = AvailableTargets.OrderBy(p => p.VanillaPlayer.transform.position.Distance(dead.VanillaPlayer.transform.position));
            currentTarget = nearest.FirstOrDefault();
            OnChangeTarget();
        }
    }

    void IGameOperator.OnPlayerMurdered(Virial.Game.Player dead, Virial.Game.Player murderer)
    {
        if (currentTarget == dead) new StaticAchievementToken("spectator.dead");
        if (currentTarget == murderer) new StaticAchievementToken("spectator.murderer");
    }
}
