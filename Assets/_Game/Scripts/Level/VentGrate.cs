using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A grate in a room's ceiling (prefab Level/VentGrate, spots from <see cref="VentPlan"/>): the way into the storey's
    /// crawl ducts. E under it pulls the player up into the duct, crouched; E in the duct over it drops them into the room.
    /// The owner moves itself (movement is the owner's, <see cref="PlayerController.TeleportOwner"/>); the grate's lid
    /// swings open while someone is close, so a robot below may notice the open hatch only by the player it reveals.
    /// </summary>
    public class VentGrate : Interactable
    {
        [Tooltip("The grille: swings down on its hinge while a player is close.")]
        public Transform Lid;

        public override UseKind Kind => UseKind.VentGrate;
        /// <summary>Under the ceiling: reached from the floor below and from the duct above.</summary>
        public override Vector3 Point => transform.position + Vector3.down * 0.6f;
        public override float AimRadius => 0.35f;
        /// <summary>Right under it (or right over it in the duct): a grate is climbed from close by, not across the room.</summary>
        public override float Reach => 0.9f;

        /// <summary>The room under it.</summary>
        public int Room { get; private set; } = -1;

        public void Setup(int index, int room)
        {
            Room = room;
            Configure(index);
        }

        /// <summary>Is a player up in the duct over it (rather than in the room below)?</summary>
        public bool Above(PlayerController player) => player.transform.position.y > transform.position.y - 0.3f;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = true;
            return Loc.T(Above(player) ? "use.vent_down" : "use.vent_up");
        }

        /// <summary>Owner: up into the duct (crouched, on the grate) or down into the room under it.</summary>
        public void Climb(PlayerController player)
        {
            var at = transform.position;
            if (Above(player))
            {
                player.TeleportOwner(LandingSpot(), player.transform.rotation);
            }
            else
            {
                var up = new Vector3(at.x, at.y + LevelMetrics.CeilingBoard + 0.03f, at.z);
                player.TeleportOwner(up, player.transform.rotation);
                player.Motor?.ForceCrouch(); // 1.15 m of headroom: the motor keeps it crouched until it is out
            }
            Audio.Sfx.At(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.Click : null, at, 0.7f, Audio.Sfx.Group.Items);
        }

        /// <summary>
        /// Where a player dropping out lands: right under the grate, or the nearest spot round it with room to stand (a desk
        /// may stand under it — then on the floor beside it).
        /// </summary>
        public Vector3 LandingSpot()
        {
            var at = transform.position;
            float floor = at.y - LevelMetrics.WallHeight;
            const float r = 0.36f;
            for (int ring = 0; ring <= 3; ring++)
                for (int k = 0; k < (ring == 0 ? 1 : 8); k++)
                {
                    float a = k * Mathf.PI / 4f;
                    var p = new Vector3(at.x + Mathf.Cos(a) * ring * 0.45f, floor + 0.05f, at.z + Mathf.Sin(a) * ring * 0.45f);
                    if (!Physics.CheckCapsule(p + Vector3.up * (r + 0.02f), p + Vector3.up * (PlayerController.StandHeight - r), r,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return p;
                }
            return new Vector3(at.x, floor + 0.05f, at.z);
        }

        float m_Open;

        void Update()
        {
            if (Lid == null || Index < 0) return;
            bool near = false;
            foreach (var p in PlayerController.All)
            {
                var d = p.transform.position - transform.position;
                if (Mathf.Abs(d.y + 1.5f) < 2.2f && new Vector2(d.x, d.z).magnitude < 1.4f) { near = true; break; }
            }
            m_Open = Mathf.MoveTowards(m_Open, near ? 1f : 0f, Time.deltaTime * 4f);
            Lid.localRotation = Quaternion.Euler(m_Open * 80f, 0f, 0f);
        }
    }
}
