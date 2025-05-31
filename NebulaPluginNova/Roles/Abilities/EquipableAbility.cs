using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles.Abilities
{
    public class EquipableAbility : FlexibleLifespan, IGameOperator
    {
        public GamePlayer Owner { get; private set; }
        protected SpriteRenderer Renderer { get; private set; }
        virtual protected float Size => 1.15f;
        virtual protected float Distance => 1.25f;
        public EquipableAbility(GamePlayer owner, bool canSeeInShadow, string name) : base()
        {
            Owner = owner;
            Renderer = UnityHelper.CreateObject<SpriteRenderer>(name, owner.VanillaPlayer.transform, Vector3.zero, LayerExpansion.GetObjectsLayer());
            Renderer.transform.localScale = new Vector3(Size, Size, 1f);
            Renderer.gameObject.layer = canSeeInShadow ? LayerExpansion.GetObjectsLayer() : LayerExpansion.GetDefaultLayer();
        }

        protected virtual float FixAngle(float angle) => angle;

        protected virtual void HudUpdate(GameHudUpdateEvent ev)
        {
            if (!Renderer) return;

            var o = Owner.Unbox();
            if (Owner.AmOwner) o.RequireUpdateMouseAngle();

            //少し横軸側に寄せる
            float rad = FixAngle(o.MouseAngle);
            

            Renderer.transform.localEulerAngles = new Vector3(0, 0, rad * 180f / Mathf.PI);
            var pos = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), -1f) * Distance;
            var diff = (pos - Renderer.transform.localPosition) * Time.deltaTime * 7.5f;
            Renderer.transform.localPosition += diff;
            Renderer.flipY = Mathf.Cos(rad) < 0f;
        }

        virtual public void OnReleased()
        {
            if (Renderer) GameObject.Destroy(Renderer.gameObject);
            Renderer = null!;
        }
    }
}
