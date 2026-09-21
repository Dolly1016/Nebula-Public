using Virial.Assignable;
using Virial.Configuration;
using Virial;
using Virial.Game;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Runtime;
using Virial.Text;

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

        public GrudgeIllusion(GamePlayer player, VVector2 position, bool flipX) {
            this.player = AmongUsUtil.GetPlayerIcon(player.DefaultOutfit.outfit, null, position.AsUnityVector3(position.y / 1000f), new(0.35f, 0.35f, 0.001f), flipX, false);
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

    [NebulaRPCHolder]
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
                if (!MeetingHud.Instance.AsBoolFast() && !ExileController.Instance.AsBoolFast())
                {
                    standingCoolDown -= ev.DeltaTime;
                }
                standingTime = 0f;
            }
            else
            {
                if (MyPlayer.IsDead && !MeetingHud.Instance.AsBoolFast() && !ExileController.Instance.AsBoolFast() && MyPlayer.VanillaPhysics.Velocity.magnitude < 0.001f)
                {
                    standingTime += ev.DeltaTime;
                }
                else
                {
                    standingTime = 0f;
                }
            }

            if(standingTime > 0.6f && currentIllusion == null)
            {
                RpcShowIllusion.Invoke((MyPlayer, MyPlayer.Position, MyPlayer.VanillaCosmetics.FlipX));
            }
            if(!(standingTime > 0f) && currentIllusion != null)
            {
                RpcDisappearIllusion.Invoke(MyPlayer);
            }

            var myPos = MyPlayer.Position;
            if(!canWin && standingTime > 0.8f)
            {
                if (GamePlayer.AllPlayers.Any(p => !p.IsDead && p.Position.Distance(myPos) < 1.5f))
                {
                    progress += ev.DeltaTime;
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
                    bored += ev.DeltaTime;
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
                text = text.Color(VColor.Green);
            }
            else
            {
                text += string.Format(" {0:0.#}", progress) + "s/" + TotalStandingTimeToWin.GetValue() + "s";
                VColor color = progress > 0f ? VColor.Yellow : VColor.White;
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
                if (ev.SetWin(true)) ev.Recorder.AddReason(ev.Player.PlayerId, GrudgeDetails.Extra);
                ev.ExtraWinMask.Add(NebulaGameEnd.ExtraGrudgeWin);
            }
        }
        
        static private readonly RemoteProcess<(GamePlayer player, VVector2 pos, bool flipX)> RpcShowIllusion = new("ShowGrudgeIllusion",
            (message, _) =>
            {
                var grudge = message.player.GhostRole as Grudge.Instance;
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
                var grudge = message.GhostRole as Grudge.Instance;
                if (grudge != null && grudge.currentIllusion != null)
                {
                    grudge.currentIllusion.IsActive = false;
                    grudge.currentIllusion = null;
                }
            });

        RemoteProcess<GamePlayer> RpcShareCanWin = new("ShareGrudgeCanWin",
            (message, _) =>
            {
                var grudge = message.GhostRole as Grudge.Instance;
                if (grudge != null) grudge.canWin = true;
            });
    }
}

[NebulaPreprocess(PreprocessPhase.PostRoles)]
file static class GrudgeDetails
{
    static internal CommunicableTextTag Extra = null!;

    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        Extra = preprocessor.RegisterCommunicableText("end.detail.grudge.extra");
    }
}
