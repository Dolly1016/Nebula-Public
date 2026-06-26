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
    bool IGameModeModule.CanOpenHelpScreen => false;

    static public IEnumerator CoIntro(bool amHost)
    {
        if (amHost) RpcIntro.Invoke((QuizCategories.TitleToRole, 5));

        SpecialModeFunctions.IntroSetUp();
        yield break;
    }

    QuizCategoryStrategy? category = null;
    int numOfQuizzes = 0;

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

    private void SetUp(QuizCategories category, int numOfQuizzes)
    {
        this.category = QuizCategoryStrategy.Create(category);
        this.numOfQuizzes = numOfQuizzes;
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

        //事前共有 (フェードアウト待機に先んじて送っておく)
        using(RPCRouter.CreateSection("PaintQuiz.PreSharing", true))
        {
            RpcPreSharing.Invoke(category?.SuggestMyCandidate(numOfQuizzes) ?? []);
            NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.SendSync(SynchronizeTag.PaintQuizPreSharing);
        }

        //フェードアウト明け待機
        yield return Effects.Wait(0.6f); 

        //事前共有の同期
        yield return NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.CoSync(SynchronizeTag.PaintQuizPreSharing, true);

        for(int i = 0; i < numOfQuizzes; i++)
        {
            int? quizSeed = category!.GenerateQuizSeed();
            if (!quizSeed.HasValue) break;
            LogUtils.WriteToConsole(category.GetQuizText(quizSeed.Value, []));
            LogUtils.WriteToConsole("答え：" + category.GetAnswerText(quizSeed.Value));
            yield return CoIntro(1 + i, numOfQuizzes);
        }
        

        var canvas = UnityHelper.CreateObject<DyingMessageCanvas>("PaintCanvas", HudManager.Instance.transform, new(0f, -0.4f, -480f));
        canvas.SetUpAsQuizPaint();
        canvas.SetBrush(brush);

        yield break;
    }

    static private readonly RemoteProcess<(QuizCategories category, int numOfQuizzes)> RpcIntro = new("PaintQuiz.Intro", (message, _) =>
    {
        SpecialModeFunctions.InRpcSetUp();
        ModSingleton<PaintQuizSenario>.Instance!.SetUp(message.category, message.numOfQuizzes);
    });

    static private readonly RemoteProcess<int[]> RpcPreSharing = new("PaintQuiz.PreSharing", (message, _) =>
    {
        ModSingleton<PaintQuizSenario>.Instance.category?.OnReceivePreSharing(message);
    });

    private IEnumerator CoIntro(int num, int maxQuiz)
    {
        var fastQ = Arithmetic.Decel(1f, 0f, 0.3f);
        var slowQ = Arithmetic.Decel(1f, 0f, 1.15f);
        var fadeOutQ = Arithmetic.Sequential((() => Arithmetic.FloatOne, 1.4f), (() => Arithmetic.Decel(1f, 0, 0.4f), 1f));
        NebulaAPI.CurrentGame?.GetModule<TitleShower>()?.SetText($"{num}<size=80%>/{maxQuiz}</size>" , Color.white, new(shower => {
            shower.Transform.localEulerAngles = new(0f, 0f, fastQ.Value * 220f);
            var scale = slowQ.Value * 0.4f + 0.6f;
            shower.Transform.localScale = new(scale, scale, 1f);

            shower.SetTextAlpha(fadeOutQ.Value);
        }));
        yield return Effects.Wait(1.8f);
    }
}
