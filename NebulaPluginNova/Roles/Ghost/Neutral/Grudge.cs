using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial;
using Virial.Game;
using Virial.Events.Game;
using Steamworks;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;

namespace Nebula.Roles.Ghost.Neutral;

public class Grudge : DefinedGhostRoleTemplate, DefinedGhostRole
{
    public Grudge() : base("grudge", new(154, 147, 80), RoleCategory.NeutralRole, [TotalStandingTimeToWin]) {
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Grudge.png");
    }

    string ICodeName.CodeName => "GRD";

    static private readonly FloatConfiguration TotalStandingTimeToWin = NebulaAPI.Configurations.Configuration("options.role.grudge.totalStandingTimeToWin", (15f, 120f, 2.5f), 30f, FloatConfigurationDecorator.Second);

    static public readonly Grudge MyRole = new();
    RuntimeGhostRole RuntimeAssignableGenerator<RuntimeGhostRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    [NebulaRPCHolder]
    public class GrudgeIllusion : FlexibleLifespan, IGameOperator
    {
        PoolablePlayer player;
        SpriteRenderer[] allRenderers;
        public bool IsActive { get; set; } = true;
        private float Alpha { get; set; } = 0f;

        public GrudgeIllusion(GamePlayer player, Vector3 position, bool flipX) {
            this.player = AmongUsUtil.GetPlayerIcon(player.DefaultOutfit.outfit, null, position, new(0.35f, 0.35f, 1f), flipX, false);
            UnityHelper.DoForAllChildren(this.player.gameObject, obj => obj.layer = LayerExpansion.GetPlayersLayer());
            allRenderers = this.player.gameObject.GetComponentsInChildren<SpriteRenderer>();
            SetAlpha(0f);
        }

        private void SetAlpha(float a)
        {
            Color color = new(1f, 1f, 1f, a);
            foreach(var renderer in allRenderers) renderer.color = color;
        }

        void Update(GameUpdateEvent ev)
        {
            if (IsDeadObject) return;

            if (IsActive)
            {
                Alpha += Time.deltaTime * 0.6f;
                if (Alpha > 1f) Alpha = 1f;
            }
            else
            {
                Alpha -= Time.deltaTime * 2.5f;
                if (Alpha < 0f)
                {
                    Alpha = 0f;
                    Release();
                }
            }

            //死者目線では幻影は見えない
            SetAlpha((GamePlayer.LocalPlayer?.IsDead ?? true) ? 0f : Alpha);
        }

        void IGameOperator.OnReleased()
        {
            if (player) GameObject.Destroy(player.gameObject);
        }
    }

    public class Instance : RuntimeAssignableTemplate, RuntimeGhostRole
    {
        DefinedGhostRole RuntimeGhostRole.Role => MyRole;

        public Instance(GamePlayer player) : base(player) { }

        void RuntimeAssignable.OnActivated() {}

        GrudgeIllusion? currentIllusion = null;
        float standingTime = 0f;
        float standingCoolDown = 10f;
        float progress = 0f;
        bool canWin = false;
        float bored = 0f;
        StaticAchievementToken? acTokenCommon1 = null;
        StaticAchievementToken? acTokenCommon2 = null;
        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            standingCoolDown = 5f;
        }

        [Local]
        void Update(GameUpdateEvent ev)
        {
            if (standingCoolDown > 0f)
            {
                if (!MeetingHud.Instance && !ExileController.Instance)
                {
                    standingCoolDown -= Time.deltaTime;
                }
                standingTime = 0f;
            }
            else
            {
                if (MyPlayer.IsDead && !MeetingHud.Instance && !ExileController.Instance && MyPlayer.VanillaPlayer.MyPhysics.Velocity.magnitude < 0.001f)
                {
                    standingTime += Time.deltaTime;
                }
                else
                {
                    standingTime = 0f;
                }
            }

            if(standingTime > 0.6f && currentIllusion == null)
            {
                RpcShowIllusion.Invoke((MyPlayer, MyPlayer.VanillaPlayer.transform.position, MyPlayer.VanillaPlayer.cosmetics.FlipX));
            }
            if(!(standingTime > 0f) && currentIllusion != null)
            {
                RpcDisappearIllusion.Invoke(MyPlayer);
            }

            var myPos = MyPlayer.Position.ToUnityVector();
            if(!canWin && standingTime > 0.8f)
            {
                if (NebulaGameManager.Instance!.AllPlayerInfo.Any(p => !p.IsDead && p.VanillaPlayer.transform.position.Distance(myPos) < 1.5f))
                {
                    progress += Time.deltaTime;
                    if (TotalStandingTimeToWin < progress)
                    {
                        RpcShareCanWin.Invoke(MyPlayer);
                        new StaticAchievementToken("grudge.common3");
                    }

                    if (!(bored > 0f)) acTokenCommon1 ??= new("grudge.common1");
                    if (bored > 5f) acTokenCommon2 ??= new("grudge.common2");
                }
                else
                {
                    bored += Time.deltaTime;
                }
            }
            else
            {
                bored = 0f;
            }
        }

        void RuntimeAssignable.OnInactivated()
        {
            if (currentIllusion != null) currentIllusion.IsActive = false;
        }

        [Local]
        void UpdateTaskText(PlayerTaskTextLocalEvent ev)
        {
            string text = Language.Translate("role.grudge.taskText");
            if (canWin)
            {
                text = text.Color(Color.green);
            }
            else
            {
                text += string.Format(" {0:0.#}", progress) + "s/" + TotalStandingTimeToWin.GetValue() + "s";
                Color color = progress > 0f ? Color.yellow : Color.white;
                if (standingTime < 0.8f) color = color.RGBMultiplied(0.5f);
                text = text.Color(color);
            }
            ev.AppendText(text);
        }

        [OnlyMyPlayer]
        void CheckExtraWin(PlayerCheckExtraWinEvent ev)
        {
            if (ev.Phase != ExtraWinCheckPhase.GrudgePhase) return;

            if (canWin)
            {
                ev.SetWin(true);
                ev.ExtraWinMask.Add(NebulaGameEnd.ExtraGrudgeWin);
            }
        }
        
        static private readonly RemoteProcess<(GamePlayer player, Vector3 pos, bool flipX)> RpcShowIllusion = new("ShowGrudgeIllusion",
            (message, _) =>
            {
                var grudge = message.player.Unbox().GhostRole as Grudge.Instance;
                if (grudge != null)
                {
                    if(grudge.currentIllusion != null) grudge.currentIllusion.IsActive = false;
                    grudge.currentIllusion = new GrudgeIllusion(message.player, message.pos, message.flipX);
                    grudge.currentIllusion.Register(grudge);
                }
            });

        static private readonly RemoteProcess<GamePlayer> RpcDisappearIllusion = new("DisappearIllusion",
            (message, _) =>
            {
                var grudge = message.Unbox().GhostRole as Grudge.Instance;
                if (grudge != null && grudge.currentIllusion != null)
                {
                    grudge.currentIllusion.IsActive = false;
                    grudge.currentIllusion = null;
                }
            });

        RemoteProcess<GamePlayer> RpcShareCanWin = new("ShareGrudgeCanWin",
            (message, _) =>
            {
                var grudge = message.Unbox().GhostRole as Grudge.Instance;
                if (grudge != null) grudge.canWin = true;
            });
    }
}
