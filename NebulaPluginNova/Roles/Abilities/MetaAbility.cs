using Cpp2IL.Core.Extensions;
using NAudio.CoreAudioApi;
using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using Nebula.Roles.Neutral;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.DI;
using Virial.Game;
using Virial.Text;

namespace Nebula.Roles.Abilities;

public record EffectCircleInfo(string translationKey, Func<float> outerRadious, Func<float?> innerRadious, Color color);

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class MetaAbility : DependentLifespan, IGameOperator, IModule
{
    static MetaAbility() => DIManager.Instance.RegisterGeneralModule<Virial.Game.IGameModeFreePlay>(() => new MetaAbility().Register(NebulaAPI.CurrentGame!));
    static private List<EffectCircleInfo> allEffectCircleInfo = new();
    static public void RegisterCircle(EffectCircleInfo info) => allEffectCircleInfo.Add(info);
    public MetaAbility()
    {
        NebulaGameManager.Instance?.ChangeToSpectator(false);

        var roleButton = NebulaAPI.Modules.AbilityButton(this, true, false, 100)
            .BindKey(Virial.Compat.VirtualKeyInput.FreeplayAction)
            .SetImage(buttonSprite).SetLabel("operate");
        roleButton.Availability = (button) => true;
        roleButton.Visibility = (button) => true;
        roleButton.OnClick = (button) => OpenRoleWindow();

        var reviveButton = NebulaAPI.Modules.AbilityButton(this, true, false, 98)
            .SetImage(reviveSprite).SetLabel("revive");
        reviveButton.Availability = (button) => true;
        reviveButton.Visibility = (button) => PlayerControl.LocalPlayer.Data.IsDead;
        reviveButton.OnClick = (button) => NebulaManager.Instance.ScheduleDelayAction(() => GamePlayer.LocalPlayer!.Revive(null, new(PlayerControl.LocalPlayer.transform.position), true, false));


        var suicideButton = NebulaAPI.Modules.AbilityButton(this, true, false, 98)
            .SetLabel("suicide");
        suicideButton.Availability = (button) => true;
        suicideButton.Visibility = (button) => !PlayerControl.LocalPlayer.Data.IsDead;
        suicideButton.OnClick = (button) => NebulaManager.Instance.ScheduleDelayAction(()=> GamePlayer.LocalPlayer!.Suicide(PlayerState.Suicide, null, KillParameter.WithDeadBody));
        

        var circleButton = NebulaAPI.Modules.AbilityButton(this, true, false, 99)
            .SetImage(circleButtonSprite).SetLabel("show");
        circleButton.Availability = (button) => true;
        circleButton.Visibility = (button) => true;
        circleButton.OnClick = (button) => OpenCircleWindow();

        if (DebugTools.DebugMode) new DebugAbility().Register(this);
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
                circle = EffectCircle.SpawnEffectCircle(null, GamePlayer.LocalPlayer.Position.ToUnityVector(), info.color, info.outerRadious.Invoke(), info.innerRadious.Invoke(), true);
                window.CloseScreen();
            }))
            ), out _), out _);
    }

    private void OpenRoleWindow()
    {
        var window = MetaScreen.GenerateWindow(new Vector2(7.5f, 4.5f), HudManager.Instance.transform, new Vector3(0, 0, -400f), true, false);

        //widget.Append(new MetaWidgetOld.Text(TextAttributeOld.TitleAttr) { RawText = Language.Translate("role.metaRole.ui.roles") });

        var roleMaskedTittleAttr = GUI.API.GetAttribute(Virial.Text.AttributeAsset.MetaRoleButton);
        var roleTitleAttr = new Virial.Text.TextAttribute(roleMaskedTittleAttr) { Font = GUI.API.GetFont(Virial.Text.FontAsset.Gothic), Size = new(1.1f, 0.22f) };
        
        void SetWidget(int tab) {

            var holder = GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Center,
                GUI.API.LocalizedButton(Virial.Media.GUIAlignment.Center, roleTitleAttr, "game.metaAbility.tabs.roles", (button) => SetWidget(0), color: tab == 0 ? Virial.Color.Yellow : null),
                GUI.API.LocalizedButton(Virial.Media.GUIAlignment.Center, roleTitleAttr, "game.metaAbility.tabs.modifiers", (button) => SetWidget(1), color: tab == 1 ? Virial.Color.Yellow : null),
                GUI.API.LocalizedButton(Virial.Media.GUIAlignment.Center, roleTitleAttr, "game.metaAbility.tabs.ghostRoles", (button) => SetWidget(2), color: tab == 2 ? Virial.Color.Yellow : null),
                GUI.API.LocalizedButton(Virial.Media.GUIAlignment.Center, roleTitleAttr, "game.metaAbility.tabs.perks", (button) => SetWidget(3), color: tab == 3 ? Virial.Color.Yellow : null)
                );

            GUIWidget inner = GUIEmptyWidget.Default;

            if (tab == 0)
            {
                inner = GUI.API.Arrange(Virial.Media.GUIAlignment.Center, Roles.AllRoles.Where(r => r.ShowOnFreeplayScreen).Select(r => GUI.API.RawButton(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, r.DisplayColoredName, button =>
                {
                    GamePlayer.LocalPlayer.Unbox().RpcInvokerSetRole(r, null).InvokeSingle();
                    window.CloseScreen();
                })).Concat(Roles.AllRoles.Where(r => r.IsJackalizable).Select(r => GUI.API.RawButton(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, r.DisplayName.Color(Jackal.MyRole.UnityColor), button =>
                {
                    GamePlayer.LocalPlayer.Unbox().RpcInvokerSetRole(Jackal.MyRole, Jackal.GenerateArgument(0, r)).InvokeSingle();
                    window.CloseScreen();
                }))), 4);
            }
            else if (tab == 1)
            {
                inner = GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
                    GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, "game.metaAbility.equipped"),
                    GUI.API.Arrange(Virial.Media.GUIAlignment.Center, Roles.AllModifiers.Where(r => GamePlayer.LocalPlayer.Unbox().AllModifiers.Any(m => m.Modifier == r)).Select(r => GUI.API.RawButton(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, r.DisplayColoredName, button =>
                    {
                        GamePlayer.LocalPlayer.Unbox().RpcInvokerUnsetModifier(r).InvokeSingle();
                        SetWidget(1);
                    })), 4),
                    GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, "game.metaAbility.unequipped"),
                    GUI.API.Arrange(Virial.Media.GUIAlignment.Center, Roles.AllModifiers.Where(r => r.ShowOnFreeplayScreen && !GamePlayer.LocalPlayer.Unbox().AllModifiers.Any(m => m.Modifier == r)).Select(r => GUI.API.RawButton(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, r.DisplayColoredName, button =>
                    {
                        GamePlayer.LocalPlayer.Unbox().RpcInvokerSetModifier(r, null).InvokeSingle();
                        SetWidget(1);
                    })), 4)
                    );
            }
            else if (tab == 2)
            {
                inner = GUI.API.Arrange(Virial.Media.GUIAlignment.Center, Roles.AllGhostRoles.Where(r => r.ShowOnFreeplayScreen).Select(r => GUI.API.RawButton(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, r.DisplayColoredName, button =>
                {
                    GamePlayer.LocalPlayer.Unbox().RpcInvokerSetGhostRole(r, null).InvokeSingle();
                    window.CloseScreen();
                })), 4);
            }
            else if (tab == 3)
            {
                Virial.Media.GUIWidget GetPerksWidget(IEnumerable<PerkFunctionalDefinition> perks) => GUI.API.Arrange(Virial.Media.GUIAlignment.Center, perks.Select(p => p.PerkDefinition.GetPerkImageWidget(true, 
                    () => {
                        ModSingleton<ItemSupplierManager>.Instance?.SetPerk(p);
                        window.CloseScreen();
                    },
                    ()=> p.PerkDefinition.GetPerkWidget())), 8);

                inner = GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
                    GUI.API.HorizontalMargin(7.4f),
                    GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, "game.metaAbility.perks.standard"),
                    GetPerksWidget(Roles.AllPerks.Where(p => p.PerkCategory == PerkFunctionalDefinition.Category.Standard)),
                    GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, roleMaskedTittleAttr, "game.metaAbility.perks.noncrewmateOnly"),
                    GetPerksWidget(Roles.AllPerks.Where(p => p.PerkCategory == PerkFunctionalDefinition.Category.NoncrewmateOnly))
                    );
            }


            window.SetWidget(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center, holder, GUI.API.VerticalMargin(0.15f), GUI.API.ScrollView(Virial.Media.GUIAlignment.Center, new(7.4f, 3.5f), null, inner, out _)), out _);
        }

        SetWidget(0);
    }
}

