using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial;
using Nebula.Modules.ScriptComponents;
using Virial.Events.Game;
using Nebula.Roles.Impostor;
using Nebula.Modules.Cosmetics;

namespace Nebula.Roles.Modifier;

internal class Disclosure : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    static private BoolConfiguration AnonymousCursorOption = NebulaAPI.Configurations.Configuration("options.role.disclosure.anonymousCursor", true);
    static private BoolConfiguration ShowCursorAtLateVotingOption = NebulaAPI.Configurations.Configuration("options.role.disclosure.showCursorAtLateVoting", false);
    private Disclosure() : base("disclosure", "DCL", new(222, 134, 111), [AnonymousCursorOption, ShowCursorAtLateVotingOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }


    static public Disclosure MyRole = new Disclosure();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        static private SpriteLoader IconSprite = SpriteLoader.FromResource("Nebula.Resources.DisclosureIcon.png", 100f);

        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated() {
            if (AmOwner)
            {
                int num = 0;
                GamePlayer? voted = null;
                float votedTime = 0f;
                AchievementToken<int> challengeToken = new("disclosure.challenge", 0, (num, _) => num >= 3);
                //票を投じたらタイマーと重複人数をリセットする。
                GameOperatorManager.Instance?.Subscribe<PlayerVoteCastLocalEvent>(ev => {

                    voted = ev.VoteFor;
                    num = 0;
                    votedTime = NebulaGameManager.Instance!.CurrentTime;
                    new StaticAchievementToken("disclosure.common1");
                }, this);
                GameOperatorManager.Instance?.Subscribe<PlayerVoteCastEvent>(ev =>
                {
                    if (MeetingHud.Instance.TryGetPlayer(MyPlayer.PlayerId, out var pva))
                    {
                        if (!pva.DidVote) return;
                        if (voted == ev.VoteFor && NebulaGameManager.Instance!.CurrentTime - votedTime > 5f) num++;
                    }
                }, this);
                GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(ev => {
                    if (voted != null && num >= 3 && MyPlayer.Role.Role.Team != voted.Role.Role.Team && ev.Exiled.Contains(voted)) challengeToken.Value++;

                    num = 0;
                    voted = null;
                }, this);
            }
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo) name += MyRole.GetRoleIconTagSmall();
        }

        VVector2 MouseToHudPos(VVector2 mousePos) => (mousePos - (new VVector2(NebulaAPI.AmongUs.ScreenWidth, NebulaAPI.AmongUs.ScreenHeight) * 0.5f)) / NebulaAPI.AmongUs.ScreenHeight * 2f * HudManager.Instance.UICamera.orthographicSize;
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if(MyPlayer.AmOwner) ShareMousePoint.Invoke((MyPlayer.PlayerId, MouseToHudPos(Input.mousePosition), 0f, ++sentSequentialId));

            if (MyPlayer.IsDead) return;

            var renderer = UnityHelper.CreateObject<SpriteRenderer>("DisclosurePointer", HudManager.Instance.transform, Vector3.zero);
            renderer.sprite = IconSprite.GetSprite();
            renderer.color = DynamicPalette.PlayerColors[(AnonymousCursorOption && !MyPlayer.AmOwner) ? NebulaPlayerTab.CamouflageColorId : MyPlayer.PlayerId].ToUnityColor();
            renderer.enabled = false;

            var pointerLifespan = new FunctionalLifespan(() => !this.IsDeadObject && MeetingHud.Instance);
            pointerLifespan.BindGameObject(renderer.gameObject);

            GameOperatorManager.Instance?.Subscribe<GameHudUpdateEvent>(ev => {
                if (renderer)
                {
                    renderer.enabled = !MyPlayer.IsDead && lastPoint != null && (ShowCursorAtLateVotingOption || MeetingHudExtension.VotingTimer > 15f);

                    if (AmOwner)
                    {
                        renderer.transform.localPosition = MouseToHudPos(Input.mousePosition).AsVector3(-50f);
                    }
                    else
                    {
                        if (lastPoint != null)
                        {
                            if (lastPoint!.Value.speed > 0f)
                            {
                                VVector2 currentPos = renderer.transform.localPosition;
                                VVector2 diff = currentPos - (VVector2)lastPoint.Value.pos;
                                if (diff.Magnitude < 0.04f)
                                {
                                    currentPos = lastPoint.Value.pos;
                                }
                                else
                                {
                                    currentPos -= diff.Normalized * Mathn.Min(diff.Magnitude, lastPoint.Value.speed * ev.DeltaTime);
                                }

                                renderer.transform.localPosition = currentPos.AsUnityVector3(-50f);
                            }
                            else
                            {
                                renderer.transform.localPosition = lastPoint.Value.pos.AsVector3(-50f);
                            }
                        }
                    }
                }
            }, pointerLifespan);
        }

        float lastSent = 0f;
        int sentSequentialId = 0;
        void Update(GameHudUpdateEvent ev)
        {
            if (AmOwner)
            {
                if (MeetingHud.Instance.AsBoolFast() && lastPoint != null)
                {
                    if (NebulaGameManager.Instance!.CurrentTime - lastSent > 0.05f)
                    {
                        float timeDiff = NebulaGameManager.Instance!.CurrentTime - lastSent;
                        var nextPos = MouseToHudPos(Input.mousePosition);
                        var mag = (nextPos - lastPoint!.Value.pos).Magnitude;
                        ShareMousePoint.Invoke((MyPlayer.PlayerId, nextPos, mag / timeDiff, ++sentSequentialId));
                    }
                }
                if (!MeetingHud.Instance) lastPoint = null;
            }
        }

        static private RemoteProcess<(byte playerId, VVector2 pos, float speed, int sequentialId)> ShareMousePoint = new("DisclosurePoint",
            (message, _) =>
            {
                if(NebulaGameManager.Instance!.GetPlayer(message.playerId)?.TryGetModifier<Instance>(out var modifier) ?? false)
                {
                    if(modifier.MyPlayer.AmOwner || modifier.sentSequentialId < message.sequentialId)
                    modifier.lastSent = NebulaGameManager.Instance.CurrentTime;
                    modifier.lastPoint = (message.pos, message.speed);
                    modifier.sentSequentialId = message.sequentialId;
                }
            }, false);

        private (VVector2 pos, float speed)? lastPoint = null;
    }
}


