﻿using Nebula.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.Rendering;
using Virial.Game;

namespace Nebula.Modules;

public record PlayerIcon(byte playerId, GameObject iconObj, PoolablePlayer display, PlayerControl relatedControl) { public Outfit? LastOutfit; public Color32 LastColor; }

public class PlayersOverlay : IGameEntity
{
    static private IDividedSpriteLoader iconSprite = DividedSpriteLoader.FromResource("Nebula.Resources.OverlayIcon.png", 100f, 2, 1);

    private List<PlayerIcon> allIcons = new();
    GameObject IconHolder = null!;
    BitMask<PlayerControl>? mask = null;

    public PlayersOverlay()
    {
        IconHolder = UnityHelper.CreateObject("IconHolder", HudManager.Instance.transform, new(0, 2.7f, -120f));
    }

    public PlayersOverlay BindMask(BitMask<PlayerControl> mask)
    {
        this.mask = mask;
        return this;
    }

    void IGameEntity.HudUpdate()
    {
        if((MeetingHud.Instance && !ExileController.Instance) || PlayerCustomizationMenu.Instance || GameSettingMenu.Instance)
        {
            IconHolder.gameObject.SetActive(false);
            return;
        }
        IconHolder.gameObject.SetActive(true);

        allIcons.RemoveAll(p =>
        {
            if (!p.relatedControl)
            {
                GameObject.Destroy(p.iconObj);
                return true;
            }
            return false;
        });

        if (AmongUsClient.Instance.GameState < InnerNet.InnerNetClient.GameStates.Started) {
            if (allIcons.Count != PlayerControl.AllPlayerControls.Count) {
                foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
                {
                    if (!allIcons.Any(i => i.playerId == p.PlayerId) && !p.gameObject.TryGetComponent<UncertifiedPlayer>(out _))
                    {
                        var obj = UnityHelper.CreateObject("Icon", IconHolder.transform, Vector3.zero);
                        obj.AddComponent<SortingGroup>();

                        var back = UnityHelper.CreateObject<SpriteRenderer>("Back", obj.transform, new(0, 0, 0.1f));
                        back.sprite = iconSprite.GetSprite(0);
                        back.color = new(0.45f, 0.45f, 0.45f);

                        var front = UnityHelper.CreateObject<SpriteRenderer>("Front", obj.transform, new(0, 0, 0.05f));
                        front.sprite = iconSprite.GetSprite(1);
                        front.color = new(0.23f, 0.23f, 0.23f);

                        var mask = UnityHelper.CreateObject<SpriteMask>("Mask", obj.transform, Vector3.zero);
                        mask.sprite = iconSprite.GetSprite(1);

                        var player = AmongUsUtil.GetPlayerIcon(p.CurrentOutfit, mask.transform, new Vector3(0, -0.3f, -1f), Vector3.one);
                        player.cosmetics.SetMaskType(PlayerMaterial.MaskType.ComplexUI);
                        player.transform.localScale = new(0.65f, 0.65f, 0.65f);
                        obj.transform.localScale = new(0.4f, 0.4f, 0.4f);

                        player.cosmetics.skin.layer.gameObject.AddComponent<ZOrderedSortingGroup>();
                        player.cosmetics.hat.FrontLayer.gameObject.AddComponent<ZOrderedSortingGroup>();
                        player.cosmetics.hat.BackLayer.gameObject.AddComponent<ZOrderedSortingGroup>();
                        player.cosmetics.visor.Image.gameObject.AddComponent<ZOrderedSortingGroup>();
                        player.cosmetics.currentBodySprite.BodySprite.gameObject.AddComponent<ZOrderedSortingGroup>();

                        allIcons.Add(new(p.PlayerId, obj, player, p) { LastColor = Palette.PlayerColors[player.ColorId], LastOutfit = new(p.CurrentOutfit) });
                    }
                }
            }

            foreach(var i in allIcons)
            {
                if(!(i.LastOutfit?.Equals(i.relatedControl.CurrentOutfit) ?? true) || !Palette.PlayerColors[i.LastOutfit!.outfit.ColorId].CompareRGB(i.LastColor))
                {
                    Debug.Log("LastColor: " + i.LastColor);
                    Debug.Log("CurrentColor: " + Palette.PlayerColors[i.LastOutfit!.outfit.ColorId]);
                    Debug.Log("LastHat: " + i.LastOutfit.outfit.HatId);
                    Debug.Log("LastVisor: " + i.LastOutfit.outfit.VisorId);
                    Debug.Log("LastColor: " + i.LastOutfit.outfit.ColorId);
                    Debug.Log("LastSkin: " + i.LastOutfit.outfit.SkinId);
                    Debug.Log("CurrentHat: " + i.relatedControl.CurrentOutfit.HatId);
                    Debug.Log("CurrentVisor: " + i.relatedControl.CurrentOutfit.VisorId);
                    Debug.Log("CurrentColor: " + i.relatedControl.CurrentOutfit.ColorId);
                    Debug.Log("CurrentSkin: " + i.relatedControl.CurrentOutfit.SkinId);

                    i.display.UpdateFromPlayerOutfit(i.relatedControl.CurrentOutfit, PlayerMaterial.MaskType.ComplexUI, false, false);
                    i.LastOutfit = new(i.relatedControl.CurrentOutfit, true);
                    i.LastColor = Palette.PlayerColors[i.LastOutfit.outfit.ColorId];
                }
            }
        }

        if(mask != null)
        {
            int num = 0;
            foreach (var i in allIcons)
            {
                i.iconObj.SetActive(mask.Test(i.relatedControl));
                if (i.iconObj.active)
                {
                    i.iconObj.transform.localPosition = new(0.45f * num, 0f);
                    num++;
                }
            }

            foreach (var i in allIcons) if (i.iconObj.active) i.iconObj.transform.localPosition -= new Vector3(0.225f * (num - 1), 0f, 0f);
        }
    }

    void IGameEntity.OnOutfitChanged(Virial.Game.Player player, Virial.Game.Outfit outfit)
    {
        allIcons.FirstOrDefault(i => i.playerId == player.PlayerId)?.display.UpdateFromPlayerOutfit(outfit.outfit, PlayerMaterial.MaskType.ComplexUI, false, false);
    } 
}