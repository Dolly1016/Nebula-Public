using Nebula.Modules.GUIWidget;
using Nebula.Roles.Abilities;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Neutral;

/*
public class Dancer : ConfigurableStandardRole
{
    static public Dancer MyRole = new Dancer();
    static public Team MyTeam = new("teams.dancer", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory Category => RoleCategory.NeutralRole;

    public override string LocalizedName => "dancer";
    public override Color RoleColor => new Color(255f / 255f, 255f / 255f, 255f / 255f);
    public override RoleTeam Team => MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);


    protected override void LoadOptions()
    {
        base.LoadOptions();
    }

    public class Instance : RoleInstance, IGamePlayerEntity
    {
        public override AbstractRole Role => MyRole;

        public Instance(PlayerModInfo player) : base(player)
        {
        }

        TMPro.TextMeshPro? text = null;
        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                var holder = HudContent.InstantiateContent("dancer", true, true, false, true);

                var guiText = new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.Instance.GetAttribute(Virial.Text.AttributeParams.StandardBaredBoldLeft), new RawTextComponent("")) { PostBuilder = t => text = t };
                var instantiated = guiText.Instantiate(new(1f,1f),out _);
                instantiated!.transform.SetParent(holder.transform);
                instantiated!.transform.localPosition = new(2f, 0f, 0f);
            }
        }

        Vector2? lastPos = null;
        Vector2 displacement = new();
        float distance = 0f;

        void IGameEntity.Update()
        {
            Vector2 currentPos = MyPlayer.MyControl.transform.position;
            if(lastPos != null)
            {
                distance *= 0.92f;
                distance += currentPos.Distance(lastPos.Value);

                displacement *= 0.92f;
                displacement += currentPos - lastPos.Value;
            }
            lastPos = currentPos;    

            if(text != null)
            {
                text.text = "Distance: " + distance.ToString() + "<br>Dis: (" + displacement.x + "," + displacement.y + ")";
            }
        }
    }
}

*/