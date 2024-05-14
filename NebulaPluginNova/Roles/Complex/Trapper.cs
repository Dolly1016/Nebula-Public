using NAudio.CoreAudioApi;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Game;

namespace Nebula.Roles.Complex;


file static class TrapperSystem
{
    private static SpriteLoader?[] buttonSprites = new SpriteLoader?[] { 
        SpriteLoader.FromResource("Nebula.Resources.Buttons.AccelTrapButton.png",115f),
        SpriteLoader.FromResource("Nebula.Resources.Buttons.DecelTrapButton.png",115f),
        SpriteLoader.FromResource("Nebula.Resources.Buttons.CommTrapButton.png",115f),
        SpriteLoader.FromResource("Nebula.Resources.Buttons.KillTrapButton.png",115f),
        null
    };

    private const int AccelTrapId = 0;
    private const int DecelTrapId = 1;
    private const int CommTrapId = 2;
    private const int KillTrapId = 3;
    public static void OnActivated(RuntimeRole myRole, (int id,int cost)[] buttonVariation, List<Trapper.Trap> localTraps)
    {
        Vector2? pos = null;
        int buttonIndex = 0;
        int leftCost = Trapper.NumOfChargesOption;
        var placeButton = myRole.Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability).SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction);
        placeButton.SetSprite(buttonSprites[buttonVariation[0].id]?.GetSprite());
        placeButton.Availability = (button) => myRole.MyPlayer.CanMove && leftCost >= buttonVariation[buttonIndex].cost;
        placeButton.Visibility = (button) => !myRole.MyPlayer.IsDead && leftCost > 0;
        var usesText = placeButton.ShowUsesIcon(myRole.Role.Category == RoleCategory.ImpostorRole ? 0 : 3);
        usesText.text = leftCost.ToString();
        placeButton.OnClick = (button) =>
        {
            button.ActivateEffect();
        };
        placeButton.OnEffectStart = (button) =>
        {
            float duration = Trapper.PlaceDurationOption.GetFloat();
            NebulaAsset.PlaySE(duration < 3f ? NebulaAudioClip.Trapper2s : NebulaAudioClip.Trapper3s);

            pos = myRole.MyPlayer.TruePosition + new Vector2(0f, 0.085f);
            PlayerModInfo.RpcAttrModulator.Invoke((myRole.MyPlayer.PlayerId, new SpeedModulator(0f, Vector2.one, true, duration, false, 10), true));
        };
        placeButton.OnEffectEnd = (button) => 
        {
            placeButton.StartCoolDown();
            localTraps.Add(Trapper.Trap.GenerateTrap(buttonVariation[buttonIndex].id, pos!.Value));
            leftCost -= buttonVariation[buttonIndex].cost;
            usesText.text = leftCost.ToString();

            if (Trapper.KillTrapSoundDistanceOption.GetFloat() > 0f)
            {
                if (buttonVariation[buttonIndex].id == KillTrapId) NebulaAsset.RpcPlaySE.Invoke((NebulaAudioClip.TrapperKillTrap, PlayerControl.LocalPlayer.transform.position, Trapper.KillTrapSoundDistanceOption.GetFloat() * 0.6f, Trapper.KillTrapSoundDistanceOption.GetFloat()));
            }

            if (myRole.Role.Category == RoleCategory.ImpostorRole && buttonVariation[buttonIndex].id == DecelTrapId)
                new StaticAchievementToken("evilTrapper.common1");
            if (myRole.Role.Category == RoleCategory.CrewmateRole && buttonVariation[buttonIndex].id == AccelTrapId)
                new StaticAchievementToken("niceTrapper.common1");

        };
        placeButton.OnSubAction = (button) =>
        {
            if (button.EffectActive) return;
            buttonIndex = (buttonIndex + 1) % buttonVariation.Length;
            placeButton.SetSprite(buttonSprites[buttonVariation[buttonIndex].id]?.GetSprite());
        };
        placeButton.CoolDownTimer = myRole.Bind(new Timer(0f, Trapper.PlaceCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
        placeButton.EffectTimer = myRole.Bind(new Timer(0f, Trapper.PlaceDurationOption.GetFloat()));
        placeButton.SetLabel("place");
    }

    public static void OnMeetingStart(List<Trapper.Trap> localTraps, List<Trapper.Trap>? specialTraps)
    {
        foreach (var lTrap in localTraps)
        {
            var gTrap = NebulaSyncObject.RpcInstantiate(Trapper.Trap.MyGlobalTag, new float[] { lTrap.TypeId, lTrap.Position.x, lTrap.Position.y }) as Trapper.Trap;
            if (gTrap != null) gTrap.SetAsOwner();
            NebulaSyncObject.LocalDestroy(lTrap.ObjectId);
            if (gTrap?.TypeId is KillTrapId or CommTrapId) specialTraps?.Add(gTrap!);
        }
        localTraps.Clear();
    }
}

[NebulaRPCHolder]
public class Trapper : ConfigurableStandardRole, DefinedRole
{
    static public Trapper MyNiceRole = new(false);
    static public Trapper MyEvilRole = new(true);

    public bool IsEvil { get; private set; }
    RoleCategory DefinedSingleAssignable.Category => IsEvil ? RoleCategory.ImpostorRole : RoleCategory.CrewmateRole;

    string DefinedAssignable.LocalizedName => IsEvil ? "evilTrapper" : "niceTrapper";
    Virial.Color DefinedAssignable.Color => new(IsEvil ? Palette.ImpostorRed : new Color(206f / 255f, 219f / 255f, 96f / 255f));
    RoleTeam DefinedSingleAssignable.Team => IsEvil ? Impostor.Impostor.MyTeam : Crewmate.Crewmate.Team;
    public override IEnumerable<IAssignableBase> RelatedOnConfig() { if (MyNiceRole != this) yield return MyNiceRole; if (MyEvilRole != this) yield return MyEvilRole; }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => IsEvil ? new EvilInstance(player) : new NiceInstance(player);

    static public NebulaConfiguration NumOfChargesOption = null!;
    static public NebulaConfiguration PlaceCoolDownOption = null!;
    static public NebulaConfiguration PlaceDurationOption = null!;
    static public NebulaConfiguration SpeedTrapDurationOption = null!;

    static public NebulaConfiguration SpeedTrapSizeOption = null!;
    static public NebulaConfiguration CommTrapSizeOption = null!;
    static public NebulaConfiguration KillTrapSizeOption = null!;

    static public NebulaConfiguration AccelRateOption = null!;
    static public NebulaConfiguration DecelRateOption = null!;

    static public NebulaConfiguration KillTrapSoundDistanceOption = null!;
    static public NebulaConfiguration CostOfKillTrapOption = null!;
    static public NebulaConfiguration CostOfCommTrapOption = null!;


    [NebulaPreLoad]
    public class Trap : NebulaSyncStandardObject, IGameOperator
    {
        public static string MyGlobalTag = "TrapGlobal";
        public static string MyLocalTag = "TrapLocal";

        static SpriteLoader[] trapSprites = new SpriteLoader[] {
            SpriteLoader.FromResource("Nebula.Resources.AccelTrap.png",150f),
            SpriteLoader.FromResource("Nebula.Resources.DecelTrap.png",150f),
            SpriteLoader.FromResource("Nebula.Resources.CommTrap.png",150f),
            SpriteLoader.FromResource("Nebula.Resources.KillTrap.png",150f),
            SpriteLoader.FromResource("Nebula.Resources.KillTrapBroken.png",150f)
        };

        public int TypeId;

        public Trap(Vector2 pos,int type, bool isLocal) : base(pos, ZOption.Back, true, trapSprites[type].GetSprite(), isLocal) {
            TypeId = type;

            //不可視
            if (TypeId >= 2 && !isLocal) Color = Color.clear;
        }

        public void SetAsOwner()
        {
            if (!(Color.a > 0f)) Color = Color.white;
        }

        public static void Load()
        {
            NebulaSyncObject.RegisterInstantiater(MyGlobalTag, (args) => new Trap(new(args[1], args[2]), (int)args[0], false));
            NebulaSyncObject.RegisterInstantiater(MyLocalTag, (args) => new Trap(new(args[1], args[2]), (int)args[0], true));
        }

        static public Trap GenerateTrap(int type,Vector2 pos)
        {
            return (NebulaSyncObject.LocalInstantiate(MyLocalTag, new float[] { (float)type, pos.x, pos.y }) as Trap)!;
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

                if (Position.Distance(PlayerControl.LocalPlayer.transform.position) < Trapper.SpeedTrapSizeOption.GetFloat()*0.35f)
                {
                    PlayerModInfo.RpcAttrModulator.Invoke((PlayerControl.LocalPlayer.PlayerId,
                        new SpeedModulator(TypeId == 0 ? Trapper.AccelRateOption.GetFloat() : Trapper.DecelRateOption.GetFloat(), Vector2.one, true, Trapper.SpeedTrapDurationOption.GetFloat(), false, 50, "nebula.trap" + TypeId), false));
                }
            }
        }
    }

    public Trapper(bool isEvil)
    {
        IsEvil = isEvil;
    }

    private static NebulaConfiguration GenerateCommonEditor(ConfigurationHolder holder)
    {
        var commonOptions = new NebulaConfiguration[] { PlaceCoolDownOption, PlaceDurationOption, NumOfChargesOption, SpeedTrapSizeOption, AccelRateOption, DecelRateOption, SpeedTrapDurationOption };
        foreach (var option in commonOptions) option.Title = new CombinedComponent(new TranslateTextComponent("role.general.common"), new RawTextComponent(" "), new TranslateTextComponent(option.Id));

        return new NebulaConfiguration(holder, () => {
            MetaWidgetOld widget = new();
            foreach (var option in commonOptions) widget.Append(option.GetEditor()!);
            return widget;
        }, () => Language.Translate("options.commonSetting") + "\n" + string.Join("\n", commonOptions.Select(option => option.GetShownString())));
    }
    protected override void LoadOptions()
    {
        base.LoadOptions();


        PlaceCoolDownOption ??= new NebulaConfiguration(null, "role.trapper.placeCoolDown", null, 5f, 60f, 5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
        PlaceDurationOption ??= new NebulaConfiguration(null, "role.trapper.placeDuration", null, 1f, 3f, 0.5f, 2f, 2f) { Decorator = NebulaConfiguration.SecDecorator };
        NumOfChargesOption ??= new NebulaConfiguration(null, "role.trapper.numOfCharges", null, 1, 15, 3, 3);
        SpeedTrapSizeOption ??= new NebulaConfiguration(null, "role.trapper.speedTrapSize", null, 0.25f, 5f, 0.25f, 1f, 1f) { Decorator = NebulaConfiguration.OddsDecorator };
        AccelRateOption ??= new NebulaConfiguration(null, "role.trapper.accelRate", null, 1f, 5f, 0.25f, 1.5f, 1.5f) { Decorator = NebulaConfiguration.OddsDecorator };
        DecelRateOption ??= new NebulaConfiguration(null, "role.trapper.decelRate", null, 0.125f, 1f, 0.125f, 0.5f, 0.5f) { Decorator = NebulaConfiguration.OddsDecorator };
        SpeedTrapDurationOption ??= new NebulaConfiguration(null, "role.trapper.speedDuration", null, 2.5f, 40f, 2.5f, 10f, 10f) { Decorator = NebulaConfiguration.SecDecorator };

        if (IsEvil)
        {
            KillTrapSoundDistanceOption = new NebulaConfiguration(RoleConfig, "killTrapSoundDistance", null, 0f, 20f, 1.25f, 10f, 10f) { Decorator = NebulaConfiguration.OddsDecorator };
            CostOfKillTrapOption = new NebulaConfiguration(RoleConfig, "costOfKillTrap", null, 1, 5, 2, 2);
            KillTrapSizeOption ??= new NebulaConfiguration(RoleConfig, "killTrapSize", null, 0.25f, 5f, 0.25f, 1f, 1f) { Decorator = NebulaConfiguration.OddsDecorator };
        }
        else
        {
            CostOfCommTrapOption = new NebulaConfiguration(RoleConfig, "costOfCommTrap", null, 1, 5, 2, 2);
            CommTrapSizeOption ??= new NebulaConfiguration(RoleConfig, "commTrapSize", null, 0.25f, 5f, 0.25f, 1f, 1f) { Decorator = NebulaConfiguration.OddsDecorator };
        }

        GenerateCommonEditor(RoleConfig, PlaceCoolDownOption, PlaceDurationOption, NumOfChargesOption, SpeedTrapSizeOption, AccelRateOption, DecelRateOption, SpeedTrapDurationOption);
    }

    public class NiceInstance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyNiceRole;
        private int leftCharge = NumOfChargesOption;
        private List<Trap> localTraps = new(), commTraps = new();

        AchievementToken<(bool cleared, int playerMask)>? acTokenChallenge = null;
        public NiceInstance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                TrapperSystem.OnActivated(this, new (int, int)[] { (0, 1), (1, 1), (2, CostOfCommTrapOption) }, localTraps);
                acTokenChallenge = new("niceTrapper.challenge", (false, 0), (val, _) => val.cleared);
            }
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
                foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
                {
                    if (p.AmOwner) continue;
                    if (p.IsDead || p.Unbox().IsInvisible) continue;
                    if (p.VanillaPlayer.transform.position.Distance(commTrap.Position) < CommTrapSizeOption.GetFloat() * 0.35f)
                    {
                        //直前にトラップを踏んでいるプレイヤーは無視する
                        commMask |= 1u << p.PlayerId;
                        if ((lastCommPlayersMask & (1u << p.PlayerId)) != 0) continue;

                        //Camo貫通(Morphingまで効果を受ける)
                        var arrow = new Arrow().SetColorByOutfit(p.Unbox().GetOutfit(75));
                        arrow.TargetPos = commTrap.Position;
                        NebulaManager.Instance.StartCoroutine(arrow.CoWaitAndDisappear(3f).WrapToIl2Cpp());

                        acTokenChallenge!.Value.playerMask |= 1 << p.PlayerId;
                        if(!acTokenChallenge.Value.cleared && NebulaGameManager.Instance.AllPlayerInfo().Count(p => (acTokenChallenge!.Value.playerMask & (1 << p.PlayerId)) != 0) >= 8)
                            acTokenChallenge.Value.cleared = true;
                    }
                }
            }

            lastCommPlayersMask = commMask;
        }
    }

    public class EvilInstance : Impostor.Impostor.Instance, RuntimeRole
    {
        public override AbstractRole Role => MyEvilRole;
        private int leftCharge = NumOfChargesOption;
        private List<Trap> localTraps = new(), killTraps = new();
        public EvilInstance(GamePlayer player) : base(player)
        {
        }

        AchievementToken<int>? acTokenChallenge = null;
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                TrapperSystem.OnActivated(this, new (int, int)[] { (0, 1), (1, 1), (3, CostOfKillTrapOption) }, localTraps);
                acTokenChallenge = new("evilTrapper.challenge", 0, (val, _) => val >= 2 && NebulaGameManager.Instance!.EndState!.Winners.Test(MyPlayer) && !MyPlayer.IsDead);
            }
        }

        [Local]
        void OnMeetingStart() => TrapperSystem.OnMeetingStart(localTraps, killTraps);

        [Local]
        void LocalUpdate(GameUpdateEvent ev)
        {
            //会議中はなにもしない
            if (MeetingHud.Instance || ExileController.Instance) return;

            if (!(PlayerControl.LocalPlayer.killTimer > 0f)) {
                killTraps.RemoveAll((killTrap) => {
                foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
                {
                    if (p.AmOwner) continue;
                    if (p.IsDead || p.VanillaPlayer.Data.Role.IsImpostor) continue;

                    if (p.VanillaPlayer.transform.position.Distance(killTrap.Position) < KillTrapSizeOption.GetFloat() * 0.35f)
                    {
                            using (RPCRouter.CreateSection("TrapKill"))
                            {
                                PlayerControl.LocalPlayer.ModKill(p.VanillaPlayer,false,PlayerState.Trapped,EventDetail.Trap);
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
