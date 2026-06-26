using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;
using Virial.Media;
using Virial.Utilities;
using static Rewired.UnknownControllerHat;

namespace Nebula.Modules;

public interface IUpperRightContent
{
    bool IsShown { get; }
    int Priority { get; }
    void Update(VVector3 localPos);
    void VisibilityUpdate() { }
}

public class FunctionalUpperRightContent : IUpperRightContent
{
    PassiveButton button;
    GameObject buttonObj;

    public FunctionalUpperRightContent(PassiveButton button, int priority)
    {
        this.button = button;
        this.buttonObj = button.gameObject;
        if (button.TryGetComponent<AspectPosition>(out var ap)) ap.enabled = false;

        (button.transform.FindChild("background") ?? button.transform.FindChild("Background")).gameObject.SetActive(false);
        Priority = priority;
    }

    public bool IsShown => buttonObj.active;
    public int Priority { get; init; }
    void IUpperRightContent.Update(VVector3 localPos)
    {
        button.transform.position = ModSingleton<UpperRightButtons>.Instance.HolderLocalPosToWorldPos(localPos);
    }
}

public class CustomUpperRightContent : IUpperRightContent
{
    public PassiveButton Button => button;
    private GameObject buttonObj;
    private Transform buttonTransform;
    PassiveButton button;
    SpriteRenderer renderer;
    Func<bool> predicate;

    bool IUpperRightContent.IsShown => buttonObj.active;

    public int Priority { get; init; }
    static private readonly IDividedSpriteLoader buttonImages = DividedSpriteLoader.FromResource("Nebula.Resources.UpperRightButton.png", 100f, 100, 94, true);

    void IUpperRightContent.Update(VVector3 localPos)
    {
        buttonTransform.localPosition = localPos;
    }

    void IUpperRightContent.VisibilityUpdate()
    {
        buttonObj.SetActive(predicate.Invoke());
    }
    public CustomUpperRightContent(int priority, Image inactiveSprite, Image activeSprite, Image selectedSprite, Action onClicked, Func<bool> predicate, Func<bool> selectedSpritePredicate)
    {
        Priority = priority;
        renderer = UnityHelper.CreateObject<SpriteRenderer>("Button", ModSingleton<UpperRightButtons>.Instance.ButtonsHolder, VVector3.Zero, out buttonObj, out buttonTransform);
        renderer.sprite = inactiveSprite.GetSprite();
        button = buttonObj.SetUpButton(true, renderer, Virial.Color.White, Virial.Color.White);
        buttonTransform.localScale = new(UpperRightButtons.BackgroundScale, UpperRightButtons.BackgroundScale, 1f);
        var collider = buttonObj.AddComponent<BoxCollider2D>();
        collider.size = new(1f, 1f);
        collider.isTrigger = true;

        bool imageLocked = false;
        button.OnMouseOver.AddListener(() =>
        {
            imageLocked = false;
            renderer.sprite = activeSprite.GetSprite();
        });
        button.OnMouseOut.AddListener(() => {
            if(!imageLocked) renderer.sprite = inactiveSprite.GetSprite();
        });
        button.OnClick.AddListener(() =>
        {
            onClicked.Invoke();
            renderer.sprite = selectedSprite.GetSprite();
            imageLocked = true;
            NebulaManager.Instance.StartCoroutine(ManagedEffects.Wait(selectedSpritePredicate, () =>
            {
                imageLocked = false;
                if (renderer.AsBoolFast()) renderer.sprite = inactiveSprite.GetSprite();
            }).WrapToIl2Cpp());
        });

        this.predicate = predicate;
    }

    internal CustomUpperRightContent(int priority, int imageIndex, Action onClicked, Func<bool> predicate, Func<bool> selectedSpritePredicate)
        :this(priority, buttonImages.AsLoader(imageIndex * 3), buttonImages.AsLoader(imageIndex * 3 + 1), buttonImages.AsLoader(imageIndex * 3 + 2), onClicked, predicate, selectedSpritePredicate)
    { }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
internal class UpperRightButtons : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static UpperRightButtons()
    {
        DIManager.Instance.RegisterModule(() => new UpperRightButtons());
    }
    public UpperRightButtons()
    {
        ModSingleton<UpperRightButtons>.Instance = this;
    }
    private OrderedList<IUpperRightContent, int> allContents = OrderedList<IUpperRightContent, int>.AscendingList(content => content.Priority);
    public void Register(IUpperRightContent content)
    {
        allContents.Add(content);
    }
    public Vector3 HolderLocalPosToWorldPos(VVector3 localPos) => buttonsHolder.transform.TransformPoint(localPos);
    protected override void OnInjected(Virial.Game.Game container)
    {
        SetUp();
        this.Register(container);
    }

    AspectPosition holder;
    SpriteRenderer background;
    GameObject buttonsHolder;
    private float backSizeY;
    ValueObserver<int> numOfButtonsObserver;
    public Transform ButtonsHolder => buttonsHolder.transform;
    public const float BackgroundScale = 0.551f;
    const float ButtonWidth = 0.55f;
    static private ResourceExpandableSpriteLoader backgroundSprite = new("Nebula.Resources.RightUpperButtonHolder.png", 100f, 50, 50);
    private void SetUp()
    {
        var hud = HudManager.Instance;

        holder = UnityHelper.CreateObject<AspectPosition>("ButtonsHolder", hud.transform, Vector3.zero);
        holder.Alignment = AspectPosition.EdgeAlignments.RightTop;
        holder.DistanceFromEdge = new(0.48f, 0.51f, -400f);
        holder.parentCam = hud.UICamera;
        holder.updateAlways = true;

        background = UnityHelper.CreateObject<SpriteRenderer>("background", holder.transform, Vector3.zero);
        background.sprite = backgroundSprite.GetSprite();
        background.drawMode = SpriteDrawMode.Sliced;
        background.tileMode = SpriteTileMode.Continuous;
        background.transform.localScale = new(BackgroundScale, BackgroundScale, 1f);

        backSizeY = background.sprite.bounds.size.y;

        buttonsHolder = UnityHelper.CreateObject("Holder", holder.transform, new(0f,0f,-10f));

        var friendListButton = hud.GetComponentInChildren<FriendsListButton>();
        friendListButton.NotifCircle.transform.localPosition = new(0.6f, 0.95f, -1f);
        Register(new FunctionalUpperRightContent(friendListButton.Button.GetComponent<PassiveButton>(), 0));

        var chat = hud.Chat;
        var chatButton = chat.chatButton;
        var zeroVec = Vector3.zero;
        chatButton.activeSprites.transform.localPosition = zeroVec;
        chatButton.inactiveSprites.transform.localPosition = zeroVec;
        chatButton.selectedSprites.transform.localPosition = zeroVec;
        chatButton.GetComponent<Collider2D>().offset = Vector2.zero;
        chat.chatNotifyDot.transform.localPosition = new(0.23f, 0.23f, -1f);
        chat.chatButtonAspectPosition = holder;
        Register(new FunctionalUpperRightContent(chatButton, 10));
        Register(new FunctionalUpperRightContent(hud.SettingsButton.GetComponent<PassiveButton>(), 5));
        Register(new FunctionalUpperRightContent(hud.MapButton, 1));

        var lastGame = new CustomUpperRightContent(101, 1, () => LastGameHistory.ShowLastGameStatistics(), () => LastGameHistory.ArchivedGame != null && AmongUsLLImpl.LobbyInstance.AsBoolFast() && !PlayerCustomizationMenu.Instance.AsBoolFast(), () => LastGameHistory.ScreenIsVisible);
        Register(lastGame);
        lastGame.Button.OnMouseOver.AddListener(() =>
        {
            if (LastGameHistory.LastWidget != null) NebulaManager.Instance.SetHelpWidget(lastGame.Button, LastGameHistory.LastWidget);
        });
        lastGame.Button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(lastGame.Button));

        var helpButton = new CustomUpperRightContent(100, 0, () => HelpScreen.TryOpenHelpScreen(HelpScreen.HelpTab.MyInfo), () => NebulaGameManager.Instance?.GameMode?.CanOpenHelpScreen ?? true, () => HelpScreen.LastHelpScreen);
        Register(helpButton);
        GameOperatorManager.Instance?.Subscribe<GameStartEvent>(_ =>
        {
            ButtonEffect.SetKeyGuideOnSmallButton(helpButton.Button.gameObject, NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Help).TypicalKey);
        }, MyContainer);

#if PC
        var formButton = new CustomUpperRightContent(102, 2, () => DevTeamContact.OpenContactWindow(hud.transform), () => AmongUsLLImpl.LobbyInstance.AsBoolFast() && !PlayerCustomizationMenu.Instance.AsBoolFast(), () => DevTeamContact.IsShown);
        Register(formButton);
#endif

        numOfButtonsObserver = new(0, index => {
            background.transform.localPosition = new(-ButtonWidth * 0.5f * (float)(index - 1) + 0.008f, 0.0228f, 0.1f);
            background.size = new(1.48f + ButtonWidth / BackgroundScale * (float)(index - 1), backSizeY);
        }, true);

        OnUpdate(null!);
    }

    void OnUpdate(UpdateEvent ev)
    {
        int index = 0;
        foreach(var c in allContents)
        {
            c.VisibilityUpdate();

            if (!c.IsShown) continue;

            c.Update(new(-ButtonWidth * (float)index, 0f, 0f));
            index++;
        }

        numOfButtonsObserver.Set(index);
    }

}
