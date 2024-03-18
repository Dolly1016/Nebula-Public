using Il2CppSystem.Reflection.Metadata.Ecma335;
using Nebula.Configuration;
using Nebula.Roles.Assignment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using static Il2CppMono.Security.X509.X520;
using static Nebula.Roles.Assignment.IRoleAllocator;

namespace Nebula.Roles;

public abstract class AbstractModifier : IAssignableBase, DefinedModifier
{
    public virtual string InternalName { get => LocalizedName; }
    public abstract string LocalizedName { get; }
    public virtual string DisplayName { get => Language.Translate("role." + LocalizedName + ".name"); }
    public abstract Color RoleColor { get; }
    public abstract ModifierInstance CreateInstance(PlayerModInfo player, int[] arguments);
    public int Id { get; set; }

    public virtual void Load() { }
    public virtual IEnumerable<IAssignableBase> RelatedOnConfig() { yield break; }

    public virtual ConfigurationHolder? RelatedConfig { get => null; }

    public AbstractModifier()
    {
        Roles.Register(this);
        SerializableDocument.RegisterColor("role." + InternalName, RoleColor);
    }

    RoleFilter? DefinedModifier.RoleFilter => null;
}

public abstract class IntroAssignableModifier : AbstractModifier, DefinedModifier, RoleFilter
{
    public virtual void Assign(IRoleAllocator.RoleTable roleTable) { }
    public abstract string CodeName { get; }
    public virtual int AssignPriority { get => 0; }

    RoleFilter? DefinedModifier.RoleFilter => this;


    void RoleFilter.Filter(FilterAction filterAction, params DefinedRole[] roles)
    {
        if (!AmongUsClient.Instance.AmHost) throw new Virial.NonHostPlayerException("Only host can edit role filter.");

        switch (filterAction)
        {
            case FilterAction.And:
                foreach(var r in Roles.AllRoles)
                    if (!(r.ModifierFilter?.Contains(this) ?? true) && roles.Contains(r)) r.ModifierFilter!.ToggleAndShare(this);
                break;
            case FilterAction.Or:
                foreach (var r in Roles.AllRoles)
                    if ((r.ModifierFilter?.Contains(this) ?? false) && roles.Contains(r)) r.ModifierFilter!.ToggleAndShare(this);
                break;
            case FilterAction.Set:
                foreach (var r in Roles.AllRoles)
                    if (r.ModifierFilter != null && r.ModifierFilter.Contains(this) == roles.Contains(r)) r.ModifierFilter!.ToggleAndShare(this);
                break;
        }
    }

    bool RoleFilter.Test(DefinedRole role)
    {
        return role.ModifierFilter?.Test(this) ?? false;
    }
}

public abstract class ConfigurableModifier : IntroAssignableModifier, IConfiguableAssignable
{
    public ConfigurationHolder RoleConfig { get; private set; } = null!;
    public override ConfigurationHolder? RelatedConfig { get => RoleConfig; }

    public int? myTabMask;

    public ConfigurableModifier(int TabMask)
    {
        myTabMask = TabMask;
        
    }

    public ConfigurableModifier()
    {
    }

    protected virtual void LoadOptions() { }

    public sealed override void Load()
    {
        RoleConfig = new ConfigurationHolder("options.role." + InternalName, new ColorTextComponent(RoleColor, new TranslateTextComponent("role." + LocalizedName + ".name")), myTabMask ?? ConfigurationTab.Modifiers, CustomGameMode.AllGameModeMask);
        RoleConfig.RelatedAssignable = this;
        LoadOptions();
    }
}

public abstract class ConfigurableStandardModifier : ConfigurableModifier
{
    protected NebulaConfiguration CrewmateRoleCountOption { get; private set; } = null!;
    protected NebulaConfiguration CrewmateRandomRoleCountOption { get; private set; } = null!;
    protected NebulaConfiguration ImpostorRoleCountOption { get; private set; } = null!;
    protected NebulaConfiguration ImpostorRandomRoleCountOption { get; private set; } = null!;
    protected NebulaConfiguration NeutralRoleCountOption { get; private set; } = null!;
    protected NebulaConfiguration NeutralRandomRoleCountOption { get; private set; } = null!;
    protected NebulaConfiguration RoleChanceOption { get; private set; } = null!;

    public ConfigurableStandardModifier(int TabMask) : base(TabMask)
    {
    }

    public ConfigurableStandardModifier()
    {
    }

    static public NebulaConfiguration GenerateRoleChanceOption(ConfigurationHolder holder) =>
        new(holder, "chance", new TranslateTextComponent("options.modifier.chance"), 10f, 90f, 10f, 0f, 0f) { Decorator = NebulaConfiguration.PercentageDecorator };

    static public NebulaConfiguration Generate100PercentRoleChanceOption(ConfigurationHolder holder) =>
        new(holder, "chance", new TranslateTextComponent("options.modifier.chance"), 10f, 100f, 10f, 0f, 0f) { Decorator = NebulaConfiguration.PercentageDecorator };

    protected override void LoadOptions() {
        CrewmateRoleCountOption = new(RoleConfig, "crewmateCount", new TranslateTextComponent("options.role.crewmateCount"), 15, 0, 0);
        ImpostorRoleCountOption = new(RoleConfig, "impostorCount", new TranslateTextComponent("options.role.impostorCount"), 5, 0, 0);
        NeutralRoleCountOption = new(RoleConfig, "neutralCount", new TranslateTextComponent("options.role.neutralCount"), 10, 0, 0);

        CrewmateRandomRoleCountOption = new(RoleConfig, "crewmateRandomCount", null, 15, 0, 0) { Editor = () => null, Shower = () => null };
        ImpostorRandomRoleCountOption = new(RoleConfig, "impostorRandomCount", null, 5, 0, 0) { Editor = () => null, Shower = () => null };
        NeutralRandomRoleCountOption = new(RoleConfig, "neutralRandomCount", null, 10, 0, 0) { Editor = () => null, Shower = () => null };

        RoleConfig.IsActivated = () => 
            CrewmateRoleCountOption > 0 || CrewmateRandomRoleCountOption > 0 ||
            ImpostorRoleCountOption > 0 || ImpostorRandomRoleCountOption > 0 ||
            NeutralRoleCountOption > 0 || NeutralRandomRoleCountOption > 0;

        void SetShowerAndEditor(NebulaConfiguration countOption, NebulaConfiguration randomOption)
        {
            countOption.Editor = () =>
            {
                return new CombinedWidgetOld(0.55f, IMetaWidgetOld.AlignmentOption.Center,
                    new MetaWidgetOld.Text(new(NebulaConfiguration.OptionTitleAttr) { Size = new(2.2f,0.4f)}) { RawText = countOption.Title.GetString(), PostBuilder = (text) => countOption.TitlePostBuild(text, null) },
                    NebulaConfiguration.OptionTextColon,
                    NebulaConfiguration.OptionButtonWidget(() => countOption.ChangeValue(false), "<<"),
                    new MetaWidgetOld.Text(NebulaConfiguration.OptionShortValueAttr) { RawText = countOption.ToDisplayString() },
                    NebulaConfiguration.OptionButtonWidget(() => countOption.ChangeValue(true), ">>"),
                    NebulaConfiguration.OptionRawText("(",0.2f),
                    NebulaConfiguration.OptionTranslatedText("options.role.randomCount", 0.7f),
                    NebulaConfiguration.OptionTextColon,
                    NebulaConfiguration.OptionButtonWidget(() => randomOption.ChangeValue(false), "<<"),
                    new MetaWidgetOld.Text(NebulaConfiguration.OptionShortValueAttr) { RawText = randomOption.ToDisplayString() },
                    NebulaConfiguration.OptionButtonWidget(() => randomOption.ChangeValue(true), ">>"),
                    NebulaConfiguration.OptionRawText(")", 0.2f)
                );
            };
            countOption.Shower = () =>
            {
                return countOption.Title.GetString() + ": (" +
                Language.Translate("options.role.definiteCount") + ": " + countOption.ToDisplayString() + ", " +
                Language.Translate("options.role.randomCount") + ": " + randomOption.ToDisplayString() + ")";
            };
        }
        SetShowerAndEditor(CrewmateRoleCountOption, CrewmateRandomRoleCountOption);
        SetShowerAndEditor(ImpostorRoleCountOption, ImpostorRandomRoleCountOption);
        SetShowerAndEditor(NeutralRoleCountOption, NeutralRandomRoleCountOption);

        RoleChanceOption = GenerateRoleChanceOption(RoleConfig);
        RoleChanceOption.Predicate = () => CrewmateRandomRoleCountOption > 0 || ImpostorRandomRoleCountOption > 0 || NeutralRandomRoleCountOption > 0;
    }

    private void TryAssign(IRoleAllocator.RoleTable roleTable, RoleCategory category,int num,int randomNum)
    {
        int reallyNum = num;
        float chance = RoleChanceOption.GetFloat() / 100f;
        for (int i = 0; i < randomNum; i++) if (!(chance < 1f) || (float)System.Random.Shared.NextDouble() < chance) reallyNum++;

        var players = roleTable.GetPlayers(category).Where(tuple=>tuple.role.CanLoad(this)).OrderBy(i => Guid.NewGuid()).ToArray();
        reallyNum = Mathf.Min(players.Length, reallyNum);

        for (int i = 0; i < reallyNum; i++) AssignToTable(roleTable, players[i].playerId);
    }

    virtual protected void AssignToTable(IRoleAllocator.RoleTable roleTable, byte playerId)
    {
        roleTable.SetModifier(playerId, this);
    }

    public override void Assign(IRoleAllocator.RoleTable roleTable)
    {
        TryAssign(roleTable, RoleCategory.CrewmateRole, CrewmateRoleCountOption,CrewmateRandomRoleCountOption);
        TryAssign(roleTable, RoleCategory.ImpostorRole, ImpostorRoleCountOption,ImpostorRandomRoleCountOption);
        TryAssign(roleTable, RoleCategory.NeutralRole, NeutralRoleCountOption,NeutralRandomRoleCountOption);
    }
}
