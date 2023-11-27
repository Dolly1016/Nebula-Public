using BepInEx.Unity.IL2CPP.Utils;
using HarmonyLib;
using System.Collections;
using Nebula.Modules;
using UnityEngine.Rendering;
using Nebula.Behaviour;
using static MeetingHud;
using Steamworks;
using System.Reflection;
using Nebula.Map;

namespace Nebula.Patches;

[NebulaRPCHolder]
public static class MeetingModRpc
{
    public static readonly RemoteProcess RpcBreakEmergencyButton = new("BreakEmergencyButton",
        (_) => ShipStatus.Instance.BreakEmergencyButton());

    public static readonly RemoteProcess<(List<VoterState> states, byte exiled, bool tie)> RpcModCompleteVoting = new("CompleteVoting", 
        (writer,message) => {
            writer.Write(message.states.Count);
            foreach(var state in message.states)
            {
                writer.Write(state.VoterId);
                writer.Write(state.VotedForId);
            }
            writer.Write(message.exiled);
            writer.Write(message.tie);
        },
        (reader) => {
            List<VoterState> states = new();
            int statesNum = reader.ReadInt32();
            for (int i = 0; i < statesNum; i++) states.Add(new() { VoterId = reader.ReadByte(), VotedForId = reader.ReadByte() });

            return (states,reader.ReadByte(),reader.ReadBoolean());
        },
        (message, _) => {
            Debug.Log($"Invoked ModVotingComplete (exiled:{message.exiled})");
            ForcelyVotingComplete(MeetingHud.Instance, message.states, message.exiled, message.tie);
        }
        );

    private static void ForcelyVotingComplete(MeetingHud meetingHud, List<VoterState> states, byte exiled, bool tie)
    {
        //追放者とタイ投票の結果だけは必ず書き換える
        meetingHud.exiledPlayer = Helpers.GetPlayer(exiled)?.Data;
        meetingHud.wasTie = tie;

        if (meetingHud.state == MeetingHud.VoteStates.Results) return;

        meetingHud.state = MeetingHud.VoteStates.Results;
        meetingHud.resultsStartedAt = meetingHud.discussionTimer;
        meetingHud.SkipVoteButton.gameObject.SetActive(false);
        meetingHud.SkippedVoting.gameObject.SetActive(true);
        AmongUsClient.Instance.DisconnectHandlers.Remove(meetingHud.TryCast<IDisconnectHandler>());
        for (int i = 0; i < GameData.Instance.PlayerCount; i++)
        {
            PlayerControl @object = GameData.Instance.AllPlayers[i].Object;
            if (@object != null && @object.Data != null && @object.Data.Role) @object.Data.Role.OnVotingComplete();
        }
        meetingHud.PopulateResults(states.ToArray());
        meetingHud.SetupProceedButton();
        try
        {
            MeetingHud.VoterState voterState = states.FirstOrDefault((MeetingHud.VoterState s) => s.VoterId == PlayerControl.LocalPlayer.PlayerId);
            GameData.PlayerInfo playerById = GameData.Instance.GetPlayerById(voterState.VotedForId);
        }
        catch 
        {
        }

        if (DestroyableSingleton<HudManager>.Instance.Chat.IsOpenOrOpening)
        {
            DestroyableSingleton<HudManager>.Instance.Chat.ForceClosed();
            ControllerManager.Instance.CloseOverlayMenu(DestroyableSingleton<HudManager>.Instance.Chat.name);
        }
        ControllerManager.Instance.CloseOverlayMenu(meetingHud.name);
        ControllerManager.Instance.OpenOverlayMenu(meetingHud.name, null, meetingHud.ProceedButtonUi);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
class ReportDeadBodyPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] GameData.PlayerInfo target)
    {
        //会議室が開くか否かのチェック
        if (AmongUsClient.Instance.IsGameOver || MeetingHud.Instance) return false;
        
        //フェイクタスクでない緊急タスクがある場合ボタンは押せない
        if (target == null &&
            PlayerControl.LocalPlayer.myTasks.Find((Il2CppSystem.Predicate<PlayerTask>)
            (task => PlayerTask.TaskIsEmergency(task) && 
                (NebulaGameManager.Instance?.LocalFakeSabotage?.MyFakeTasks.Any(
                    type => ShipStatus.Instance.GetSabotageTask(type)?.TaskType == task.TaskType) ?? false))) == null)
                return false;
            
        

        if (__instance.Data.IsDead) return false;
        MeetingRoomManager.Instance.AssignSelf(__instance, target);
        if (!AmongUsClient.Instance.AmHost) return false;
        
        HudManager.Instance.OpenMeetingRoom(__instance);
        __instance.RpcStartMeeting(target);

        if (target == null)
        {
            NebulaGameManager.Instance!.EmergencyCalls++;
            if (NebulaGameManager.Instance!.EmergencyCalls == GeneralConfigurations.NumOfMeetingsOption) MeetingModRpc.RpcBreakEmergencyButton.Invoke();
        }

        return false;
    }
}


[HarmonyPatch(typeof(LogicOptionsNormal), nameof(LogicOptionsNormal.GetDiscussionTime))]
class GetDiscussionTimePatch
{
    public static void Postfix(LogicOptionsNormal __instance, ref int __result)
    {
        __result -= (int)(GeneralConfigurations.DeathPenaltyOption.GetFloat() * (float)(NebulaGameManager.Instance?.AllPlayerInfo().Count(p => p.IsDead) ?? 0f));
        __result = Mathf.Max(0, __result);
    }
}


[HarmonyPatch(typeof(MeetingIntroAnimation), nameof(MeetingIntroAnimation.CoRun))]
class MeetingIntroStartPatch
{
    public static void Postfix(MeetingIntroAnimation __instance,ref Il2CppSystem.Collections.IEnumerator __result)
    {
        __result = Effects.Sequence(
            __result,
            Effects.Action((Il2CppSystem.Action)(() =>
            {
                NebulaGameManager.Instance?.OnMeetingStart();
            }))
            );
    }
}


[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
class MeetingStartPatch
{
    static private ISpriteLoader LightColorSprite = SpriteLoader.FromResource("Nebula.Resources.ColorLight.png", 100f);
    static private ISpriteLoader DarkColorSprite = SpriteLoader.FromResource("Nebula.Resources.ColorDark.png", 100f);

    class MeetingPlayerContent
    {
        public TMPro.TextMeshPro NameText = null!, RoleText = null!;
        public PlayerModInfo Player = null!;
    }

    static private void Update(List<MeetingPlayerContent> meetingContent)
    {
        foreach(var content in meetingContent)
        {
            try
            {
                if (content.NameText) content.Player.UpdateNameText(content.NameText, true);
                if (content.RoleText) content.Player.UpdateRoleText(content.RoleText);
            }
            catch
            {
                if(content.RoleText) content.RoleText.gameObject.SetActive(false);
            }
        }

        foreach(var p in MeetingHud.Instance.playerStates)
        {
            p.PlayerIcon.cosmetics.skin.layer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            p.PlayerIcon.cosmetics.currentBodySprite.BodySprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }
    }

    static void Postfix(MeetingHud __instance)
    {
        MeetingHudExtension.Reset();

        NebulaManager.Instance.CloseAllUI();

        List<MeetingPlayerContent> allContents = new();

        __instance.transform.localPosition = new Vector3(0f, 0f, -25f);


        //色の明暗を表示
        foreach (var player in __instance.playerStates)
        {
            bool isLightColor = DynamicPalette.IsLightColor(Palette.PlayerColors[player.TargetPlayerId]);

            SpriteRenderer renderer = UnityHelper.CreateObject<SpriteRenderer>("Color", player.transform, new Vector3(1.2f, -0.18f, -1f));
            renderer.sprite = isLightColor ? LightColorSprite.GetSprite() : DarkColorSprite.GetSprite();

            player.ColorBlindName.gameObject.SetActive(false);

            var roleText = GameObject.Instantiate(player.NameText, player.transform);
            roleText.transform.localPosition = new Vector3(0.3384f, -0.13f, -0.02f);
            roleText.transform.localScale = new Vector3(0.57f,0.57f);
            roleText.rectTransform.sizeDelta += new Vector2(0.35f, 0f);

            allContents.Add(new() { Player = NebulaGameManager.Instance!.GetModPlayerInfo(player.TargetPlayerId)!, NameText = player.NameText, RoleText = roleText });

            player.CancelButton.GetComponent<SpriteRenderer>().material = __instance.Glass.material;
            player.ConfirmButton.GetComponent<SpriteRenderer>().material = __instance.Glass.material;
            player.CancelButton.transform.GetChild(0).GetComponent<SpriteRenderer>().maskInteraction = SpriteMaskInteraction.None;
            player.ConfirmButton.transform.GetChild(0).GetComponent<SpriteRenderer>().maskInteraction = SpriteMaskInteraction.None;

        }

        Update(allContents);

        IEnumerator CoUpdate()
        {
            while (true)
            {
                Update(allContents);
                yield return null;
            }
        }
        __instance.StartCoroutine(CoUpdate().WrapToIl2Cpp());
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
class MeetingClosePatch
{ 
    public static void Postfix(MeetingHud __instance)
    {
        NebulaGameManager.Instance?.AllAssignableAction(r => r.OnStartExileCutScene());
        NebulaManager.Instance.CloseAllUI();
    }
}

[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.SetMaskLayer))]
class VoteMaskPatch
{
    public static bool Prefix(PlayerVoteArea __instance)
    {
        return false;
    }
}

[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.SetTargetPlayerId))]
class VoteAreaVCPatch
{
    private static SpriteLoader VCFrameSprite = SpriteLoader.FromResource("Nebula.Resources.MeetingVCFrame.png", 119f);
    public static void Postfix(PlayerVoteArea __instance)
    {
        try
        {
            if (GeneralConfigurations.UseVoiceChatOption)
            {
                var frame = UnityHelper.CreateObject<SpriteRenderer>("VCFrame", __instance.transform, new Vector3(0, 0, -0.5f));
                frame.sprite = VCFrameSprite.GetSprite();
                frame.color = Color.clear;
                var col = Palette.PlayerColors[__instance.TargetPlayerId];
                if(Mathf.Max((int)col.r, (int)col.g, (int)col.b) < 100) col = Color.Lerp(col, Color.white, 0.4f);
                
                var client = NebulaGameManager.Instance?.VoiceChatManager?.GetClient(__instance.TargetPlayerId);
                float alpha = 0f;
                if (client != null)
                {
                    var script = frame.gameObject.AddComponent<ScriptBehaviour>();
                    script.UpdateHandler += () =>
                    {
                        if (client.Level > 0.09f)
                            alpha = Mathf.Clamp(alpha + Time.deltaTime * 4f, 0f, 1f);
                        else
                            alpha = Mathf.Clamp(alpha - Time.deltaTime * 4f, 0f, 1f);
                        col.a = (byte)(alpha * 255f);
                        frame.color = col;
                    };
                }
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.Start))]
class VoteAreaPatch
{
    public static void Postfix(PlayerVoteArea __instance)
    {
        try
        {
            var maskParent = UnityHelper.CreateObject<SortingGroup>("MaskedObjects", __instance.transform, new Vector3(0, 0, -0.1f));
            __instance.MaskArea.transform.SetParent(maskParent.transform);
            __instance.PlayerIcon.transform.SetParent(maskParent.transform);
            __instance.Overlay.maskInteraction = SpriteMaskInteraction.None;
            __instance.Overlay.material = __instance.Megaphone.material;

            var mask = __instance.MaskArea.gameObject.AddComponent<SpriteMask>();
            mask.sprite = __instance.MaskArea.sprite;
            mask.transform.localScale = __instance.MaskArea.size;
            __instance.MaskArea.enabled = false;

            __instance.Background.material = __instance.Megaphone.material;

            __instance.PlayerIcon.cosmetics.currentBodySprite.BodySprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            __instance.PlayerIcon.cosmetics.hat.FrontLayer.gameObject.AddComponent<ZOrderedSortingGroup>();
            __instance.PlayerIcon.cosmetics.hat.BackLayer.gameObject.AddComponent<ZOrderedSortingGroup>();
            __instance.PlayerIcon.cosmetics.visor.Image.gameObject.AddComponent<ZOrderedSortingGroup>();
            __instance.PlayerIcon.cosmetics.skin.layer.gameObject.AddComponent<ZOrderedSortingGroup>();
            __instance.PlayerIcon.cosmetics.skin.layer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            __instance.PlayerIcon.cosmetics.currentBodySprite.BodySprite.gameObject.AddComponent<ZOrderedSortingGroup>();
        }
        catch { }
    }
}



[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Confirm))]
class CastVotePatch
{
    public static bool Prefix(MeetingHud __instance, [HarmonyArgument(0)] byte suspectStateIdx)
    {
        if (PlayerControl.LocalPlayer.Data.IsDead) return false;

        foreach (var state in __instance.playerStates)
        {
            state.ClearButtons();
            state.voteComplete = true;
        }

        __instance.SkipVoteButton.ClearButtons();
        __instance.SkipVoteButton.voteComplete = true;
        __instance.SkipVoteButton.gameObject.SetActive(false);

        if (__instance.state != MeetingHud.VoteStates.NotVoted) return false;
        
        __instance.state = MeetingHud.VoteStates.Voted;
        
        //CmdCastVote(Mod)
        int vote = 1;
        NebulaGameManager.Instance?.GetModPlayerInfo(PlayerControl.LocalPlayer.PlayerId)?.AssignableAction((r) => r.OnCastVoteLocal(suspectStateIdx, ref vote));
        __instance.ModCastVote(PlayerControl.LocalPlayer.PlayerId, suspectStateIdx, vote);
        return false;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CheckForEndVoting))]
static class CheckForEndVotingPatch
{
    public static void AddValue(this Dictionary<byte,int> self, byte target,int num)
    {
        if (self.TryGetValue(target, out var last))
            self[target] = last + num;
        else
            self[target] = num;
    }

    public static Dictionary<byte, int> ModCalculateVotes(MeetingHud __instance)
    {
        Dictionary<byte, int> dictionary = new();

        for (int i = 0; i < __instance.playerStates.Length; i++)
        {
            PlayerVoteArea playerVoteArea = __instance.playerStates[i];
            if (playerVoteArea.VotedFor != 252 && playerVoteArea.VotedFor != 255 && playerVoteArea.VotedFor != 254)
            {
                if (!MeetingHudExtension.WeightMap.TryGetValue((byte)i, out var vote)) vote = 1;
                dictionary.AddValue(playerVoteArea.VotedFor,vote);
            }
        }
        
        return dictionary;
    }

    public static KeyValuePair<byte, int> MaxPair(this Dictionary<byte, int> self, out bool tie)
    {
        tie = true;
        KeyValuePair<byte, int> result = new KeyValuePair<byte, int>(byte.MaxValue, int.MinValue);
        foreach (KeyValuePair<byte, int> keyValuePair in self)
        {
            if (keyValuePair.Value > result.Value)
            {
                result = keyValuePair;
                tie = false;
            }
            else if (keyValuePair.Value == result.Value)
            {
                tie = true;
            }
        }
        return result;
    }

    public static bool Prefix(MeetingHud __instance)
    {
        //投票が済んでない場合、なにもしない
        if (!__instance.playerStates.All((PlayerVoteArea ps) => ps.AmDead || ps.DidVote)) return false;

        {
            Dictionary<byte, int> dictionary = ModCalculateVotes(__instance);
            KeyValuePair<byte, int> max = dictionary.MaxPair(out bool tie);

            List<byte> extraVotes = new();

            if (tie)
            {
                foreach (var state in __instance.playerStates)
                {
                    if (!state.DidVote) continue;

                    var modInfo = NebulaGameManager.Instance?.GetModPlayerInfo(state.TargetPlayerId);
                    modInfo?.AssignableAction(r=>r.OnTieVotes(ref extraVotes,state));
                }

                foreach (byte target in extraVotes) dictionary.AddValue(target, 1);

                //再計算する
                max = dictionary.MaxPair(out tie);
            }


            GameData.PlayerInfo exiled = null!;
            try
            {
                exiled = GameData.Instance.AllPlayers.Find((Il2CppSystem.Predicate<GameData.PlayerInfo>)((GameData.PlayerInfo v) => !tie && v.PlayerId == max.Key));
            }
            catch { }
            List<MeetingHud.VoterState> allStates = new();

            //記名投票分
            foreach (var state in __instance.playerStates)
            {
                if (!state.DidVote) continue;

                if (!MeetingHudExtension.WeightMap.TryGetValue((byte)state.TargetPlayerId, out var vote)) vote = 1;

                for (int i = 0; i < vote; i++)
                {
                    allStates.Add(new MeetingHud.VoterState
                    {
                        VoterId = state.TargetPlayerId,
                        VotedForId = state.VotedFor
                    });
                }
            }

            //追加投票分
            foreach(var votedFor in extraVotes)
            {
                allStates.Add(new MeetingHud.VoterState
                {
                    VoterId = byte.MaxValue,
                    VotedForId = votedFor
                });
            }

            allStates.Add(new() { VoterId = byte.MaxValue-1});
            //__instance.RpcVotingComplete(allStates.ToArray(), exiled, tie);
            MeetingModRpc.RpcModCompleteVoting.Invoke((allStates, exiled?.PlayerId ?? byte.MaxValue, tie));


        }

        return false;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
class CancelVotingCompleteDirectlyPatch
{
    public static bool Prefix(MeetingHud __instance)
    {
        Debug.Log($"Canceled VotingComplete Directly");
        return false;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.HandleRpc))]
class CancelVotingCompleteByRPCPatch
{
    public static bool Prefix(MeetingHud __instance, [HarmonyArgument(0)] byte callId)
    {
        if (callId == 23)
        {
            Debug.Log($"Canceled VotingComplete on HandleRpc");
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.PopulateResults))]
class PopulateResultPatch
{
    private static void ModBloopAVoteIcon(MeetingHud __instance,GameData.PlayerInfo? voterPlayer, int index, Transform parent,bool isExtra)
    {
        SpriteRenderer spriteRenderer = GameObject.Instantiate<SpriteRenderer>(__instance.PlayerVotePrefab);
        if (GameManager.Instance.LogicOptions.GetAnonymousVotes() || voterPlayer == null)
            PlayerMaterial.SetColors(Palette.DisabledGrey, spriteRenderer);
        else
            PlayerMaterial.SetColors(voterPlayer.DefaultOutfit.ColorId, spriteRenderer);
        
        spriteRenderer.transform.SetParent(parent);
        spriteRenderer.transform.localScale = Vector3.zero;
        __instance.StartCoroutine(Effects.Bloop((float)index * 0.3f + (isExtra ? 0.85f : 0f), spriteRenderer.transform, 1f, isExtra ? 0.5f : 0.7f));

        if (isExtra)
            __instance.StartCoroutine(Effects.Sequence(Effects.Wait((float)index * 0.3f + 0.85f), ManagedEffects.Action(() => parent.GetComponent<VoteSpreader>().AddVote(spriteRenderer)).WrapToIl2Cpp()));
        else
            parent.GetComponent<VoteSpreader>().AddVote(spriteRenderer);
    }


    public static bool Prefix(MeetingHud __instance, [HarmonyArgument(0)]Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<MeetingHud.VoterState> states)
    {
        Debug.Log("Called PopulateResults");

        NebulaGameManager.Instance?.AllAssignableAction(r => r.OnEndVoting());

        __instance.TitleText.text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.MeetingVotingResults);
        foreach (var voteArea in __instance.playerStates)
        {
            voteArea.ClearForResults();
            MeetingHudExtension.LastVotedForMap[voteArea.TargetPlayerId]= voteArea.VotedFor;
        }

        int lastVoteFor = -1;
        int num = 0;
        Transform? voteFor = null;

        //OrderByは安定ソート
        foreach (var state in states.OrderBy(s => s.VotedForId)){
            if (state.VoterId == byte.MaxValue - 1) continue;
            if(state.VotedForId != lastVoteFor)
            {
                lastVoteFor = state.VotedForId;
                num = 0;
                if (state.SkippedVote)
                    voteFor = __instance.SkippedVoting.transform;
                else
                    voteFor = __instance.playerStates.FirstOrDefault((area) => area.TargetPlayerId == lastVoteFor)?.transform ?? null;
            }

            if (voteFor != null)
            {
                GameData.PlayerInfo? playerById = GameData.Instance.GetPlayerById(state.VoterId);

                ModBloopAVoteIcon(__instance, playerById, num, voteFor, state.VoterId == byte.MaxValue);
                num++;
            }
        }

        return false;
    }
}