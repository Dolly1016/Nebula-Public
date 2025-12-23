using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Compat;
using Virial.Text;

namespace Nebula.AeroGuesser;

internal interface IMapButtonInteraction : IWithAnswerPhase
{
    int MapMask { get; }
    float MapQ { get; }
    void SelectMap(byte mapId);
}

internal class MapButtons
{
    GameObject mapButtonsHolder;
    GameObject clickGuard;
    List<(int mapId, GameObject button, SpriteRenderer renderer)> mapButtons = [];
    const int MapButtonPerLine = 3;
    private IMapButtonInteraction mapButtonInteraction;
    internal MapButtons(Transform parent, IMapButtonInteraction interaction)
    {
        this.mapButtonInteraction = interaction;
        SetUpMapButtons(interaction.MapMask, parent);
    }

    private void SetUpMapButtons(int mapMask, Transform transform)
    {
        mapButtonsHolder = UnityHelper.CreateObject("MapButtons", transform, new(0f, -1.5f, -10f));
        var script = mapButtonsHolder.AddComponent<ScriptBehaviour>();
        script.UpdateHandler += () => mapButtonsHolder.transform.localPosition = new(-mapButtonInteraction.MapQ * 12f, -1.5f - mapButtonInteraction.AnswerQ * 4f, -10f);
        script.InvokeUpdateOnEnabed = true;

        int mask = mapMask;
        byte mapId = 0;

        TextAttribute attr = new TextAttribute(GUI.API.GetAttribute(AttributeAsset.CenteredBoldFixed)) { Size = new(2.4f, 0.6f) };
        while (mask != 0)
        {
            if ((mask & 1) != 0)
            {
                byte copiedMapId = mapId;
                SpriteRenderer renderer = null!;
                var button = new GUIButton(Virial.Media.GUIAlignment.Center, attr, GUI.API.RawTextComponent(""))
                {
                    OnClick = _ => SelectMap(copiedMapId),
                    OnMouseOver = _ => renderer.color = Color.green,
                    OnMouseOut = _ => renderer.color = Color.white,
                    PostBuilder = text =>
                    {
                        renderer = UnityHelper.CreateObject<SpriteRenderer>("MapSprite", text.transform, new(0f, 0f, -0.1f));
                        renderer.transform.localScale = new(0.7f, 0.7f, 1f);
                        renderer.sprite = VanillaAsset.MapImages.Find(info => (int)info.Name == copiedMapId, out var found) ? found.NameImage : null;
                    }
                };

                var instantiated = button.Instantiate(new(10f, 10f), out _);

                mapButtons.Add((mapId, instantiated!, renderer));
            }
            mapId++;
            mask >>= 1;
        }

        int lines = ((mapButtons.Count - 1) / MapButtonPerLine) + 1;
        for (int i = 0; i < mapButtons.Count; i++)
        {
            var (id, button, renderer) = mapButtons[i];
            int line = i / MapButtonPerLine;
            int column = i % MapButtonPerLine;
            int itemsInThisLine = (line + 1 == lines) ? mapButtons.Count % MapButtonPerLine : MapButtonPerLine;
            if (itemsInThisLine == 0) itemsInThisLine = MapButtonPerLine;
            float xOffset = (column - (itemsInThisLine - 1) / 2f) * 2.75f;
            float yOffset = (line - (lines - 1) / 2f) * -0.9f;
            button.transform.SetParent(mapButtonsHolder.transform);
            button.transform.localPosition = new(xOffset, yOffset, 0f);
        }

        clickGuard = UnityHelper.CreateObject("Clickguard", mapButtonsHolder.transform, new(0f, 0f, -0.1f));
        clickGuard.SetUpButton();
        var collider = clickGuard.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new(6f, 2f);
    }

    private bool canSelect = false;

    private void SelectMap(byte mapId) {
        if (!canSelect) return;
        canSelect = false;
        clickGuard.SetActive(true);

        IEnumerator CoSelect(byte mapId)
        {
            var selected = mapButtons.Find(mapButtons => mapButtons.mapId == mapId);
            for(int i = 0; i < 2; i++)
            {
                selected.renderer.enabled = false;
                yield return Effects.Wait(0.04f);
                selected.renderer.enabled = true;
                yield return Effects.Wait(0.04f);
            }
            mapButtonInteraction.SelectMap(mapId);
        }

        CoSelect(mapId).StartOnScene();
    }

    public void SetActive(bool active)
    {
        mapButtonsHolder.SetActive(active);
    }
    

    public void Activate() {
        SetActive(true);
        canSelect = true; 
        clickGuard.SetActive(false);
    }
}
