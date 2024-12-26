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
                //票を投じたらタイマーと重複人数をリセットする。(条件を満たしていれば銀称号をクリア)
                GameOperatorManager.Instance?.Register<PlayerVoteCastLocalEvent>(ev => {

                    voted = ev.VoteFor;
                    num = 0;
                    votedTime = NebulaGameManager.Instance!.CurrentTime;
                    if (PointerShown) new StaticAchievementToken("disclosure.common1");
                }, this);
                GameOperatorManager.Instance?.Register<PlayerVoteCastEvent>(ev =>
                {
                    if (MeetingHud.Instance.playerStates.Find(p => p.TargetPlayerId == MyPlayer.PlayerId, out var pva))
                    {
                        if (!pva.DidVote) return;
                        if (voted == ev.VoteFor && NebulaGameManager.Instance!.CurrentTime - votedTime > 5f) num++;
                    }
                }, this);
                GameOperatorManager.Instance?.Register<MeetingEndEvent>(ev => {
                    if (voted != null && num >= 3 && MyPlayer.Role.Role.Team != voted.Role.Role.Team && ev.Exiled.Contains(voted)) challengeToken.Value++;
                }, this);
            }
        }

        ComponentBinding<SpriteRenderer>? Pointer = null;
        bool PointerShown => Pointer != null && Pointer.MyObject && Pointer.MyObject!.enabled;

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            if (AmOwner || canSeeAllInfo) name += " ❖".Color(MyRole.UnityColor);
        }

        Vector2 MouseToHudPos(Vector2 mousePos) => (mousePos - (new Vector2(Screen.width, Screen.height) * 0.5f)) / Screen.height * 2f * HudManager.Instance.UICamera.orthographicSize;
        void OnMeetingStart(MeetingStartEvent ev)
        {
            Pointer?.ReleaseIt();

            if(MyPlayer.AmOwner) ShareMousePoint.Invoke((MyPlayer.PlayerId, MouseToHudPos(Input.mousePosition), 0f, ++sentSequentialId));

            if (MyPlayer.IsDead) return;

            var renderer = UnityHelper.CreateObject<SpriteRenderer>("DisclosurePointer", HudManager.Instance.transform, Vector3.zero);
            renderer.sprite = IconSprite.GetSprite();
            renderer.color = Palette.PlayerColors[(AnonymousCursorOption && !MyPlayer.AmOwner) ? NebulaPlayerTab.CamouflageColorId : MyPlayer.PlayerId];
            renderer.enabled = false;
            Pointer = Bind(new ComponentBinding<SpriteRenderer>(renderer));
        }

        float lastSent = 0f;
        int sentSequentialId = 0;
        void Update(GameHudUpdateEvent ev)
        {
            if (Pointer != null)
            {
                if (!MeetingHud.Instance)
                {
                    Pointer.ReleaseIt();
                    Pointer = null;
                }
                else if (Pointer.MyObject)
                {
                    Pointer.MyObject!.enabled = !MyPlayer.IsDead && lastPoint != null && (ShowCursorAtLateVotingOption || MeetingHudExtension.VotingTimer > 15f);
                }
            }

            if (AmOwner)
            {
                if (MeetingHud.Instance && lastPoint != null)
                {
                    if (NebulaGameManager.Instance!.CurrentTime - lastSent > 0.05f)
                    {
                        float timeDiff = NebulaGameManager.Instance!.CurrentTime - lastSent;
                        var nextPos = MouseToHudPos(Input.mousePosition);
                        var mag = (nextPos - lastPoint!.Value.pos).magnitude;
                        ShareMousePoint.Invoke((MyPlayer.PlayerId, nextPos, mag / timeDiff, ++sentSequentialId));
                    }
                }
                if (!MeetingHud.Instance) lastPoint = null;

                if (Pointer != null && Pointer.MyObject) Pointer.MyObject!.transform.localPosition = MouseToHudPos(Input.mousePosition).AsVector3(-50f);
            }
            else
            {
                if (Pointer != null && Pointer.MyObject)
                {
                    if (lastPoint != null)
                    {
                        if (lastPoint!.Value.speed > 0f)
                        {
                            Vector2 currentPos = Pointer.MyObject.transform.localPosition;
                            var diff = currentPos - lastPoint.Value.pos;
                            if (diff.magnitude < 0.04f)
                            {
                                currentPos = lastPoint.Value.pos;
                            }
                            else
                            {
                                currentPos -= diff.normalized * Mathf.Min(diff.magnitude, lastPoint.Value.speed * Time.deltaTime);
                            }

                            Pointer.MyObject.transform.localPosition = currentPos.AsVector3(-50f);
                        }
                        else
                        {
                            Pointer.MyObject.transform.localPosition = lastPoint.Value.pos.AsVector3(-50f);
                        }
                    }
                }
            }
        }

        static private RemoteProcess<(byte playerId, Vector2 pos, float speed, int sequentialId)> ShareMousePoint = new("DisclosurePoint",
            (message, _) =>
            {
                if(NebulaGameManager.Instance!.GetPlayer(message.playerId)?.TryGetModifier<Instance>(out var modifier) ?? false)
                {
                    if(modifier.MyPlayer.AmOwner || modifier.sentSequentialId < message.sequentialId)
                    modifier.lastSent = NebulaGameManager.Instance.CurrentTime;
                    modifier.lastPoint = (message.pos, message.speed);
                    modifier.sentSequentialId = message.sequentialId;
                }
            });

        private (Vector2 pos, float speed)? lastPoint = null;
    }
}


