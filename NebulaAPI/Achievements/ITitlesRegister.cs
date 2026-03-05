using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Virial.Achievements;

public enum TrophyLevel
{
    Silver = 0,
    Gold = 1,
    Rainbow = 2,
}

public class TitleBuilder
{
    public TitleBuilder(string group, string id)
    {
        this.Group = group;
        this.Id = id;
    }

    public string Group { get; private init; }
    public string Id { get; private init; }
    public bool IsSecret { get; set; } = false;
    public string? RelatedRecord { get; set; } = null;
    public int? Goal { get; set; } = null;
    public DefinedAssignable? RelatedRole { get; set; } = null;
    public string? MigrationSource { get; set; } = null;
    public TrophyLevel Rank { get; set; }
}

public class RecordBuilder
{
    public RecordBuilder(string group, string id)
    {
        this.Group = group;
        this.Id = id;
    }

    public string Group { get; private init; }
    public string Id { get; private init; }
    public int Goal { get; set; } = 10000000;
    public string? MigrationSource { get; set; } = null;
}

public interface ITitlesRegister
{
    void Register(TitleBuilder builder);
    void Register(RecordBuilder builder);
}
