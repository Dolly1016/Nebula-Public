using Nebula.Modules.Cosmetics;
using Nebula.Roles.Abilities;
using Nebula.SpecialModes.AeroGuesser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.DI;
using Virial.Game;
using static Rewired.UnknownControllerHat;
using static Sentry.MeasurementUnit;

namespace Nebula.SpecialModes.PaintQuiz;

[NebulaRPCHolder]
internal class PaintQuizSenario : AbstractModuleContainer, IModule, IGameModePaintQuiz
{
    internal PaintQuizSenario()
    {
        ModSingleton<PaintQuizSenario>.Instance = this;
        CoPlaySenario().StartOnScene();
    }

    //ゲームモードの設定
    bool IGameModeModule.AllowSpecialGameEnd => false;
    bool IGameModeModule.ShowMap => false;
    bool IGameModeModule.ShowStatistics => false;
    bool IGameModeModule.CanUseStampOnly => true;

    static public IEnumerator CoIntro(bool amHost)
    {
        if (amHost) RpcIntro.Invoke((0, 0));

        SpecialModeFunctions.IntroSetUp();
        yield break;
    }

    private GameObject baseObject;
    private SpriteRenderer backgroundRenderer;
    private GameObject clickGuard;

    private DyingMessageBrush brush = new(Color.black, 0.003f);
    private void Initialize()
    {
        var hud = HudManager.Instance;
        hud.roomTracker.gameObject.SetActive(false);

        baseObject = UnityHelper.CreateObject("PaintQuiz", hud.transform, UnityEngine.Vector3.zero);
        //背景の作成
        backgroundRenderer = UnityHelper.CreateObject<SpriteRenderer>("Background", baseObject.transform, new(0f, 0f, 5f));
        backgroundRenderer.sprite = VanillaAsset.TitleBackgroundSprite;
        backgroundRenderer.material = new(NebulaAsset.HSVNAShader);
        backgroundRenderer.sharedMaterial.SetFloat("_Sat", 0.55f);
        backgroundRenderer.sharedMaterial.SetFloat("_Hue", 280f);
        backgroundRenderer.sharedMaterial.SetFloat("_Val", 0.8f);

        //クリックガード
        clickGuard = UnityHelper.CreateObject("ClickGuard", baseObject.transform, new(0f, 0f, -50f));
        clickGuard.SetUpButton(false);
        var collider = clickGuard.AddComponent<BoxCollider2D>();
        collider.size = new(20f, 20f);
        collider.isTrigger = false;
        clickGuard.SetActive(true);

        //各種モジュールの初期化
        

        //モジュールを初期状態へ
        

        //InitializeForOneQuiz();
    }


    private IEnumerator CoPlaySenario()
    {
        //1フレーム待つ
        yield return null;

        //スタンプ
        StampHelpers.SetStampShowerToUnderHud();

        Initialize();

        //同期
        yield return NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.CoSync(SynchronizeTag.PreStartGame, true);

        HudManager.Instance.StartCoroutine(HudManager.Instance.CoFadeFullScreen(Color.black, Color.clear, 0.8f));

        var canvas = UnityHelper.CreateObject<DyingMessageCanvas>("PaintCanvas", HudManager.Instance.transform, new(0f, -0.4f, -480f));
        canvas.SetUpAsQuizPaint();
        canvas.SetBrush(brush);

        yield break;
    }

    static private readonly RemoteProcess<(int unusedNum1, int unusedNum2)> RpcIntro = new("PaintQuiz.Intro", (message, _) =>
    {
        SpecialModeFunctions.InRpcSetUp();
        //ModSingleton<PaintQuizSenario>.Instance!.SetUp(message.mapMask, message.quizNum, message.monoColor);
    });
}
