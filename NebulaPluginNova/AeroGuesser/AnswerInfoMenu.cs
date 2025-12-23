using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering;

namespace Nebula.AeroGuesser;

internal interface IAnswerMenuInteraction
{
    void ShowAnswerInfo(int index);
    void ProceedAsHost();
}

internal class AnswerInfoMenu
{
    private IAnswerMenuInteraction interaction;
    public AnswerInfoMenu(Transform transform, IAnswerMenuInteraction interaction) { 
        this.interaction = interaction;
        SetUp(transform);
    }

    private int currentIndex = 2;
    private int length = 3;
    private TMPro.TextMeshPro text = null!;
    private GameObject holder = null;
    private GameObject hostObj = null;
    private void SetUp(Transform transform)
    {
        var buttonAttr = GUI.API.GetAttribute(Virial.Text.AttributeAsset.SmallArrowButton);
        holder = GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Center,
            GUI.API.RawButton(Virial.Media.GUIAlignment.Center, buttonAttr, "<<", _ =>
            {
                currentIndex = (currentIndex - 1 + length) % length;
                interaction.ShowAnswerInfo(currentIndex);
                UpdateText();
            }),
            new NoSGUIText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.MarketplaceTabNonMaskedButton), GUI.API.RawTextComponent(""))
            {
                PostBuilder = text =>
                {
                    this.text = text;
                }
            },
            GUI.API.RawButton(Virial.Media.GUIAlignment.Center, buttonAttr, ">>", _ =>
            {
                currentIndex = (currentIndex + 1) % length;
                interaction.ShowAnswerInfo(currentIndex);
                UpdateText();
            })
            ).Instantiate(new(10f, 10f), out _)!;

        holder.transform.SetParent(transform);
        holder.AddComponent<SortingGroup>();
        holder.transform.localPosition = new Vector3(0f, -2f, -10f);

        hostObj = GUI.API.LocalizedButton(Virial.Media.GUIAlignment.Center, Virial.Text.AttributeAsset.MarketplaceTabNonMaskedButton, "aeroGuesser.ui.proceed", _ =>
        {
            interaction.ProceedAsHost();
        }).Instantiate(new(10f,10f), out _)!;
        hostObj.transform.SetParent(holder.transform);
        hostObj.transform.localPosition = new Vector3(3f, -0.2f, 0f);
        hostObj.SetActive(false);

        holder.AddComponent<ScriptBehaviour>().UpdateHandler += () =>
        {
            hostObj.SetActive(AmongUsClient.Instance.AmHost);
        };

        holder.SetActive(false);
    }
    private static string[] translationKeys = [
        "aeroguesser.answerInfo.view",
        "aeroguesser.answerInfo.minimap",
        "aeroguesser.answerInfo.singleScore",
        "aeroguesser.answerInfo.sumScore",
        ];
    private void UpdateText()
    {
        text.text = Language.Translate(translationKeys[currentIndex]);
    }
    public void Show(bool canSeeSumRanking) {
        length = canSeeSumRanking ? 4 : 3;
        currentIndex = length - 1;
        UpdateText();
        holder.SetActive(true);
    }
    public void Hide() { 
        holder.SetActive(false);
    }
}
