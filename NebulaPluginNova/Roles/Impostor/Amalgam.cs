using Il2CppSystem.Buffers;
using Nebula.Roles.Complex;
using Nebula.Roles.Neutral;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Core.Internal;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Events.Role;
using Virial.Game;
using Virial.Helpers;
using Virial.Media;
using Virial.Runtime;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

namespace Nebula.Roles.Impostor;

internal class Amalgam : DefinedRoleTemplate, DefinedRole, DefinedSingleAbilityRole<Amalgam.Ability>, ICustomAssignableStatus, IAssignableDocument
{
    private Amalgam() : base("amalgam", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [MaxRolesOption, RandomAssignmentOption, CanBeGuessedAsLoadedRolesOption, RoleFilterOption])
    {
        ICustomAssignableStatus.Register(this);
    }

    Ability DefinedSingleAbilityRole<Amalgam.Ability>.CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Length > 1 ? arguments.Skip(1) : null);
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Virial.Game.Player player, int[] arguments) => new Instance(player, arguments);
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.KillersSide;
    bool ICustomAssignableStatus.CanSpawn(DefinedAssignable assignable)
    {
        bool amSpawnable = (this as ISpawnable).IsSpawnable;
        if (assignable == this) return amSpawnable;
        bool amSpawnableInAnyForm = (this as DefinedRole).IsSpawnableInSomeForm();
        if (!amSpawnableInAnyForm) return false;
        if (assignable is DefinedRole role) return RoleFilterOption.Contains(role);
        return false;
    }

    static private readonly IntegerConfiguration MaxRolesOption = NebulaAPI.Configurations.Configuration("options.role.amalgam.maxRoles", (1, 16), 3);
    static private readonly BoolConfiguration CanBeGuessedAsLoadedRolesOption = NebulaAPI.Configurations.Configuration("options.role.amalgam.canBeGuessedAsLoadedRoles", true);
    static private readonly BoolConfiguration RandomAssignmentOption = NebulaAPI.Configurations.Configuration("options.role.amalgam.randomAssignment", false);
    static private readonly SimpleRoleFilterConfiguration RoleFilterOption = new("options.role.amalgam.abilityFilter") { RolePredicate = r => r.MultipleAssignment != MultipleAssignmentType.NotAllowed, ScrollerTag = "amalgamFilter", InvertOption = true, PreviewOnlySpawnableRoles = false };
    static public readonly Amalgam MyRole = new();

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        if (RandomAssignmentOption) yield break;
        yield return new(buttonSprite, "role.amalgam.ability.amalgam");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%ABILITY%", Language.Translate(RandomAssignmentOption ? "role.amalgam.ability.main.random" : "role.amalgam.ability.main.selection"));
    }
        

    int[]? DefinedAssignable.DefaultAssignableArguments => RandomAssignmentOption ? GetRandomAssignment() : null;
    static int[] GetRandomAssignment()
    {
        int leftNum = MaxRolesOption.GetValue();
        if (leftNum > 0)
        {
            var cand = Roles.AllRoles.Where(CheckAssignable).ToList();
            List<int> args = [0];

            if (cand.Count <= leftNum && cand.Count(r => r.MultipleAssignment == MultipleAssignmentType.AsUniqueKillAbility) <= 1 && cand.Count(r => r.MultipleAssignment == MultipleAssignmentType.AsUniqueMapAbility) <= 1)
            {
                //候補を全て割り当て可能な場合はそのまま割り当てる
                foreach (var c in cand)
                {
                    args.Add(c.Id);
                    args.Add(0);
                }
            }
            else
            {
                for (int i = 0; i < leftNum; i++)
                {
                    if (cand.Count == 0) break;

                    var selected = cand.Random();
                    args.Add(selected.Id);
                    args.Add(0); //サブ能力の引数長は0

                    if (selected.MultipleAssignment == MultipleAssignmentType.Allowed)
                    {
                        cand.Remove(selected);
                    }
                    else
                    {
                        var targetType = selected.MultipleAssignment;
                        cand.RemoveAll(r => r.MultipleAssignment == targetType);
                    }
                }
            }
            return args.ToArray();
        }
        return [];
    }


    static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.AmalgamButton.png", 115f);
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        public Ability MyAbility { get; private set; }
        DefinedRole RuntimeRole.Role => MyRole;
        int[] cachedArguments;
        public Instance(GamePlayer myPlayer, int[] arguments) : base(myPlayer)
        {
            cachedArguments = arguments;
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                NebulaGameManager.Instance?.RegisterInputMapper(input =>
                {
                    if (input == Virial.Compat.VirtualKeyInput.Ability) return Virial.Compat.VirtualKeyInput.None;
                    if (input == Virial.Compat.VirtualKeyInput.SecondaryAbility) return Virial.Compat.VirtualKeyInput.None;
                    return input;
                }, this);
                SetUpChallengeAchievement();
            }
            MyAbility = (MyRole as DefinedSingleAbilityRole<Amalgam.Ability>).CreateAbility(MyPlayer, cachedArguments).Register(this);
        }

        void SetUpChallengeAchievement()
        {
            new AchievementToken<int>("amalgam.challenge", 0, (_, achievement) =>
            {
                return NebulaGameManager.Instance!.AllAchievementTokens.Where(r =>
                    r.Achievement is AbstractAchievement a && 
                    a.AchievementType().Contains(AchievementType.Challenge) && 
                    !a.RelatedRole.IsEmpty() && a.RelatedRole.Any(r => r is DefinedRole) && 
                    a.Id != "amalgam.challenge" && 
                    r.UniteTo(false) != AbstractAchievement.ClearDisplayState.None).DistinctBy(a => a.Achievement.Id).Count() >= 2;
            });
        }
        void RuntimeRole.Usurp() => (MyAbility as IUsurpableAbility)?.Usurp();
        int[] RuntimeAssignable.RoleArguments => (MyAbility as IPlayerAbility).AbilityArguments;
        int[] RuntimeRole.UsurpedAbilityArguments => (MyAbility as IPlayerAbility).AbilityArguments;
        IEnumerable<IPlayerAbility> GetAbilities()
        {
            if (MyAbility != null)
            {
                yield return MyAbility;
                foreach (var sa in MyAbility.GetSubAbilities()) yield return sa;
            }
        }
        IEnumerable<IPlayerAbility> RuntimeAssignable.MyAbilities => GetAbilities();
        IEnumerable<DefinedAssignable> RuntimeAssignable.AssignableOnHelp => [MyRole, ..(MyAbility as IPlayerAbility).SubAssignableOnHelp];

        string RuntimeAssignable.DisplayName => (MyRole as DefinedRole).GetDisplayName(MyAbility);
        string RuntimeAssignable.DisplayColoredName => (this as RuntimeAssignable).DisplayName.Color((MyRole as DefinedRole).UnityColor);

        bool RuntimeRole.CheckGuessAbility(DefinedRole abilityRole)
        {
            if (abilityRole == MyRole) return true;
            if (CanBeGuessedAsLoadedRolesOption && MyAbility.GetSubRoles().Contains(abilityRole)) return true;
            return false;
        }
    }

    static bool CheckAssignable(DefinedRole role) => /*role.IsSpawnable &&*/ RoleFilterOption.Contains(role);

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt(), ..abilities.Select<(DefinedRole role, IPlayerAbility ability), int[]>(a => {
            int[] args = a.ability.AbilityArguments;
            return [a.role.Id, args.Length, ..args];
        }).Smooth()];
        List<(DefinedRole role, IPlayerAbility ability)> abilities = [];
        public IEnumerable<IPlayerAbility> GetSubAbilities()
        {
            foreach (var entry in abilities) yield return entry.ability;
        }
        public IEnumerable<DefinedRole> GetSubRoles()
        {
            foreach (var entry in abilities) yield return entry.role;
        }

        IEnumerable<int> GetSubAbilityArguments()
        {
            foreach (var entry in abilities)
            {
                yield return entry.role.Id;
                var args = entry.ability.AbilityArguments ?? [];
                yield return args.Length;
                foreach (var arg in args) yield return arg;
            }
        }

        void ParseAndLoadSubAbilityArguments(IEnumerable<int> args)
        {
            var enumerator = args.GetEnumerator();
            bool TryGet(out int num)
            {
                var result = enumerator.MoveNext();
                num = enumerator.Current;
                return result;
            }

            while(TryGet(out var roleId) && TryGet(out var argLength))
            {
                int[] subArgs = new int[argLength];
                for(int i = 0; i < argLength; i++) TryGet(out subArgs[i]);
                var role = Roles.GetRole(roleId);
                if(role != null) AddAbility(role, subArgs);
            }
        }

        IEnumerable<IPlayerAbility> IPlayerAbility.SubAbilities => GetSubAbilities();
        IEnumerable<DefinedAssignable> IPlayerAbility.SubAssignableOnHelp => GetSubRoles();

        void AddAbility(DefinedRole role, int[] args) {
            abilities.Add((role, role.GetAbilityOnRole(MyPlayer, AbilityAssignmentStatus.CanLoadToImpostor, args).Register(this)));
            if (AmOwner && abilities.Count == 2) new StaticAchievementToken("amalgam.common1");
        }

        int GetLeftNum() => MaxRolesOption.GetValue() - abilities.Count;
        bool IsAssignable(DefinedRole role) => CheckAssignable(role) && !abilities.Any(entry => entry.role == role);
        public Ability(GamePlayer player, bool isUsurped, IEnumerable<int>? subAbilities) : base(player, isUsurped)
        {
            if (subAbilities != null) ParseAndLoadSubAbilityArguments(subAbilities);

            if (AmOwner && !RandomAssignmentOption)
            {
                var selectButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.None,
                        0f, "select", buttonSprite, null, _ => GetLeftNum() > 0);
                selectButton.ShowUsesIcon(GetLeftNum().ToString(), MyRole.RoleColor);
                selectButton.OnClick = _ =>
                {
                    MetaScreen window = null!;
                    Predicate<DefinedRole> GetRoleFilter(MultipleAssignmentType type) => r => r.MultipleAssignment == type && IsAssignable(r);
                    List<(string?, Predicate<DefinedRole>?)> roleTabs = [];

                    if (abilities.All(a => a.role.MultipleAssignment != MultipleAssignmentType.AsUniqueKillAbility))
                    {
                        roleTabs.Add((Language.Translate("role.amalgam.tabs.kill"), GetRoleFilter(MultipleAssignmentType.AsUniqueKillAbility)));
                    }
                    if (abilities.All(a => a.role.MultipleAssignment != MultipleAssignmentType.AsUniqueMapAbility))
                    {
                        roleTabs.Add((Language.Translate("role.amalgam.tabs.map"), GetRoleFilter(MultipleAssignmentType.AsUniqueMapAbility)));
                    }
                    roleTabs.Add((Language.Translate("role.amalgam.tabs.others"), GetRoleFilter(MultipleAssignmentType.Allowed)));

                    window = MeetingRoleSelectWindow.OpenRoleSelectWindowUsingTabs(Roles.AllRoles, roleTabs.ToArray(), true, "", r =>
                    {
                        RpcAddRole.Invoke((MyPlayer, r));
                        selectButton.UpdateUsesIcon(GetLeftNum().ToString());
                        window.CloseScreen();
                    });
                };
            }
        }

        public override bool Usurp()
        {
            var result = base.Usurp();
            abilities.Do(a => (a.ability as IUsurpableAbility)?.Usurp());
            return true;
        }

        GUIWidget? IPlayerAbility.ProgressWidget => abilities.Count == 0 ? null : ProgressGUI.Holder(abilities.Select(ability => ProgressGUI.Holder(
                ProgressGUI.SmallAssignableNameText(ability.role, "-"),
                ability.ability.ProgressWidget?.Move(new(0.14f, 0f))
                )));
        
        RemoteProcess<(GamePlayer amalgam, DefinedRole role)> RpcAddRole = new("AddRoleAmalgam", (message, _) =>
        {
            if(message.amalgam.TryGetAbility<Ability>(out var ability))
            {
                ability.AddAbility(message.role, []);
            }
        });
    }
}
