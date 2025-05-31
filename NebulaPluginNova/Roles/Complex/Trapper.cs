using NAudio.CoreAudioApi;
using Nebula.Game.Statistics;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Game;

namespace Nebula.Roles.Complex;


file static class TrapperSystem
{
    private static SpriteLoader?[] buttonSprites = [
        SpriteLoader.FromResource("Nebula.Resources.Buttons.AccelTrapButton.png",115f),
        SpriteLoader.FromResource("Nebula.Resources.Buttons.DecelTrapButton.png",115f),
        SpriteLoader.FromResource("Nebula.Resources.Buttons.CommTrapButton.png",115f),
        SpriteLoader.FromResource("Nebula.Resources.Buttons.KillTrapButton.png",115f),
        null
    ];

    private const int AccelTrapId = 0;
    private const int DecelTrapId = 1;
    private const int CommTrapId = 2;
    private const int KillTrapId = 3;
    public static void OnActivated(IUsurpableAbility myRole, bool isEvil, (int id,int cost)[] buttonVariation, List<Trapper.Trap> localTraps, int leftCost)
    {
        Vector2? pos = null;
        int buttonIndex = 0;
        var placeButton = NebulaAPI.Modules.EffectButton(myRole, myRole.MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "trapper.place",
            Trapper.PlaceCoolDownOption, Trapper.PlaceDurationOption, "place", buttonSprites[buttonVariation[0].id]!, 
            _ => leftCost >= buttonVariation[buttonIndex].cost, _ => leftCost > 0).SetAsUsurpableButton(myRole);
        placeButton.BindSubKey(Virial.Compat.VirtualKeyInput.AidAction, "trapper.switch", true);
        int iconVariation = isEvil ? 0 : 3;
        placeButton.ShowUsesIcon(iconVariation, leftCost.ToString());
        placeButton.OnEffectStart = (button) =>
        {
            float duration = Trapper.PlaceDurationOption;
            NebulaAsset.PlaySE(duration < 3f ? NebulaAudioClip.Trapper2s : NebulaAudioClip.Trapper3s);

            pos = (Vector2)myRole.MyPlayer.TruePosition + new Vector2(0f, 0.085f);
            myRole.MyPlayer.GainSpeedAttribute(0f, duration, false, 10);
        };
        placeButton.OnEffectEnd = (button) => 
        {
            NebulaGameManager.Instance?.RpcDoGameAction(myRole.MyPlayer, myRole.MyPlayer.Position, myRole.MyPlayer.IsImpostor ? GameActionTypes.EvilTrapPlacementAction : GameActionTypes.NiceTrapPlacementAction);

            placeButton.StartCoolDown();
            localTraps.Add(Trapper.Trap.GenerateTrap(buttonVariation[buttonIndex].id, pos!.Value));
            leftCost -= buttonVariation[buttonIndex].cost;
            button.UpdateUsesIcon(leftCost.ToString());

            if (Trapper.KillTrapSoundDistanceOption > 0f)
            {
                if (buttonVariation[buttonIndex].id == KillTrapId) NebulaAsset.RpcPlaySE.Invoke((NebulaAudioClip.TrapperKillTrap, PlayerControl.LocalPlayer.transform.position, Trapper.KillTrapSoundDistanceOption * 0.6f, Trapper.KillTrapSoundDistanceOption));
            }

            if (isEvil && buttonVariation[buttonIndex].id == DecelTrapId)
                new StaticAchievementToken("evilTrapper.common1");
            if (!isEvil && buttonVariation[buttonIndex].id == AccelTrapId)
                new StaticAchievementToken("niceTrapper.common1");

        };
        placeButton.OnSubAction = (button) =>
        {
            if (button.IsInEffect) return;
            buttonIndex = (buttonIndex + 1) % buttonVariation.Length;
            placeButton.SetImage(buttonSprites[buttonVariation[buttonIndex].id]!);
        };
    }

    public static void OnMeetingStart(List<Trapper.Trap> localTraps, List<Trapper.Trap>? specialTraps)
    {
        foreach (var lTrap in localTraps)
        {
            var gTrap = NebulaSyncObject.RpcInstantiate(Trapper.Trap.MyGlobalTag, [lTrap.TypeId, lTrap.Position.x, lTrap.Position.y])?.SyncObject as Trapper.Trap;
            gTrap?.SetAsOwner();
            NebulaSyncObject.LocalDestroy(lTrap.ObjectId);
            if (gTrap?.TypeId is KillTrapId or CommTrapId) specialTraps?.Add(gTrap!);
        }
        localTraps.Clear();
    }

    public static void OnInactivated(List<Trapper.Trap> localTraps, List<Trapper.Trap>? specialTraps)
    {
        foreach (var lTrap in localTraps) NebulaSyncObject.LocalDestroy(lTrap.ObjectId);
        foreach (var sTrap in specialTraps ?? []) NebulaSyncObject.LocalDestroy(sTrap.ObjectId);
        localTraps.Clear();
        specialTraps?.Clear();
    }
}

[NebulaRPCHolder]
public class Trapper : DefinedSingleAbilityRoleTemplate<IUsurpableAbility>, DefinedRole
{
    private Trapper(bool isEvil) : base(
        isEvil ? "evilTrapper" : "niceTrapper",
        isEvil ? new(Palette.ImpostorRed) : new(206,219,96),
        isEvil ? RoleCategory.ImpostorRole : RoleCategory.CrewmateRole,
        isEvil ? Impostor.Impostor.MyTeam : Crewmate.Crewmate.MyTeam,
        [NumOfChargesOption, isEvil ? CostOfKillTrapOption : CostOfCommTrapOption, PlaceCoolDownOption, PlaceDurationOption, SpeedTrapSizeOption, isEvil ? KillTrapSizeOption : CommTrapSizeOption, SpeedTrapDurationOption, AccelRateOption, DecelRateOption])
    {
        //IsEvil = isEvil;

        if(IsEvil) ConfigurationHolder?.AppendConfiguration(KillTrapSoundDistanceOption);
        ConfigurationHolder?.ScheduleAddRelated(() => [isEvil ? MyNiceRole.ConfigurationHolder! : MyEvilRole.ConfigurationHolder!]);

        if (IsEvil)
            GameActionTypes.EvilTrapPlacementAction = new("trapper.placement.evil", this, isPlacementAction: true);
        else
            GameActionTypes.NiceTrapPlacementAction = new("trapper.placement.nice", this, isPlacementAction: true);
    }


    public bool IsEvil => Category == RoleCategory.ImpostorRole;
    bool DefinedRole.IsJackalizable => IsEvil;
    public override IUsurpableAbility CreateAbility(GamePlayer player, int[] arguments) => IsEvil ? new EvilAbility(player, arguments.GetAsBool(0), arguments.Get(1, NumOfChargesOption)) : new NiceAbility(player, arguments.GetAsBool(0), arguments.Get(1, NumOfChargesOption));

    static internal readonly IntegerConfiguration NumOfChargesOption = NebulaAPI.Configurations.Configuration("options.role.trapper.numOfCharges", (1, 15), 3);
    static internal readonly FloatConfiguration PlaceCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.trapper.placeCoolDown", (5f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);
    static internal readonly FloatConfiguration PlaceDurationOption = NebulaAPI.Configurations.Configuration("options.role.trapper.placeDuration", (1f, 3f, 0.5f), 2f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration SpeedTrapDurationOption = NebulaAPI.Configurations.Configuration("options.role.trapper.speedDuration", (2.5f, 40f, 2.5f), 10f, FloatConfigurationDecorator.Second);

    static private readonly FloatConfiguration SpeedTrapSizeOption = NebulaAPI.Configurations.Configuration("options.role.trapper.speedTrapSize", (0.25f, 5f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration CommTrapSizeOption = NebulaAPI.Configurations.Configuration("options.role.niceTrapper.commTrapSize", (0.25f, 5f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration KillTrapSizeOption = NebulaAPI.Configurations.Configuration("options.role.evilTrapper.killTrapSize", (0.25f, 5f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);

    static private readonly FloatConfiguration AccelRateOption = NebulaAPI.Configurations.Configuration("options.role.trapper.accelRate", (1f, 5f, 0.125f), 1.5f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration DecelRateOption = NebulaAPI.Configurations.Configuration("options.role.trapper.decelRate", (0.125f, 1f, 0.125f), 0.5f, FloatConfigurationDecorator.Ratio);

    static internal readonly FloatConfiguration KillTrapSoundDistanceOption = NebulaAPI.Configurations.Configuration("options.role.evilTrapper.killTrapSoundDistance", (0f, 20f, 2.5f), 10f, FloatConfigurationDecorator.Ratio);
    static private readonly IntegerConfiguration CostOfKillTrapOption = NebulaAPI.Configurations.Configuration("options.role.evilTrapper.costOfKillTrap", (1, 5), 2);
    static private readonly IntegerConfiguration CostOfCommTrapOption = NebulaAPI.Configurations.Configuration("options.role.niceTrapper.costOfCommTrap", (1, 5), 2);

    static public readonly Trapper MyNiceRole = new(false);
    static public readonly Trapper MyEvilRole = new(true);


    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class Trap : NebulaSyncStandardObject, IGameOperator
    {
        public const string MyGlobalTag = "TrapGlobal";
        public const string MyLocalTag = "TrapLocal";

        static readonly SpriteLoader[] trapSprites = [
            SpriteLoader.FromResource("Nebula.Resources.AccelTrap.png",150f),
            SpriteLoader.FromResource("Nebula.Resources.DecelTrap.png",150f),
            SpriteLoader.FromResource("Nebula.Resources.CommTrap.png",150f),
            SpriteLoader.FromResource("Nebula.Resources.KillTrap.png",150f),
            SpriteLoader.FromResource("Nebula.Resources.KillTrapBroken.png",150f)
        ];

        public int TypeId;
        private float lastAccelTime = 0f;
        public Trap(Vector2 pos,int type, bool isLocal) : base(pos, ZOption.Back, true, trapSprites[type].GetSprite(), isLocal) {
            TypeId = type;

            //不可視
            if (TypeId >= 2 && !isLocal) Color = Color.clear;
        }

        public void SetAsOwner()
        {
            if (!(Color.a > 0f)) Color = Color.white;
        }

        static Trap()
        {
            NebulaSyncObject.RegisterInstantiater(MyGlobalTag, (args) => new Trap(new(args[1], args[2]), (int)args[0], false));
            NebulaSyncObject.RegisterInstantiater(MyLocalTag, (args) => new Trap(new(args[1], args[2]), (int)args[0], true));
        }

        static public Trap GenerateTrap(int type,Vector2 pos)
        {
            return (NebulaSyncObject.LocalInstantiate(MyLocalTag, [(float)type, pos.x, pos.y]).SyncObject as Trap)!;
        }

        public void SetSpriteAsUsedKillTrap()
        {
            Sprite = trapSprites[4].GetSprite();
            Color = Color.white;
        }

        void Update(GameUpdateEvent ev)
        {
            if(TypeId < 2 && !(Color.a < 1f))
            {
                //加減速トラップはそれぞれで処理する

                if (Position.Distance(PlayerControl.LocalPlayer.transform.position) < SpeedTrapSizeOption*0.35f)
                {
                    var invoker = PlayerModInfo.RpcAttrModulator.GetInvoker((PlayerControl.LocalPlayer.PlayerId,
                        new SpeedModulator(TypeId == 0 ? AccelRateOption : DecelRateOption, Vector2.one, true, Trapper.SpeedTrapDurationOption, false, 50, "nebula.trap" + TypeId), false));

                    if(NebulaGameManager.Instance?.HavePassed(lastAccelTime, 0.3f) ?? false)
                    {
                        lastAccelTime = NebulaGameManager.Instance!.CurrentTime;
                        invoker.InvokeSingle();
                    }
                    else
                    {
                        invoker.InvokeLocal();
                    }
                }
            }
        }
    }

    public class NiceAbility : AbstractPlayerUsurpableAbility, IPlayerAbility, IGameOperator
    {
        private List<Trap> localTraps = [], commTraps = [];

        AchievementToken<(bool cleared, int playerMask)>? acTokenChallenge = null;
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public NiceAbility(GamePlayer player, bool isUsurped, int leftCost) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                TrapperSystem.OnActivated(this, false, [(0, 1), (1, 1), (2, CostOfCommTrapOption)], localTraps, leftCost);
                acTokenChallenge = new("niceTrapper.challenge", (false, 0), (val, _) => val.cleared);
            }
        }

        void IGameOperator.OnReleased()
        {
            if (AmOwner) TrapperSystem.OnInactivated(localTraps, commTraps);
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            TrapperSystem.OnMeetingStart(localTraps, commTraps);
            acTokenChallenge!.Value.playerMask = 0;
        }

        private uint lastCommPlayersMask = 0;

        [Local]
        void LocalUpdate(GameUpdateEvent ev)
        {
            //会議中はなにもしない
            if (MeetingHud.Instance || ExileController.Instance) return;

            uint commMask = 0;
            foreach(var commTrap in commTraps)
            {
                foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
                {
                    if (p.AmOwner) continue;
                    if (p.IsDead || p.Unbox().IsInvisible) continue;
                    if (p.VanillaPlayer.transform.position.Distance(commTrap.Position) < CommTrapSizeOption * 0.35f)
                    {
                        //直前にトラップを踏んでいるプレイヤーは無視する
                        commMask |= 1u << p.PlayerId;
                        if ((lastCommPlayersMask & (1u << p.PlayerId)) != 0) continue;

                        //Camo貫通(Morphingまで効果を受ける)
                        var arrow = new Arrow().SetColorByOutfit(p.Unbox().GetOutfit(75).Outfit.outfit);
                        arrow.TargetPos = commTrap.Position;
                        NebulaManager.Instance.StartCoroutine(arrow.CoWaitAndDisappear(3f).WrapToIl2Cpp());

                        acTokenChallenge!.Value.playerMask |= 1 << p.PlayerId;
                        if(!acTokenChallenge.Value.cleared && NebulaGameManager.Instance.AllPlayerInfo.Count(p => (acTokenChallenge!.Value.playerMask & (1 << p.PlayerId)) != 0) >= 8)
                            acTokenChallenge.Value.cleared = true;
                    }
                }
            }

            lastCommPlayersMask = commMask;
        }
    }

    public class EvilAbility : AbstractPlayerUsurpableAbility, IPlayerAbility, IGameOperator
    {
        private List<Trap> localTraps = [], killTraps = [];

        AchievementToken<int>? acTokenChallenge = null;
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public EvilAbility(GamePlayer player, bool isUsurped, int leftCost) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                TrapperSystem.OnActivated(this, true, [(0, 1), (1, 1), (3, CostOfKillTrapOption)], localTraps, leftCost);
                acTokenChallenge = new("evilTrapper.challenge", 0, (val, _) => val >= 2 && NebulaGameManager.Instance!.EndState!.Winners.Test(MyPlayer) && !MyPlayer.IsDead);
            }
        }

        void IGameOperator.OnReleased()
        {
            if (AmOwner) TrapperSystem.OnInactivated(localTraps, killTraps);
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev) => TrapperSystem.OnMeetingStart(localTraps, killTraps);

        [Local]
        void LocalUpdate(GameUpdateEvent ev)
        {
            //会議中はなにもしない
            if (MeetingHud.Instance || ExileController.Instance) return;

            if (!(PlayerControl.LocalPlayer.killTimer > 0f)) {
                killTraps.RemoveAll((killTrap) => {
                foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
                {
                    if (p.AmOwner) continue;
                    if (p.IsDead || p.VanillaPlayer.Data.Role.IsImpostor) continue;

                    if (p.VanillaPlayer.transform.position.Distance(killTrap.Position) < KillTrapSizeOption * 0.35f)
                    {
                            using (RPCRouter.CreateSection("TrapKill"))
                            {
                                MyPlayer.MurderPlayer(p, PlayerState.Trapped,EventDetail.Trap, KillParameter.RemoteKill);
                                NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                                RpcTrapKill.Invoke(killTrap.ObjectId);
                                acTokenChallenge!.Value++;
                            }

                            return true;
                        }
                    }
                    return false;
                });
            }
        }

    }

    static private RemoteProcess<int> RpcTrapKill = new(
        "UseKillTrap",
        (message, _) =>
        {
            NebulaSyncObject.GetObject<Trap>(message)?.SetSpriteAsUsedKillTrap();
        }
        );
}
