using Il2CppInterop.Runtime.Injection;
using Nebula.Behavior;
using Nebula.Map;
using Nebula.Modules.Cosmetics;
using Nebula.Modules.GUIWidget;
using Nebula.Roles.Abilities;
using Nebula.Roles.MapLayer;
using Nebula.Roles.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Game.Minimap;
using Virial.Events.Player;
using Virial.Game;
using static Nebula.Roles.Crewmate.Doppelganger;
using static Nebula.Roles.Impostor.Disturber;

namespace Nebula.Roles.Impostor;

internal class Graffiti : DefinedSingleAbilityRoleTemplate<Graffiti.Ability>, DefinedRole, IAssignableDocument
{
    public Graffiti() : base("graffiti", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [NumOfDrawingOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.Get(1, NumOfDrawingOption), arguments.GetAsBool(0));

    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.KillersSide;

    static private readonly IntegerConfiguration NumOfDrawingOption = NebulaAPI.Configurations.Configuration("options.role.graffiti.numOfDrawings", (int[])[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 15, 20, 25, 30, 40], 3);
    static public readonly Graffiti MyRole = new();
    static private readonly GameStatsEntry StatsGraffiti = NebulaAPI.CreateStatsEntry("stats.graffiti.drawing", GameStatsCategory.Roles, MyRole);

    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(buttonSprite, "role.graffiti.ability.draw");
    }

    public class GraffitiMapLayer : FakePlayerMapLayer
    {
        static GraffitiMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<GraffitiMapLayer>();

        private Ability myAbility;
        public void InjectAbility(Ability ability) => myAbility = ability;

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void OnClick(Vector2 worldPos, Vector2 minimapPos)
        {
            var canvas = DyingMessages.GenerateCanvas(worldPos, Modifier.DyingMessage.MessageDuration, MyRole, texture => myAbility.ConsumeDrawingToken());
            myAbility.StartDrawing(canvas);
            MapBehaviour.Instance.Close();
        }
    }
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.AsUniqueMapAbility;

    static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.GraffitiButton.png", 115f);
    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int leftDrawing;
        ModAbilityButton? button;
        DyingMessageCanvas? currentCanvas;
        static private readonly RoleRPC.Definition UpdateState = RoleRPC.Get<Ability>("graffiti.draw", (ability, num, calledByMe) => ability.leftDrawing = num);

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt(), leftDrawing];
        public Ability(GamePlayer player, int leftDrawing, bool isUsurped) : base(player, isUsurped)
        {
            this.leftDrawing = leftDrawing;
            if (AmOwner)
            {
                button = NebulaAPI.Modules.AbilityButton(this, alwaysShow: true);
                button.BindKey(Virial.Compat.VirtualKeyInput.Ability);
                button.SetLabel("draw");
                button.SetImage(buttonSprite);
                button.Visibility = _ => !MyPlayer.IsDead && (currentCanvas == null || !currentCanvas) && !AmongUsUtil.MapIsOpen && !ExileController.Instance;
                button.Availability = _ => (MeetingHud.Instance.AsBoolFast() || MyPlayer.CanMove) && this.leftDrawing > 0;
                button.OnClick = _ =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        HudManager.Instance.InitMap();
                        MapBehaviour.Instance.ShowNormalMap();
                        MapBehaviour.Instance.taskOverlay.gameObject.SetActive(false);
                    });
                };
                button.ShowUsesIcon(0, leftDrawing.ToString());
                button.SetAsUsurpableButton(this);

                //チャレンジ称号
                bool dyingMessageGenerated = false;
                GameOperatorManager.Instance?.Subscribe<DyingMessageGenerateEvent>(ev => dyingMessageGenerated |= ev.RelatedAssignable == DyingMessage.MyRole, this);
                GameOperatorManager.Instance?.Subscribe<GameEndEvent>(ev =>
                {
                    var players = GamePlayer.AllPlayers.Where(p => p == MyPlayer || p.IsSameSideOf(MyPlayer));
                    if (dyingMessageGenerated && ev.EndState.Winners.Test(MyPlayer) && players.Count() >= 2 && players.All(p => p.IsAlive)) new StaticAchievementToken("graffiti.challenge");
                }, this);
            }
        }

        internal void ConsumeDrawingToken()
        {
            var nextNum = leftDrawing - 1;
            UpdateState.RpcSync(MyPlayer, nextNum);
            button?.UpdateUsesIcon(nextNum.ToString());
            StatsGraffiti.Progress();
            new StaticAchievementToken("graffiti.common1");
        }
        internal void StartDrawing(DyingMessageCanvas canvas) => currentCanvas = canvas;

        private GraffitiMapLayer mapLayer = null!;
        [Local]
        void OnOpenMap(AbstractMapOpenEvent ev)
        {
            if (ev is MapOpenNormalEvent && !IsUsurped)
            {
                if (!mapLayer.AsBoolFast())
                {
                    mapLayer = UnityHelper.CreateObject<GraffitiMapLayer>("GraffitiLayer", MapBehaviour.Instance.transform, new(0, 0, -1f));
                    this.BindGameObject(mapLayer.gameObject);
                    mapLayer.InjectAbility(this);
                }
                mapLayer.gameObject.SetActive(true);
            }
            else
            {
                if (mapLayer.AsBoolFast()) mapLayer.gameObject.SetActive(false);
            }
        }
    }
}