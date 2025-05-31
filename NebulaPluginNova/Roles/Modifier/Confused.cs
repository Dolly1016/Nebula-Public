using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Modifier;

public class Confused : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private Confused() : base("confused", "CFD", new(242,247,226), [ChanceOfShuffleOption, NumOfMaxShuffledPairsOption])
    {

    }

    static private IntegerConfiguration ChanceOfShuffleOption = NebulaAPI.Configurations.Configuration("options.role.confused.chanceOfShuffle", (10,100,10),60, decorator: val => val + "%");
    static private IntegerConfiguration NumOfMaxShuffledPairsOption = NebulaAPI.Configurations.Configuration("options.role.confused.numOfMaxShuffledPairs", (1, 7), 3);

    static public Confused MyRole = new Confused();

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        bool RuntimeAssignable.CanBeAwareAssignment => NebulaGameManager.Instance?.CanSeeAllInfo ?? false;

        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated() { }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            if (canSeeAllInfo) name += " 〻".Color(MyRole.UnityColor);
        }


        bool skipMeeting = false;
        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (MyPlayer.IsDead) return;

            var alives = NebulaGameManager.Instance!.AllPlayerInfo.Where(p => !p.IsDead && !p.AmOwner).ToArray();
            var randomArray = Helpers.GetRandomArray(alives.Length);
            int maxPairs = Mathf.Min(NumOfMaxShuffledPairsOption, alives.Length / 2);

            float prob = ChanceOfShuffleOption;

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

                player1.Unbox().AddOutfit(new(outfit2, "Confused", 20, false));
                player2.Unbox().AddOutfit(new(outfit1, "Confused", 20, false));
            }

            if (shuffled && !skipMeeting)
            {
                new StaticAchievementToken("confused.common1");
                skipMeeting = true;
            }

        }

        [Local, OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            if (!skipMeeting) new StaticAchievementToken("confused.another1");
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            //無能本人で、生存していて、生存者が全員クルーで、クルーメイト勝利の場合
            if (
                NebulaGameManager.Instance!.AllPlayerInfo.Count(p => p.Role.Role.Category == Virial.Assignable.RoleCategory.NeutralRole) >= 2 &&
                NebulaGameManager.Instance!.AllPlayerInfo.Count(p => p.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole) >= 2 &&
                !MyPlayer.IsDead &&
                ev.EndState.EndCondition == NebulaGameEnd.CrewmateWin &&
                NebulaGameManager.Instance!.AllPlayerInfo.All(p => p.IsDead || p.Role.Role.Category == Virial.Assignable.RoleCategory.CrewmateRole))
                new StaticAchievementToken("confused.challenge");
        }

    }
}
