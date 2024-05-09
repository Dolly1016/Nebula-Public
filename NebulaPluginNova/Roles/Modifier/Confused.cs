using Virial.Events.Game.Meeting;
using Virial.Game;

namespace Nebula.Roles.Modifier;

public class Confused : ConfigurableStandardModifier
{
    static public Confused MyRole = new Confused();
    public override string LocalizedName => "confused";
    public override string CodeName => "CFD";
    public override Color RoleColor => new Color(242f / 255f, 247f / 255f, 226f / 255f);

    private NebulaConfiguration ChanceOfShuffleOption = null!;
    private NebulaConfiguration NumOfMaxShuffledPairsOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        ChanceOfShuffleOption = new NebulaConfiguration(RoleConfig, "chanceOfShuffle", null, 10f,100f,10f,60f,60f) { Decorator = NebulaConfiguration.PercentageDecorator };
        NumOfMaxShuffledPairsOption = new NebulaConfiguration(RoleConfig, "numOfMaxShuffledPairs", null, 1, 7, 3, 3);
    }
    public override ModifierInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public class Instance : ModifierInstance, IGamePlayerOperator
    {
        public override AbstractModifier Role => MyRole;
        public override bool CanBeAwareAssignment => NebulaGameManager.Instance?.CanSeeAllInfo ?? false;

        public Instance(GamePlayer player) : base(player)
        {
        }

        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            if (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) text += " 〻".Color(MyRole.RoleColor);
        }


        bool skipMeeting = false;
        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (MyPlayer.IsDead) return;

            var alives = NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.IsDead && !p.AmOwner).ToArray();
            var randomArray = Helpers.GetRandomArray(alives.Length);
            int maxPairs = Mathf.Min(MyRole.NumOfMaxShuffledPairsOption.GetMappedInt(), alives.Length / 2);

            float prob = MyRole.ChanceOfShuffleOption.GetFloat();

            bool shuffled = false;
            for (int i = 0; i < maxPairs; i++)
            {
                if (System.Random.Shared.NextSingle() * 100 > prob) continue;

                shuffled = true;

                var player1 = alives[randomArray[i * 2]];
                var player2 = alives[randomArray[i * 2 + 1]];
                var outfit1 = player1.GetOutfit(50);
                var outfit2 = player2.GetOutfit(50);
                
                //もともとのConfusedの見た目を競合防止のため削除
                player1.Unbox().RemoveOutfit("Confused");
                player2.Unbox().RemoveOutfit("Confused");

                player1.Unbox().AddOutfit(new("Confused", 20, false, outfit2.outfit));
                player2.Unbox().AddOutfit(new("Confused", 20, false, outfit1.outfit));
            }

            if (shuffled && !skipMeeting)
            {
                new StaticAchievementToken("confused.common1");
                skipMeeting = true;
            }

        }

        void IGamePlayerOperator.OnDead()
        {
            if (AmOwner && !skipMeeting) new StaticAchievementToken("confused.another1");
        }

        public override void OnGameEnd(NebulaEndState endState)
        {
            //無能本人で、生存していて、生存者が全員クルーで、クルーメイト勝利の場合
            if (
                NebulaGameManager.Instance!.AllPlayerInfo().Count(p => p.Role.Role.Category == Virial.Assignable.RoleCategory.NeutralRole) >= 2 &&
                NebulaGameManager.Instance!.AllPlayerInfo().Count(p => p.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole) >= 2 &&
                AmOwner &&
                !MyPlayer.IsDead &&
                endState.EndCondition == NebulaGameEnds.CrewmateGameEnd &&
                NebulaGameManager.Instance!.AllPlayerInfo().All(p => p.IsDead || p.Role.Role.Category == Virial.Assignable.RoleCategory.CrewmateRole))
                new StaticAchievementToken("confused.challenge");
        }

    }
}
