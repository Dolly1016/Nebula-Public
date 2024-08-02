using NAudio.CoreAudioApi;
using Nebula.Behaviour;
using Nebula.Modules.GUIWidget;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.DI;
using Virial.Game;
using Virial.Text;

namespace Nebula.Roles.Abilities;

public record EffectCircleInfo(string translationKey, Func<float> outerRadious, Func<float?> innerRadious, Color color);

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class MetaAbility : ComponentHolder, IGameOperator, IModule
{
    static MetaAbility() => DIManager.Instance.RegisterGeneralModule<Virial.Game.IGameModeFreePlay>(() => new MetaAbility());
    static private List<EffectCircleInfo> allEffectCircleInfo = new();
    static public void RegisterCircle(EffectCircleInfo info) => allEffectCircleInfo.Add(info);
    public MetaAbility()
    {
        this.Register(NebulaAPI.CurrentGame!);
        NebulaGameManager.Instance?.ChangeToSpectator(false);

        var roleButton = Bind(new ModAbilityButton(true, false, 100)).KeyBind(new VirtualInput(KeyCode.Z));
        roleButton.SetSprite(buttonSprite.GetSprite());
        roleButton.Availability = (button) => true;
        roleButton.Visibility = (button) => true;
        roleButton.OnClick = (button) =>
        {
            OpenRoleWindow();
        };
        roleButton.SetLabel("operate");

        var reviveButton = Bind(new ModAbilityButton(true, false, 98));
        reviveButton.SetSprite(reviveSprite.GetSprite());
        reviveButton.Availability = (button) => true;
        reviveButton.Visibility = (button) => PlayerControl.LocalPlayer.Data.IsDead;
        reviveButton.OnClick = (button) =>
        {
            NebulaManager.Instance.ScheduleDelayAction(() => NebulaGameManager.Instance?.LocalPlayerInfo.Revive(null, new(PlayerControl.LocalPlayer.transform.position), true, false));
        };
        reviveButton.SetLabel("revive");

        var suicideButton = Bind(new ModAbilityButton(true, false, 98));
        suicideButton.Availability = (button) => true;
        suicideButton.Visibility = (button) => !PlayerControl.LocalPlayer.Data.IsDead;
        suicideButton.OnClick = (button) =>
        {
            NebulaManager.Instance.ScheduleDelayAction(()=> NebulaGameManager.Instance?.LocalPlayerInfo.Suicide(PlayerState.Suicide, null, KillParameter.WithDeadBody));
        };
        suicideButton.SetLabel("suicide");

        var circleButton = Bind(new ModAbilityButton(true, false, 99));
        circleButton.SetSprite(circleButtonSprite.GetSprite());
        circleButton.Availability = (button) => true;
        circleButton.Visibility = (button) => true;
        circleButton.OnClick = (button) =>
        {
            OpenCircleWindow();
        };
        circleButton.SetLabel("show");
    }

    static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.MetaActionButton.png", 115f);
    static private Image circleButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.MetaCircleButton.png", 115f);
    static private Image reviveSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ReviveButton.png", 115f);

    static EffectCircle circle = null!;
    private void OpenCircleWindow()
    {
        var window = MetaScreen.GenerateWindow(new Vector2(4f, 3.8f), HudManager.Instance.transform, new Vector3(0, 0, -400f), true, false);
        var maskedTittleAttr = new TextAttribute(GUI.API.GetAttribute(Virial.Text.AttributeAsset.MetaRoleButton)) { Size = new(3f, 0.26f) };

        window.SetWidget(GUI.API.ScrollView(Virial.Media.GUIAlignment.Center, new(3.8f, 3.8f), "circleMenu", GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
            allEffectCircleInfo.Select(info => GUI.API.Button(Virial.Media.GUIAlignment.Center, maskedTittleAttr, GUI.API.ColorTextComponent(new(info.color), GUI.API.LocalizedTextComponent(info.translationKey)), button =>
            {
                if (circle) circle.Disappear();
                circle = EffectCircle.SpawnEffectCircle(null, NebulaGameManager.Instance!.LocalPlayerInfo.Position.ToUnityVector(), info.color, info.outerRadious.Invoke(), info.innerRadious.Invoke(), true);
                window.CloseScreen();
            }))
            ), out _), out _);
    }

    private void OpenRoleWindow()
    {
        var window = MetaScreen.GenerateWindow(new Vector2(7.5f, 4.5f), HudManager.Instance.transform, new Vector3(0, 0, -400f), true, false);

        //widget.Append(new MetaWidgetOld.Text(TextAttributeOld.TitleAttr) { RawText = Language.Translate("role.metaRole.ui.roles") });

        var roleMaskedTittleAttr = GUI.API.GetAttribute(Virial.Text.AttributeAsset.MetaRoleButton);
        var roleTittleAttr = new Virial.Text.TextAttribute(roleMaskedTittleAttr) { Font = GUI.API.GetFont(Virial.Text.FontAsset.Gothic) };
        
        void SetWidget(int tab) {

            var holder = GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Center,
                GUI.API.LocalizedButton(Virial.Media.GUIAlignment.Center, roleTittleAttr, "game.metaAbility.tabs.roles", (button) => SetWidget(0), color: tab == 0 ? Virial.Color.Yellow : null),
                GUI.API.LocalizedButton(Virial.Media.GUIAlignment.Center, roleTittleAttr, "game.metaAbility.tabs.modifiers", (button) => SetWidget(1), color: tab == 1 ? Virial.Color.Yellow : null),
                GUI.API.LocalizedButton(Virial.Media.GUIAlignment.Center, roleTittleAttr, "game.metaAbility.tabs.ghostRoles", (button) => SetWidget(2), color: tab == 2 ? Virial.Color.Yellow : null)
                );

            GUIWidget inner = GUIEmptyWidget.Default;

            if (tab == 0)
            {
                inner = GUI.API.Arrange(Virial.Media.GUIAlignment.Center, Roles.AllRoles.Select(r => GUI.API.RawButton(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, r.DisplayColoredName, button =>
                {
                    NebulaGameManager.Instance!.LocalPlayerInfo.Unbox().RpcInvokerSetRole(r, null).InvokeSingle();
                    window.CloseScreen();
                })), 4);
            }
            else if (tab == 1)
            {
                inner = GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
                    GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, "game.metaAbility.equipped"),
                    GUI.API.Arrange(Virial.Media.GUIAlignment.Center, Roles.AllModifiers.Where(r => NebulaGameManager.Instance!.LocalPlayerInfo.Unbox().AllModifiers.Any(m => m.Modifier == r)).Select(r => GUI.API.RawButton(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, r.DisplayColoredName, button =>
                    {
                        NebulaGameManager.Instance!.LocalPlayerInfo.Unbox().RpcInvokerUnsetModifier(r).InvokeSingle();
                        SetWidget(1);
                    })), 4),
                    GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, "game.metaAbility.unequipped"),
                    GUI.API.Arrange(Virial.Media.GUIAlignment.Center, Roles.AllModifiers.Where(r => !NebulaGameManager.Instance!.LocalPlayerInfo.Unbox().AllModifiers.Any(m => m.Modifier == r)).Select(r => GUI.API.RawButton(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, r.DisplayColoredName, button =>
                    {
                        NebulaGameManager.Instance!.LocalPlayerInfo.Unbox().RpcInvokerSetModifier(r, null).InvokeSingle();
                        SetWidget(1);
                    })), 4)
                    );
            }
            else if (tab == 2)
            {
                inner = GUI.API.Arrange(Virial.Media.GUIAlignment.Center, Roles.AllGhostRoles.Select(r => GUI.API.RawButton(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, r.DisplayColoredName, button =>
                {
                    NebulaGameManager.Instance!.LocalPlayerInfo.Unbox().RpcInvokerSetGhostRole(r, null).InvokeSingle();
                    window.CloseScreen();
                })), 4);
            }
            

            window.SetWidget(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center, holder, GUI.API.VerticalMargin(0.15f), GUI.API.ScrollView(Virial.Media.GUIAlignment.Center, new(7.4f, 3.5f), null, inner, out _)), out _);
        }

        SetWidget(0);
    }
}

