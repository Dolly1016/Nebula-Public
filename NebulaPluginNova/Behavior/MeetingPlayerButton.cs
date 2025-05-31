using Nebula.Modules.GUIWidget;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Game;
using static Nebula.Behavior.MeetingPlayerButtonManager;

namespace Nebula.Behavior;

public record MeetingPlayerAction(Image icon, Action<MeetingPlayerButtonState> buttonAction, Predicate<MeetingPlayerButtonState> predicate);

public class MeetingPlayerButtonManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    internal static DividedSpriteLoader Icons = DividedSpriteLoader.FromResource("Nebula.Resources.MeetingButtons.png", 115f, 100, 110, true);

    public record MeetingPlayerButton(GameObject gameObject, SpriteRenderer renderer, GamePlayer player, Reference<MeetingPlayerButtonState> state);

    public class MeetingPlayerButtonState
    {
        public MeetingPlayerButton MyButton { get; init; } = null!;

        public GamePlayer MyPlayer => MyButton.player;
        public bool IsSelected { get; private set; } = false;

        public void SetSelect(bool select)
        {
            IsSelected = select;
            Update();
        }

        public void Update()
        {
            MyButton.renderer.color = IsSelected ? Color.green : Color.white;
        }

        public void Reset()
        {
            IsSelected = false;
            Update();
        }
    }

    public MeetingPlayerButtonManager()
    {
        this.Register(NebulaAPI.CurrentGame!);
    }

    List<MeetingPlayerAction> allActions = new();
    MeetingPlayerAction? currentAction = null;

    List<MeetingPlayerButton> allButtons = new();

    public IEnumerable<MeetingPlayerButtonState> AllStates => allButtons.Select(b => b.state.Value!);

    public void RegisterMeetingAction(MeetingPlayerAction action)
    {
        allActions.Add(action);
    }

    private void ResetActions()
    {
        allActions.Clear();
        allButtons.Clear();
        currentAction = null;
    }

    void OnPreMeetingStart(MeetingPreStartEvent ev) => ResetActions();

    void IncrementCurrentAction()
    {
        if (allActions.Count <= 1) return;

        int index = allActions.IndexOf(currentAction!);

        if(index < 0)index = 0;
        index++;

        SetAction(allActions.Get(index, allActions[0]));

        UpdatePlayerState();
    }

    void OnMeetingStart(MeetingStartEvent ev)
    {
        allButtons.Clear();

        foreach (var playerVoteArea in MeetingHud.Instance.playerStates)
        {
            var player = NebulaGameManager.Instance?.GetPlayer(playerVoteArea.TargetPlayerId);
            if (player == null) continue;

            GameObject template = playerVoteArea.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = UnityEngine.Object.Instantiate(template, playerVoteArea.transform);

            targetBox.name = "MeetingModButton";
            targetBox.transform.localPosition = new Vector3(-0.95f, 0f, -2.5f);

            SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = null;
            PassiveButton button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();

            Reference<MeetingPlayerButtonState> stateRef = new();
            MeetingPlayerButton myRecord = new(targetBox, renderer, player, stateRef);
            stateRef.Value = new() { MyButton = myRecord };

            allButtons.Add(myRecord);


            button.OnClick.AddListener(() => DoClick(stateRef.Value));
            button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent),
                new RawTextComponent(allActions.Count > 1 ? (Language.Translate("ui.meeting.leftClick") + "<br>" + Language.Translate("ui.meeting.rightClick")) : Language.Translate("ui.meeting.leftClick")))));
            button.OnMouseOut.AddListener(()=>NebulaManager.Instance.HideHelpWidgetIf(button));
            var epb = button.gameObject.AddComponent<ExtraPassiveBehaviour>();
            epb.OnRightClicked = IncrementCurrentAction;
        }
    }

    private void SetAction(MeetingPlayerAction? action)
    {
        currentAction = action;

        foreach (var button in allButtons)
        {
            button.state.Value?.Reset();

            if (currentAction == null)
            {
                button.gameObject.SetActive(false);
            }
            else
            {
                button.gameObject.SetActive(true);
                button.renderer.sprite = currentAction!.icon.GetSprite();
            }
        }
    }

    private void DoClick(MeetingPlayerButtonState player)
    {
        if (currentAction != null) currentAction.buttonAction.Invoke(player);
    }

    void UpdatePlayerState()
    {
        foreach (var button in allButtons)
        {
            button.gameObject.SetActive(currentAction?.predicate(button.state.Value!) ?? false);
        }
    }

    void CheckCurrentAction()
    {
        if (currentAction == null)
        {
            if (allActions.Count > 0) SetAction(allActions[0]);
            else return;
        }

        //今のアクションが無効なものであれば消去して他のアクションへ変更
        var lastAction = currentAction;
        var nextAction = currentAction;
        while (nextAction != null && AllStates.All(p => !(nextAction!.predicate.Invoke(p))))
        {
            int index = allActions.IndexOf(nextAction);
            allActions.RemoveAt(index);
            if (allActions.Count <= index) index = 0;

            //次のアクションへ
            nextAction = allActions.Count > 0 ? allActions[index] : null;
        }

        if (lastAction != nextAction) SetAction(nextAction);
    }

    void OnEndVoting(MeetingVoteEndEvent ev)
    {
        allButtons.Do(b =>
        {
            try
            {
                GameObject.Destroy(b.gameObject);
            }
            catch { }
        });
        allButtons.Clear();
    }

    void Update(GameUpdateEvent ev)
    {
        if (!MeetingHud.Instance) return;

        CheckCurrentAction();
        UpdatePlayerState();
    }

}
