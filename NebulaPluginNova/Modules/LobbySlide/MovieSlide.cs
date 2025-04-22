using Nebula.Modules.LobbySlideVariations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Il2CppMono.Security.X509.X520;
using static UnityEngine.RemoteConfigSettingsHelper;

namespace Nebula.Modules.LobbySlideVariations;

[NebulaRPCHolder]
public class LobbyOnlineMovieSlide : LobbySlide
{
    public override bool Loaded => true;

    public string Caption { get; private set; }
    public string Url { get; private set; }

    public LobbyOnlineMovieSlide(string tag, string title, string caption, string url, bool amOwner, string? prev = null, string? next = null) : base(tag, title, amOwner, prev, next)
    {
        Caption = caption;
        Url = url;
    }
    public override void Abandon()
    {
    }

    public override IMetaWidgetOld Show(out float height)
    {
        height = 1.4f + (9f * 0.35f);

        MetaWidgetOld widget = new();

        widget.Append(new MetaWidgetOld.Text(TitleAttribute) { RawText = Title, Alignment = IMetaWidgetOld.AlignmentOption.Center });

        Vector2 meshSize = new Vector2(16f, 9f) * 0.35f;
        widget.Append(new MetaWidgetOld.CustomWidget(meshSize, IMetaWidgetOld.AlignmentOption.Center, (parent, center) => {
            var mesh = UnityHelper.CreateMeshRenderer("MeshRenderer", parent, center.AsVector3(-0.5f), LayerExpansion.GetUILayer());
            mesh.filter.CreateRectMesh(meshSize);

            var movie = UnityHelper.SetMovieToMesh(mesh.renderer.gameObject, mesh.renderer, Url, true);
            movie.Play();

            bool paused = false;
            mesh.renderer.gameObject.SetUpButton(true).OnClick.AddListener(() =>
            {
                if (paused)
                    movie.Play();
                else
                    movie.Pause();
                paused = !paused;
            });
            var collider = mesh.renderer.gameObject.AddComponent<BoxCollider2D>();
            collider.size = meshSize;
            collider.isTrigger = true;
            
        }));

        widget.Append(new MetaWidgetOld.VerticalMargin(0.2f));

        widget.Append(new MetaWidgetOld.Text(CaptionAttribute) { RawText = Caption, Alignment = IMetaWidgetOld.AlignmentOption.Center });


        return widget;
    }

    public override void Load() => LobbySlideManager.StartCoroutine(CoLoad());
    public override void Reshare()
    {
        RpcShare.Invoke((Tag, Title, Caption, Url));
    }

    private IEnumerator CoLoad()
    {
        NebulaGameManager.Instance?.LobbySlideManager.OnLoaded(this);
        yield break;
    }

    static private RemoteProcess<(string tag, string title, string caption, string url)> RpcShare = new(
        "ShareOnlineLobbyMovieSlide",
        (message, amOwner) => NebulaGameManager.Instance?.LobbySlideManager.RegisterSlide(new LobbyOnlineMovieSlide(message.tag, message.title, message.caption, message.url, amOwner))
        );
}