﻿using AmongUs.Data;

namespace Nebula.Patches;

[HarmonyPatch(typeof (HudManager),nameof(HudManager.Start))]
public static class CameraLayerPatch
{
    static void Postfix(HudManager __instance)
    {
        Camera.main.cullingMask |= 1 << LayerExpansion.GetShadowObjectsLayer();
    }
}

[HarmonyPatch(typeof(ShadowCamera), nameof(ShadowCamera.OnEnable))]
public static class ShadowCameraLayerPatch
{
    static void Postfix(ShadowCamera __instance)
    {
        __instance.GetComponent<Camera>().cullingMask |= 1 << LayerExpansion.GetShadowObjectsLayer();
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class UpdatePatch
{
    private static SpriteRenderer FS_DeathGuage;

    static private void UpdateFullScreen()
    {
        if (!PlayerControl.LocalPlayer) return;
        if (PlayerControl.LocalPlayer.GetModData() == null) return;

        if (!FS_DeathGuage)
        {
            FS_DeathGuage = GameObject.Instantiate(HudManager.Instance.FullScreen, HudManager.Instance.transform);
            FS_DeathGuage.color = Palette.ImpostorRed.AlphaMultiplied(0f);
            FS_DeathGuage.enabled = true;
            FS_DeathGuage.gameObject.SetActive(true);
        }

        if (PlayerControl.LocalPlayer.Data.IsDead)
            FS_DeathGuage.color = Palette.ClearWhite;
        else if (FS_DeathGuage.color.a != PlayerControl.LocalPlayer.GetModData().DeathGuage * 0.25f)
            FS_DeathGuage.color = Palette.ImpostorRed.AlphaMultiplied(PlayerControl.LocalPlayer.GetModData().DeathGuage * 0.25f);
    }

    static private bool CannotSeeNameTag(PlayerControl player)
    {
        return
            (player.GetModData().Attribute.HasAttribute(Game.PlayerAttribute.Invisible) && player != PlayerControl.LocalPlayer && !Game.GameData.data.myData.CanSeeEveryoneInfo)
            || (player == PlayerControl.LocalPlayer && EyesightPatch.ObserverMode)
            || (player.GetModData().Property.UnderTheFloor);
    }

    static private bool IsInvisible(PlayerControl player)
    {
        bool flag =
            (player == PlayerControl.LocalPlayer && EyesightPatch.ObserverMode)
            || (player.GetModData().Property.UnderTheFloor)
            || (player.inVent && !player.walkingToVent)
            || (!PlayerControl.LocalPlayer.Data.IsDead && player.Data.IsDead);

        if (!flag)
        {
            Helpers.RoleAction(PlayerControl.LocalPlayer, (r) => flag |= r.CannotSee(player));
        }

        return flag;
    }

    static private Color rewriteImpostorColor(Game.PlayerData player, Color currentColor, Color impostorColor)
    {
        if (player.role.category == Roles.RoleCategory.Impostor)
        {
            return impostorColor;
        }
        else
        {
            if (player.IsMyPlayerData())
            {
                if (player.role.DeceiveImpostorInNameDisplay)
                {
                    return Palette.ImpostorRed;
                }
            }

            if (player.role.DeceiveImpostorInNameDisplay)
            {
                return impostorColor;
            }
        }
        return currentColor;
    }

    static private bool AnyShadowsBetween(Vector2 pos, Vector2 target)
    {
        var vector = target - pos;
        return Helpers.AnyShadowsBetween(pos, vector.normalized, vector.magnitude);
    }

    static void ResetNameTagsAndColors()
    {
        if (PlayerControl.LocalPlayer == null) return;
        if (Game.GameData.data == null) return;

        Color? impostorColor = null;
        if (Game.GameData.data.myData.getGlobalData().role.CanKnowImpostors)
        {
            impostorColor = Palette.ImpostorRed;
        }
        else
        {
            impostorColor = Color.white;
        }



        string name;
        Game.PlayerData? playerData;
        bool hideFlag;
        foreach (PlayerControl player in PlayerControl.AllPlayerControls.GetFastEnumerator())
        {
            try
            {
                playerData = Game.GameData.data.playersArray[player.PlayerId];
            }
            catch { continue; }
            if (playerData == null) continue;

            /* 表示・非表示を設定する */

            player.Visible = !IsInvisible(player);

            if (player.MyPhysics?.Animations.glowAnimator != null)
            {
                player.MyPhysics.Animations.glowAnimator.gameObject.SetActive(player.Visible && !ShipStatus.Instance);
            }



            /* 名前を編集する */
            name = "";
            hideFlag = playerData.currentName.Length == 0;

            Helpers.RoleAction(player, (role) => { role.EditDisplayName(player.PlayerId, ref name, hideFlag); });
            Helpers.RoleAction(PlayerControl.LocalPlayer, (role) => { role.EditOthersDisplayName(player.PlayerId, ref name, hideFlag); });

            {
                var vec = player.cosmetics.nameText.transform.localPosition;
                vec.z = -3f;
                player.cosmetics.nameText.transform.localPosition = vec;
            }

            player.cosmetics.nameText.text = playerData.currentName;
            if (Game.GameData.data.myData.CanSeeEveryoneInfo && playerData.currentName != playerData.name) player.cosmetics.nameText.text += Helpers.cs(new Color(0.65f, 0.65f, 0.65f), $" ({playerData.name})");
            player.cosmetics.nameText.text += " " + name;
            if (player == PlayerControl.LocalPlayer)
            {
                //自分自身ならロールの色にする
                if(playerData.ShouldBeGhostRole)
                    player.cosmetics.nameText.color = playerData.ghostRole.Color;
                else
                    player.cosmetics.nameText.color = playerData.role.Color;
            }
            else
            {
                player.cosmetics.nameText.color = Color.white;
            }
            player.cosmetics.nameText.color = rewriteImpostorColor(playerData, player.cosmetics.nameText.color, (Color)impostorColor);

            //ロールによる色の変更
            Color color = player.cosmetics.nameText.color;
            Helpers.RoleAction(player.PlayerId, (role) => { role.EditDisplayNameColor(player.PlayerId, ref color); });
            Helpers.RoleAction(PlayerControl.LocalPlayer.PlayerId, (role) => { role.EditOthersDisplayNameColor(player.PlayerId, ref color); });
            player.cosmetics.nameText.color = color;


            bool showNameFlag = !CannotSeeNameTag(player);



            //自分自身以外の名前は適宜隠す
            if (!PlayerControl.LocalPlayer.Data.IsDead && player != PlayerControl.LocalPlayer && showNameFlag)
            {
                var targetPos = player.transform.position;

                var result = AnyShadowsBetween(PlayerControl.LocalPlayer.transform.position, targetPos);
                if (result && (PlayerControl.LocalPlayer.transform.position - targetPos).magnitude < PlayerControl.LocalPlayer.lightSource.viewDistance + 5f)
                {
                    //ある程度近いプレイヤーはより精密に判定する
                    var norm = (targetPos - PlayerControl.LocalPlayer.transform.position).normalized * 0.22f;

                    result &= AnyShadowsBetween(PlayerControl.LocalPlayer.transform.position,
                        (Vector2)targetPos + new Vector2(norm.y, norm.x));
                    result &= AnyShadowsBetween(PlayerControl.LocalPlayer.transform.position,
                        (Vector2)targetPos + new Vector2(-norm.y, -norm.x));

                }
                showNameFlag &= !result;

            }



            player.cosmetics.nameText.enabled = showNameFlag;
            player.cosmetics.colorBlindText.gameObject.SetActive(showNameFlag && DataManager.Settings.Accessibility.ColorBlindMode);
            
            if (player.cosmetics.colorBlindText.transform.localPosition.z > -2f)
            {
                //色の表示をより手前に移動
                var locPos = player.cosmetics.colorBlindText.transform.localPosition;
                locPos.z = -2f;
                player.cosmetics.colorBlindText.transform.localPosition = locPos;
            }
        }

        if (MeetingHud.Instance != null)
        {
            foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
            {
                playerData = Game.GameData.data.playersArray[player.TargetPlayerId];
                if (playerData == null) continue;

                /* 名前を編集する */
                name = "";
                string prefix = "<line-height=80%>";
                Helpers.RoleAction(player.TargetPlayerId, (role) => { role.EditDisplayName(player.TargetPlayerId, ref name, false); });
                Helpers.RoleAction(PlayerControl.LocalPlayer, (role) => { role.EditOthersDisplayName(player.TargetPlayerId, ref name, false); });
                if (!name.Equals(""))
                    player.NameText.text = prefix + playerData.name + " " + name;
                else
                    player.NameText.text = prefix + playerData.name;

                if (player.TargetPlayerId == PlayerControl.LocalPlayer.PlayerId)
                {
                    //自分自身ならロールの色にする
                    player.NameText.color = playerData.ShouldBeGhostRole ? playerData.ghostRole.Color : playerData.role.Color;
                }
                else
                {
                    player.NameText.color = Color.white;
                }

                player.NameText.color = rewriteImpostorColor(playerData, player.NameText.color, (Color)impostorColor);

                //色の変更
                Color color = player.NameText.color;
                Helpers.RoleAction(player.TargetPlayerId, (role) => { role.EditDisplayNameColor(player.TargetPlayerId, ref color); });
                Helpers.RoleAction(PlayerControl.LocalPlayer.PlayerId, (role) => { role.EditOthersDisplayNameColor(player.TargetPlayerId, ref color); });
                player.NameText.color = color;

            }
        }

    }

    private static DeadBody? GetDeadBody(byte playerId, DeadBody[] deadBodies)
    {
        foreach (DeadBody player in deadBodies)
        {
            if (player.ParentId == playerId)
            {
                return player;
            }
        }
        return null;
    }

    public static void UpdateDraggedPlayer()
    {
        Game.PlayerData data;
        DeadBody[] deadBodies = Helpers.AllDeadBodies();
        DeadBody? deadBody;
        float distance;
        Vector3 targetPosition;

        foreach (PlayerControl player in PlayerControl.AllPlayerControls.GetFastEnumerator())
        {
            if (!Game.GameData.data.AllPlayers.ContainsKey(player.PlayerId))
            {
                continue;
            }
            data = Game.GameData.data.playersArray[player.PlayerId];

            if (data.dragPlayerId == Byte.MaxValue)
            {
                continue;
            }

            deadBody = GetDeadBody(data.dragPlayerId, deadBodies);

            if ((deadBody == null) || (!data.IsAlive))
            {
                data.DropPlayer();
            }
            else
            {
                if (player.inVent)
                {
                    deadBody.Reported = true;
                    foreach (var r in deadBody.bodyRenderers) r.enabled = false;
                }
                else
                {
                    deadBody.Reported = false;
                    foreach (var r in deadBody.bodyRenderers) r.enabled = true;
                }
            }

            targetPosition = player.transform.position + new Vector3(-0.1f, -0.1f);
            distance = player.transform.position.Distance(deadBody.transform.position);

            if (distance < 1.8f)
            {
                deadBody.transform.position += (targetPosition - deadBody.transform.position) * 0.15f;
            }
            else
            {
                deadBody.transform.position = targetPosition;
            }
        }
    }

    public static void UpdateImpostorKillButton(HudManager __instance)
    {


        if (MeetingHud.Instance != null) return;
        if (PlayerControl.LocalPlayer.Data.Role.IsImpostor)
        {
            if (Game.GameData.data.myData.getGlobalData().role.HideKillButtonEvenImpostor)
            {
                __instance.KillButton.Hide();
            }
            else
            {
                __instance.KillButton.Show();

                PlayerControl target = PlayerControlPatch.SetMyTarget(!Roles.Roles.Spy.CanKillImpostor());
                __instance.KillButton.SetTarget(target);
                __instance.KillButton.enabled = target != null;
            }
        }
    }

    public static void MapUpdate()
    {
        MapBehaviour __instance = MapBehaviour.Instance;

        bool lastMinimapFlag = MapBehaviorPatch.minimapFlag;

        if (Minigame.Instance)
            MapBehaviorPatch.minimapFlag = true;
        else if (!MeetingHud.Instance && !__instance.countOverlay.gameObject.activeSelf && !__instance.infectedOverlay.gameObject.activeSelf && !PlayerControl.LocalPlayer.Data.IsDead)
        {
            if (Input.GetKeyDown(KeyCode.V)) MapBehaviorPatch.minimapFlag = !MapBehaviorPatch.minimapFlag;
        }
        else
            MapBehaviorPatch.minimapFlag = false;

        if (__instance.IsOpen && lastMinimapFlag != MapBehaviorPatch.minimapFlag) DestroyableSingleton<HudManager>.Instance.SetHudActive(MapBehaviorPatch.minimapFlag);

        MapBehaviorPatch.UpdateMapSize(__instance);
        __instance.transform.GetChild(2).gameObject.SetActive(!MapBehaviorPatch.minimapFlag);
    }

    public static void Postfix(HudManager __instance)
    {
        Module.MetaDialog.Update(); 
        Objects.EffectCircle.Update();


        //アニメーションを無効化
        if (__instance.GameLoadAnimation.active)
        {
            __instance.GameLoadAnimation.SetActive(false);
        }

        try
        {
            if (AmongUsClient.Instance == null) return;
            if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started) return;
            if (!Helpers.HasModData(PlayerControl.LocalPlayer.PlayerId)) return;

            // スクリーンの更新
            UpdateFullScreen();

            // サボタージュを確認
            if (Helpers.SabotageIsActive())
            {
                EmergencyPatch.SabotageUpdate();
            }

            //視界の更新
            Game.GameData.data.myData.Vision.Update();

            // ボタン類の更新 
            CustomButton.HudUpdate();
            ModAbilityButton.HudUpdate();

            if (MapBehaviour.Instance && MapBehaviour.Instance.gameObject.active) MapUpdate();

            Helpers.RoleAction(PlayerControl.LocalPlayer, (role) => { role.MyUpdate(); });

            //死後経過時間を更新
            foreach (Game.DeadPlayerData deadPlayer in Game.GameData.data.deadPlayers.Values)
            {
                deadPlayer.Elapsed += Time.deltaTime;
            }

            //名前タグの更新
            ResetNameTagsAndColors();

            //引きずられているプレイヤーの処理
            UpdateDraggedPlayer();

            //マウス角度の調整
            Vector3 mouseDirection = Input.mousePosition - new Vector3(Screen.width / 2, Screen.height / 2);
            Game.GameData.data.myData.getGlobalData().MouseAngle = Mathf.Atan2(mouseDirection.y, mouseDirection.x);
            Game.GameData.data.myData.getGlobalData().MouseDistance = mouseDirection.magnitude / 100f;

            //インポスターのキルボタンのパッチ
            if (PlayerControl.LocalPlayer.Data.Role.IsImpostor)
            {
                UpdateImpostorKillButton(__instance);
            }

            if (PlayerControl.LocalPlayer.GetModData().role.VentPermission != Roles.VentPermission.CanNotUse)
            {
                //ベントの色の設定
                Color ventColor;
                foreach (Vent vent in ShipStatus.Instance.AllVents)
                {
                    ventColor = PlayerControl.LocalPlayer.GetModData().role.VentColor;
                    vent.myRend.material.SetColor("_OutlineColor", ventColor);

                    if (vent.myRend.material.GetColor("_AddColor").a > 0f)
                        vent.myRend.material.SetColor("_AddColor", ventColor);
                }
            }

            if (Helpers.RoleActionAll(PlayerControl.LocalPlayer.GetModData(),(r)=>r.CanReport)) __instance.ReportButton.Show(); else __instance.ReportButton.Hide();

            Events.GlobalEvent.Update();
            Events.LocalEvent.Update();

            Game.GameData.data.ColliderManager.Update();

            Objects.SoundPlayer.Update();

            Objects.Ghost.Update();

            if (CustomOptionHolder.timeLimitOption.getBool()) Game.GameData.data.TimerUpdate();

            Module.Information.UpperInformationManager.Update();

        }
        catch (NullReferenceException excep) { UnityEngine.Debug.Log(excep.StackTrace); }

    }

}

[HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.HandleHud))]
public static class KeyboardJoystickUpdatePatch
{
    public static void Postfix(KeyboardJoystick __instance)
    {
        if (!DestroyableSingleton<HudManager>.InstanceExists) return;

        if (PlayerControl.LocalPlayer.Data != null && PlayerControl.LocalPlayer.Data.Role != null)
        {
            if (PlayerControl.LocalPlayer.Data.Role.IsImpostor && Input.GetKeyDown(Module.NebulaInputManager.modKillInput.keyCode))
            {
                DestroyableSingleton<HudManager>.Instance.KillButton.DoClick();
            }
            if (Game.GameData.data != null && Helpers.HasModData(PlayerControl.LocalPlayer.PlayerId))
            {
                if (!PlayerControl.LocalPlayer.Data.Role.IsImpostor && Game.GameData.data.myData.getGlobalData().role.VentPermission < Roles.VentPermission.CanUseInUnusualWays && KeyboardJoystick.player.GetButtonDown(50))
                {
                    DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.DoClick();
                }
            }
        }
    }
}

[HarmonyPatch]
public static class SetHudActivePatch
{
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.SetHudActive), typeof(PlayerControl), typeof(RoleBehaviour), typeof(bool)), HarmonyPrefix]
    public static bool Prefix(HudManager __instance, bool isActive)
    {
        if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started)
        {
            __instance.ImpostorVentButton.gameObject.SetActive(false);
            __instance.ReportButton.gameObject.SetActive(false);
            __instance.KillButton.ToggleVisible(false);
        }
        else
        {
            if (isActive)
            {
                try { UpdatePatch.UpdateImpostorKillButton(__instance); }
                catch { }
            }
            else
            {
                __instance.KillButton.ToggleVisible(false);
            }
        }

        __instance.AbilityButton.gameObject.SetActive(false);
        __instance.UseButton.transform.parent.gameObject.SetActive(isActive);
        __instance.TaskPanel.gameObject.SetActive(isActive);
        __instance.roomTracker.gameObject.SetActive(isActive);
        
        IVirtualJoystick virtualJoystick = __instance.joystick;
        if (virtualJoystick != null)
        {
            virtualJoystick.ToggleVisuals(isActive);
        }
        VirtualJoystick virtualJoystick2 = __instance.joystickR;
        if (virtualJoystick2 != null)
        {
            virtualJoystick2.ToggleVisuals(isActive);
        }


        return false;
    }
}