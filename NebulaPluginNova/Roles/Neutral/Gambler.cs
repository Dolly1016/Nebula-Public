using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial;
using Virial.Media;
using Nebula.Modules.GUIWidget;
using Virial.Text;
using UnityEngine.Rendering;
using TMPro;
using Nebula.Behavior;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Neutral;


internal class Gambler : DefinedRoleTemplate, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.gambler", new(216, 194, 79), TeamRevealType.OnlyMe);

    private Gambler() : base("gambler", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [DeceiveCoolDownOption, DeceiveDurationOption, InitialChipsOption, GoalChipsOption, MaxBettingPatternsOption, CanVoteOption, ChipsLimitForNoVoteBettingOption, VentConfiguration])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private FloatConfiguration DeceiveCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.gambler.deceiveCooldown", (0f, 20f, 1f), 3f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration DeceiveDurationOption = NebulaAPI.Configurations.Configuration("options.role.gambler.deceiveDuration", (0.25f, 5f, 0.25f), 1f, FloatConfigurationDecorator.Second);
    static private IntegerConfiguration InitialChipsOption = NebulaAPI.Configurations.Configuration("options.role.gambler.initialChips", (1, 15), 10);
    static public IntegerConfiguration GoalChipsOption = NebulaAPI.Configurations.Configuration("options.role.gambler.goalChips", (20, 100, 5), 30);
    static private IntegerConfiguration MaxBettingPatternsOption = NebulaAPI.Configurations.Configuration("options.role.gambler.maxBettingPatterns", (1, 4), 4);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.gambler.vent", true);
    static private BoolConfiguration CanVoteOption = NebulaAPI.Configurations.Configuration("options.role.gambler.canVote", false);
    static private IntegerConfiguration ChipsLimitForNoVoteBettingOption = NebulaAPI.Configurations.Configuration("options.role.gambler.chipsLimitForNoVoteBetting", (1, 10, 1), 10);

    static public Gambler MyRole = new Gambler();
    static internal GameStatsEntry StatsDeceive = NebulaAPI.CreateStatsEntry("stats.gambler.deceive", GameStatsCategory.Roles, MyRole);
    static internal GameStatsEntry StatsBetAll = NebulaAPI.CreateStatsEntry("stats.gambler.bet", GameStatsCategory.Roles, MyRole);
    static internal GameStatsEntry StatsBetSuccess = NebulaAPI.CreateStatsEntry("stats.gambler.bet.success", GameStatsCategory.Roles, MyRole);
    static internal GameStatsEntry StatsBetFailed = NebulaAPI.CreateStatsEntry("stats.gambler.bet.failed", GameStatsCategory.Roles, MyRole);

    /// <summary>
    /// 霊界でのみ見える投票先の表示
    /// </summary>
    private static class GamblerSpreader
    {
        static private readonly Dictionary<int, List<SpriteRenderer>> renderers = [];
        static public void Push(GamePlayer gambler, int to, int num)
        {
            Color color = Palette.PlayerColors[gambler.PlayerId];
            color.ToHSV(out var h, out var s, out _);
            h = 360f - h;

            if (MeetingHud.Instance.playerStates.Find(pva => pva.TargetPlayerId == to, out var pva) && MeetingHud.Instance.playerStates.Find(pva => pva.TargetPlayerId == gambler.PlayerId, out var from))
            {
                Vector3 fromPos = pva.transform.InverseTransformPoint(from.PlayerIcon.cosmetics.hat.transform.position);
                fromPos.z = -2f;

                var renderer = UnityHelper.CreateObject<SpriteRenderer>("GamblerIcon", pva.transform, fromPos);
                renderer.sprite = chipsImage.GetSprite(num - 1);
                renderer.material = new(NebulaAsset.HSVShader);
                renderer.material.SetFloat("_Hue", h);
                renderer.material.SetFloat("_Sat", s * 0.7f + 0.3f);
                renderer.transform.localScale = new(0.18f, 0.18f, 1f);
                
                if(!renderers.TryGetValue(to, out var list))
                {
                    list = [];
                    renderers.Add(to, list);
                }
                int index = list.Count; 
                list.Add(renderer);

                
                Vector3 toPos = new(1.3f - index * 0.11f, 0.27f, -2f + -0.1f * index);
                if (MeetingHud.Instance.reporterId == to) toPos.x -= 0.15f;
                pva.StartCoroutine(ManagedEffects.Lerp(1f, p =>
                {
                    p = 1f - p;
                    p = 1f - p * p;
                    renderer.transform.localPosition = Vector3.Lerp(fromPos, toPos, p);
                }).WrapToIl2Cpp());
            }
        }

        static public void ClearRenderers()
        {
            foreach (var entry in renderers) foreach (var renderer in entry.Value) if (renderer) GameObject.Destroy(renderer.gameObject);
            ResetWithoutChecking();
        }

        static public void ResetWithoutChecking()
        {
            renderers.Clear();
        }
    }

    static private readonly MultiImage chipsImage = DividedSpriteLoader.FromResource("Nebula.Resources.GamblerChip.png", 100f, 10, 1);

    [NebulaRPCHolder]
    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;
        static private readonly MultiImage meetingButtonImage = DividedSpriteLoader.FromResource("Nebula.Resources.GamblerButton.png", 100f, 2, 2);

        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DeceiveButton.png", 115f);
        
        private int myChips = InitialChipsOption;


        public Instance(GamePlayer player) : base(player, VentConfiguration)
        {
        }

        EditableBitMask<GamePlayer> interactedPlayers = BitMasks.AsPlayer();
        //イカサマを仕掛けた人数。
        int deceivedNum = 0;
        AchievementToken<(bool useDeceive, bool failed)>? acTokenChallenge = null;
        public override void OnActivated()
        {
            if (AmOwner)
            {
                var IconsHolder = HudContent.InstantiateContent("GamblerIcons", true, true, false);
                IconsHolder.gameObject.AddComponent<SortingGroup>();
                var playersHolder = UnityHelper.CreateObject("PlayersHolder", IconsHolder.transform, Vector3.zero);
                this.BindGameObject(IconsHolder.gameObject);

                deceivedNum = 0;
                void AddPlayer(GamePlayer player)
                {
                    var icon = AmongUsUtil.GetPlayerIcon(player.Unbox().DefaultOutfit, playersHolder.transform, Vector3.zero, Vector3.one * 0.31f);
                    icon.ToggleName(false);
                    icon.transform.localPosition = new((float)deceivedNum * 0.29f + 0.2f, -0.1f, -(float)deceivedNum * 0.01f);
                    interactedPlayers.Add(player);
                    deceivedNum++;
                }

                SpriteRenderer iconRenderer = UnityHelper.CreateObject<SpriteRenderer>("Icon", IconsHolder.transform, new(-0.4f, -0.2f, 0f));
                iconRenderer.transform.localScale = new(0.25f, 0.25f, 1f);
                iconRenderer.sprite = chipsImage.GetSprite(9);

                TextMeshPro numText = null!;
                var numTextObj = new NoSGUIText(GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayContent, new RawTextComponent(myChips.ToString())) {
                    PostBuilder = t => numText = t
                }.Instantiate(new(10f, 10f), out _);
                numTextObj!.transform.SetParent(IconsHolder.transform);
                numTextObj.transform.localPosition = new(-0.31f, -0.37f, 0f);

                acTokenChallenge = new("gambler.challenge", (false, false), (val, _) => !val.failed && !val.useDeceive && NebulaGameManager.Instance?.EndState?.EndCondition == NebulaGameEnd.GamblerWin && (NebulaGameManager.Instance?.EndState?.Winners.Test(MyPlayer) ?? false));

                var playerTracker = ObjectTrackers.ForPlayer(null, MyPlayer, (p) => ObjectTrackers.StandardPredicate(p) && !interactedPlayers.Test(p)).Register(this);

                var deceiveButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    DeceiveCoolDownOption, DeceiveDurationOption, "deceive", buttonSprite,
                    _ => playerTracker.CurrentTarget != null);
                deceiveButton.OnEffectEnd = (button) =>
                {
                    if (playerTracker.CurrentTarget == null) return;
                    acTokenChallenge.Value.useDeceive = true;
                    AddPlayer(playerTracker.CurrentTarget);
                    deceiveButton.StartCoolDown();
                    StatsDeceive.Progress();
                };
                deceiveButton.OnUpdate = (button) => {
                    if (!button.IsInEffect) return;
                    if (playerTracker.CurrentTarget == null) button.InterruptEffect();
                };

                GameOperatorManager.Instance?.Subscribe<TaskPhaseStartEvent>(ev =>
                {
                    playersHolder.transform.DestroyChildren();
                    interactedPlayers.Clear();
                    deceivedNum = 0;

                    numText.text = myChips.ToString();
                }, this);
            }
        }

        private const float MaxVoteBettingNum = 1;
        private const float NoVoteBettingNum = 3;
        private const int MaxBettingChips = 10;
        private const int MinBettingChips = 1;
        private record Betting(float Hue, GameObject BettingShower, SpriteRenderer Icon, TextMeshPro Text, int PlayerMask, SpriteRenderer[] TargetRenderers)
        {
            public int Num { get; private set; }

            public void CheckAndUpdateNumber(int add, IEnumerable<Betting> bettings, int myChips)
            {
                if (add == 0) return;

                var lastSum = bettings.Sum(b => b.Num);
                
                if(add > 0 && myChips - lastSum < add) add = myChips - lastSum;

                UncheckedSetNumber(Math.Clamp(Num + add, 1, IsNoVoteBetting ? ChipsLimitForNoVoteBettingOption : 10));
            }

            public void UncheckedSetNumber(int num)
            {
                Num = num;
                Icon.sprite = chipsImage.GetSprite(Num - 1);
                Text.text = Num.ToString();
            }

            public bool IsNoVoteBetting => TargetRenderers.Length == NoVoteBettingNum;
            public bool IsMaxVoteBetting => TargetRenderers.Length == MaxVoteBettingNum;

            public bool IsSuccess = false;
            public int AfterNum => IsSuccess ? Num * 2 : 0;
        }

        void OnMeetingStartGlobal(MeetingStartEvent ev) => GamblerSpreader.ResetWithoutChecking();
        void OnResetVoteGlobal(MeetingResetEvent ev) => GamblerSpreader.ClearRenderers();
        

        //会議画面の改変
        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (MyPlayer.IsDead) return;

            int lastChips = myChips;

            ev.CanVote = false;

            //ハイライトの生成
            Dictionary<byte, SpriteRenderer> highlights = [];
            foreach (var pva in MeetingHud.Instance.playerStates)
            {
                var highlight = GameObject.Instantiate(pva.HighlightedFX, pva.transform);
                highlight.enabled = false;
                highlight.color = Color.red;
                highlight.transform.localPosition = new(-0.0155f, 0.0127f, -2.8f);
                highlights[pva.TargetPlayerId] = highlight;
            }
            void ClearAllHighlights() => highlights.Do(entry => entry.Value.enabled = false);
            void UpdateHighlight(byte player, bool on) => highlights[player].enabled = on;
            void ToggleHighlight(byte player)
            {
                var hl = highlights[player];
                hl.enabled = !hl.enabled;
            }

            var skipButton = MeetingHud.Instance.SkipVoteButton;

            var footer = UnityHelper.CreateObject("GamblerFooter", skipButton.transform.parent, skipButton.transform.localPosition - new Vector3(0.3f, 0.1f, 0f));
            footer.AddComponent<SortingGroup>();
            var icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", footer.transform, new Vector3(0f, 0f, 0f));
            icon.transform.localScale = new(0.25f, 0.25f, 1f);
            icon.sprite = chipsImage.GetSprite(9);

            var numText = new NoSGUIText(GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayContent, new RawTextComponent(myChips.ToString())).Instantiate(new(10f, 10f), out _);
            numText!.transform.SetParent(footer.transform);
            numText.transform.localPosition = new(0.2f, -0.15f, 0f);

            var arrowText = new NoSGUIText(GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayContent, new RawTextComponent("➡")) { PostBuilder = text => text.outlineColor = Color.clear }.Instantiate(new(10f, 10f), out _);
            arrowText!.transform.SetParent(footer.transform);
            arrowText.transform.localPosition = new(0.6f, 0f, 0f);

            Stack<float> hueStack = new([50f, 240f, 330f, 100f]);
            List<Betting> bettings = [];

            GameObject GenerateMeetingButton(int index, Action onClick, string overlayKey)
            {
                var buttonRenderer = UnityHelper.CreateObject<SpriteRenderer>("Button", footer.transform, new(1f, 0f, 0f));
                buttonRenderer.sprite = meetingButtonImage.GetSprite(index);
                var button = buttonRenderer.gameObject.SetUpButton(true);
                var buttonCollider = button.gameObject.AddComponent<BoxCollider2D>();
                buttonCollider.size = new(0.45f, 0.45f);
                buttonCollider.isTrigger = true;
                button.OnMouseOver.AddListener(() =>
                {
                    NebulaManager.Instance.SetHelpWidget(button, Language.Translate(overlayKey));
                    buttonRenderer.sprite = meetingButtonImage.GetSprite(index + 2);
                    buttonRenderer.transform.localScale = new(1f, 1f, 1f);
                });
                button.OnMouseOut.AddListener(() =>
                {
                    NebulaManager.Instance.HideHelpWidgetIf(button);
                    buttonRenderer.sprite = meetingButtonImage.GetSprite(index);
                    buttonRenderer.transform.localScale = new(0.8f, 0.8f, 1f);
                });
                buttonRenderer.transform.localScale = new(0.8f, 0.8f, 1f);
                button.OnClick.AddListener(onClick);
                return buttonRenderer.gameObject;
            }

            void AddBedding(int targetMask)
            {
                var lastSum = bettings.Sum(b => b.Num);
                int initNum = Math.Min(5, myChips - lastSum);
                if(initNum <= 0)
                {
                    DebugScreen.Push(Language.Translate("gambler.ui.lackOfChips"), 3f);
                    return;
                }

                Betting betting = null!;

                GameObject holder = UnityHelper.CreateObject("Bedding", footer.transform, new(0.5f, 0f, 0f));
                SpriteRenderer renderer = UnityHelper.CreateObject<SpriteRenderer>("Icon", holder.transform, new(0f, 0f, 0f));
                renderer.transform.localScale = new(0.25f, 0.25f, 1f);
                renderer.material = new Material(NebulaAsset.HSVShader);
                renderer.material.SetFloat("_Hue", hueStack.Peek());
                CircleCollider2D collider = renderer.gameObject.AddComponent<CircleCollider2D>();

                collider.isTrigger = true;
                collider.radius = 0.8f;
                var button = renderer.gameObject.SetUpButton(true);
                button.OnClick.AddListener(() =>
                {
                    if (MeetingHud.Instance.state != MeetingHud.VoteStates.NotVoted) return;

                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                        betting.CheckAndUpdateNumber(-1, bettings, myChips);
                    else
                        betting.CheckAndUpdateNumber(1, bettings, myChips);
                });
                button.OnMouseOver.AddListener(() =>
                {
                    if (MeetingHud.Instance.state != MeetingHud.VoteStates.NotVoted) return;
                    NebulaManager.Instance.SetHelpWidget(button, Language.Translate(betting.IsNoVoteBetting ? "gambler.ui.zeroBetting" : "gambler.ui.maxBetting").Bold() + "<br>" + Language.Translate("gambler.betting.control"));
                    betting.TargetRenderers.Do(r => r.transform.localScale = new(0.35f, 0.35f, 1f));
                });
                button.OnMouseOut.AddListener(() =>
                {
                    NebulaManager.Instance.HideHelpWidgetIf(button);
                    betting.TargetRenderers.Do(r => r.transform.localScale = new(0.23f, 0.23f, 1f));
                });
                var exButton = button.gameObject.AddComponent<ExtraPassiveBehaviour>();
                exButton.OnRightClicked = () =>
                {
                    if (MeetingHud.Instance.state != MeetingHud.VoteStates.NotVoted) return;
                    RemoveBetting(betting);
                };

                TextMeshPro text = null!;
                var numText = new NoSGUIText(GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayContent, new RawTextComponent(myChips.ToString())) { PostBuilder = t => text = t }.Instantiate(new(10f, 10f), out _);
                numText!.transform.SetParent(holder.transform);
                numText.transform.localPosition = new(0.15f, -0.15f, 0f);

                List<SpriteRenderer> renderers = [];
                foreach(var pva in MeetingHud.Instance.playerStates)
                {
                    if ((targetMask & (1 << pva.TargetPlayerId)) == 0) continue;

                    var chipsRenderer = UnityHelper.CreateObject<SpriteRenderer>("GamblerChips", pva.transform, new(1.3f, 0.15f, -2f));
                    chipsRenderer.material = renderer.sharedMaterial;
                    chipsRenderer.sprite = chipsImage.GetSprite(9);
                    chipsRenderer.transform.localScale = new(0.23f, 0.23f, 1f);
                    renderers.Add(chipsRenderer);
                }

                betting = new(hueStack.Pop(), holder, renderer, text, targetMask, renderers.ToArray());
                betting.UncheckedSetNumber(initNum);
                bettings.Add(betting);

                ArrangeBeddings();
            }
            void RemoveBetting(Betting betting)
            {
                if (betting.BettingShower) GameObject.Destroy(betting.BettingShower);
                hueStack.Push(betting.Hue);
                bettings.Remove(betting);
                betting.TargetRenderers.Do(r => { if (r) GameObject.Destroy(r); });
                ArrangeBeddings();
            }

            GameObject addButtonObj = null!;
            GameObject checkButtonObj = null!;

            addButtonObj = GenerateMeetingButton(0, () =>
            {
                if (MeetingHud.Instance.state != MeetingHud.VoteStates.NotVoted) return;

                int mask = 0;
                int count = 0;
                highlights.Do(entry => {
                    if (entry.Value.enabled)
                    {
                        mask |= 1 << entry.Key;
                        count++;
                    }
                });
                if (count == MaxVoteBettingNum || count == NoVoteBettingNum)
                {
                    AddBedding(mask);
                    ClearAllHighlights();
                }
                else
                {
                    DebugScreen.Push(Language.Translate("gambler.ui.selectingHint").Replace("%NV%", NoVoteBettingNum.ToString()).Replace("%MV%", MaxVoteBettingNum.ToString()), 5f);
                }
            }, "gambler.ui.help.add");
            checkButtonObj = GenerateMeetingButton(1, () =>
            {
                if (MeetingHud.Instance.state == MeetingHud.VoteStates.NotVoted)
                {
                    if (CanVoteOption)
                    {
                        if (bettings.Find(b => b.IsMaxVoteBetting, out var mvb) && GamePlayer.AllPlayers.Find(p => (mvb.PlayerMask & (1 << p.PlayerId)) != 0, out var p))
                        {
                            MeetingHud.Instance.ModCastVote(PlayerControl.LocalPlayer.PlayerId, p.PlayerId, 1);
                        }
                        else
                        {
                            MeetingHud.Instance.ModCastVote(PlayerControl.LocalPlayer.PlayerId, 253, 1);
                        }
                    }
                    else
                    {
                        MeetingHud.Instance.ModCastVote(PlayerControl.LocalPlayer.PlayerId, 253, 0);
                    }
                    ArrangeBeddings();
                    List<(int to, int num)> list = [];
                    foreach (var betting in bettings) { 
                        foreach(var player in GamePlayer.AllPlayers)
                        {
                            if((betting.PlayerMask & (1 << player.PlayerId)) != 0)
                            {
                                list.Add((player.PlayerId, betting.Num));
                            }
                        }
                    }
                    RpcShareBetting.Invoke((MyPlayer, list.ToArray()));
                }
            }, "gambler.ui.help.check");

            void ArrangeBeddings()
            {
                float x = 0.9f;
                foreach (var betting in bettings)
                {
                    betting.BettingShower.transform.localPosition = new(x, 0f, 0f);
                    x += 0.38f;
                }

                bool finished = MeetingHud.Instance.state == MeetingHud.VoteStates.Voted;

                if (hueStack.Count == 0 || finished || bettings.Count >= MaxBettingPatternsOption)
                {
                    addButtonObj.SetActive(false);
                }
                else
                {
                    addButtonObj.SetActive(true);
                    addButtonObj.transform.localPosition = new(x, 0f, 0f);
                    x += 0.4f;
                }

                checkButtonObj.SetActive(!finished);
                checkButtonObj.transform.localPosition = new(x, 0f, 0f);

            }

            ArrangeBeddings();

            void ClearVoteForIcons()
            {
                foreach(var pva in MeetingHud.Instance.playerStates)
                {
                    var votes = pva.GetComponent<VoteSpreader>().Votes;
                    foreach (var v in votes) GameObject.Destroy(v.gameObject);
                    votes.Clear();
                }
            }
            var lifespan = new GameObjectLifespan(footer);
            GameOperatorManager.Instance?.Subscribe<InvokeVoteAlternateEvent>(ev =>
            {
                if (bettings.Any(b => (b.PlayerMask & (1 << ev.Player.PlayerId)) != 0))
                {
                    UpdateHighlight(ev.Player.PlayerId, false);
                    DebugScreen.Push(Language.Translate("gambler.ui.multiSelecting"), 2.5f);
                    return;
                }
                VanillaAsset.PlaySelectSE();
                ToggleHighlight(ev.Player.PlayerId);
            }, lifespan);
            //フッタは開示時に消去する。
            GameOperatorManager.Instance?.Subscribe<MeetingVoteDisclosedEvent>(ev =>
            {
                ClearAllHighlights();
                GameObject.Destroy(footer);

                //賭けの結果を計算する。
                var states = ev.VoteStates.GroupBy(v => v.VotedForId).Select(group => (group.Key, group.Count())).ToArray();
                var maxVotes = states.Max(g => g.Item2);
                foreach(var betting in bettings)
                {
                    if (betting.IsMaxVoteBetting)
                        betting.IsSuccess = states.Any(g => (betting.PlayerMask & (1 << g.Key)) != 0 && g.Item2 == maxVotes);
                    else if (betting.IsNoVoteBetting)
                        betting.IsSuccess = states.All(g => (betting.PlayerMask & (1 << g.Key)) == 0 || g.Item2 == 0);

                    myChips += betting.AfterNum - betting.Num;
                }

                StatsBetAll.Progress(bettings.Count);
                int successNum = bettings.Count(b => b.IsSuccess);
                int failedNum = bettings.Count - successNum;
                StatsBetSuccess.Progress(successNum);
                StatsBetFailed.Progress(failedNum);
                if (deceivedNum >= 3 && successNum > 0) new StaticAchievementToken("gambler.common1");
                if(bettings.Count == 1 && bettings[0].Num == lastChips)
                {
                    //オールイン
                    new StaticAchievementToken(bettings[0].IsSuccess ? "gambler.common3" : "gambler.another1");
                }
                if (bettings.Count >= 3 && failedNum == 0) new StaticAchievementToken("gambler.common4");
                if (failedNum > 0 && acTokenChallenge != null) acTokenChallenge.Value.failed = true; 

                ClearVoteForIcons();
            }, lifespan);

            //投票状況がリセットされたら無効な賭けを削除する。
            GameOperatorManager.Instance?.Subscribe<MeetingResetEvent>(ev =>
            {
                ClearAllHighlights();

                for (int i = 0; i < bettings.Count; i++)
                {
                    //賭け対象で投票できないプレイヤーがいた場合、その賭けを削除する
                    if (GamePlayer.AllPlayers.Any(p => (bettings[i].PlayerMask & (1 << p.PlayerId)) != 0 && !MeetingHudExtension.CanVoteFor(p.PlayerId))){
                        RemoveBetting(bettings[i]);
                        i--;
                    }
                }

                ArrangeBeddings();

                ClearVoteForIcons();
            }, lifespan);

            GameOperatorManager.Instance?.Subscribe<PlayerVoteCastEvent>(ev =>
            {
                if (ev.VoteFor == null) return;
                if (!interactedPlayers.Test(ev.Voter)) return;
                if(MeetingHud.Instance.playerStates.Find(pva => pva.TargetPlayerId == ev.VoteFor.PlayerId, out var pva))
                {
                    MeetingHud.Instance.BloopAVoteIcon(ev.Voter.VanillaPlayer.Data, 0, pva.transform);
                }
            }, lifespan);


            GameOperatorManager.Instance?.Subscribe<ExileSceneStartEvent>(ev => {
                var holder = UnityHelper.CreateObject("GambleResult", ExileController.Instance.transform, new(0f,0f,-10f));
                holder.AddComponent<SortingGroup>();
                var parentScale = ExileController.Instance.transform.localScale;
                holder.transform.localScale = new(1f / parentScale.x, 1f / parentScale.y, 1f);

                var arrowTextWidget = new NoSGUIText(GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayContent, new RawTextComponent("➡")) { PostBuilder = text => text.outlineColor = Color.clear };
                var failedTextWidget = new NoSGUIText(GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayContent, new RawTextComponent("×")) { PostBuilder = text => text.outlineColor = Color.clear };
                (GameObject holder, IEnumerator coroutine)[] results = bettings.Select(betting =>
                {
                    var bHolder = UnityHelper.CreateObject("BettingResult", holder.transform, new(0f, 0f, 0));

                    var arrowText = arrowTextWidget.Instantiate(new(10f, 10f), out _);
                    arrowText!.transform.SetParent(bHolder.transform);
                    arrowText.transform.localPosition = new(-0.05f, 0f, 0f);

                    SpriteRenderer beforeRenderer = UnityHelper.CreateObject<SpriteRenderer>("Before", bHolder.transform, new(-0.35f, 0f, 0f));
                    beforeRenderer.transform.localScale = new(0.25f, 0.25f, 1f);
                    beforeRenderer.material = new Material(NebulaAsset.HSVShader);
                    beforeRenderer.material.SetFloat("_Hue", betting.Hue);
                    beforeRenderer.sprite = chipsImage.GetSprite(betting.Num - 1);

                    var beforeNumText = new NoSGUIText(GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayContent, new RawTextComponent(betting.Num.ToString())).Instantiate(new(10f, 10f), out _);
                    beforeNumText!.transform.SetParent(bHolder.transform);
                    beforeNumText.transform.localPosition = new(-0.17f, -0.17f, 0f);

                    var afterHolder = UnityHelper.CreateObject("After", bHolder.transform, new(0.35f, 0f, 0f));

                    if(betting.AfterNum > 0){
                        if (betting.AfterNum > 10)
                        {
                            SpriteRenderer afterRenderer1 = UnityHelper.CreateObject<SpriteRenderer>("After", afterHolder.transform, new(-0.095f, 0.02f, 0f));
                            afterRenderer1.transform.localScale = new(0.25f, 0.25f, 1f);
                            afterRenderer1.material = beforeRenderer.sharedMaterial;
                            afterRenderer1.sprite = chipsImage.GetSprite(9);


                            SpriteRenderer afterRenderer2 = UnityHelper.CreateObject<SpriteRenderer>("After", afterHolder.transform, new(0.095f, -0.02f, -0.01f));
                            afterRenderer2.transform.localScale = new(0.25f, 0.25f, 1f);
                            afterRenderer2.material = beforeRenderer.sharedMaterial;
                            afterRenderer2.sprite = chipsImage.GetSprite((betting.AfterNum - 1) % 10);
                        }
                        else
                        {
                            SpriteRenderer afterRenderer1 = UnityHelper.CreateObject<SpriteRenderer>("After", afterHolder.transform, new(0f, 0.01f, 0f));
                            afterRenderer1.transform.localScale = new(0.25f, 0.25f, 1f);
                            afterRenderer1.material = beforeRenderer.sharedMaterial;
                            afterRenderer1.sprite = chipsImage.GetSprite(betting.AfterNum - 1);
                        }

                        var afterNumText = new NoSGUIText(GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayContent, new RawTextComponent(betting.AfterNum.ToString())).Instantiate(new(10f, 10f), out _);
                        afterNumText!.transform.SetParent(afterHolder.transform);
                        afterNumText.transform.localPosition = new(0.22f, -0.17f, 0f);
                    }
                    else
                    {
                        var afterNumText = new NoSGUIText(GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayContent, new RawTextComponent("×")).Instantiate(new(10f, 10f), out _);
                        afterNumText!.transform.SetParent(afterHolder.transform);
                        afterNumText.transform.localPosition = new(0f, 0f, 0f);
                        afterNumText.transform.localScale = new(1.2f, 1.2f, 1f);
                    }

                    afterHolder.transform.localScale = new(0f, 0f, 1f);

                    IEnumerator CoAnim() {
                        yield return Effects.Bloop(0f, afterHolder.transform, 1f, 0.5f);
                    }
                    return (bHolder, CoAnim());
                }).ToArray();

                //手持ちチップ数の表示
                var leftHolder = UnityHelper.CreateObject("LowerHolder", holder.transform, new(0f, -1.8f, 0f));
                var leftTextObj = new NoSGUIText(GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayContent, new TranslateTextComponent("gambler.currentChips")).Instantiate(new(10f, 10f), out _);
                leftTextObj!.transform.SetParent(leftHolder.transform);
                leftTextObj.transform.localPosition = new(-0.7f, 0f, 0f);
                leftTextObj.transform.localScale = new(1.5f, 1.5f, 1f);

                TMPro.TextMeshPro leftText = null!;
                var leftNumObj = new NoSGUIText(GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayContent, new RawTextComponent(lastChips.ToString()))
                {
                    PostBuilder = t => leftText = t
                }.Instantiate(new(10f, 10f), out _);
                leftNumObj!.transform.SetParent(leftHolder.transform);
                leftNumObj.transform.localPosition = new(0f, 0f, 0f);
                leftNumObj.transform.localScale = new(0.9f, 0.9f, 1f);

                List<SpriteRenderer> leftIcons = [];
                void UpdateLeftNum(int num)
                {
                    int required = Math.Max(1, (num - 1) / 10 + 1);
                    while(leftIcons.Count < required) {
                        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Icon", leftHolder.transform, new(0.2f + 0.18f * leftIcons.Count, 0f, 0f));
                        renderer.sprite = chipsImage.GetSprite(0);
                        renderer.transform.localScale = new(0.25f, 0.25f, 1f);
                        leftIcons.Add(renderer);
                    }
                    for(int i = 0; i < leftIcons.Count; i++)
                    {
                        leftIcons[i].enabled = num > i * 10;

                        if (num > (i + 1) * 10) leftIcons[i].sprite = chipsImage.GetSprite(9);
                        else if(num > 0) leftIcons[i].sprite = chipsImage.GetSprite((num - 1) % 10);
                    }
                    leftNumObj.transform.localPosition = new(0.18f + required * 0.18f, -0.17f, -0.1f);

                    leftText.text = num.ToString();
                }
                UpdateLeftNum(lastChips);

                for (int i = 0; i < results.Length; i++)
                {
                    results[i].holder.transform.localPosition = new(((float)i - (float)(results.Length - 1) * 0.5f) * 1.4f, -1f, 0f);
                    ExileController.Instance.StartCoroutine(ManagedEffects.Sequence(ManagedEffects.Wait(1f + 0.45f * i), results[i].coroutine).WrapToIl2Cpp());
                }
                ExileController.Instance.StartCoroutine(ManagedEffects.Sequence(
                    ManagedEffects.Wait(1.5f + 0.45f * results.Length),
                    ManagedEffects.Lerp(1.2f, p => {
                        UpdateLeftNum((int)Mathf.Lerp(lastChips, myChips, p));
                    }),
                    ManagedEffects.Lerp(0.25f, p =>
                    {
                        float coeff = 1f + Mathf.Sin(Mathf.PI * p) * 0.7f;
                        leftHolder.transform.localScale = new(coeff, coeff, 1f);
                    })
                    ).WrapToIl2Cpp());

            }, new GameObjectLifespan(MeetingHud.Instance.gameObject));

        }

        [Local]
        void OnMeetingEnd(MeetingPreSyncEvent ev)
        {
            if (!MyPlayer.IsDead && myChips <= 0) MyPlayer.VanillaPlayer.ModMarkAsExtraVictim(null, PlayerStates.Lost, EventDetails.Kill);
            if (!MyPlayer.IsDead && !(MeetingHudExtension.ExiledAll?.Any(p => p.AmOwner) ?? false) && myChips >= GoalChipsOption) NebulaAPI.CurrentGame?.RequestGameEnd(NebulaGameEnd.GamblerWin, BitMasks.AsPlayer(1u << MyPlayer.PlayerId));
        }

        static private readonly RemoteProcess<(GamePlayer gambler, (int to, int num)[] bettings)> RpcShareBetting = new("ShareBetting", (message, _) =>
        {
            if((GamePlayer.LocalPlayer?.IsDead ?? false) && NebulaGameManager.Instance!.CanSeeAllInfo)
            {
                foreach(var betting in message.bettings)
                {
                    GamblerSpreader.Push(message.gambler, betting.to, betting.num);
                }
            }
        });
    }
}

