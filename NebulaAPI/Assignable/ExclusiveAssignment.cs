using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Assignable;

public interface IExclusiveAssignmentRule
{
    static private List<IExclusiveAssignmentRule> rules = [];
    static internal void RegisterRule(IExclusiveAssignmentRule rule) => rules.Add(rule);
    static public IEnumerable<IExclusiveAssignmentRule> AllRules => rules;

    /// <summary>
    /// 排他的に割り当てる役職を全て列挙します。
    /// </summary>
    /// <returns></returns>
    IEnumerable<DefinedRole> GetExclusiveRoles();

    /// <summary>
    /// 排他的に割り当てる役職の場合はtrueを返します。
    /// </summary>
    /// <param name="role"></param>
    /// <returns></returns>
    bool Contains(DefinedRole role);
}