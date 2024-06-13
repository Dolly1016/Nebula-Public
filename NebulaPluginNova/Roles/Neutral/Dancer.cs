using Nebula.Behaviour;
using Nebula.Modules.GUIWidget;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Events.Game;

namespace Nebula.Roles.Neutral;

public class Dancer : DefinedRoleTemplate, DefinedRole
{
    static public RoleTeam MyTeam = new Team("teams.dancer", new(255, 255, 255), TeamRevealType.OnlyMe);

    private Dancer() : base("dancer", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, []) { }

    static public Dancer MyRole = new Dancer();

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static public float DancePlayerRange => 3f;
    static public float DanceCorpseRange => 3f;
    static public float DanceDuration => 3f;

    private class DanceProgress
    {
        public Vector2 Position { get; private set; }
        public EditableBitMask<GamePlayer> Players = BitMasks.AsPlayer();
        public EditableBitMask<GamePlayer> Corpses = BitMasks.AsPlayer();
        public float Progress { get; private set; }
        public float Percentage => Progress / DanceDuration;
        public float LeftProgress => DanceDuration - LeftProgress;
        private float failProgress = 0f;
        public bool IsFailed { get; private set; } = false;
        public bool IsCompleted { get; private set; } = false;
        public bool IsFinishedWithSomeReason => IsCompleted || IsFailed;
        private EffectCircle Effect;
        public void Update(bool isDancing)
        {
            if (IsFinishedWithSomeReason) return;

            if (isDancing)
            {
                NebulaAPI.CurrentGame?.GetAllPlayers().Where(p => p.VanillaPlayer.transform.position.Distance(Position) < DancePlayerRange).Do(p => Players.Add(p));
                Helpers.AllDeadBodies().Where(p => p.transform.position.Distance(Position) < DanceCorpseRange).Do(p => Corpses.Add(NebulaGameManager.Instance!.GetPlayer(p.ParentId)!));

                Progress += Time.deltaTime;
                if (Progress > DanceDuration)
                {
                    IsCompleted = true;
                    OnFinished();
                }
            }
            else
            {
                failProgress += Time.deltaTime;
                if (failProgress > 0.4f)
                {
                    IsFailed = true;
                    OnFinished();
                }       
            }
        }

        private void OnFinished()
        {
            if (Effect) Effect.Disappear();
        }

        public void Destroy()
        {
            if (Effect) Effect.DestroyFast();
        }

        public DanceProgress(Vector2 pos)
        {
            Position = pos;
            Effect = EffectCircle.SpawnEffectCircle(null, pos, MyRole.UnityColor, DancePlayerRange, null, true);
        }
    }

    private class DancePlayerIcon
    {
        public PoolablePlayer PlayerIcon;
        public float LeftTime;
        public bool Success;
    }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        public Instance(GamePlayer player) : base(player)
        {
        }

        GameTimer? danceCoolDownTimer = null;
        List<DancePlayerIcon> iconList = null!;

        void RuntimeAssignable.OnActivated()
        {
            iconList = new();

            danceCoolDownTimer = Bind(new Timer(10f));
            danceCoolDownTimer.ResetsAtTaskPhase();

            var danceButton = Bind(new Modules.ScriptComponents.ModAbilityButton());
            //danceButton.SetSprite(buttonSprite.GetSprite());
            danceButton.Availability = (button) => currentDance != null && IsDancing;
            danceButton.Visibility = (button) => !MyPlayer.IsDead;
            danceButton.CoolDownTimer = new ScriptVisualTimer(
                () => danceCoolDownTimer.IsProgressing ? danceCoolDownTimer.Percentage : (currentDance?.Percentage ?? 0f),
                () => danceCoolDownTimer.IsProgressing ? danceCoolDownTimer.TimerText : currentDance != null ? Mathf.CeilToInt(currentDance.LeftProgress).ToString().Color(Color.blue) : null
                );
            danceButton.SetLabel("dance");
        }


        void RuntimeAssignable.OnInactivated()
        {
            currentDance?.Destroy();
        }
        

        DanceProgress? currentDance = null;

        Vector2? lastPos = null;
        Vector2 displacement = new();
        float distance = 0f;
        float danceGuage = 0f;

        [Local]
        void OnHudUpdate(GameHudUpdateEvent ev)
        {
            if(currentDance != null)
            {
                NebulaGameManager.Instance?.AllPlayerInfo().Where(currentDance.Players.Test).Do(p => HighlightHelpers.SetHighlight(p, MyRole.UnityColor));
                NebulaGameManager.Instance?.AllPlayerInfo().Where(p => currentDance.Players.Test(p)).Do(p => HighlightHelpers.SetHighlight(p.RelatedDeadBody, MyRole.UnityColor));
            }
        }

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            if (AmOwner)
            {
                Vector2 currentPos = MyPlayer.VanillaPlayer.transform.position;
                if (lastPos != null)
                {
                    distance *= 0.89f;
                    distance += currentPos.Distance(lastPos.Value);

                    displacement *= 0.89f;
                    displacement += currentPos - lastPos.Value;
                }
                lastPos = currentPos;

                if (distance > 0.3f && displacement.magnitude < 0.18f)
                    danceGuage = Math.Min(danceGuage + Time.deltaTime * 4.2f, 1f);
                else
                    danceGuage = Math.Max(danceGuage - Time.deltaTime * 2.7f, 0f);


                if (currentDance != null)
                {
                    if (currentDance.IsCompleted)
                    {
                        currentDance = null;
                    }
                    else if (currentDance.IsFailed)
                        currentDance = null;
                }

                if (IsDancing) currentDance ??= new DanceProgress(MyPlayer.Position);
                currentDance?.Update(IsDancing);
            }
        }

        bool IsDancing => danceGuage > 0.7f;
    }
}