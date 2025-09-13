using Il2CppInterop.Runtime.Injection;
using Mono.Cecil.Rocks;
using Nebula.Modules.Cosmetics;
using UnityEngine.SocialPlatforms.Impl;
using static Nebula.Modules.HelpScreen;

namespace Nebula.Behavior;

public class PlayerTitle
{
    INebulaAchievement?[] achievements;

    public PlayerTitle(INebulaAchievement?[] achievements)
    {
        this.achievements = achievements;
    }

    public GUIWidget? GetDetailWidget()
    {
        if (achievements.Length == 1)
        {
            return achievements[0]?.GetOverlayWidget(false, true, false, true, true);
        }
        else
        {
            return GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left, achievements.Select(a => a?.GetOverlayWidget(false, true, false, true, false)).Join(GUI.API.VerticalMargin(0.08f)));
        }
    }

    public string GetLocalizedText()
    {
        if(achievements.Length == 1)
        {
            return Language.TranslateIfNotNull(achievements[0]?.TranslationKey);
        }
        else
        {
            string text = Language.TranslateIfNotNull(achievements[0]?.PrefixTranslationKey);
            for(int i = 1; i < achievements.Length - 1; i++)
            {
                text += Language.TranslateIfNotNull(achievements[i]?.InfixTranslationKey);
            }
            text += Language.TranslateIfNotNull(achievements[^1]?.SuffixTranslationKey);
            return text;
        }
    }
}
public class ModTitleShower : MonoBehaviour
{
    static ModTitleShower() => ClassInjector.RegisterTypeInIl2Cpp<ModTitleShower>();

    private TMPro.TextMeshPro text = null!;
    private PlayerTitle achievement = null;
    private BoxCollider2D collider = null!;

    private PlayerControl player = null!;
    private PoolablePlayer poolablePlayer = null!;

    private TMPro.TextMeshPro OrigText => player ? player.cosmetics.nameText : poolablePlayer.cosmetics.nameText;
    private bool AmOwner => player ? player.AmOwner : false;
    private byte PlayerId => player ? player.PlayerId : (byte)poolablePlayer.ColorId;

    public void Awake()
    {
        if (!TryGetComponent<PlayerControl>(out player)) TryGetComponent<PoolablePlayer>(out poolablePlayer);

        text = GameObject.Instantiate(OrigText, OrigText.transform.parent);
        text.transform.localPosition = new Vector3(0, player ? 0.245f : 0.155f, -0.01f);
        text.fontSize = player ? 1.7f : 1.2f;

        text.text = "";

        collider = UnityHelper.CreateObject<BoxCollider2D>("Button", text.transform, new(0f, 0f, -10f));
        collider.isTrigger = true;
        var button = collider.gameObject.SetUpButton(false);
        button.OnMouseOver.AddListener(() =>
            {
                if (achievement != null) NebulaManager.Instance.SetHelpWidget(button, achievement.GetDetailWidget());
                else if (AmOwner) NebulaManager.Instance.SetHelpWidget(button, GUI.Instance.LocalizedText(Virial.Media.GUIAlignment.Left, INebulaAchievement.DetailTitleAttribute, "achievement.ui.unselected.detail"));
                else return;
                VanillaAsset.PlayHoverSE();
            }
            );
        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));

        button.OnClick.AddListener(() => {
            if (AmOwner)
            {
                VanillaAsset.PlaySelectSE();
                HelpScreen.TryOpenHelpScreen(HelpTab.Achievements, new() { CanCloseEasily = true });
            }
        });

        if (poolablePlayer)
        {
            if (NebulaGameManager.Instance?.TitleMap.TryGetValue((byte)poolablePlayer.ColorId, out var title) ?? false)
                SetTitle(title);
            else
                SetTitle(null);
        }

    }

    public PlayerTitle? SetAchievement(string[] achievement)
    {
        if (achievement.Length == 0)
            return SetTitle(null);
        else if (achievement.Length == 1) {
            if (NebulaAchievementManager.GetAchievement(achievement[0], out var ach0))
            {
                return SetTitle(new([ach0]));
            }
            else
            {
                return SetTitle(null);
            }
        }
        else
        {
            return SetTitle(new(achievement.Select(a => NebulaAchievementManager.GetAchievement(a, out var ach) ? ach : null).ToArray()));
        }
    }
    public PlayerTitle? SetTitle(PlayerTitle? title)
    {
        void SetNotNullTitle(PlayerTitle title)
        {
            text.text = title.GetLocalizedText();
            text.ForceMeshUpdate();
            collider.size = (Vector2)text.bounds.size + new Vector2(0.1f, 0.1f);
            this.achievement = title;
        }
  
        void SetNullForOwner()
        {
            text.text = Language.Translate("achievement.ui.unselected");
            text.ForceMeshUpdate();
            collider.size = (Vector2)text.bounds.size + new Vector2(0.1f, 0.1f);
            text.color = Color.gray;
            this.achievement = null;
            this.time = -1f;
        }

        void SetNullForNonOwner()
        {
            text.text = "";
            this.achievement = null;
            collider.size = Vector2.zero;
        }

        if (title != null)
            SetNotNullTitle(title);
        else if (player.AmOwner)
            SetNullForOwner();
        else
            SetNullForNonOwner();
        return achievement;
    }

    float time = 1f;
    public void Update()
    {
        if (this.achievement != null)
        {
            time -= Time.deltaTime;
            if (time < 0f)
            {
                text.color = Color.Lerp(Color.white, DynamicPalette.PlayerColors[PlayerId], 0.25f);
                time = 1f;
            }
        }

        if (player && ShipStatus.Instance)
        {
            GameObject.Destroy(text.gameObject);
            GameObject.Destroy(this);
        }
    }
}
