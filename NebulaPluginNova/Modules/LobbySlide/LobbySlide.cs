using System.Net;
using Virial.Runtime;

namespace Nebula.Modules;

public abstract class LobbySlide
{
    public string Tag { get; private set; }
    public string Title { get; private set; }
    public bool AmOwner { get; private set; }
    public string? Prev { get; private set; } = null;
    public string? Next { get; private set; } = null;

    public bool Shared = false;
    public abstract bool Loaded { get; }

    public LobbySlide(string tag, string title, bool amOwner, string? prev = null, string? next = null)
    {
        Tag = tag;
        Title = title;
        AmOwner = amOwner;
        Prev = prev;
        Next = next;
    }

    public virtual void Load() { }

    public void Share()
    {
        if (Shared) return;
        Reshare();
        Shared = true;
    }

    public abstract void Reshare();
    public virtual void Abandon() { }
    public abstract IMetaWidgetOld Show(out float height);


    protected static readonly TextAttributeOld TitleAttribute = new(TextAttributeOld.TitleAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new Vector2(5f, 0.5f) };
    protected static readonly TextAttributeOld CaptionAttribute = new(TextAttributeOld.NormalAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new Vector2(6f, 0.5f) };
}

public class LobbySlideTemplate
{
    [JsonSerializableField]
    public string Tag = null!;
    [JsonSerializableField]
    public string Title = "None";
    [JsonSerializableField]
    public string SlideType = "None";
    [JsonSerializableField]
    public string Argument = "None";
    [JsonSerializableField(true)]
    public string Caption = "None";

    [JsonSerializableField(true)]
    public string? Next = null;
    [JsonSerializableField(true)]
    public string? Prev = null;
    [JsonSerializableField(true)]
    public bool IsHidden = false;

    public LobbySlide? Generate()
    {
        switch (SlideType.ToLower())
        {
            case "online":
            case "onlineimage":
                return new LobbySlideVariations.LobbyOnlineImageSlide(Tag,Title,Caption,true,Argument, Prev, Next);
            case "onlinemovie":
                return new LobbySlideVariations.LobbyOnlineMovieSlide(Tag, Title, Caption, Argument, true, Prev, Next);
        }

        return null;
    }

    public void TryRegisterAndShow()
    {
        NebulaGameManager.Instance?.LobbySlideManager.TryRegisterAndShow(Generate());
    }
}

[NebulaRPCHolder]
[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
public class LobbySlideManager
{
    public readonly Dictionary<string,LobbySlide> allSlides = [];
    static public readonly Dictionary<string, LobbySlideTemplate> AllTemplates = [];
    private MetaScreen? myScreen = null;
    private (string tag, bool detatched, bool calledByMe)? lastShowRequest;
    public bool IsValid { get; private set; } = true;

    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Loading Lobby Slides");

        foreach (var addon in NebulaAddon.AllAddons)
        {
            using var stream = addon.OpenStream("Slides/LobbySlides.json");
            if (stream == null) continue;

            var templates = JsonStructure.Deserialize<List<LobbySlideTemplate>>(stream);
            if (templates == null) continue;

            foreach (var entry in templates)
            {
                entry.Tag = addon.AddonName + "." + entry.Tag;
                if (entry.Prev != null) entry.Prev = addon.AddonName + "." + entry.Prev;
                if (entry.Next != null) entry.Next = addon.AddonName + "." + entry.Next;

                AllTemplates[entry.Tag] = entry;
            }

            yield return null;
        }
    }

    public void RegisterSlide(LobbySlide slide)
    {
        if (!IsValid) return;

        if (!allSlides.ContainsKey(slide.Tag))
        {
            allSlides[slide.Tag] = slide;
            slide.Load();
            if (slide.AmOwner) slide.Share();
        }
    }

    public void RpcReshareSlide(string tag)
    {
        if (!IsValid) return;

        if (allSlides.TryGetValue(tag,out var slide))
        {
            slide.Reshare();
        }
    }

    public void Abandon()
    {
        if(!IsValid) return;

        foreach (var slide in allSlides.Values) slide.Abandon();
        if (myScreen) myScreen!.CloseScreen();
        IsValid = false;
    }

    static public readonly RemoteProcess<(string tag, bool detatched)> RpcShow = new(
        "ShowSlide", (message, calledByMe) => NebulaGameManager.Instance?.LobbySlideManager.ShowSlide(message.tag, message.detatched, calledByMe)
        );

    public void RpcShowScreen(string tag,bool detached)
    {
        if (!IsValid) return;

        if (allSlides.TryGetValue(tag, out var slide))
        {
            slide.Reshare();
            RpcShow.Invoke((tag, detached));
        }
    }

    private void ShowSlide(string tag, bool detached, bool calledByMe)
    {
        if (!allSlides.TryGetValue(tag, out var slide) || !slide.Loaded)
            lastShowRequest = (tag, detached, calledByMe);
        else
        {
            if (myScreen)
            {
                myScreen!.CloseScreen();
                myScreen = null;
            }

            var widget = slide.Show(out float height);
            var screen = MetaScreen.GenerateWindow(new(6.2f, Mathf.Min(height, 4.3f)), HudManager.Instance.transform, new Vector3(0, 0, -100f), true, false);
            screen.SetWidget(widget);

            if (calledByMe)
            {
                Debug.Log("Prev: " + slide.Prev + ", Next: " + slide.Next);
                bool requestedChangeSlide = false;
                if (slide.Prev != null && AllTemplates.TryGetValue(slide.Prev, out var prev))
                {
                    GUI.API.RawButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), "<<", _ =>
                    {
                        if (requestedChangeSlide) return;
                        requestedChangeSlide = true;
                        prev.TryRegisterAndShow();
                    }).Instantiate(new Virial.Media.Anchor(new(0.5f, 0.5f), new(-3.3f, 0f, -0.2f)), new(100f, 100f), out _)?.transform.SetParent(screen.transform, false);
                }

                if (slide.Next != null && AllTemplates.TryGetValue(slide.Next, out var next))
                {
                    GUI.API.RawButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), ">>", _ =>
                    {
                        if (requestedChangeSlide) return;
                        requestedChangeSlide = true;
                        next.TryRegisterAndShow();
                    }).Instantiate(new Virial.Media.Anchor(new(0.5f,0.5f), new(3.3f,0f,-0.2f)), new(100f,100f), out _)?.transform.SetParent(screen.transform, false);
                }
            }

            if (!detached) myScreen = screen;

            lastShowRequest = null;
        }
    }

    public void OnLoaded(LobbySlide slide)
    {
        if (lastShowRequest == null) return;

        if (slide.Tag == lastShowRequest?.tag)
        {
            ShowSlide(lastShowRequest.Value.tag, lastShowRequest.Value.detatched, lastShowRequest.Value.calledByMe);
            lastShowRequest = null;
        }
    }

    static public void StartCoroutine(IEnumerator coroutine)
    {
        if (LobbyBehaviour.Instance) LobbyBehaviour.Instance.StartCoroutine(coroutine.WrapToIl2Cpp());
    }

    public void TryRegisterAndShow(LobbySlide? slide)
    {
        if (slide == null) return;

        RegisterSlide(slide);
        RpcShowScreen(slide.Tag, false);
    }
}
