using Il2CppSystem.Collections.Concurrent;
using Nebula.Patches;
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
        ModImpostor     = 0x00001,
        ModImpostorPrb  = 0x01000,
        ModImpostor100  = 0x00100,
        ModImpostorAdd  = 0x10000,
        VanillaImpostor = 0x00002,
        ModNeutral      = 0x00004,
        ModNeutralPrb   = 0x04000,
        ModNeutral100   = 0x00400,
        ModNeutralAdd   = 0x40000,
        ModCrewmate     = 0x00008,
        ModCrewmatePrb  = 0x08000,
        ModCrewmate100  = 0x00800,
        ModCrewmateAdd  = 0x80000,
        VanillaCrewmate = 0x00010,
        ImageFlag       = ModImpostor | VanillaImpostor | ModNeutral | ModCrewmate | VanillaCrewmate,
    }

    public record SpecialElement(ulong GroupBit, int RoleCount, int RoleId, int AdditionalImpostors, int AdditionalCrewmates, int AdditionalNeutrals)
    {
        public int AdditionalSum => AdditionalCrewmates + AdditionalImpostors + AdditionalNeutrals;
    }
    public record ExclusiveElement(ulong GroupBit, int RoleCount, int RoleId);
    public record ExtraAssignmentInfo(int Impostor, int Neutral, int Crewmate)
    {
        public int Sum => Impostor + Neutral + Crewmate;
        public ExtraAssignmentInfo Add(int impostor, int neutral, int crewmate) => new ExtraAssignmentInfo(Impostor + impostor, Neutral + neutral, Crewmate + crewmate);
    }
    public record ResolvedAssignmentPhase(int IndepentendRoles, AssignmentPool[] Followers, ExtraAssignmentInfo AdditionalAssignments, bool expectedSurplus);
    public record AssignmentPool(int IndependentRoles, ExclusiveElement[] ExclusiveElements, SpecialElement[] SpecialElements)
    {
        /// <summary>
        /// <see cref="count"/>だけ割り当てられる枠がある想定で、全割り当てパターンを返します。
        /// </summary>
        /// <param name="count">割り当て数</param>
        /// <param name="followers">自身を除く後続</param>
        /// <param name="usedCountMin">既にほかの理由で占有された割り当ての最小数</param>
        /// <param name="additionalMargin">追加割り当てのための余裕</param>
        /// <param name="syncCountWithMargin">追加割り当てが割り当て可能数を同時に消費する場合true</param>
        /// <returns>可能な全割り当てパターン</returns>
        public IEnumerable<ResolvedAssignmentPhase> TryResolve(int count, int wholeCount, AssignmentPool[] followers, int preassigned, ExtraAssignmentInfo additionalAssignments, bool expectedSurplus)
        {
            //新たに割り当てられる枠が1つ以上あるとき、排他割り当てや特別な割り当てを試せる。
            if (count > 0)
            {
                if (ExclusiveElements.Length > 0)
                {
                    //排他要素の選択パターンを試行する
                    for (int i = 0; i < ExclusiveElements.Length; i++)
                    {
                        var selected = ExclusiveElements[i];
                        AssignmentPool[] resolvedFollowers = Filter(followers, selected.GroupBit, selected.RoleId);
                        AssignmentPool resolved = Filter(this, selected.GroupBit, selected.RoleId, selected.RoleCount - 1, i + 1, 0); //排他要素のうち1つは割り当てられたものとして消費する。

                        //選択した排他要素から1人分を割り当てた場合をシミュレートする。
                        foreach (var pattern in resolved.TryResolve(count - 1, wholeCount - 1, resolvedFollowers, preassigned + 1, additionalAssignments, expectedSurplus || i > 0)) yield return pattern;
                    }

                    //排他要素を全く選ばなかったケースをシミュレートする。
                    foreach (var pattern in Filter(this, 0ul, -1, 0, ExclusiveElements.Length, 0).TryResolve(count, wholeCount, followers, preassigned, additionalAssignments, true)) yield return pattern;

                    yield break;
                }
                else if(SpecialElements.Length > 0)
                {
                    //追加割り当てを持つ要素の選択パターンを試行する
                    bool canAssignSpecialElements = false;
                    for (int i = 0; i < SpecialElements.Length; i++)
                    {
                        var selected = SpecialElements[i];

                        AssignmentPool[] resolvedFollowers = Filter(followers, selected.GroupBit, selected.RoleId);
                        AssignmentPool resolved = Filter(this, selected.GroupBit, selected.RoleId, 0, 0, i + 1);
                        ExtraAssignmentInfo resolvedAdditionalInfo = additionalAssignments;
                        //追加割り当てを持つ役職を指定回数割り当てた場合をそれぞれシミュレートする。
                        for (int c = 0;c <= selected.RoleCount; c++)
                        {
                            bool nextBreak = (count < c + 1) || (wholeCount < (c + 1) * (1 + selected.AdditionalSum));//割り当てできない、あるいは追加割り当てが割り当てられない場合はこれを最後にシミュレーションを打ち切る。

                            if (c >= 1)
                            {
                                canAssignSpecialElements |= true;

                                resolvedAdditionalInfo = resolvedAdditionalInfo.Add(selected.AdditionalImpostors, selected.AdditionalNeutrals, selected.AdditionalCrewmates);
                                int nextWholeCount = wholeCount - c * (selected.AdditionalSum + 1);
                                int nextCount = Math.Min(nextWholeCount, count - c);
                                foreach (var pattern in resolved.TryResolve(nextCount, nextWholeCount, resolvedFollowers, preassigned + c, resolvedAdditionalInfo, expectedSurplus || i > 0 || (!nextBreak && c < selected.RoleCount))) yield return pattern;
                            }

                            if (nextBreak) break;
                        }
                    }

                    //追加割り当て要素を全く選ばなかったケースをシミュレートする。
                    foreach (var pattern in Filter(this, 0ul, -1, 0, 0, SpecialElements.Length).TryResolve(count, wholeCount, followers, preassigned, additionalAssignments, canAssignSpecialElements | expectedSurplus)) yield return pattern;
                    yield break;
                }
            }

            //考慮すべき特殊な割り当てがないため、消費できるだけ消費させるパターンを返す。
            yield return new(IndependentRoles + preassigned, followers, additionalAssignments, expectedSurplus);
        }

        static private AssignmentPool Filter(AssignmentPool pool, ulong groupBit, int selectedId, int additionalCount = 0, int skipExclusive = 0, int skipSpecial = 0)
        {
            if (groupBit == 0 && additionalCount == 0 && skipExclusive == 0 && skipSpecial == 0) return pool;
            return new AssignmentPool(pool.IndependentRoles + additionalCount,
                pool.ExclusiveElements.Skip(skipExclusive).Where(e => (e.GroupBit & groupBit) == 0 || e.RoleId == selectedId).ToArray(),
                pool.SpecialElements.Skip(skipSpecial).Where(e => (e.GroupBit & groupBit) == 0 || e.RoleId == selectedId).ToArray()
                );
        }
        static private AssignmentPool[] Filter(AssignmentPool[] pool, ulong groupBit, int selectedId)
        {
            if (groupBit == 0) return pool;
            return pool.Select(p => Filter(p, groupBit, selectedId)).ToArray();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="Consumed">消費済みのプレイヤー数</param>
    /// <param name="RoleCount"></param>
    /// <param name="Propagation">直前の割り当て数</param>
    /// <param name="Followers">後続の割り当て</param>
    private record AssignmentPhaseArgument(int Consumed, int RoleCount, int Propagation, AssignmentPool[] Followers, int extraAssignments);

    /// <summary>
    /// 割り当ての可能性を計算します。
    /// </summary>
    /// <param name="players"></param>
    /// <returns></returns>
    public static AssignmentFlag[] CalcPreview(int players)
    {
        AssignmentFlag[] result = new AssignmentFlag[players];
        for (int i = 0; i < players; i++) result[i] = 0;

        int impostors = GameOptionsManager.Instance.CurrentGameOptions.GetAdjustedNumImpostorsModded(players);
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
        AssignmentPool GetPhase(RoleCategory category, bool get100)
        {
            List<ExclusiveElement> exclusiveElements = [];
            List<SpecialElement> specialElements = [];
            int independentRoles = 0;
            foreach(var role in Roles.AllRoles)
            {
                if (role.Category != category) continue;

                var allocParam = role.AllocationParameters;
                var count = allocParam?.GetRoleCountWhich(get100) ?? 0;

                if ((allocParam?.HasExtraAssignment ?? false) && count > 0)
                {
                        var otherAssignment = allocParam.OthersAssignment;
                        specialElements.Add(new(PickUpExOptionsIndecies(role), count, role.Id,
                            otherAssignment.Count(a => a.Category == RoleCategory.ImpostorRole),
                            otherAssignment.Count(a => a.Category == RoleCategory.CrewmateRole),
                            otherAssignment.Count(a => a.Category == RoleCategory.NeutralRole)
                            ));
                }else if(exOptions.Any(e => e.Contains(role)) && count > 0)
                {
                    exclusiveElements.Add(new(PickUpExOptionsIndecies(role), count, role.Id));
                }
                else
                {
                    independentRoles += count;
                }
            }
            return new AssignmentPool(independentRoles, exclusiveElements.ToArray(), specialElements.ToArray());
        }

        AssignmentPool[] allPools = [
            GetPhase(RoleCategory.ImpostorRole, true), GetPhase(RoleCategory.ImpostorRole, false),
            GetPhase(RoleCategory.NeutralRole, true), GetPhase(RoleCategory.NeutralRole, false),
            GetPhase(RoleCategory.CrewmateRole, true), GetPhase(RoleCategory.CrewmateRole, false)
            ];
        (Func<int,int> GetMax,Func<AssignmentPhaseArgument, (int consumed, int propagation)> ReflectToPreview)[] allPhases = [
            //インポスター100%
            (
            _ => Math.Min(impostors, modImpostors),
            arg => {
                for(int i =0;i< arg.RoleCount;i++) result[arg.Consumed + i] |= AssignmentFlag.ModImpostor100 | AssignmentFlag.ModImpostor;
                return (arg.Consumed + arg.RoleCount, arg.RoleCount);
            }
            ),
            //インポスター確率 + あまり
            (
            prop => Math.Min(impostors - prop, modImpostors - prop),
            arg => {
                for (int i = 0; i < arg.RoleCount; i++) result[arg.Consumed + i] |= AssignmentFlag.ModImpostorPrb | AssignmentFlag.ModImpostor;

                int vanillaImpostors = impostors - arg.Propagation - arg.RoleCount;
                for(int i = 0; i < vanillaImpostors; i++) result[arg.Consumed + arg.RoleCount + i] |= AssignmentFlag.VanillaImpostor;
                return (arg.Consumed + arg.RoleCount + vanillaImpostors, 0);
            }
            ),
            //第三陣営100%
            (
            _ => modNeutral,
            arg => {
                for(int i =0;i<arg.RoleCount;i++) result[arg.Consumed + i] |= AssignmentFlag.ModNeutral100 | AssignmentFlag.ModNeutral;
                return (arg.Consumed + arg.RoleCount, arg.RoleCount);
            }
            ),
            //第三陣営確率
            (
            prop =>modNeutral - prop,
            arg => {
                for (int i = 0; i < arg.RoleCount; i++) result[arg.Consumed + i] |= AssignmentFlag.ModNeutralPrb | AssignmentFlag.ModNeutral;
                return (arg.Consumed + arg.RoleCount, 0);
            }
            ),
            //クルー100%
            (
            _ => modCrewmates,
            arg => {
                for(int i =0;i<arg.RoleCount;i++) result[arg.Consumed + i] |= AssignmentFlag.ModCrewmate100 | AssignmentFlag.ModCrewmate;
                return (arg.Consumed + arg.RoleCount, arg.RoleCount);
            }
            ),
            //クルー確率 + あまり
            (
            prop => modCrewmates - prop,
            arg => {
                for (int i = 0; i < arg.RoleCount; i++) result[arg.Consumed + i] |= AssignmentFlag.ModCrewmatePrb | AssignmentFlag.ModCrewmate;
                for(int i = arg.Consumed + arg.RoleCount; i < players - arg.extraAssignments;i++) result[i] |= AssignmentFlag.VanillaCrewmate;
                return (players, 0);
            }
            )
            ];

        //consumed: 既に割り当て済みのプレイヤー数
        //propagation: 直前の割り当てメソッドから伝播した値 (自身が割り当てた数を伝播させて使う。)
        //followersAndMe: 後続のパターン(0番目を取り出して試行する。)
        void TryAllPatterns(int phase, int consumed, int propagation, AssignmentPool[] followersAndMe, ExtraAssignmentInfo extraAssignmentInfo)
        {
            //最後まで到達したら追加割り当てを反映させて終了
            if(phase >= allPhases.Length)
            {
                int n = 1;
                for(int i = 0;i< extraAssignmentInfo.Impostor; i++)
                {
                    result[^n] |= AssignmentFlag.ModImpostor | AssignmentFlag.ModImpostorAdd;
                    n++;
                }

                for (int i = 0; i < extraAssignmentInfo.Neutral; i++)
                {
                    result[^n] |= AssignmentFlag.ModNeutral | AssignmentFlag.ModNeutralAdd;
                    n++;
                }

                for (int i = 0; i < extraAssignmentInfo.Crewmate; i++)
                {
                    result[^n] |= AssignmentFlag.ModCrewmate | AssignmentFlag.ModCrewmateAdd;
                    n++;
                }
                return;
            }

            var currentPool = followersAndMe[0];
            var followers = followersAndMe.Skip(1).ToArray();

            var max = Math.Min(players - consumed, allPhases[phase].GetMax.Invoke(propagation));
            var wholeMax = players - consumed;
            foreach (var pattern in currentPool.TryResolve(max, wholeMax, followers, 0, extraAssignmentInfo, false))
            {
                int assigned = Math.Min(max, pattern.IndepentendRoles);
                if (assigned < max && pattern.expectedSurplus) continue; //あふれを期待したにもかかわらず溢れない場合、このシミュレーションを棄てる。

                var next = allPhases[phase].ReflectToPreview.Invoke(new(consumed, assigned, propagation, followers, extraAssignmentInfo.Sum));
                TryAllPatterns(phase + 1, next.consumed, next.propagation, pattern.Followers, pattern.AdditionalAssignments);
            }
        }

        //全てのパターンを試し始める。
        TryAllPatterns(0, 0, 0, allPools, new(0, 0, 0));

        return result;
    }
}
