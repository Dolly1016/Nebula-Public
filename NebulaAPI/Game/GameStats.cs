using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Text;

namespace Virial.Game;

public enum GameStatsCategory
{
    Kill,
    Death,
    Game,
    Roles,
    Perks,
}

public interface GameStatsEntry
{
    string Id { get; }
    TextComponent DisplayName { get; }
    int Progress { get; }
    GameStatsCategory Category { get; }
    DefinedAssignable? RelatedAssignable { get; }
    internal int InnerPriority { get; }
}
