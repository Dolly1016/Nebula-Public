using Virial;

namespace Nebula.Game;

public class UpperInfomation
{
    List<(GameObject widget,float height, Func<GameObject>? updater,ILifespan? lifespan)> widgets = new();
    GameObject informationHolder;
    public bool RegisterWidgets(GUIWidget widget, ILifespan? lifespan = null, Func<GameObject>? updater = null)
    {
        var instantiated = widget.Instantiate(new(7f, 5f), out var size);
        if (instantiated == null) return false;

        instantiated.transform.SetParent(informationHolder.transform);

        widgets.Add((instantiated, size.Height, updater,lifespan));
        return true;
    }

    public void Update()
    {
        //寿命の尽きた情報を破棄する
        widgets.RemoveAll(w => {
            bool isDead = w.lifespan?.IsDeadObject ?? false;
            if (isDead) GameObject.Destroy(w.widget);
            return isDead;
        });
    }

    public UpperInfomation() {
        informationHolder = UnityHelper.CreateObject("UpperInformation", HudManager.Instance.transform, new(0f, 2f, -50f));
    }
}
