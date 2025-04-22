using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Virial.Runtime;
using Nebula.Modules;

namespace Nebula.Modules.LobbySlideVariations;

public abstract class LobbyImageSlide : LobbySlide
{
    protected Sprite? mySlide { get; set; } = null;
    public override bool Loaded => mySlide;

    public string Caption { get; private set; }

    public LobbyImageSlide(string tag, string title, string caption, bool amOwner, string? prev = null, string? next = null) : base(tag, title, amOwner, prev, next)
    {
        Caption = caption;
    }
    public override void Abandon()
    {
        if (mySlide && mySlide!.texture) GameObject.Destroy(mySlide!.texture);
    }

    public override IMetaWidgetOld Show(out float height)
    {
        height = 1.4f;

        MetaWidgetOld widget = new();

        widget.Append(new MetaWidgetOld.Text(TitleAttribute) { RawText = Title, Alignment = IMetaWidgetOld.AlignmentOption.Center });

        if (mySlide != null)
        {
            //縦に大きすぎる画像はそれに合わせて調整する
            float width = Mathf.Min(5.4f, mySlide.bounds.size.x / mySlide.bounds.size.y * 2.9f);
            height += width / mySlide.bounds.size.x * mySlide.bounds.size.y;

            widget.Append(new MetaWidgetOld.Image(mySlide) { Alignment = IMetaWidgetOld.AlignmentOption.Center, Width = width });
        }

        widget.Append(new MetaWidgetOld.VerticalMargin(0.2f));

        widget.Append(new MetaWidgetOld.Text(CaptionAttribute) { RawText = Caption, Alignment = IMetaWidgetOld.AlignmentOption.Center });


        return widget;
    }
}

[NebulaRPCHolder]
public class LobbyOnlineImageSlide : LobbyImageSlide
{
    private string url;

    public LobbyOnlineImageSlide(string tag, string title, string caption, bool amOwner, string url, string? prev = null, string? next = null) : base(tag, title, caption, amOwner, prev, next)
    {
        this.url = url;
    }

    public override void Load() => LobbySlideManager.StartCoroutine(CoLoad());
    public override void Reshare()
    {
        RpcShare.Invoke((Tag, Title, Caption, url));
    }

    private async Task<byte[]> DownloadAsync()
    {
        var response = await NebulaPlugin.HttpClient.GetAsync(url);
        if (response.StatusCode != HttpStatusCode.OK) return Array.Empty<byte>();
        return await response.Content.ReadAsByteArrayAsync();
    }

    private IEnumerator CoLoad()
    {
        var task = DownloadAsync();
        while (!task.IsCompleted) yield return new WaitForSeconds(0.5f);

        if (task.Result.Length > 0)
        {
            mySlide = GraphicsHelper.LoadTextureFromByteArray(task.Result).ToSprite(100f);
            NebulaGameManager.Instance?.LobbySlideManager.OnLoaded(this);
        }
    }

    static private RemoteProcess<(string tag, string title, string caption, string url)> RpcShare = new(
        "ShareOnlineLobbyImageSlide",
        (message, amOwner) => NebulaGameManager.Instance?.LobbySlideManager.RegisterSlide(new LobbyOnlineImageSlide(message.tag, message.title, message.caption, amOwner, message.url))
        );
}
