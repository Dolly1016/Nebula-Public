using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.Cosmetics;

namespace Nebula.Behavior
{
    public enum UncertifiedReason
    {
        Waiting,
        UnmatchedVanilla,
        UnmatchedEpoch,
        UnmatchedBuild,
        UnmatchedAddon,
        Uncertified,
    }

    [NebulaRPCHolder]
    public class Certification
    {
        private static RemoteProcess<(byte playerId, int epoch, int build, int addonHash, string vanilla)> RpcHandshake = new(
            "Handshake", (message, calledByMe) => {
                var player = Helpers.GetPlayer(message.playerId);
                if (player?.gameObject.TryGetComponent<UncertifiedPlayer>(out var certification) ?? false)
                {
                    if(message.vanilla != Application.version)
                        certification.Reject(UncertifiedReason.UnmatchedVanilla);
                    else if (message.epoch != NebulaPlugin.PluginEpoch)
                        certification.Reject(UncertifiedReason.UnmatchedEpoch);
                    else if (message.build != NebulaPlugin.PluginBuildNum)
                        certification.Reject(UncertifiedReason.UnmatchedBuild);
                    else if (message.addonHash != NebulaAddon.AddonHandshakeHash)
                        certification.Reject(UncertifiedReason.UnmatchedAddon);
                    else
                        certification.Certify();
                }
            }
            , false);

        public static RemoteProcess<(byte playerId, string achievement)> RpcShareAchievement = new(
            "ShareAchievement",
            (message, _) =>
            {
                NebulaGameManager.Instance!.TitleMap[message.playerId] = Helpers.GetPlayer(message.playerId)?.GetTitleShower().SetAchievement(message.achievement);
            }, false);

        private static RemoteProcess RpcRequireHandshake = new(
            "RequireHandshake", (_) => Handshake()
            , false);

        private static void Handshake()
        {
            byte id = PlayerControl.LocalPlayer.PlayerId;
            RpcHandshake.Invoke((PlayerControl.LocalPlayer.PlayerId, NebulaPlugin.PluginEpoch, NebulaPlugin.PluginBuildNum, NebulaAddon.AddonHandshakeHash, Application.version));
            RpcShareAchievement.Invoke((PlayerControl.LocalPlayer.PlayerId, NebulaAchievementManager.MyTitle?.Id ?? "-"));
            DynamicPalette.RpcShareMyColor();
            NebulaAchievementManager.SendLastClearedAchievements();
            if (AmongUsClient.Instance.AmHost) ModSingleton<ShowUp>.Instance?.ShareSocialSettingsAsHost();
        }

        public static void RequireHandshake()
        {
            IEnumerator CoWaitAndRequireHandshake()
            {
                yield return new WaitForSeconds(0.5f);
                RpcRequireHandshake.Invoke();
            }

            AmongUsClient.Instance.StartCoroutine(CoWaitAndRequireHandshake().WrapToIl2Cpp());
        }

    }
    public class UncertifiedPlayer : MonoBehaviour
    {
        static UncertifiedPlayer() => ClassInjector.RegisterTypeInIl2Cpp<UncertifiedPlayer>();

        private static string ReasonToTranslationKey(UncertifiedReason reason) => "certification." + reason.ToString().HeadLower();

        public UncertifiedReason State { get; private set; }
        private TMPro.TextMeshPro myText = null!;
        private GameObject myShower = null!;
        public PlayerControl? MyControl = null;
        public void Start()
        {
            State = UncertifiedReason.Waiting;

            myShower = UnityHelper.CreateObject("UncertifiedHolder",gameObject.transform,new Vector3(0,0,-20f), LayerExpansion.GetPlayersLayer());
            (new MetaWidgetOld.Text(TextAttributeOld.BoldAttr) {
                TranslationKey = ReasonToTranslationKey(UncertifiedReason.Uncertified),
                PostBuilder = (text) => myText = text })
                .Generate(myShower, Vector2.zero,out _);
            myText.color = Color.red.RGBMultiplied(0.92f);
            myText.gameObject.layer = LayerExpansion.GetPlayersLayer();

            var button = myShower.SetUpButton(false);
            var collider = myShower.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.6f, 0.2f);
            button.OnMouseOver.AddListener(() =>
            {
                NebulaManager.Instance.SetHelpWidget(button, new MetaWidgetOld.VariableText(TextAttributeOld.ContentAttr) { Alignment = IMetaWidgetOld.AlignmentOption.Left, TranslationKey = ReasonToTranslationKey(State) + (AmongUsClient.Instance.AmHost ? ".detail" : ".client") });
            });
            button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
            OnStateChanged();

            IEnumerator CoWaitAndUpdate()
            {
                yield return new WaitForSeconds(0.8f);

                int tried = 0;

                do
                {
                    if (tried > 0)
                    {
                        Certification.RequireHandshake();
                    }
                    yield return new WaitForSeconds(0.5f);
                    tried++;
                } while (tried < 10 && State == UncertifiedReason.Waiting);

                if (State == UncertifiedReason.Waiting) Reject(UncertifiedReason.Uncertified);
            }
            StartCoroutine(CoWaitAndUpdate().WrapToIl2Cpp());
        }
        public void Certify()
        {
            if(this) GameObject.Destroy(this);
        }
        public void Reject(UncertifiedReason reason)
        {
            State = reason;
            OnStateChanged();

            //MyControl?.OwnerId == AmongUsClient.Instance.HostId
            if ((MyControl?.AmOwner ?? false) && !AmongUsClient.Instance.AmHost)
            {
                var screen = MetaScreen.GenerateWindow(new(3.8f,1.78f),HudManager.Instance.transform,Vector3.zero, true,false);
                var widget = new MetaWidgetOld();
                widget.Append(new MetaWidgetOld.Text(TextAttributeOld.BoldAttr) { Alignment = IMetaWidgetOld.AlignmentOption.Center, TranslationKey = ReasonToTranslationKey(State) });
                widget.Append(new MetaWidgetOld.Text(new TextAttributeOld(TextAttributeOld.NormalAttr) { Alignment = TMPro.TextAlignmentOptions.Top, Size = new(3.7f, 0.9f) }.EditFontSize(1.5f, 0.7f, 1.5f)) { TranslationKey = ReasonToTranslationKey(State) + ".client", Alignment = IMetaWidgetOld.AlignmentOption.Center });
                widget.Append(new MetaWidgetOld.Button(() => AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame), new(TextAttributeOld.BoldAttr) { Size = new(1f,0.26f)}) { TranslationKey = "ui.dialog.exit", Alignment = IMetaWidgetOld.AlignmentOption.Center });
                screen.SetWidget(widget);
            }
            
        }

        private void OnStateChanged()
        {
            myText.text = Language.Translate(ReasonToTranslationKey(State));
        }

        public void Update()
        {
            myShower.SetActive((AmongUsClient.Instance.AmHost || (MyControl?.AmHost() ?? false) || (MyControl?.AmOwner ?? false)));
        }

        public void OnDestroy()
        {
            GameObject.Destroy(myShower);
        }
    }
}
