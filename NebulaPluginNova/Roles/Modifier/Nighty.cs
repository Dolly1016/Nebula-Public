using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Game;
using Virial.Text;

namespace Nebula.Roles.Modifier;

internal class Nighty : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier, HasCitation
{
    private Nighty() : base("nighty", "NHT", new(Palette.ImpostorRed), [PlaceCooldownOption, MineSizeOption, MineDurationOption, BlindDurationOption], allocateToCrewmate: false, allocateToNeutral: false)
    {
        GameActionTypes.BlindTrapPlaceAction = new("nighty.place", this, isPlacementAction: true);
    }
    Citation? HasCitation.Citation => Citations.TownOfImpostors;

    static private FloatConfiguration PlaceCooldownOption = NebulaAPI.Configurations.Configuration("options.role.nighty.placeCooldown", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration MineDurationOption = NebulaAPI.Configurations.Configuration("options.role.nighty.mineDuration", (10f, 120f, 10f), 30f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration MineSizeOption = NebulaAPI.Configurations.Configuration("options.role.nighty.mineSize", (0.25f, 2f, 0.25f), 0.5f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration BlindDurationOption = NebulaAPI.Configurations.Configuration("options.role.nighty.blindDuration", (2.5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);

    static public Nighty MyRole = new Nighty();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        private int usedBombCounter = 0;

        static private readonly Image placeButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BlindTrapButton.png", 115f);

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var placeButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, false, true, Virial.Compat.VirtualKeyInput.SidekickAction, null,
                    PlaceCooldownOption, "place", placeButtonSprite, null);
                placeButton.OnClick = (button) =>
                {
                    new StaticAchievementToken("nighty.common1");
                    var pos = PlayerControl.LocalPlayer.GetTruePosition();
                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, pos, GameActionTypes.BlindTrapPlaceAction);
                    var objRef = NebulaSyncObject.RpcInstantiate(NightyBomb.MyTag, new float[] { pos.x, pos.y });
                    NebulaManager.Instance.StartDelayAction(MineDurationOption, () =>
                    {
                        if (!(objRef.SyncObject?.IsDeadObject ?? true)) NebulaSyncObject.RpcDestroy(objRef.ObjectId);
                    });
                    placeButton.StartCoolDown();
                };
            }
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            if (AmOwner || canSeeAllInfo) name += MyRole.GetRoleIconTag();
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if(ev.EndState.EndCondition == NebulaGameEnd.ImpostorWin && usedBombCounter >= 8 && GamePlayer.AllPlayers.Count(p => p.IsImpostor) >= 2 && GamePlayer.AllPlayers.All(p => !p.IsImpostor || p.IsAlive) && ev.EndState.Winners.Test(MyPlayer))
            {
                HashSet<CommunicableTextTag> states = [];
                foreach(var p in GamePlayer.AllPlayers)
                {
                    var state = p.PlayerState;
                    if (state == PlayerState.Alive) continue;
                    if (state == PlayerState.Disconnected) continue;
                    if (state == PlayerState.Revived) continue;
                    states.Add(state);
                }
                if(states.Count >= 8) new StaticAchievementToken("nighty.challenge");
            }
        }

        static internal readonly RemoteProcess<GamePlayer> RpcNoticeBomb = new("nightyNoticeBomb", (player, _) =>
        {
            if (player.AmOwner && player.TryGetModifier<Instance>(out var nighty)) nighty.usedBombCounter++;
        });
    }


    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class NightyBomb : NebulaSyncStandardObject
    {
        public const string MyTag = "NightyBomb";
        private static readonly Image bombImage = SpriteLoader.FromResource("Nebula.Resources.BlindTrap.png", 100f);
        public NightyBomb(Vector2 pos) : base(pos, ZOption.Back, true, bombImage.GetSprite()) {}

        public override void OnInstantiated()
        {
            base.OnInstantiated();
            if (!Owner.AmOwner && !(NebulaGameManager.Instance?.CanSeeAllInfo ?? false)) Color = Color.clear;
        }

        void OnUpdate(GameUpdateEvent ev)
        {
            if (MeetingHud.Instance || ExileController.Instance) return;
            if (Owner.AmOwner) return;
            if (GamePlayer.LocalPlayer?.IsDead ?? true) return;
            if (GamePlayer.LocalPlayer.IsImpostor) return;

            if(Position.Distance(GamePlayer.LocalPlayer.TruePosition) < MineSizeOption)
            {
                GetBlind();
                Instance.RpcNoticeBomb.Invoke(Owner);
                NebulaSyncObject.RpcDestroy(this.ObjectId);
            }
        }

        static private void GetBlind()
        {
            float speedMul = 3.5f;
            GameOperatorManager.Instance?.Subscribe<LightRangeUpdateEvent>(ev =>
            {
                speedMul -= Time.deltaTime;
                if (speedMul > 1f) ev.LightSpeed *= speedMul;
            }, FunctionalLifespan.GetTimeLifespan(BlindDurationOption));
            GamePlayer.LocalPlayer?.GainAttribute(PlayerAttributes.Eyesight, BlindDurationOption, 0.1f, false, 0);
        }

        static NightyBomb()
        {
            NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new NightyBomb(new(args[0], args[1])));
        }
    }

}
