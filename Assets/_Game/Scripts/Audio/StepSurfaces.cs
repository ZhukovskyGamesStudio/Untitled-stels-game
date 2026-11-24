using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Audio
{
    /// <summary>What a foot comes down on, by its sound.</summary>
    public enum StepSurface { Concrete, Tile, Wood, Soft, Carpet, Metal, Wet }

    /// <summary>
    /// The surface under a player's feet: what they stand on (a ray down) and, on a house's floor, the finish the house's
    /// look lays in that room (<see cref="StyleDecor.FloorMaterial"/>). Sound only: robots hear a step by its loudness
    /// (<see cref="Stealth.Player.FootstepNoise"/>), whatever it lands on.
    /// </summary>
    public static class StepSurfaces
    {
        static readonly (string part, StepSurface surface)[] Words =
        {
            // (the first match counts: the telling words before the general ones)
            ("Water", StepSurface.Wet), ("SewerConcrete", StepSurface.Wet), ("SewerStone", StepSurface.Wet), ("SewerBrick", StepSurface.Wet), ("SewerWood", StepSurface.Wood),
            ("Carpet", StepSurface.Carpet), ("Felt", StepSurface.Carpet), ("Fabric", StepSurface.Carpet), ("Cloth", StepSurface.Carpet), ("BeanBag", StepSurface.Carpet),
            ("Grat", StepSurface.Metal), ("FloorPlate", StepSurface.Metal), ("Vault_Floor", StepSurface.Metal), ("Booth_Floor", StepSurface.Metal),
            ("Rubber", StepSurface.Soft), ("Lino", StepSurface.Soft), ("Clinic_Floor", StepSurface.Soft), ("Theatre_Floor", StepSurface.Soft), ("Shop_Floor", StepSurface.Soft),
            ("Herringbone", StepSurface.Wood), ("Laminate", StepSurface.Wood), ("Plank", StepSurface.Wood), ("Parquet", StepSurface.Wood), ("Wood", StepSurface.Wood),
            ("PropBody", StepSurface.Wood), ("PropAccent", StepSurface.Wood), ("Cardboard", StepSurface.Wood), ("Folders", StepSurface.Wood),
            ("Tile", StepSurface.Tile), ("Checker", StepSurface.Tile), ("Marble", StepSurface.Tile), ("KitchenFloor", StepSurface.Tile), ("BathFloor", StepSurface.Tile),
            ("StairFloor", StepSurface.Tile), ("LiftFloor", StepSurface.Soft),
            ("Metal", StepSurface.Metal), ("Steel", StepSurface.Metal), ("Rust", StepSurface.Metal), ("Rack", StepSurface.Metal), ("Util", StepSurface.Metal), ("Lift", StepSurface.Metal),
            ("Hatch", StepSurface.Metal), ("Car", StepSurface.Metal), ("Vending", StepSurface.Metal), ("DoorFrame", StepSurface.Metal),
        };

        /// <summary>The surface a material is (pure, by its name); bare or unknown: concrete.</summary>
        public static StepSurface OfMaterial(string material)
        {
            if (!string.IsNullOrEmpty(material))
                foreach (var (part, surface) in Words)
                    if (material.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0) return surface;
            return StepSurface.Concrete;
        }

        /// <summary>The surface of a staircase's look (pure).</summary>
        public static StepSurface OfStairs(StairStyle style) => style switch
        {
            StairStyle.Wood => StepSurface.Wood,
            StairStyle.Steel => StepSurface.Metal,
            StairStyle.Glass => StepSurface.Tile, // (white stone between panes)
            _ => StepSurface.Concrete,
        };

        static readonly RaycastHit[] s_Hits = new RaycastHit[8];

        /// <summary>What lies under the feet at <paramref name="feet"/>.</summary>
        public static StepSurface At(Vector3 feet)
        {
            int n = Physics.RaycastNonAlloc(feet + Vector3.up * 0.3f, Vector3.down, s_Hits, 0.9f, ~0, QueryTriggerInteraction.Ignore);
            Collider under = null;
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = s_Hits[i].collider;
                if (s_Hits[i].distance >= nearest || c is CharacterController || c.GetComponentInParent<Stealth.Player.PlayerController>() != null) continue;
                nearest = s_Hits[i].distance;
                under = c;
            }
            if (under == null) return StepSurface.Concrete;
            var stairs = under.GetComponentInParent<StairLook>();
            if (stairs != null) return OfStairs(stairs.Current);
            var layout = ActiveLevel.Layout;
            if (layout != null && feet.y > LevelLayout.Underground && (under.name.StartsWith("Floor") || under.name.StartsWith("Slab")))
            {
                float up = feet.y - LevelLayout.FloorY(LevelLayout.FloorOf(feet.y + 0.1f));
                if (up > 1f) return StepSurface.Metal; // in the ducts over the ceilings
                var room = layout.RoomAt(feet + Vector3.up * 0.3f);
                if (room != null) return OfMaterial(StyleDecor.FloorMaterial(layout, room)?.name);
            }
            if (under.name.StartsWith("CeilingBoard") || under.name.StartsWith("Duct")) return StepSurface.Metal;
            var r = under.GetComponent<MeshRenderer>();
            if (r == null) r = under.GetComponentInChildren<MeshRenderer>();
            if (r == null && under.transform.parent != null) r = under.transform.parent.GetComponent<MeshRenderer>();
            return OfMaterial(r != null && r.sharedMaterial != null ? r.sharedMaterial.name : null);
        }
    }
}
