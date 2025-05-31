using Rewired.Utils.Platforms.Windows;
using Virial;
using Virial.Components;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;
using static Il2CppSystem.Xml.Schema.FacetsChecker.FacetsCompiler;

namespace Nebula.Modules.ScriptComponents;


[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.IsKillTimerEnabled), MethodType.Getter)]
class PlayerIsKillTimerEnabledPatch
{
    public static void Postfix(PlayerControl __instance, ref bool __result)
    {
        //地下にダイブ中、あるいは停電スイッチを直している間はキルクールは進まない
        if (
            (__instance.GetModInfo()?.IsDived ?? false) ||
            (Minigame.Instance && Minigame.Instance.TryCast<SwitchMinigame>()))
        {
            __result = false;
        }
    }
}

public class TimerImpl : FlexibleLifespan, GameTimer, IGameOperator
{
    private Func<bool>? predicate = null;
    private bool isActive;
    protected float currentTime;
    protected float min, max;

    public float Max => max;

    /// <summary>
    /// タイマーの進行を強制的に止めます。
    /// IsProgressingはfalseを返します。
    /// </summary>
    /// <returns></returns>
    public TimerImpl StopForcely() => SetTime(min);
    
    public TimerImpl Pause()
    {
        isActive = false;
        return this;
    }
    public virtual TimerImpl Start(float? time = null)
    {
        isActive = true;
        currentTime = time.HasValue ? time.Value : max;
        return this;
    }
    public TimerImpl Resume()
    {
        isActive = true;
        return this;
    }
    public TimerImpl Reset()
    {
        currentTime = max;
        return this;
    }
    public TimerImpl SetTime(float time)
    {
        currentTime = time;
        return this;
    }
    public TimerImpl SetRange(float min, float max)
    {
        if (min > max)
        {
            this.max = min;
            this.min = max;
        }
        else
        {
            this.max = max;
            this.min = min;
        }
        return this;
    }
    public TimerImpl Expand(float time)
    {
        this.max += time;
        return this;
    }

    public float CurrentTime { get => currentTime; }
    public virtual float Percentage { get => max > min ? (currentTime - min) / (max - min) : 0f; }
    public bool IsProgressing => CurrentTime > min;

    void Update(UpdateEvent ev)
    {
        if (isActive && (predicate?.Invoke() ?? true))
        {
            float deltaTime = Time.fixedDeltaTime;
            if (AffectedByCooldownEffect)
            {
                float coeff = GamePlayer.LocalPlayer?.Unbox().CalcAttributeVal(PlayerAttributes.CooldownSpeed, true) ?? 1f;
                //if (coeff < 0f) coeff = 0f;
                deltaTime *= coeff;
            }

            currentTime = Mathf.Clamp(currentTime - deltaTime, min, max);
        }
    }

    public TimerImpl() : this(0f, 0f) { }
    public TimerImpl(float max) : this(0f, max) { }

    public TimerImpl(float min, float max)
    {
        SetRange(min, max);
        Reset();
        Pause();
    }

    GameTimer GameTimer.SetCondition(System.Func<bool> progressWhile) => SetPredicate(progressWhile);
    public TimerImpl SetPredicate(Func<bool>? predicate)
    {
        this.predicate = predicate;
        return this;
    }

    public Func<bool>? Predicate => this.predicate;
    public bool AffectedByCooldownEffect = false;

    public TimerImpl SetAsKillCoolDown()
    {
        AffectedByCooldownEffect = true;
        return SetPredicate(() => PlayerControl.LocalPlayer.IsKillTimerEnabled);
    }

    public TimerImpl SetAsAbilityCoolDown()
    {
        AffectedByCooldownEffect = true;
        return SetPredicate(() =>
        {
            if (PlayerControl.LocalPlayer.CanMove) return true;
            if (PlayerControl.LocalPlayer.inMovingPlat || PlayerControl.LocalPlayer.onLadder) return true;

            if (Minigame.Instance && 
            ((bool)Minigame.Instance.MyNormTask
            || Minigame.Instance.TryCast<SwitchMinigame>() != null
            || Minigame.Instance.TryCast<IDoorMinigame>() != null
            || Minigame.Instance.TryCast<VitalsMinigame>() != null
            || Minigame.Instance.TryCast<MultistageMinigame>() != null
            || Minigame.Instance.TryCast<AutoMultistageMinigame>() != null
            )) return true;
            return false;
        });
    }

    GameTimer GameTimer.SetAsKillCoolTimer() => SetAsKillCoolDown();

    GameTimer GameTimer.SetAsAbilityTimer() => SetAsAbilityCoolDown();

    IVisualTimer IVisualTimer.Start(float? time) => Start(time);

    GameTimer GameTimer.Pause() => Pause();

    GameTimer GameTimer.Resume() => Resume();

    GameTimer GameTimer.SetRange(float min, float max) => SetRange(min, max);

    GameTimer GameTimer.SetTime(float time) => SetTime(time);

    GameTimer GameTimer.Expand(float time) => Expand(time);
    string IVisualTimer.TimerText => currentTime > 0f ? Mathf.CeilToInt(currentTime).ToString() : null;
    internal class TimerCoolDownHelper : IGameOperator
    {
        private TimerImpl myTimer;
        public TimerCoolDownHelper(TimerImpl timer) { 
            this.myTimer = timer; 
        }

        void ResetVentCoolDownOnTaskPhaseRestart(TaskPhaseRestartEvent ev) => myTimer?.Start();
        void ResetVentCoolDownOnGameStart(GameStartEvent ev) => myTimer?.Start();
    }

    GameTimer GameTimer.ResetsAtTaskPhase()
    {
        GameOperatorManager.Instance?.Subscribe(new TimerCoolDownHelper(this), this);
        return this;
    }
}

public class AdvancedTimer : TimerImpl
{
    protected float visualMax;
    protected float defaultMax;
    public AdvancedTimer(float visualMax,float max) : base(0f, max) {
        this.visualMax = visualMax;
    }

    public AdvancedTimer SetVisualMax(float visualMax)
    {
        this.visualMax = visualMax;
        return this;
    }

    public AdvancedTimer SetDefault(float defaultMax)
    {
        this.defaultMax = defaultMax;
        return this;
    }

    public override TimerImpl Start(float? time = null) => base.Start(time ?? defaultMax);

    public override float Percentage { get => Mathf.Min(1f, visualMax > min ? (currentTime - min) / (visualMax - min) : 0f); }
}

internal class ScriptVisualTimer : SimpleLifespan, IVisualTimer
{
    private Func<float> percentage;
    private Func<string?> text;

    public ScriptVisualTimer(Func<float> percentage, Func<string?> text)
    {
        this.percentage = percentage;
        this.text = text;
    }

    string? IVisualTimer.TimerText => text.Invoke();

    float IVisualTimer.Percentage => percentage.Invoke();

    bool IVisualTimer.IsProgressing => true;

    float IVisualTimer.CurrentTime => int.TryParse(text.Invoke(), out var num) ? num : 0f;


    IVisualTimer IVisualTimer.Start(float? time) => this;
}