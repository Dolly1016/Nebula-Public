using BepInEx.Unity.IL2CPP.Utils;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public static class FootprintHelpers
{
    static public Vector2? GetFootprintPosition(PlayerControl player, bool isLeft)
    {
        if (player.inVent || player.Data.IsDead) return null;

        if (player.MyPhysics.Velocity.magnitude > 0)
        {
            var vec = player.MyPhysics.Velocity.normalized * 0.08f * (isLeft ? 1f : -1f);
            return player.transform.position + new Vector3(-vec.y, vec.x - 0.22f);
        }
        else
        {
            return player.transform.position + new Vector3(0f, -0.22f);
        }
    }
}
internal class Investigator : DefinedRoleTemplate, HasCitation, DefinedRole
{
    internal record KillData(GamePlayer Killer, GamePlayer Dead, float Time)
    {
        public bool Discovered => discoveredText != null;
        public string DiscoveredText => discoveredText ?? Language.Translate("role.investigator.overlay.notFound");
        private string? discoveredText = null;
        public float ElapsedTime = -10f;
        private RoleTeam killerTeam = Killer.Role.Role.Team;
        public string? FindIn = null;
        public void Discover(Vector2 at)
        {
            StatsFound.Progress();

            if (discoveredText != null) return;

            FindIn = AmongUsUtil.GetRoomName(at, true, true);
            string foundTimeText;
            if(ElapsedTime < 0f)
            {
                foundTimeText = Language.Translate("role.investigator.overlay.fastFind");
            }
            else
            {
                var time = Helpers.Round((int)ElapsedTime, 5);
                if(time == 0)
                {
                    foundTimeText = Language.Translate("role.investigator.overlay.reportedImmediately");
                }
                else
                {
                    foundTimeText = Language.Translate("role.investigator.overlay.elapsed").Replace("%ELAPSED%", Helpers.Round((int)ElapsedTime, 5).ToString());
                }
            }
            discoveredText = foundTimeText + "<br>" + Language.Translate("role.investigator.overlay.killer").Replace("%TEAM%", Language.Translate(killerTeam.TranslationKey).Color(killerTeam.UnityColor));

            NebulaAPI.CurrentGame?.GetModule<MeetingOverlayHolder>()?.RegisterOverlay(InvestigatorManager.GetWidget(this), MeetingOverlayHolder.IconsSprite[6], MyRole.RoleColor);

            bool done = false;
            GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(ev =>
            {
                if (ev.Exiled.Contains(Killer)) new StaticAchievementToken("investigator.challenge");
                done = true;
            }, new FunctionalLifespan(() => !done));
        }
    }

    private record FootprintInfo(UnityEngine.Vector2 Position, float Magnitude, KillData Info) { 
        public SpriteRenderer? Renderer = null;
        private bool spawned = false;
        public bool IsSpawned => spawned;
        public void TrySpawn(Vector2 cameraPos)
        {
            if (spawned) return;
            if (Mathf.Abs(cameraPos.x - Position.x) > 10f || Mathf.Abs(cameraPos.y - Position.y) > 10f) return;

            Renderer = AmongUsUtil.GenerateFootprint(Position, Color.black.AlphaMultiplied(Mathf.Pow(Magnitude, 0.5f)), null);
            spawned = true;
        }

        public void Update(Vector2 cameraPos, bool canSeeFootprint)
        {
            if (!spawned && canSeeFootprint) TrySpawn(cameraPos);

            if (spawned && Renderer) Renderer!.enabled = canSeeFootprint;
            
        }
    }

    static private bool CanSeeInvestigatorFootprints => ModSingleton<NightVision>.Instance?.CanSeeFootprints ?? false;

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class InvestigatorManager : AbstractModule<Virial.Game.Game>, IGameOperator
    {
        static InvestigatorManager() => DIManager.Instance.RegisterModule(() => new InvestigatorManager());

        private List<FootprintInfo> allFootprints = [];
        private List<KillData> unsolvedKillData = [];
        private FootprintInfo? lastFootprint = null;
        private float discoverProgress = 0f;

        public bool AnyFootprintsNearPosition(Vector2 position) => allFootprints.Any(f => f.Position.Distance(position) < 1f);
        public IEnumerable<GamePlayer> FoundPlayers => allFootprints.Where(f => f.Info.Discovered).Select(f => f.Info.Killer);

        private InvestigatorManager()
        {
            ModSingleton<InvestigatorManager>.Instance = this;
            this.Register(NebulaAPI.CurrentGame!);    
        }

        static internal Virial.Media.GUIWidget GetWidget(KillData data) => GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                            GUI.API.RawText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle),
                                data.Discovered ? Language.Translate("role.investigator.overlay.title.checked").Replace("%ROOM%", data.FindIn) :
                                Language.Translate("role.investigator.overlay.title.unchecked")
                            ),
                            GUI.API.RawText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), data.DiscoveredText)
                        );

        private void SearchFootprintAndShowPopup(bool canInvestigateFootprints)
        {
            if (!canInvestigateFootprints)
            {
                lastFootprint = null;
                return;
            }

            GamePlayer localPlayer = GamePlayer.LocalPlayer!;
            FootprintInfo? selected = null;
            UnityEngine.Vector2 pos = localPlayer.Position.ToUnityVector();
            foreach (var footprint in allFootprints)
            {
                if (!footprint.IsSpawned) continue;
                float d = footprint.Position.Distance(pos);
                if (d > 0.7f) continue;//ある程度とおくの足跡は無視
                if (footprint.Magnitude < 0.05f) continue;//弱すぎる証拠は無視

                if (
                    selected == null ||
                    (selected.Info == footprint.Info && footprint.Magnitude > selected.Magnitude) ||
                    (selected.Info != footprint.Info && !footprint.Info.Discovered && selected.Info.Discovered)
                    )
                {
                    selected = footprint;
                }
            }

            if (selected != null) {
                Vector2 targetPos = selected.Position;
                UnityEngine.Vector2 GetScreenPos()
                {
                    Vector2 pos = (lastFootprint ?? selected).Position;
                    targetPos += (pos - targetPos).Delta(2f, 0.005f);
                    return UnityHelper.WorldToScreenPoint(NebulaGameManager.Instance!.WideCamera.ConvertToWorldPos(targetPos), LayerExpansion.GetDefaultLayer());
                }

                void ShowDiscoveredPopup()
                {
                    NebulaManager.Instance.RegisterStaticPopup(
                        () => lastFootprint?.Info != selected.Info,
                        () => !NebulaInput.SomeUiIsActive && lastFootprint?.Info == selected.Info,
                        () => (GetWidget(selected.Info), GetScreenPos, null)
                        );
                }

                if (selected.Info != lastFootprint?.Info)
                {
                    if (!selected.Info.Discovered)
                    {
                        //さらに得られる情報がある場合、情報を得るポップアップを出す。
                        discoverProgress = 0f;
                        NebulaManager.Instance.RegisterStaticPopup(
                            () => localPlayer.IsDead || lastFootprint?.Info != selected.Info || selected.Info.Discovered,
                            () => localPlayer.CanMove && !NebulaInput.SomeUiIsActive && lastFootprint?.Info == selected.Info && !selected.Info.Discovered,
                            () => (GetWidget(selected.Info), GetScreenPos, null),
                            () => discoverProgress);
                    }
                    else
                    {
                        ShowDiscoveredPopup();
                    }
                }
                else
                {
                    discoverProgress += Time.deltaTime / 3f * InvestigateSpeedOption * (0.05f + selected.Magnitude * 0.95f);
                    if (discoverProgress > 1f)
                    {
                        discoverProgress = 0f;
                        selected.Info.Discover(selected.Position);
                        ShowDiscoveredPopup();
                    }
                }
            }
            lastFootprint = selected;
        }

        public bool CanSeeFootprints => CanSeeInvestigatorFootprints;
        private bool CanInvestigateFootprints => ModSingleton<NightVision>.Instance?.CanInvestigateFootprints ?? false;
        void OnUpdate(GameHudUpdateEvent ev)
        {
            var localPlayer = GamePlayer.LocalPlayer;
            if (localPlayer == null) return;
            var pos = localPlayer.VanillaPlayer.transform.position;
            var canSeeFootprints = CanSeeFootprints;

            allFootprints.Do(chunk => chunk.Update(pos, canSeeFootprints));
            SearchFootprintAndShowPopup(CanInvestigateFootprints);
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            unsolvedKillData.Do(k => k.ElapsedTime = NebulaGameManager.Instance!.CurrentTime - k.Time);
            unsolvedKillData.Clear();
        }
        void OnDied(PlayerDieEvent ev)
        {
            if(ev.Player.AmOwner) ModSingleton<NightVision>.Instance?.MarkReleased();
        }



        void OnAnyoneDead(PlayerMurderedEvent ev)
        {
            if (!ev.WithBlink) return;

            KillData killData = new(ev.Murderer, ev.Dead, NebulaGameManager.Instance!.CurrentTime);
            unsolvedKillData.Add(killData);

            float footprintDuration = FootprintDuration;
            IEnumerator CoSpawnFootprint()
            {
                bool isLeft = false;

                float time = 0f;
                while (true)
                {
                    yield return new WaitForSeconds(0.24f);
                    time += 0.24f;
                    
                    if (time > footprintDuration || MeetingHud.Instance) break;

                    var pos = FootprintHelpers.GetFootprintPosition(ev.Murderer.VanillaPlayer, isLeft);
                    isLeft = !isLeft;

                    if (pos.HasValue) allFootprints.Add(new(pos.Value, (footprintDuration - time) / footprintDuration, killData));
                }
            }

            NebulaManager.Instance.StartCoroutine(CoSpawnFootprint());
        }

    }

    private Investigator() : base("investigator", new(125, 108, 167), RoleCategory.CrewmateRole, Crewmate.MyTeam, [InvestigateCoolDownOption, InvestigateDurationOption, InvestigateSpeedOption, InvestigateEyesightOption, FootprintDuration])
    {
    }

    Citation? HasCitation.Citation => Citations.TheOtherRoles;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private readonly FloatConfiguration InvestigateCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.investigator.investigateCooldown", (2.5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration InvestigateDurationOption = NebulaAPI.Configurations.Configuration("options.role.investigator.investigateDuration", (5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration InvestigateSpeedOption = NebulaAPI.Configurations.Configuration("options.role.investigator.investigateSpeed", (0.25f, 2f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration InvestigateEyesightOption = NebulaAPI.Configurations.Configuration("options.role.investigator.investigateEyesight", (0.125f, 2f, 0.125f), 0.5f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration FootprintDuration = NebulaAPI.Configurations.Configuration("options.role.investigator.footprintDuration", (1f,10f,1f), 3f, FloatConfigurationDecorator.Second);

    static public readonly Investigator MyRole = new();
    static public readonly GameStatsEntry StatsFound = NebulaAPI.CreateStatsEntry("stats.investigator.found", GameStatsCategory.Roles, MyRole);

    private class NightVision : FlexibleLifespan, IGameOperator {
        //遷移中に使用する
        bool preBlacklightVision = true, postBlacklightVision = false;
        float t = 0f;
        bool gainEffect = false;
        bool removeEffect = false;
        static private UnityEngine.Color blacklightColor = new(0.24f, 0.5f, 0.12f, 1f);

        public NightVision()
        {
            ModSingleton<NightVision>.Instance = this;
        }

        public bool CanSeeFootprints => !(GamePlayer.LocalPlayer?.IsDead ?? false) && (preBlacklightVision ? t > 0.4f : postBlacklightVision ? t < 0.4f : true);
        public bool CanInvestigateFootprints => !(GamePlayer.LocalPlayer?.IsDead ?? false) && (preBlacklightVision ? t > 0.8f : postBlacklightVision ? t < 0.4f : true);
        public void MarkReleased()
        {
            if (postBlacklightVision) return;
            postBlacklightVision = true;
            t = 0f;
        }

        public void OnReleased() => RemoveEffect();

        private void RemoveEffect()
        {
            PlayerModInfo.RpcRemoveAttrByTag.LocalInvoke((GamePlayer.LocalPlayer!.PlayerId, "nebula::investigator"));
        }
        void OnEditCameraEffect(CameraUpdateEvent ev)
        {
            t += Time.deltaTime;

            if(!(preBlacklightVision && t < 0.4f) && !gainEffect)
            {
                PlayerModInfo.RpcAttrModulator.LocalInvoke((GamePlayer.LocalPlayer!.PlayerId, new FloatModulator(PlayerAttributes.Roughening, 2, 999999f, false, 0, "nebula::investigator", false), true));
                gainEffect = true;
            }

            if (preBlacklightVision)
            {
                if (t < 0.4f)
                {
                    var num = 1f - (t / 0.4f);
                    ev.Color = Color.white * num;
                }else if(t < 0.6f)
                {
                    ev.Color = Color.black;
                }else if(t < 1f)
                {
                    var num = (t - 0.6f) / 0.4f;
                    ev.Color = blacklightColor * num;
                    ev.UpdateBrightness(2.2f, true);
                }
                else
                {
                    preBlacklightVision = false;
                }
            }else if (postBlacklightVision)
            {
                if (t < 0.4f)
                {
                    var num = 1f - (t / 0.4f);
                    ev.Color = blacklightColor * num;
                    ev.UpdateBrightness(2.2f, true);
                }
                else if (t < 0.6f)
                {
                    ev.Color = Color.black;
                    if (!removeEffect) RemoveEffect();
                }
                else if (t < 1f)
                {
                    var num = (t - 0.6f) / 0.4f;
                    ev.Color = Color.white * num;
                }
                else if(!IsDeadObject)
                {
                    this.Release();
                }
            }
            else
            {
                ev.Color = blacklightColor;
                ev.UpdateBrightness(2.2f, true);
            }
        }

        void OnEditEyesight(LightRangeUpdateEvent ev)
        {
            if(
                preBlacklightVision ||
                (postBlacklightVision && t < 0.4f) ||
                (!preBlacklightVision && !postBlacklightVision))
                ev.LightRange *= InvestigateEyesightOption;
        }

        void UpdateFootprintVisibility(UpdateFootprintVisibilityEvent ev)
        {
            ev.Visible &= !CanSeeFootprints;
        }
    }


    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }
        

        static private readonly Image buttonImage = SpriteLoader.FromResource("Nebula.Resources.Buttons.InvestigatorButton.png", 115f);
        void RuntimeAssignable.OnActivated() {
            if (AmOwner)
            {
                var investigatorButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, InvestigateCoolDownOption, InvestigateDurationOption, "investigate", buttonImage);
                investigatorButton.OnEffectEnd = (button) =>
                {
                    ModSingleton<NightVision>.Instance?.MarkReleased();
                    investigatorButton.StartCoolDown();
                };
                investigatorButton.OnEffectStart = (button) => new NightVision().Register(this);
            }
        }

        [OnlyMyPlayer, Local]
        void OnDied(PlayerDieEvent ev)
        {
            if (CanSeeInvestigatorFootprints && ModSingleton<InvestigatorManager>.Instance.AnyFootprintsNearPosition(MyPlayer.Position)) new StaticAchievementToken("investigator.another1");
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            //全員がともども追放されていない
            var found = ModSingleton<InvestigatorManager>.Instance.FoundPlayers;
            if (!found.IsEmpty() && found.All(p => p.PlayerState != PlayerState.Exiled)) new StaticAchievementToken("investigator.another2");
        }
    }
}

