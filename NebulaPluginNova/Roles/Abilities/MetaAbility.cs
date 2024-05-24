using Virial;
using Virial.Assignable;
using Virial.DI;
using Virial.Game;

namespace Nebula.Roles.Abilities;



public class MetaAbility : ComponentHolder, IGameOperator, IModule
{
    public MetaAbility()
    {
        this.Register(NebulaAPI.CurrentGame!);

        var roleButton = Bind(new ModAbilityButton(true, false, 100)).KeyBind(new VirtualInput(KeyCode.Z));
        roleButton.SetSprite(buttonSprite.GetSprite());
        roleButton.Availability = (button) => true;
        roleButton.Visibility = (button) => true;
        roleButton.OnClick = (button) =>
        {
            OpenRoleWindow();
        };
        roleButton.SetLabel("operate");
    }

    static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.MetaActionButton.png", 115f);

    private void OpenRoleWindow()
    {
        var window = MetaScreen.GenerateWindow(new Vector2(7.5f, 4.5f), HudManager.Instance.transform, new Vector3(0, 0, -400f), true, false);

        MetaWidgetOld widget = new MetaWidgetOld();

        widget.Append(new MetaWidgetOld.Text(TextAttributeOld.TitleAttr) { RawText = Language.Translate("role.metaRole.ui.roles") });

        var roleTitleAttr = new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new Vector2(1.4f, 0.26f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };
        MetaWidgetOld scrollInnner = new();
        MetaWidgetOld.ScrollView scrollView = new(new(7.4f, 4f), scrollInnner);
        scrollInnner.Append(Roles.AllRoles, (role) => new MetaWidgetOld.Button(() =>
        {
            NebulaGameManager.Instance.LocalPlayerInfo.Unbox().RpcInvokerSetRole(role, null).InvokeSingle();
            window.CloseScreen();
        }, roleTitleAttr)
        {
            RawText = role.DisplayColordName,
            PostBuilder = (button, renderer, text) => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask,
            Alignment = IMetaWidgetOld.AlignmentOption.Center
        }, 4, -1, 0, 0.6f);
        widget.Append(scrollView);

        window.SetWidget(widget);
    }
}

