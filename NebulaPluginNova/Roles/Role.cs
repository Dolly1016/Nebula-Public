using AmongUs.GameOptions;
using Nebula.Compat;
using Nebula.Roles.Crewmate;
using Virial.Assignable;
using Virial.Configuration;

namespace Nebula.Roles;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class NebulaRoleHolder : Attribute
{

}

public enum RoleType
{
    Role,
    Modifier,
    GhostRole,
}

public abstract class AbstractRole : IAssignableBase, DefinedRole
{
    public virtual bool IsDefaultRole { get => false; }
    public int Id { get; set; }

    //追加付与ロールに役職プールの占有性があるか(追加付与ロールが無い場合、無意味)
    public virtual bool AdditionalRolesConsumeRolePool { get => true; }
    public virtual AbstractRole[]? AdditionalRole { get => null; }

    public NebulaConfiguration? CanBeGuessOption { get; set; } = null;

    //For Config
    public virtual NebulaModifierFilterConfigEntry? ModifierFilter { get => null; }
    public virtual NebulaGhostRoleFilterConfigEntry? GhostRoleFilter { get => null; }

    public virtual IEnumerable<IAssignableBase> RelatedOnConfig() { yield break; }
    public virtual ConfigurationHolder? RelatedConfig { get => null; }

    public virtual bool CanBeGuessDefault { get => true; }
    public virtual bool CanBeGuess => CanBeGuessDefault && (CanBeGuessOption?.GetBool() ?? true);
    public virtual bool IsSpawnable { get => true; }

    


    public abstract void Load();

    public AbstractRole()
    {
        Roles.Register(this);
    }
}

public abstract class ConfigurableRole : AbstractRole, IConfiguableAssignable {
    public ConfigurationHolder RoleConfig { get; private set; } = null!;
    public override ConfigurationHolder? RelatedConfig { get => RoleConfig; }

    private int? myTabMask;
    public ConfigurableRole(int TabMask)
    {
        myTabMask = TabMask;
    }

    public ConfigurableRole() {
    }

    protected virtual void LoadOptions() { }

    public override sealed void Load()
    {
        DefinedRole me = this;
        RoleConfig = new ConfigurationHolder("options.role." + me.InternalName, new ColorTextComponent(me.Color.ToUnityColor(), new TranslateTextComponent("role." + me.LocalizedName + ".name")), myTabMask ?? ConfigurationTab.FromRoleCategory(me.Category), CustomGameMode.AllGameModeMask);
        RoleConfig.Priority = IsDefaultRole ? 0 : 1;
        RoleConfig.RelatedAssignable = this;
        LoadOptions();
    }


    public class VentConfiguration
    {
        private bool isOptional = false;
        private NebulaConfiguration selectionOption;
        private NebulaConfiguration? coolDownOption, durationOption, usesOption;
        public VentConfiguration(ConfigurationHolder holder, (int min, int max, int defaultValue)? ventUses, (float min, float max, float defaultValue)? ventCoolDown, (float min, float max, float defaultValue)? ventDuration,bool optional = false)
        {
            isOptional = optional;
            selectionOption = new NebulaConfiguration(holder, "ventOption", new TranslateTextComponent("role.general.ventOption"), false, false);

            coolDownOption = durationOption = usesOption = null;

            List<IMetaParallelPlacableOld> list = new();
            if (isOptional)
                list.Add(new MetaWidgetOld.Button(() =>
                {
                    selectionOption.ChangeValue(true);
                    if (NebulaSettingMenu.Instance) NebulaSettingMenu.Instance.UpdateSecondaryPage();
                }, new(TextAttributeOld.BoldAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial, Size = new(1.4f, 0.3f) })
                { Text = selectionOption.Title });
            else
                list.Add(new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(1.4f, TMPro.TextAlignmentOptions.Left)) { MyText = selectionOption.Title });
            list.Add(NebulaConfiguration.OptionTextColon);

            void AddOptionToEditor(NebulaConfiguration config)
            {
                list.Add(new MetaWidgetOld.HorizonalMargin(0.1f));
                list.Add(new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(1.1f)) { MyText = config!.Title });
                list.Add(NebulaConfiguration.OptionButtonWidget(() => config.ChangeValue(false), "<<"));
                list.Add(new MetaWidgetOld.Text(NebulaConfiguration.OptionShortValueAttr) { MyText = new LazyTextComponent(() => config.ToDisplayString()) });
                list.Add(NebulaConfiguration.OptionButtonWidget(() => config.ChangeValue(true), ">>"));
            }

            if (ventCoolDown.HasValue)
            {
                coolDownOption = new NebulaConfiguration(holder, "ventCoolDown", new TranslateTextComponent("role.general.ventCoolDown"), ventCoolDown.Value.min, ventCoolDown.Value.max, 2.5f, ventCoolDown.Value.defaultValue, ventCoolDown.Value.defaultValue) { Editor = NebulaConfiguration.EmptyEditor, Decorator = NebulaConfiguration.SecDecorator };
                coolDownOption.Shower = null;
                AddOptionToEditor(coolDownOption);
            }
            if (ventDuration.HasValue)
            {
                durationOption = new NebulaConfiguration(holder, "ventDuration", new TranslateTextComponent("role.general.ventDuration"), ventDuration.Value.min, ventDuration.Value.max, 2.5f, ventDuration.Value.defaultValue, ventDuration.Value.defaultValue) { Editor = NebulaConfiguration.EmptyEditor, Decorator = NebulaConfiguration.SecDecorator };
                durationOption.Shower = null;
                AddOptionToEditor(durationOption);
            }
            if (ventUses.HasValue)
            {
                usesOption = new NebulaConfiguration(holder, "ventUses", new TranslateTextComponent("role.general.ventUses"), ventUses.Value.min, ventUses.Value.max, ventUses.Value.defaultValue, ventUses.Value.defaultValue) { Editor = NebulaConfiguration.EmptyEditor };
                usesOption.Shower = null;
                AddOptionToEditor(usesOption);
            }

            selectionOption.Editor = () =>
            {
                MetaWidgetOld widget = new();
                if (isOptional && !selectionOption.GetBool())
                    widget.Append(new CombinedWidgetOld(
                        new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(1.8f)) { TranslationKey = "role.general.canUseVent" },
                        NebulaConfiguration.OptionTextColon,
                        new MetaWidgetOld.HorizonalMargin(0.1f),
                        NebulaConfiguration.OptionButtonWidget(() => selectionOption.ChangeValue(true), selectionOption.ToDisplayString())
                        ));

                if (!isOptional || selectionOption.GetBool())
                    widget.Append(new CombinedWidgetOld(0.55f, IMetaWidgetOld.AlignmentOption.Center, list.ToArray()));
                return widget;
            };
            selectionOption.Shower = () =>
            {
                if(isOptional && !selectionOption)
                {
                    return Language.Translate("role.general.canUseVent") + " : " + selectionOption.ToDisplayString();
                }
                var str = selectionOption.Title.GetString() + " :";
                if (coolDownOption != null) str += "\n" + Language.Translate("role.general.ventCoolDown.short") + " : " + coolDownOption.ToDisplayString();
                if (durationOption != null) str += "\n" + Language.Translate("role.general.ventDuration.short") + " : " + durationOption.ToDisplayString();
                if (usesOption != null) str += "\n" + Language.Translate("role.general.ventUses.short") + " : " + usesOption.ToDisplayString();
                return str;
            };
        }

        public int Uses => usesOption?.GetMappedInt() ?? 0;
        public float CoolDown => coolDownOption?.GetFloat() ?? 0f;
        public float Duration => durationOption?.GetFloat() ?? 0f;
        public bool CanUseVent => !isOptional || selectionOption.GetBool();
    }
}

public abstract class ConfigurableStandardRole : ConfigurableRole, DefinedRole
{
    protected NebulaConfiguration RoleCountOption { get; private set; } = null!;
    protected NebulaConfiguration RoleChanceOption { get; private set; } = null!;
    protected NebulaConfiguration RoleSecondaryChanceOption { get; private set; } = null!;

    private NebulaModifierFilterConfigEntry modifierFilter = null!;
    public override NebulaModifierFilterConfigEntry? ModifierFilter { get => modifierFilter; }

    private NebulaGhostRoleFilterConfigEntry ghostRoleFilter = null!;
    public override NebulaGhostRoleFilterConfigEntry? GhostRoleFilter { get => ghostRoleFilter; }

    public ConfigurableStandardRole(int TabMask):base(TabMask){ }
    public ConfigurableStandardRole() : base() { }

    public override int RoleCount => RoleCountOption.CurrentValue;
    public override float GetRoleChance(int count)
    {
        if (count > 0 && RoleSecondaryChanceOption.CurrentValue > 0)
            return RoleSecondaryChanceOption.GetFloat();
        return RoleChanceOption.GetFloat();
    }

    bool DefinedRole.CanLoad(DefinedAssignable assignable) => (this as DefinedRole).CanLoadDefault(assignable) && !modifierFilter.Contains(assignable);

    public static TranslateTextComponent CountOptionText = new("options.role.count");
    public static TranslateTextComponent ChanceOptionText = new("options.role.chance");
    public static TranslateTextComponent SecondaryChanceOptionText = new("options.role.secondaryChance");
    protected override void LoadOptions() {
        RoleCountOption = new(RoleConfig, "count", CountOptionText, 15, 0, 0);
        RoleConfig.IsActivated = () => RoleCountOption > 0;

        RoleChanceOption = new(RoleConfig, "chance", ChanceOptionText, 10f, 100f, 10f, 0f, 0f) { Decorator = NebulaConfiguration.PercentageDecorator };
        RoleChanceOption.Editor = () =>
        {
            if (RoleCount <= 1)
            {
                return new CombinedWidgetOld(0.55f, IMetaWidgetOld.AlignmentOption.Center,
                new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(1.8f)) { RawText = ChanceOptionText.GetString() },
                NebulaConfiguration.OptionTextColon,
                NebulaConfiguration.OptionButtonWidget(() => RoleChanceOption.ChangeValue(false), "<<"),
                new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(0.8f)) { RawText = RoleChanceOption.ToDisplayString() },
                NebulaConfiguration.OptionButtonWidget(() => RoleChanceOption.ChangeValue(true), ">>")
                );
            }
            else
            {
                return new CombinedWidgetOld(0.55f, IMetaWidgetOld.AlignmentOption.Center,
               new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(1.8f)) { RawText = ChanceOptionText.GetString() },
               NebulaConfiguration.OptionTextColon,
               NebulaConfiguration.OptionButtonWidget(() => RoleChanceOption.ChangeValue(false), "<<"),
               new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(0.8f)) { RawText = RoleChanceOption.ToDisplayString() },
               NebulaConfiguration.OptionButtonWidget(() => RoleChanceOption.ChangeValue(true), ">>"),
               new MetaWidgetOld.HorizonalMargin(0.3f),
               new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(1.4f)) { RawText = SecondaryChanceOptionText.GetString() },
               NebulaConfiguration.OptionTextColon,
               NebulaConfiguration.OptionButtonWidget(() => RoleSecondaryChanceOption.ChangeValue(false), "<<"),
               new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(0.8f)) { RawText = RoleSecondaryChanceOption.ToDisplayString() },
               NebulaConfiguration.OptionButtonWidget(() => RoleSecondaryChanceOption.ChangeValue(true), ">>")
               );
            }
        };

        RoleSecondaryChanceOption = new(RoleConfig, "secondaryChance", SecondaryChanceOptionText, 0f, 100f, 10f, 0f, 0f) { Decorator = (mapped)=>
        {
            if ((float)mapped! > 0f)
                return NebulaConfiguration.PercentageDecorator.Invoke(mapped);
            else
                return Language.Translate("options.followPrimaryChance");
        }
        };
        RoleSecondaryChanceOption.Editor = NebulaConfiguration.EmptyEditor;

        RoleCountOption.Shower = () => {
            var str = Language.Translate("options.role.count.short") + " : " + RoleCountOption.ToDisplayString();
            if (RoleCountOption > 1 && RoleSecondaryChanceOption.CurrentValue > 0)
                str += " (" + RoleChanceOption.ToDisplayString() + "," + RoleSecondaryChanceOption.ToDisplayString() + ")";
            else if(RoleCountOption >= 1)
                str += " (" + RoleChanceOption.ToDisplayString() + ")";
            return str;
        };

        RoleChanceOption.Shower = null;
        RoleSecondaryChanceOption.Shower = null;

        modifierFilter = new NebulaModifierFilterConfigEntry(RoleConfig.Id + ".modifierFilter", Array.Empty<string>());
        ghostRoleFilter = new NebulaGhostRoleFilterConfigEntry(RoleConfig.Id + ".ghostFilter", Array.Empty<string>());
    }

    public override bool IsSpawnable { get => RoleCountOption.CurrentValue > 0; }

    protected static NebulaConfiguration GenerateCommonEditor(ConfigurationHolder holder, params NebulaConfiguration[] commonOptions)
    {
        foreach (var option in commonOptions) option.Title = new CombinedComponent(new TranslateTextComponent("role.general.common"), new RawTextComponent(" "), new TranslateTextComponent(option.Id));

        return new NebulaConfiguration(holder, () => {
            MetaWidgetOld widget = new();
            foreach (var option in commonOptions) widget.Append(option.GetEditor()!);
            return widget;
        }, () => Language.Translate("options.commonSetting") + "\n" + string.Join("\n", commonOptions.Select(option => option.GetShownString())));
    }
}

public abstract class AbstractGhostRole : IAssignableBase, ICodeName
{
    public abstract RoleCategory Category { get; }
    //内部用の名称。AllRolesのソートに用いる
    public virtual string InternalName { get => LocalizedName; }
    //翻訳キー用の名称。
    public abstract string LocalizedName { get; }
    public virtual string DisplayName { get => Language.Translate("role." + LocalizedName + ".name"); }
    public virtual string ShortName { get => Language.Translate("role." + LocalizedName + ".short"); }
    public abstract string CodeName { get; }
    public abstract Color RoleColor { get; }
    public abstract GhostRoleInstance CreateInstance(GamePlayer player, int[] arguments);
    public int Id { get; set; }
    public abstract int RoleCount { get; }
    public abstract float GetRoleChance(int count);

    public virtual IEnumerable<IAssignableBase> RelatedOnConfig() { yield break; }
    public virtual ConfigurationHolder? RelatedConfig { get => null; }

    public abstract void Load();

    public AbstractGhostRole()
    {
        Roles.Register(this);
    }
}


public abstract class ConfigurableGhostRole : AbstractGhostRole, IConfiguableAssignable, DefinedGhostRole {
    public ConfigurationHolder RoleConfig { get; private set; } = null!;
    public override ConfigurationHolder? RelatedConfig { get => RoleConfig; }

    public RoleFilter? GhostRoleFilter { get; protected set; }

    protected virtual void LoadOptions() { }

    public override sealed void Load()
    {
        SerializableDocument.RegisterColor("role." + InternalName, RoleColor);
        RoleConfig = new ConfigurationHolder("options.role." + InternalName, new ColorTextComponent(RoleColor, new TranslateTextComponent("role." + LocalizedName + ".name")), ConfigurationTab.GhostRoles, CustomGameMode.AllGameModeMask);
        RoleConfig.Priority = 1;
        RoleConfig.RelatedAssignable = this;
        LoadOptions();
    }
}

public abstract class ConfigurableStandardGhostRole : ConfigurableGhostRole
{
    protected NebulaConfiguration RoleCountOption { get; private set; } = null!;
    protected NebulaConfiguration RoleChanceOption { get; private set; } = null!;

    protected override void LoadOptions()
    {
        RoleCountOption = new(RoleConfig, "count", ConfigurableStandardRole.CountOptionText, 15, 0, 0);
        RoleConfig.IsActivated = () => RoleCountOption > 0;

        RoleChanceOption = new(RoleConfig, "chance", ConfigurableStandardRole.ChanceOptionText, 10f, 100f, 10f, 0f, 0f) { Decorator = NebulaConfiguration.PercentageDecorator };
        RoleChanceOption.Editor = () =>
        {
            return new CombinedWidgetOld(0.55f, IMetaWidgetOld.AlignmentOption.Center,
            new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(1.8f)) { RawText = ConfigurableStandardRole.ChanceOptionText.GetString() },
            NebulaConfiguration.OptionTextColon,
            NebulaConfiguration.OptionButtonWidget(() => RoleChanceOption.ChangeValue(false), "<<"),
            new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(0.8f)) { RawText = RoleChanceOption.ToDisplayString() },
            NebulaConfiguration.OptionButtonWidget(() => RoleChanceOption.ChangeValue(true), ">>")
            );
        };
    }

    public override int RoleCount => RoleCountOption.CurrentValue;
    public override float GetRoleChance(int count)
    {
        return RoleChanceOption.GetFloat();
    }
}