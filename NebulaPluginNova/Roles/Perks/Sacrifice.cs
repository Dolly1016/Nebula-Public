using Il2CppSystem;
using Nebula.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Perks;

internal class Sacrifice : PerkFunctionalInstance
{
    static float Duration => DurationOption;
    static FloatConfiguration DurationOption = NebulaAPI.Configurations.Configuration("perk.sacrifice.duration", (10f,60f,5f), 20f, FloatConfigurationDecorator.Second);
    static PerkFunctionalDefinition Def = new("sacrifice", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("sacrifice", 4, 58, new(175,115,200)).DurationText("%D%", () => Duration), (def, instance) => new Sacrifice(def, instance));

    bool used = false;
    public Sacrifice(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        var durationTimer = NebulaAPI.Modules.Timer(this, Duration).SetTime(0f);
        PerkInstance.BindTimer(durationTimer);
    }

    bool CanUse => !used && !MyPlayer.IsDead && MyPlayer.CanMove && !(PerkInstance.MyTimer?.IsProgressing ?? false);

    public override bool HasAction => true;
    private FakePlayerController? myFakePlayer = null;
    public override void OnClick()
    {
        if (used) return;

        myFakePlayer = FakePlayerController.SpawnSyncFakePlayer(MyPlayer, new(MyPlayer.Position, KillCharacteristics.KillOne, true, MyPlayer.VanillaCosmetics.FlipX, MyPlayer.VanillaCosmetics.GetPetPosition())).BindLifespan(NebulaAPI.CurrentGame);
        PerkInstance.MyTimer?.Start();
        used = true;
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        if ((PerkInstance.MyTimer?.IsProgressing ?? false) && (myFakePlayer?.IsDeadObject ?? false)) PerkInstance.MyTimer.SetTime(0f);
        if (!(PerkInstance.MyTimer?.IsProgressing ?? true) && (myFakePlayer?.IsActive ?? false)) RequestDespawn();

        PerkInstance.SetDisplayColor(CanUse ? Color.white : Color.gray);
    }

    void OnInteract(PlayerInteractionFailedForMyFakePlayerEvent ev)
    {
        if (myFakePlayer != null && myFakePlayer.IsActive && ev.Target == myFakePlayer) RequestDespawn();
    }

    void RequestDespawn()
    {
        ManagedEffects.RpcDisappearEffect.Invoke((myFakePlayer.Position.ToUnityVector().AsVector3(-1f), LayerExpansion.GetPlayersLayer()));
        myFakePlayer.Release();
    }

}