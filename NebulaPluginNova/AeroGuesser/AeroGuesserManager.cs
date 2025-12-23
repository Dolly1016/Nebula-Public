using Epic.OnlineServices.CustomInvites;
using Nebula.Behavior;
using Nebula.Modules;
using Nebula.Modules.Cosmetics;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Virial;
using Virial.Assignable;
using Virial.DI;
using Virial.Game;
using Virial.Text;

namespace Nebula.AeroGuesser;

[NebulaRPCHolder]
internal class AeroGuesserSenario : AbstractModuleContainer, IModule, IGameModeAeroGuesser, IMinimapViewerInteraction, IMapButtonInteraction, IMapCameraInteraction, IWithAnswerPhase, IScoreBoardViewerInteraction, IAnswerMenuInteraction
{
    internal AeroGuesserSenario()
    {
        ModSingleton<AeroGuesserSenario>.Instance = this;
        CoPlaySenario().StartOnScene();
    }

    //ゲームモードの設定
    bool IGameModeModule.AllowSpecialGameEnd => false;
    bool IGameModeModule.ShowMap => false;
    bool IGameModeModule.ShowStatistics => false;
    bool IGameModeModule.CanUseStampOnly => true;

    string? IGameModeModule.GetAlternativeWinOrLoseText()
    {
        if(GamePlayer.AllOrderedPlayers.Count <= 1)
        {
            return String.Format("{0:N0}", ScoreOrderedPlayers[0].score) + "pt";
        }
        int rank = 0;
        int score = -1;
        foreach(var p in ScoreOrderedPlayers)
        {
            if (score != p.score)
            {
                rank++;
                score = p.score;
            }
            if (p.player.AmOwner) break;
        }
        return Language.Translate("aeroGuesser.rank." + rank) + ": " + String.Format("{0:N0}", score) + "pt";
    }
    bool IGameModeModule.ShowButtons => false;

    static private readonly Virial.Color[] RankColor = [new(255, 196, 0), new(170, 180, 186), new(193, 119, 31)];

    (GamePlayer player, int score)[] ScoreOrderedPlayers => GamePlayer.AllPlayers.Select(p => (p, scoreMap.GetScore(p.PlayerId))).OrderBy(p => -p.Item2).ToArray();
    string? IGameModeModule.GetAlternativePlayerStatusText()
    {
        var orderedPlayers = ScoreOrderedPlayers;
        if (orderedPlayers.Length == 0) return "";
        StringBuilder sb = new();

        int rank = 0;
        int lastScore = orderedPlayers[0].score;
        foreach(var entry in orderedPlayers)
        {
            int score = entry.score;
            if (lastScore != score) rank++;

            sb.Append(Language.Translate("aeroGuesser.rank." + (rank + 1)).Color(rank < RankColor.Length ? RankColor[rank].ToUnityColor() : Color.white));
            sb.Append("<indent=3.4em>");
            sb.Append(String.Format("{0:N0}", score) + "pt");
            sb.Append("</indent>");
            sb.Append("<indent=10.4em>");
            sb.Append(entry.player.Name);
            sb.Append("</indent>");
            sb.AppendLine();
        }
        return sb.ToString();
    }
    //カスタマイズされたゲーム設定
    private record Settings(int MapMask, int QuizNum, bool MonoColor);
    private Settings CurrentSetting { get; set; }
    int IMapButtonInteraction.MapMask => CurrentSetting.MapMask;

    //各種Q
    IFunctionalValue<float> mapButtonsQ = Arithmetic.FloatZero;
    IFunctionalValue<float> answerQ = Arithmetic.FloatZero;
    IFunctionalValue<float> showWidelyQ = Arithmetic.FloatZero;
    IFunctionalValue<float> endQuizQ = Arithmetic.FloatZero;
    IFunctionalValue<float> scoreBoardQ = Arithmetic.FloatZero;
    
    float IMinimapViewerInteraction.MapQ => 1f - mapButtonsQ.Value;
    float IMapButtonInteraction.MapQ => mapButtonsQ.Value;
    float IMapCameraInteraction.MapButtonQ => Mathn.Max(mapButtonsQ.Value, 0f);
    float IMapCameraInteraction.ShowWidelyQ => showWidelyQ.Value;
    float IWithAnswerPhase.AnswerQ => answerQ.Value;
    float IMapCameraInteraction.EndQuizQ => endQuizQ.Value;
    bool IMapCameraInteraction.MonoColor =>  CurrentSetting.MonoColor;
    float IScoreBoardViewerInteraction.ScoreBoardQ =>  scoreBoardQ.Value;

    static public int MapMask { get {
            int mask = 0;
            if (GeneralConfigurations.AeroGuesserSkeldOption > 0) mask |= 0b000001;
            if (GeneralConfigurations.AeroGuesserMIRAOption > 0) mask |= 0b000010;
            if (GeneralConfigurations.AeroGuesserPolusOption > 0) mask |= 0b000100;
            if (GeneralConfigurations.AeroGuesserAirshipOption > 0) mask |= 0b010000;
            if (GeneralConfigurations.AeroGuesserFungleOption > 0) mask |= 0b100000;
            if (mask == 0) mask = 0b110111;
            return mask;
        }
    }

    static public IEnumerator CoIntro(bool amHost) {
        if (amHost) RpcIntro.Invoke((MapMask, GeneralConfigurations.NumOfQuizOption, GeneralConfigurations.MonochromeModeOption)); 
        AmongUsClient.Instance.SendClientReady();
        HudManager.Instance.OnGameStart();

        //フレンドボタン非表示
        if (FriendsListManager.InstanceExists && FriendsListManager.Instance.FriendsListButton) FriendsListManager.Instance.FriendsListButton.showInScene = false;

        //ボタン類非表示
        var buttonHolder = HudManager.Instance.AbilityButton.transform.parent.gameObject;
        buttonHolder.transform.localPosition = new(0f, 0f, 20f);
        buttonHolder.SetActive(false);

        NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.SendSync(SynchronizeTag.PreStartGame);
        yield break;
    }

    private IEnumerator CoPlaySenario() {
        //1フレーム待つ
        yield return null;

        //スタンプ
        StampHelpers.SetStampShowerToUnderHud();

        Initialize();
        ClientOption.CoChangeAmbientVolume(true).StartOnScene();

        //同期
        yield return NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.CoSync(SynchronizeTag.PreStartGame, true);

        
        HudManager.Instance.StartCoroutine(HudManager.Instance.CoFadeFullScreen(Color.black, Color.clear, 0.8f));

        //ホストであればここで問題を作成する
        AeroGuesserQuizData.QuizEntry[]? entries = AmongUsClient.Instance.AmHost ? AeroGuesserQuizData.GetRandomEntry(CurrentSetting.QuizNum, [
                GeneralConfigurations.AeroGuesserSkeldOption, 
                GeneralConfigurations.AeroGuesserMIRAOption, 
                GeneralConfigurations.AeroGuesserPolusOption, 
                0f, 
                GeneralConfigurations.AeroGuesserAirshipOption, 
                GeneralConfigurations.AeroGuesserFungleOption
            ], [
                GeneralConfigurations.AeroGuesserNormalOption,
                GeneralConfigurations.AeroGuesserHardOption
            ]) : null;
        
        //Debug
        //entries = AmongUsClient.Instance.AmHost ? AeroGuesserQuizData.GetAllEntry() : null;
        
        int entriesIndex = 0;

        while (true)
        {
            //出題 or ゲーム終了
            if (AmongUsClient.Instance.AmHost)
            {
                //ホストで問題を持っているなら次の問題をすすめる。繰り上がりホストであれば廃村。
                if (entries == null)
                {
                    NebulaGameManager.Instance?.RpcInvokeForciblyWin(NebulaGameEnd.NoGame, 0);
                }
                else
                {
                    if(entriesIndex >= entries.Length)
                    {
                        int winnersMask = 0;
                        var orderedPlayers = ScoreOrderedPlayers;
                        var maxScore = orderedPlayers[0].score;
                        foreach(var entry in orderedPlayers)
                        {
                            if (entry.score < maxScore) break;
                            winnersMask |= 1 << entry.player.PlayerId;
                        }
                        NebulaGameManager.Instance?.RpcInvokeForciblyWin(NebulaGameEnd.GameEnd, winnersMask);
                    }
                    else
                    {
                        var entry = entries[entriesIndex];
                        RpcSendQuiz.Invoke((entriesIndex == 0 || entriesIndex + 1 == entries.Length, entry.mapId, (int)entry.difficulty, entry.position, entry.viewport));
                    }
                }
            }

            while (!unprocessedQuiz.HasValue) yield return null;
            var currentQuiz = unprocessedQuiz.Value.entry;
            var isFirstOrLast = unprocessedQuiz.Value.isFirstOrLast;
            unprocessedQuiz = null;
            CoPlayOneQuizAsHost(currentQuiz).StartOnScene();
            yield return CoPlayOneQuiz(currentQuiz, isFirstOrLast);
            entriesIndex++;
            
        }
    }

    private IEnumerator CoCountDown()
    {
        int nextCount = 3;

        int ShowCount(int count)
        {
            var fastQ = Arithmetic.Decel(1f, 0f, 0.3f);
            var slowQ = Arithmetic.Decel(1f, 0f, 1.15f);
            var fadeOutQ = Arithmetic.Sequential((() => Arithmetic.FloatOne, 0.85f), (()=> Arithmetic.Decel(1f, 0, 0.4f), 1f));
            NebulaAPI.CurrentGame?.GetModule<TitleShower>()?.SetText(count.ToString(), Color.white, new(shower => {
                shower.Transform.localEulerAngles = new(0f, 0f, fastQ.Value * 220f);
                var scale = slowQ.Value * 0.8f + 1f;
                shower.Transform.localScale = new(scale, scale, 1f);

                shower.SetTextAlpha(fadeOutQ.Value);
            }));
            return count - 1;
        }

        yield return Effects.Lerp(3f, (Il2CppSystem.Action<float>)(p => {
            p = 3f - p * 3f;
            if (p < (float)nextCount && nextCount > 0) nextCount = ShowCount(nextCount);
        }));
    }


    private IEnumerator CoPlayOneQuiz(AeroGuesserQuizData.QuizEntry entry, bool isFirstOrLast)
    {
        var synchronizer = NebulaAPI.CurrentGame?.GetModule<Synchronizer>();

        SetClickGuard(true);

        mapViewer.Hide();
        mapButtons.SetActive(false);
        mapButtonsQ = Arithmetic.Constant(-1f);
        minimap.ResetSelection();
        minimap.SetAsSelectPhase();
        answerQ = Arithmetic.FloatZero;
        endQuizQ = Arithmetic.FloatZero;
        mapViewer.SetReshowing(false);

        //マップのセットアップ
        yield return mapViewer.SetUpMap(entry.mapId, entry.position, entry.viewport);

        

        //クイズ開始前の同期
        synchronizer?.SendSync(SynchronizeTag.AeroGuessPreQuiz);
        yield return synchronizer?.CoSync(SynchronizeTag.AeroGuessPreQuiz, true);
        synchronizer?.ResetSync(SynchronizeTag.AeroGuessPreQuiz);

        yield return CoCountDown();

        //問題の提示
        IFunctionalValue<float> quizTimer = Arithmetic.Timer(GeneralConfigurations.GuessTimeOption);
        mapViewer.Show();
        mapButtons.SetActive(true);
        SetClickGuard(false);
        mapButtonsQ = Arithmetic.Decel(-1f, 0f, 0.7f);
        mapButtons.Activate();
        timer.SetActive(true);
        timer.SetTimer(quizTimer);

        while(quizTimer.Value > 0f && unprocessedStatus == null) yield return null;

        //解答時間終了
        minimap.DisableToClick();
        answerQ = Arithmetic.Decel(0f, 1f, 1f);
        timer.SetActive(false);
        PushToUnfixedAnswer();//ゲーム進行のため、未回答分を埋める

        //進捗の同期
        while (unprocessedStatus == null) yield return null;
        foreach (var status in unprocessedStatus) scoreMap.AddScore(status.PlayerId, status.Score);
        scoreBoard.UpdateRanking(scoreMap);
        var currentStatus = unprocessedStatus;
        unprocessedStatus = null;
        achievementChecker.OnGetAnswer(entry, currentStatus);

        //答えの提示
        mapViewer.StartShowAnswer();
        yield return Effects.Wait(3f);
        answerMinimap.ShowAnswer(entry.mapId, entry.position, currentStatus);
        yield return Effects.Wait(3f);

        //答えの提示を終了
        answerMinimap.Hide(true);
        endQuizQ = Arithmetic.Decel(0f, 1f, 0.45f);

        yield return Effects.Wait(0.25f);

        //スコアボード表示
        scoreBoard.ShowContent(0f, true);
        scoreBoardQ = Arithmetic.Decel(0f, 1f, 0.5f);

        if (!isFirstOrLast)
        {
            yield return Effects.Wait(2f);
            scoreBoard.ShowContent(1f, false, true);
        }

        yield return Effects.Wait(0.2f);

        answerInfoMenu.Show(!isFirstOrLast);
        mapViewer.SetReshowing(true);

        while (!proceedingToNext) yield return null;
        proceedingToNext = false;

        answerInfoMenu.Hide();

        //スコアボードと答えの提示を全て終了
        answerMinimap.Hide(false);
        endQuizQ = Arithmetic.Decel(endQuizQ.Value, 1f, 0.45f);
        scoreBoardQ = Arithmetic.Decel(scoreBoardQ.Value, 0f, 0.3f);


        InitializeForOneQuiz();

        yield return Effects.Wait(0.5f);
        yield break;
    }

    private void PushToUnfixedAnswer()
    {
        foreach(var p in GamePlayer.AllPlayers)
        {
            if(cachedPlayerSelections.TryGetValue(p.PlayerId, out var found))
            {
                if(!found.Fixed) cachedPlayerSelections[p.PlayerId] = found.GetAsFixed();
            }
            else 
            {
                cachedPlayerSelections[p.PlayerId] = new(byte.MaxValue, Vector2.zero, -1, true);
            }
        }
    }
    private void InitializeForOneQuiz()
    {
        cachedPlayerSelections.Clear();
    }

    
    private record PlayerSelection(byte MapId, Vector2 Position, int Num, bool Fixed)
    {
        public PlayerSelection GetAsFixed() => new(MapId, Position, Num, true);
    }
    private Dictionary<byte, PlayerSelection> cachedPlayerSelections = new();
    
    private IEnumerator CoPlayOneQuizAsHost(AeroGuesserQuizData.QuizEntry entry)
    {
        //解答終了まで待つ
        while (!GamePlayer.AllPlayers.All(p => cachedPlayerSelections.TryGetValue(p.PlayerId, out var found) && found.Fixed)) yield return null;

        //得点計算
        if (AmongUsClient.Instance.AmHost)
        {
            (byte playerId, int score, byte mapId, Vector2 position)[] results = cachedPlayerSelections.Select(selection =>
            {
                int score = 0;
                if (selection.Value.MapId == entry.mapId)
                {
                    var distance = selection.Value.Position.Distance(entry.position);
                    if (distance < 0.2f) score = 1000;
                    else if (distance < 2f)
                    {
                        distance = (distance - 0.2f) / 1.8f;
                        score = 1000 - (int)(distance * 100f);
                    }
                    else if (distance < 6f)
                    {
                        distance = (distance - 2f) / 4f;
                        score = 900 - (int)(distance * 500f);
                    }
                    else if (distance < 15f)
                    {
                        distance = (distance - 2f) / 9f;
                        score = 400 - (int)(distance * 350f);
                    }
                    else if (distance < 25f)
                    {
                        distance = (distance - 15f) / 10f;
                        score = 50 - (int)(distance * 50f);
                    }
                }
                return (selection.Key, score, selection.Value.MapId, selection.Value.Position);
            }).ToArray();
            RpcSendAnswer.Invoke(results);
        }
        cachedPlayerSelections.Clear();
        yield break;
    }
    

    private GameObject baseObject;
    private SpriteRenderer backgroundRenderer;
    private GameObject clickGuard;

    private void Initialize()
    {
        achievementChecker.OnGameStart();

        var hud = HudManager.Instance;
        hud.roomTracker.gameObject.SetActive(false);

        baseObject = UnityHelper.CreateObject("AeroGuesser", hud.transform, UnityEngine.Vector3.zero);
        //背景の作成
        backgroundRenderer = UnityHelper.CreateObject<SpriteRenderer>("Background", baseObject.transform, new(0f, 0f, 5f));
        backgroundRenderer.sprite = VanillaAsset.TitleBackgroundSprite;
        backgroundRenderer.material = new(NebulaAsset.HSVNAShader);
        backgroundRenderer.sharedMaterial.SetFloat("_Sat", 0.55f);
        backgroundRenderer.sharedMaterial.SetFloat("_Hue", 320f);
        backgroundRenderer.sharedMaterial.SetFloat("_Val", 0.8f);

        //クリックガード
        clickGuard = UnityHelper.CreateObject("ClickGuard", baseObject.transform, new(0f, 0f, -50f));
        clickGuard.SetUpButton(false);
        var collider = clickGuard.AddComponent<BoxCollider2D>();
        collider.size = new(20f, 20f);
        collider.isTrigger = false;
        clickGuard.SetActive(false);

        //各種モジュールの初期化
        mapButtons = new(baseObject.transform, this);
        mapViewer = new(this);
        minimap = new(baseObject.transform, this);
        answerMinimap = new(baseObject.transform);
        scoreBoard = new(baseObject.transform, this);
        answerInfoMenu = new(baseObject.transform, this);

        //モジュールを初期状態へ
        mapViewer.Hide();
        mapButtons.SetActive(false);
        answerMinimap.HideForcibly();
        timer.SetActive(false);

        InitializeForOneQuiz();
    }

    void IAnswerMenuInteraction.ProceedAsHost()
    {
        RpcProceeding.Invoke();
    }

    void IAnswerMenuInteraction.ShowAnswerInfo(int index)
    {
        switch (index)
        {
            case 0:
                scoreBoardQ = Arithmetic.Decel(scoreBoardQ.Value, 0f, 0.2f);
                answerMinimap.Hide(true);

                endQuizQ = Arithmetic.Decel(endQuizQ.Value, 0f, 0.2f);
                break;
            case 1:
                scoreBoardQ = Arithmetic.Decel(scoreBoardQ.Value, 0f, 0.2f);
                endQuizQ = Arithmetic.Decel(endQuizQ.Value, 1f, 0.2f);

                answerMinimap.Reshow();
                break;
            case 2:
                answerMinimap.Hide(true);
                endQuizQ = Arithmetic.Decel(endQuizQ.Value, 1f, 0.2f);

                scoreBoard.ShowContent(0f, true);
                scoreBoardQ = Arithmetic.Decel(scoreBoardQ.Value, 1f, 0.2f);
                break;
            case 3:
                endQuizQ = Arithmetic.Decel(endQuizQ.Value, 1f, 0.2f);

                scoreBoard.ShowContent(1f, true);
                scoreBoardQ = Arithmetic.Decel(scoreBoardQ.Value, 1f, 0.2f);
                break;
        }
    }

    void IMapButtonInteraction.SelectMap(byte mapId)
    {
        mapButtonsQ = Arithmetic.Decel(0f, 1f, 0.38f);
        minimap.SetMap(mapId);
    }

    void IMinimapViewerInteraction.BackToMapSelection()
    {
        mapButtonsQ = Arithmetic.Decel(1f, 0f, 0.38f);
        mapButtons.Activate();
    }

    int clickCount = 0;
    void IMinimapViewerInteraction.ClickMap(byte mapId, Vector2 position, bool isFixed)
    {
        RpcSendSelection.Invoke((GamePlayer.LocalPlayer!.PlayerId, mapId, position, isFixed ? -1 : clickCount++));
        if (isFixed)
        {
            mapButtonsQ = Arithmetic.Decel(1f, 0f, 0.6f);
            mapButtons.SetActive(false);
        }
    }

    void SetClickGuard(bool active) => clickGuard.SetActive(active);

    private Timer timer = new(-20f);
    private MapButtons mapButtons = null!;
    private MapViewer mapViewer = null!;
    private MinimapViewer minimap = null!;
    private AnswerMinimapViewer answerMinimap = null!;
    private ScoreBoardShower scoreBoard = null!;
    private AnswerInfoMenu answerInfoMenu = null!;
    private ScoreMap scoreMap = new();
    private AchievementChecker achievementChecker = new();

    static private void SetUpPlayers()
    {
        foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
        {
            p.transform.position = new(0f, -10000f, 0f);
            var player = NebulaGameManager.Instance?.RegisterPlayer(p);
            player?.Unbox().RpcInvokerSetRole(Roles.AeroGuesser.AeroguesserPlayer.MyRole, null).InvokeLocal();
        }
    }

    private void SetUp(int mapMask, int quizNum, bool monoColor)
    {
        CurrentSetting = new(mapMask, quizNum, monoColor);
    }

    static private readonly RemoteProcess<(int mapMask, int quizNum, bool monoColor)> RpcIntro = new("AeroGuesser.Intro", (message, _) =>
    {
        SetUpPlayers();
        NebulaGameManager.Instance?.CheckGameState(false);
        NebulaGameManager.Instance?.SetGameModeModule(); //ここでPlaySenarioが開始する
        ModSingleton<AeroGuesserSenario>.Instance!.SetUp(message.mapMask, message.quizNum, message.monoColor);
    });

    private (AeroGuesserQuizData.QuizEntry entry, bool isFirstOrLast)? unprocessedQuiz = null;
    static private readonly RemoteProcess<(bool isFirstOrLast, byte mapId, int difficulty, Vector2 position, Vector2 viewport)> RpcSendQuiz = new("AeroGuesser.SendQuiz", (message, _) => {
        ModSingleton<AeroGuesserSenario>.Instance?.unprocessedQuiz = (new(message.mapId, (AeroGuesserQuizData.Difficulty)message.difficulty, message.position, message.viewport), message.isFirstOrLast);
    });

    static private readonly RemoteProcess<(byte playerId, byte mapId, Vector2 position, int num)> RpcSendSelection = new("AeroGuesser.SendSelection", (message, _) => {
        var cache = ModSingleton<AeroGuesserSenario>.Instance?.cachedPlayerSelections;
        if (cache == null) return;
        if (message.num == -1 || (cache.TryGetValue(message.playerId, out var cached) ? !cached.Fixed && cached.Num < message.num : true))
        {
            cache[message.playerId] = new(message.mapId, message.position, message.num, message.num == -1);
        }
    });

    private AeroPlayerOneQuizStatus[]? unprocessedStatus = null;
    static private readonly RemoteProcess<(byte playerId, int score, byte mapId, Vector2 position)[]> RpcSendAnswer = new("AeroGuesser.SendAnswer", (message, _) => {
        ModSingleton<AeroGuesserSenario>.Instance?.unprocessedStatus = message.Select(entry => new AeroPlayerOneQuizStatus(entry.playerId, entry.score, entry.mapId, entry.position)).ToArray();
    });

    private bool proceedingToNext = false;
    static private readonly RemoteProcess RpcProceeding = new("AeroGuesser.Proceeding", (_) => {
        ModSingleton<AeroGuesserSenario>.Instance!.proceedingToNext = true;
    });
}

internal record AeroPlayerOneQuizStatus(byte PlayerId, int Score, byte selectedMap, Vector2 selectedPosition);
