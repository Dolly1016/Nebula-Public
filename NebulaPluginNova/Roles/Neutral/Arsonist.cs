﻿using Nebula.Behaviour;
using Nebula.Configuration;
using Nebula.Utilities;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Nebula.Roles.Neutral;

public class Arsonist : ConfigurableStandardRole
{
    static public Arsonist MyRole = new Arsonist();
    static public Team MyTeam = new("teams.arsonist", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory RoleCategory => RoleCategory.NeutralRole;

    public override string LocalizedName => "arsonist";
    public override Color RoleColor => new Color(229f / 255f, 93f / 255f, 0f / 255f);
    public override RoleTeam Team => MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player,arguments);

    private NebulaConfiguration DouseCoolDownOption = null!;
    private NebulaConfiguration DouseDurationOption = null!;
    private new VentConfiguration VentConfiguration = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        VentConfiguration = new(RoleConfig, null, (5f, 60f, 15f), (2.5f, 30f, 10f), true);
        DouseCoolDownOption = new NebulaConfiguration(RoleConfig, "douseCoolDown", null, 2.5f, 30f, 2.5f, 10f, 10f);
        DouseDurationOption = new NebulaConfiguration(RoleConfig, "douseDuration", null, 1f, 10f, 0.5f, 3f, 3f);
    }

    public class Instance : RoleInstance
    {
        public override AbstractRole Role => MyRole;

        private Timer ventCoolDown = new Timer(MyRole.VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start();
        private Timer ventDuration = new(MyRole.VentConfiguration.Duration);
        private bool canUseVent = MyRole.VentConfiguration.CanUseVent;
        public override Timer? VentCoolDown => ventCoolDown;
        public override Timer? VentDuration => ventDuration;
        public override bool CanUseVent => canUseVent;
        private int initialDousedMask = 0;
        public Instance(PlayerModInfo player, int[] arguments) : base(player)
        {
            if (arguments.Length == 1) initialDousedMask = arguments[0];
        }

        public override int[]? GetRoleArgument()
        {
            int mask = 0;
            foreach(var icon in playerIcons.Where(icon => icon.icon.GetAlpha() > 0.8f))
            {
                mask |= 1 << icon.playerId;
            }
            return new int[] { mask };
        }


        private ModAbilityButton? douseButton = null;
        private ModAbilityButton? igniteButton = null;
        static private ISpriteLoader douseButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DouseButton.png", 115f);
        static private ISpriteLoader IgniteButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.IgniteButton.png", 115f);
        private List<(byte playerId,PoolablePlayer icon)> playerIcons = new();
        private bool canIgnite;

        private void CheckIgnitable()
        {
            canIgnite = playerIcons.All(tuple => tuple.icon.GetAlpha() > 0.8f);
        }
        
        public override bool CheckWins(CustomEndCondition endCondition, ref ulong _) => false;

        private void UpdateIcons()
        {
            for(int i=0;i<playerIcons.Count;i++)
            {
                playerIcons[i].icon.transform.localPosition = new(i * 0.29f - 0.3f, -0.1f, -i * 0.01f);
            }
        }

        public override void OnActivated()
        {
            if (AmOwner)
            {
                var IconsHolder = HudContent.InstantiateContent("ArsonistIcons", true, true, false, true);
                Bind(IconsHolder.gameObject);

                var ajust = UnityHelper.CreateObject<ScriptBehaviour>("Ajust", IconsHolder.transform, Vector3.zero);
                ajust.UpdateHandler += () =>
                {
                    if (MeetingHud.Instance)
                    {
                        ajust.transform.localScale = new(0.65f, 0.65f, 1f);
                        ajust.transform.localPosition = new(-0.45f, -0.37f, 0f);
                    }
                    else
                    {
                        ajust.transform.localScale = Vector3.one;
                        ajust.transform.localPosition = Vector3.zero;
                    }
                };
                foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
                {
                    if (p.AmOwner) continue;

                    var icon = AmongUsUtil.GetPlayerIcon(p.DefaultOutfit, ajust.transform, Vector3.zero, Vector3.one * 0.31f);
                    icon.ToggleName(false);
                    icon.SetAlpha(0.35f);
                    playerIcons.Add((p.PlayerId,icon));

                    UpdateIcons();
                }

                if (initialDousedMask != 0)
                {
                    foreach (var icon in playerIcons) if (((1 << icon.playerId) & initialDousedMask) != 0) icon.icon.SetAlpha(1f);
                    CheckIgnitable();
                }

                var douseTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, (p) => playerIcons.Any(tuple => tuple.playerId == p.PlayerId && tuple.icon.GetAlpha() < 0.8f)));

                douseButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                douseButton.SetSprite(douseButtonSprite.GetSprite());
                douseButton.Availability = (button) => MyPlayer.MyControl.CanMove && douseTracker.CurrentTarget != null;
                douseButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead && !canIgnite;
                douseButton.OnClick = (button) => {
                    button.ActivateEffect();
                };
                douseButton.OnEffectEnd = (button) =>
                {
                    if (douseTracker.CurrentTarget == null) return;

                    if (!button.EffectTimer!.IsInProcess)
                        foreach (var icon in playerIcons) if (icon.playerId == douseTracker.CurrentTarget.PlayerId) icon.icon.SetAlpha(1f);

                    CheckIgnitable();
                    douseButton.StartCoolDown();
                };
                douseButton.OnUpdate = (button) => {
                    if (!button.EffectActive) return;
                    if (douseTracker.CurrentTarget == null) button.InactivateEffect();
                };
                douseButton.CoolDownTimer = Bind(new Timer(0f, MyRole.DouseCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                douseButton.EffectTimer = Bind(new Timer(0f, MyRole.DouseDurationOption.GetFloat()));
                douseButton.SetLabel("douse");

                bool won = false;
                igniteButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                igniteButton.SetSprite(IgniteButtonSprite.GetSprite());
                igniteButton.Availability = (button) => MyPlayer.MyControl.CanMove;
                igniteButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead && canIgnite && !won;
                igniteButton.OnClick = (button) => {
                    NebulaGameManager.Instance.RpcInvokeSpecialWin(NebulaGameEnd.ArsonistWin, 1 << MyPlayer.PlayerId);
                    won = true;
                };
                igniteButton.SetLabel("ignite");
            }

        }

        public override void LocalUpdate()
        {
            UpdateIcons();
        }

        public override void OnMeetingEnd()
        {
            if (!AmOwner) return;
            playerIcons.RemoveAll(tuple =>
            {
                if (NebulaGameManager.Instance?.GetModPlayerInfo(tuple.playerId)?.IsDead ?? true)
                {
                    GameObject.Destroy(tuple.icon.gameObject);
                    return true;
                }
                return false;
            });
            CheckIgnitable();
        }
    }
}
