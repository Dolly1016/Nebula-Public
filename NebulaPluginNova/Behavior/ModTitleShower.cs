using Il2CppInterop.Runtime.Injection;
using Mono.Cecil.Rocks;
using Nebula.Modules.Cosmetics;
using static Nebula.Modules.HelpScreen;

namespace Nebula.Behavior;

public class ModTitleShower : MonoBehaviour
{
    static ModTitleShower() => ClassInjector.RegisterTypeInIl2Cpp<ModTitleShower>();

    private TMPro.TextMeshPro text = null!;
    private INebulaAchievement? achievement = null;
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
                if (achievement != null) NebulaManager.Instance.SetHelpWidget(button, achievement.GetOverlayWidget(false, true, false, true, true));
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
            if(NebulaGameManager.Instance?.TitleMap.TryGetValue((byte)poolablePlayer.ColorId, out var title) ?? false && title != null)
                SetAchievement(title?.Id ?? "-");
            else
                SetAchievement("-");
        }

    }

    public INebulaAchievement? SetAchievement(string achievement)
    {
        if(NebulaAchievementManager.GetAchievement(achievement,out var ach)){
            text.text = Language.Translate(ach.TranslationKey);
            text.ForceMeshUpdate();
            collider.size = (Vector2)text.bounds.size + new Vector2(0.1f, 0.1f);
            this.achievement = ach;
        }
        else if(AmOwner)
        {
            text.text = Language.Translate("achievement.ui.unselected");
            text.ForceMeshUpdate();
            collider.size = (Vector2)text.bounds.size + new Vector2(0.1f, 0.1f);
            text.color = Color.gray;
            this.achievement = null;
            this.time = -1f;
        }
        else
        {
            text.text = "";
            this.achievement = null;
            collider.size = Vector2.zero;
        }

        return this.achievement;
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
