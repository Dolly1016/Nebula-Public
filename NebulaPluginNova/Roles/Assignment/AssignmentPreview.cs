using Il2CppSystem.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Nebula.Roles.Assignment;

internal class AssignmentPreview
{
    [Flags]
    public enum AssignmentFlag
    {
        ModImpostor     = 0x01,
        VanillaImpostor = 0x02,
        ModNeutral      = 0x04,
        ModCrewmate     = 0x08,
        VanillaCrewmate = 0x10,
    }

    public record ExclusiveElement(ulong GroupBit, int RoleCount, int RoleId);
    public record ResolvedAssignmentPhase(int IndepentendRoles, AssignmentPhase[] Followers);
    public record AssignmentPhase(int IndependentRoles, ExclusiveElement[] ExclusiveElements)
    {
        /// <summary>
        /// <see cref="count"/>だけ割り当てられる枠がある想定で、排他要素の全選択パターンを返します。
        /// </summary>
        /// <param name="count">割り当て数</param>
        /// <returns></returns>
        public IEnumerable<ResolvedAssignmentPhase> TryResolve(int count, AssignmentPhase[] followers, int requiredCountMin = 0)
        {
            if((count <= IndependentRoles || ExclusiveElements.Length == 0))
            {
                //独立な割り当てだけですべて割り当てられうるとき、あるいは独立な割り当てしか選べないとき。どれも選択されないパターン
                yield return new (IndependentRoles, followers);
            }

            //新たに割り当てられる枠が1つ以上あるとき、割り当てることができる
            if (count > requiredCountMin)
            {
                //排他要素の選択パターンを試行する
                foreach (var selected in ExclusiveElements)
                {
                    AssignmentPhase[] resolvedFollowers = followers.Select(f => new AssignmentPhase(f.IndependentRoles, f.ExclusiveElements.Where(elem => (elem.GroupBit & selected.GroupBit) == 0 || selected.RoleId == elem.RoleId).ToArray())).ToArray();
                    AssignmentPhase resolved = new(IndependentRoles + selected.RoleCount, ExclusiveElements.Where(elem => (elem.GroupBit & selected.GroupBit) == 0).ToArray());
                    if (resolved.ExclusiveElements.Length == 0)
                        yield return new(resolved.IndependentRoles, resolvedFollowers);
                    else
                    {
                        foreach (var pattern in resolved.TryResolve(count, resolvedFollowers, requiredCountMin + 1)) yield return pattern;
                    }
                }
            }
        }
    }

    private record AssignmentPhaseArgument(int Consumed, int RoleCount, int Propagation, AssignmentPhase[] Followers);
    public static AssignmentFlag[] CalcPreview(int players)
    {
        AssignmentFlag[] result = new AssignmentFlag[players];
        for (int i = 0; i < players; i++) result[i] = 0;

        int impostors = GameOptionsManager.Instance.CurrentGameOptions.GetAdjustedNumImpostors(players);
        int modImpostors = GeneralConfigurations.AssignmentImpostorOption;
        if (modImpostors < 0) modImpostors = 99;
        int modNeutral = GeneralConfigurations.AssignmentNeutralOption;
        if (modNeutral < 0) modNeutral = 99;
        int modCrewmates = GeneralConfigurations.AssignmentCrewmateOption;
        if (modCrewmates < 0) modCrewmates = 99;

        var exOptions = GeneralConfigurations.exclusiveAssignmentOptions;

        ulong PickUpExOptionsIndecies(DefinedRole role)
        {
            ulong bit = 0;
            for (int i = 0; i < exOptions.Length; i++) if (exOptions[i].Contains(role)) bit |= 1u << i;
            return bit;
        }
        AssignmentPhase GetPhase(RoleCategory category, bool get100) => new(
            Roles.AllRoles.Where(r => r.Category == category && exOptions.All(e => !e.Contains(r))).Sum(r => r.AllocationParameters?.GetRoleCountWhich(get100) ?? 0),
            Roles.AllRoles.Where(r => r.Category == category && exOptions.Any(e => e.Contains(r))).Select(r => new ExclusiveElement(PickUpExOptionsIndecies(r), r.AllocationParameters?.GetRoleCountWhich(get100) ?? 0, r.Id)).Where(e => e.RoleCount != 0).ToArray());

        AssignmentPhase[] allPhases = [
            GetPhase(RoleCategory.ImpostorRole, true), GetPhase(RoleCategory.ImpostorRole, false),
            GetPhase(RoleCategory.NeutralRole, true), GetPhase(RoleCategory.NeutralRole, false),
            GetPhase(RoleCategory.CrewmateRole, true), GetPhase(RoleCategory.CrewmateRole, false)
            ];
        (Func<int,int> GetMax,Func<AssignmentPhaseArgument, (int consumed, int propagation)> ReflectToPreview)[] allActions = [
            //インポスター100%
            (
            _ => Math.Min(impostors, modImpostors),
            arg => {
                for(int i =0;i< arg.RoleCount;i++) result[arg.Consumed + i] |= AssignmentFlag.ModImpostor;
                return (arg.Consumed + arg.RoleCount, arg.RoleCount);
            }
            ),
            //インポスター確率 + あまり
            (
            prop => Math.Min(impostors - prop, modImpostors - prop),
            arg => {
                for (int i = 0; i < arg.RoleCount; i++) result[arg.Consumed + i] |= AssignmentFlag.ModImpostor;

                int vanillaImpostors = impostors - arg.Propagation - arg.RoleCount;
                for(int i = 0; i < vanillaImpostors; i++) result[arg.Consumed + arg.RoleCount + i] |= AssignmentFlag.VanillaImpostor;
                Debug.Log("Impostors: " + impostors);
                Debug.Log("Prop: " + arg.Propagation);
                Debug.Log("RoleCount: " + arg.RoleCount);
                Debug.Log("VanillaImpostors: " + vanillaImpostors);
                return (arg.Consumed + arg.RoleCount + vanillaImpostors, 0);
            }
            ),
            //第三陣営100%
            (
            _ => modNeutral,
            arg => {
                for(int i =0;i<arg.RoleCount;i++) result[arg.Consumed + i] |= AssignmentFlag.ModNeutral;
                return (arg.Consumed + arg.RoleCount, arg.RoleCount);
            }
            ),
            //第三陣営確率
            (
            prop =>modNeutral - prop,
            arg => {
                for (int i = 0; i < arg.RoleCount; i++) result[arg.Consumed + i] |= AssignmentFlag.ModNeutral;
                return (arg.Consumed + arg.RoleCount, 0);
            }
            ),
            //クルー100%
            (
            _ => modCrewmates,
            arg => {
                for(int i =0;i<arg.RoleCount;i++) result[arg.Consumed + i] |= AssignmentFlag.ModCrewmate;
                return (arg.Consumed + arg.RoleCount, arg.RoleCount);
            }
            ),
            //クルー確率 + あまり
            (
            prop => modCrewmates - prop,
            arg => {
                for (int i = 0; i < arg.RoleCount; i++) result[arg.Consumed + i] |= AssignmentFlag.ModCrewmate;
                for(int i = arg.Consumed + arg.RoleCount; i < players;i++) result[i] |= AssignmentFlag.VanillaCrewmate;
                return (players, 0);
            }
            )
            ];

        void TryAllPatterns(int phase, int consumed, int propagation, AssignmentPhase[] followersAndMe)
        {
            //最後まで到達したら終了
            if(phase >= allActions.Length) return;

            var currentPhase = followersAndMe[0];
            var followers = followersAndMe.Skip(1).ToArray();

            var max = Math.Min(players - consumed, allActions[phase].GetMax.Invoke(propagation));
            foreach(var pattern in currentPhase.TryResolve(max, followers))
            {
                var next = allActions[phase].ReflectToPreview.Invoke(new(consumed, Math.Min(max, pattern.IndepentendRoles), propagation, followers));
                TryAllPatterns(phase + 1, next.consumed, next.propagation, pattern.Followers);
            }
        }

        TryAllPatterns(0, 0, 0, allPhases);

        return result;
    }
}
