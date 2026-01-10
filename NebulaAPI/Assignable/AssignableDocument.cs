using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Compat;
using Virial.Helpers;

namespace Virial.Assignable;

public record AssignableDocumentReplacement(string Key, string Replacement);
public record AssignableDocumentImage(Virial.Media.Image Image, string Content);

public interface IAssignableDocument
{
    bool HasWinCondition => false;
    bool HasTips => false;
    bool HasAbility => false;
    IEnumerable<AssignableDocumentReplacement> GetDocumentReplacements() { yield break; }
    IEnumerable<AssignableDocumentImage> GetDocumentImages() { yield break; }

    /// <summary>
    /// 通常のキルクールダウンと比較して「より短い」「と同じ」「より長い」の3パターンに場合分けします。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="translationKey"></param>
    /// <param name="cooldown"></param>
    /// <returns></returns>
    static protected AssignableDocumentReplacement GetComparisonWithKillCooldown(string key, string translationKey, float cooldown) =>
        new(key, NebulaAPI.Language.Translate(translationKey + ((cooldown - NebulaAPI.AmongUs.VanillaKillCooldown) switch
        {
            > 0f => ".longer",
            < 0f => ".shorter",
            _ => ".just"
        })));

    static protected IEnumerable<AssignableDocumentReplacement> GetComparisonWithKillCooldown(string key, string secKey, string translationKey, float cooldown)
    {
        yield return new("%SEC%", cooldown.DecimalToString("1"));
        yield return GetComparisonWithKillCooldown(key, translationKey, cooldown);
    }

    static protected AssignableDocumentReplacement GetKeyInput(string key, VirtualKeyInput input) => new(key, "<client>" + NebulaAPI.Language.Translate(input) + "</client>");
}
