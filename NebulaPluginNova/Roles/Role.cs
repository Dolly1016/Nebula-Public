using AmongUs.GameOptions;
using Epic.OnlineServices;
using Il2CppSystem.Reflection.Metadata.Ecma335;
using Nebula.Configuration;
using Nebula.Modules;
using Nebula.Utilities;
using Virial.Assignable;
using Virial.Configuration;
using static Il2CppMono.Security.X509.X520;

namespace Nebula.Roles;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class NebulaRoleHoler : Attribute
{

}

public abstract class AbstractRole : IAssignableBase, DefinedRole
{
    public abstract RoleCategory Category { get; }
    //内部用の名称。AllRolesのソートに用いる
    public virtual string InternalName { get => LocalizedName; }
    //翻訳キー用の名称。
    public abstract string LocalizedName { get; }
    public virtual string DisplayName { get => Language.Translate("role." + LocalizedName + ".name"); }
    public virtual string IntroBlurb { get => Language.Translate("role." + LocalizedName + ".blurb"); }
    public virtual string ShortName { get => Language.Translate("role." + LocalizedName + ".short"); }
    public abstract Color RoleColor { get; }
    Virial.Color DefinedRole.RoleColor => new Virial.Color(RoleColor);
    public virtual bool IsDefaultRole { get => false; }
    public abstract RoleInstance CreateInstance(PlayerModInfo player, int[] arguments);
    public int Id { get; set; }
    public abstract int RoleCount { get; }
    public abstract float GetRoleChance(int count);
    public abstract RoleTeam Team { get; }

    //追加付与ロールに役職プールの占有性があるか(追加付与ロールが無い場合、無意味)
    public virtual bool HasAdditionalRoleOccupancy { get => true; }
    public virtual AbstractRole[]? AdditionalRole { get => null; }

    public NebulaConfiguration? CanBeGuessOption { get; set; } = null;

    //For Config
    public virtual NebulaModifierFilterConfigEntry? ModifierFilter { get => null; }
    
    public virtual IEnumerable<IAssignableBase> RelatedOnConfig() { yield break; }
    public virtual ConfigurationHolder? RelatedConfig { get => null; }

    public virtual bool CanLoadDefault(IntroAssignableModifier modifier) => true;
    public virtual bool CanLoad(IntroAssignableModifier modifier)=> CanLoadDefault(modifier);

    public virtual bool CanBeGuessDefault { get => true; }
    public virtual bool CanBeGuess => CanBeGuessDefault && (CanBeGuessOption?.GetBool() ?? true);
    public virtual bool IsSpawnable { get => true; }

    ModifierFilter? DefinedRole.ModifierFilter => ModifierFilter;

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
        SerializableDocument.RegisterColor("role." + InternalName, RoleColor);
        RoleConfig = new ConfigurationHolder("options.role." + InternalName, new ColorTextComponent(RoleColor, new TranslateTextComponent("role." + LocalizedName + ".name")), myTabMask ?? ConfigurationTab.FromRoleCategory(Category), CustomGameMode.AllGameModeMask);
        RoleConfig.Priority = IsDefaultRole ? 0 : 1;
        RoleConfig.RelatedAssignable = this;
        LoadOptions();
    }

    public class KillCoolDownConfiguration
    {
        public enum KillCoolDownType
        {
            Immediate = 0,
            Relative = 1,
            Ratio = 2
        }

        private NebulaConfiguration selectionOption;
        private NebulaConfiguration immediateOption, relativeOption, ratioOption;

        public NebulaConfiguration EditorOption => selectionOption;

        private float minCoolDown;

        private NebulaConfiguration GetCurrentOption()
        {
            switch (selectionOption.CurrentValue)
            {
                case 0:
                    return immediateOption;
                case 1:
                    return relativeOption;
                case 2:
                    return ratioOption;
            }
            
            throw new Exception("Invalid kill cool option is selected.");
        }
        private static string[] AllSelections = new string[] { "options.killCoolDown.type.immediate", "options.killCoolDown.type.relative", "options.killCoolDown.type.ratio" };

        private static Func<object?, string> RelativeDecorator = (mapped) =>
        {
            float val = (float)mapped!;
            string str = val.ToString();
            if (val > 0f) str = "+" + str;
            else if (!(val < 0f)) str = "±" + str;
            return str + Language.Translate("options.sec");
        };

        public KillCoolDownConfiguration(ConfigurationHolder holder, string id, KillCoolDownType defaultType, float step, float immediateMin, float immediateMax, float relativeMin, float relativeMax, float ratioStep, float ratioMin, float ratioMax, float defaultImmediate, float defaultRelative, float defaultRatio)
        {
            new NebulaGlobalFunctionProperty(holder.Id + "." + id, () => NebulaConfiguration.SecDecorator.Invoke(CurrentCoolDown), () => CurrentCoolDown);

            selectionOption = new NebulaConfiguration(holder, id, null, AllSelections, (int)defaultType, (int)defaultType);
            selectionOption.Editor = () =>
            {
                var currentOption = GetCurrentOption();

                return new CombinedWidgetOld(0.55f, IMetaWidgetOld.AlignmentOption.Center,
                    new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(2.5f, TMPro.TextAlignmentOptions.Left)) { RawText = selectionOption.Title.GetString() },
                    NebulaConfiguration.OptionTextColon,
                    new MetaWidgetOld.HorizonalMargin(0.04f),
                    NebulaConfiguration.OptionButtonWidget(() => selectionOption.ChangeValue(true), selectionOption.ToDisplayString(), 0.9f),
                    new MetaWidgetOld.HorizonalMargin(0.2f),
                    NebulaConfiguration.OptionButtonWidget(() => currentOption.ChangeValue(false), "<<"),
                    new MetaWidgetOld.Text(NebulaConfiguration.OptionValueAttr) { RawText = currentOption.ToDisplayString() },
                    NebulaConfiguration.OptionButtonWidget(() => currentOption.ChangeValue(true), ">>")
                );
            };
            selectionOption.Shower = () =>
            {
                var str = selectionOption.Title.GetString() + " : ";
                switch (selectionOption.CurrentValue)
                {
                    case 0:
                        str += immediateOption!.ToDisplayString();
                        break;
                    case 1:
                        str += relativeOption!.ToDisplayString();
                        break;
                    case 2:
                        str += ratioOption!.ToDisplayString();
                        break;
                }
                str += (" (" + NebulaConfiguration.SecDecorator.Invoke(CurrentCoolDown) + ")").Color(Color.gray);
                return str;
            };

            immediateOption = new NebulaConfiguration(holder, id + ".immediate", null, immediateMin, immediateMax, step, defaultImmediate, defaultImmediate) { Decorator = NebulaConfiguration.SecDecorator, Editor = NebulaConfiguration.EmptyEditor };
            immediateOption.Shower = null;
            
            relativeOption = new NebulaConfiguration(holder, id + ".relative", null, relativeMin, relativeMax, step, defaultRelative, defaultRelative) { Decorator = RelativeDecorator, Editor = NebulaConfiguration.EmptyEditor };
            relativeOption.Shower = null;
            
            ratioOption = new NebulaConfiguration(holder, id + ".ratio", null, ratioMin, ratioMax, ratioStep, defaultRatio, defaultRatio) { Decorator = NebulaConfiguration.OddsDecorator, Editor = NebulaConfiguration.EmptyEditor };
            ratioOption.Shower = null;

            minCoolDown = immediateMin;
        }

        public float CurrentCoolDown
        {
            get
            {
                float vanillaCoolDown = GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.KillCooldown);
                switch (selectionOption.CurrentValue)
                {
                    case 0:
                        return immediateOption.GetFloat();
                    case 1:
                        return Mathf.Max(relativeOption.GetFloat() + vanillaCoolDown, minCoolDown);
                    case 2:
                        return ratioOption.GetFloat() * vanillaCoolDown;
                }
                return vanillaCoolDown;
            }
        }
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

public abstract class ConfigurableStandardRole : ConfigurableRole
{
    protected NebulaConfiguration RoleCountOption { get; private set; } = null!;
    protected NebulaConfiguration RoleChanceOption { get; private set; } = null!;
    protected NebulaConfiguration RoleSecondaryChanceOption { get; private set; } = null!;
    private NebulaModifierFilterConfigEntry modifierFilter = null!;
    public override NebulaModifierFilterConfigEntry? ModifierFilter { get => modifierFilter; }

    public ConfigurableStandardRole(int TabMask):base(TabMask){ }
    public ConfigurableStandardRole() : base() { }

    public override int RoleCount => RoleCountOption.CurrentValue;
    public override float GetRoleChance(int count)
    {
        if (count > 0 && RoleSecondaryChanceOption.CurrentValue > 0)
            return RoleSecondaryChanceOption.GetFloat();
        return RoleChanceOption.GetFloat();
    }
    public override bool CanLoad(IntroAssignableModifier modifier) => CanLoadDefault(modifier) && !modifierFilter.Contains(modifier);

    protected static TranslateTextComponent CountOptionText = new("options.role.count");
    protected static TranslateTextComponent ChanceOptionText = new("options.role.chance");
    protected static TranslateTextComponent SecondaryChanceOptionText = new("options.role.secondaryChance");
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