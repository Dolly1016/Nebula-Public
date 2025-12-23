using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.Rendering;
using Virial.Text;

namespace Nebula.AeroGuesser;

internal interface IScoreBoardViewerInteraction
{
    float ScoreBoardQ { get; }
}

internal class ScoreBoardShower
{
    private IScoreBoardViewerInteraction interaction;
    private IFunctionalValue<float> contentQ = Arithmetic.FloatZero;
    internal ScoreBoardShower(Transform transform, IScoreBoardViewerInteraction interaction)
    {
        this.interaction = interaction;
        SetUp(transform);
    }

    private static TextAttribute NameAttr = GUI.API.GenerateAttribute(
        (AttributeParams)(AttributeTemplateFlag.FontBarlow | AttributeTemplateFlag.MaterialBared | AttributeTemplateFlag.AlignmentLeft),
        Virial.Color.White,
        new FontSize(1.6f, false),
        new(2.7f, 1f)
        );
    private static TextAttribute ScoreAttr = GUI.API.GenerateAttribute(
        (AttributeParams)(AttributeTemplateFlag.FontBarlow | AttributeTemplateFlag.MaterialBared | AttributeTemplateFlag.AlignmentRight),
        Virial.Color.White,
        new FontSize(1.6f, false),
        new(1.2f, 1f)
        );
    private (GamePlayer player, GameObject holder, TMPro.TextMeshPro nameText, TMPro.TextMeshPro scoreText)[] texts;
    private GameObject holder;
    private bool requireUpdate = false;
    
    private class RankingEntry
    {
        public int SingleRank { get; set; }
        public int SingleScore { get; set; }
        public int SumRank { get; set; }
        public int SumScore { get; set; }
    }
    private void SetUp(Transform transform)
    {
        holder = UnityHelper.CreateObject("RankingHolder", transform, new(0f, 0f, -20f));
        holder.AddComponent<SortingGroup>();

        TMPro.TextMeshPro nameText = null!, scoreText = null!;
        var nameTextWidget = new NoSGUIText(Virial.Media.GUIAlignment.Center, NameAttr, GUI.API.RawTextComponent("")) { PostBuilder = text => nameText = text };
        var scoreTextWidget = new NoSGUIText(Virial.Media.GUIAlignment.Center, ScoreAttr, GUI.API.RawTextComponent("")) { PostBuilder = text => scoreText = text };
        texts = GamePlayer.AllPlayers.Select(p =>
        {
            var myHolder = UnityHelper.CreateObject(p.Name, holder.transform, new(0f, 0f, 0f));
            var nameObj = nameTextWidget.Instantiate(new(10f, 10f), out _);
            var scoreObj = scoreTextWidget.Instantiate(new(10f, 10f), out _);
            nameObj!.transform.SetParent(myHolder.transform);
            nameObj.transform.localPosition = new(-0.2f, 0f, p.PlayerId * 0.002f);
            scoreObj!.transform.SetParent(myHolder.transform);
            scoreObj.transform.localPosition = new(1f, 0f, p.PlayerId * 0.002f);
            nameText.text = p.PlayerName;
            return (p, myHolder, nameText, scoreText);
        }).ToArray();

        float lastUpdateQ = -1f;
        holder.AddComponent<ScriptBehaviour>().UpdateHandler += () =>
        {
            var alpha = interaction.ScoreBoardQ;
            foreach(var entry in texts)
            {
                Color color = entry.player.AmOwner ? Color.yellow : Color.white;
                color = color.AlphaMultiplied(alpha);
                entry.nameText.color = color;
                entry.scoreText.color = color;
            }
            if(alpha > 0f && rankingCache != null)
            {
                float q = contentQ.Value;
                if (lastUpdateQ != q || requireUpdate)
                {
                    int counts = rankingCache.Count;
                    foreach(var text in texts)
                    {
                        if (!rankingCache.TryGetValue(text.player.PlayerId, out var entry)) continue;

                        int score = (int)Mathn.Lerp(entry.SingleScore, entry.SumScore, q);
                        float pos = Mathn.Lerp(entry.SingleRank, entry.SumRank, q);
                        text.scoreText.text = score + "pt";
                        text.holder.transform.localPosition = new(0f, ((counts - 1) * 0.5f - pos) * 0.25f, 0f);
                    }

                    lastUpdateQ = q;
                    requireUpdate = false;
                }
            }
        };
    }

    public void ShowContent(float num, bool immediately, bool bloop = false)
    {
        if (immediately)
            contentQ = Arithmetic.Constant(num);
        else
        {
            contentQ = Arithmetic.Decel(contentQ.Value, num, 0.45f);
            if (bloop) NebulaManager.Instance.StartCoroutine(Effects.Bloop(0.45f, holder.transform));
        }
        requireUpdate = true;
    }


    Dictionary<byte, RankingEntry>? rankingCache = null;
    static private Dictionary<byte, RankingEntry> GetRanking(ScoreMap scoreMap)
    {
        var dic = new Dictionary<byte, RankingEntry>();
        foreach (var p in GamePlayer.AllPlayers) dic[p.PlayerId] = new();

        var singleRanking = GamePlayer.AllPlayers.Select(p => (p, scoreMap.GetLastAddedScore(p.PlayerId))).OrderBy(entry => -entry.Item2).ToArray();
        var sumRanking = GamePlayer.AllPlayers.Select(p => (p, scoreMap.GetScore(p.PlayerId))).OrderBy(entry => -entry.Item2).ToArray();

        for (int i = 0; i < singleRanking.Length; i++)
        {
            var entry = dic[singleRanking[i].p.PlayerId];
            entry.SingleRank = i;
            entry.SingleScore = singleRanking[i].Item2;
        }
        for (int i = 0; i < sumRanking.Length; i++)
        {
            var entry = dic[sumRanking[i].p.PlayerId];
            entry.SumRank = i;
            entry.SumScore = sumRanking[i].Item2;
        }
        return dic;
    }

    public void UpdateRanking(ScoreMap scoreMap)
    {
        rankingCache = GetRanking(scoreMap);
    }
}
