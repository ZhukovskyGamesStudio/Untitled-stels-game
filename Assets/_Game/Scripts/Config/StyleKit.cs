using System;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>
    /// Prefabs of the style architecture (Prefabs/StyleKit, built by the asset builder from Art/Models/StyleKit):
    /// one per <see cref="Level.DecorKind"/>, plus the quad that carries floor and wall finishes. Decoration only:
    /// no colliders, no shadows.
    /// </summary>
    public class StyleKit : ScriptableObject
    {
        [Tooltip("The models of which family the pieces are shaped like")] public ModelFamily Family;
        [Tooltip("Indexed by DecorKind; the finish kinds use the Finish quad")]
        public GameObject[] Pieces = new GameObject[0];
        public GameObject Finish;
        [Tooltip("The bank vault's steel wall cladding and floor plate (every style: a vault looks like a vault)")]
        public Material VaultWall, VaultFloor;
        [Tooltip("Metres one texture repeat covers on the vault's walls / floor")]
        public float VaultWallTile = 2f, VaultFloorTile = 1f;
        [Tooltip("The metal strip over a doorway where two different floors meet")]
        public Material Threshold;
        [Tooltip("The auto repair shop in every style (Level.ShopLook): painted block walls with a dark band low, an epoxy floor, a metal deck overhead")]
        public Material ShopWall, ShopWallLower, ShopFloor, ShopCeiling;
        [Tooltip("The shop's paint booth: white filter panels on the walls and overhead, a steel grating underfoot")]
        public Material BoothWall, BoothFloor, BoothCeiling;
        [Tooltip("The hospital in every style (Level.ClinicLook): washable paint over a wall-protection band, the wards warmer, acoustic tiles, sheet vinyl")]
        public Material ClinicWall, ClinicWardWall, ClinicGuard, ClinicCeiling, ClinicFloor;
        [Tooltip("The hospital's theatres and imaging rooms: white panels, a seamless green-grey floor")]
        public Material TheatreWall, TheatreFloor;
    }

    /// <summary>Every model mesh and its variant in one family (Art/Models/Styles/&lt;Family&gt;).</summary>
    [Serializable]
    public class MeshVariants
    {
        public ModelFamily Family;
        public Mesh[] From = new Mesh[0];
        public Mesh[] To = new Mesh[0];
    }

    /// <summary>Models made for one style only (its own guard robot): they win over the family's version.</summary>
    [Serializable]
    public class StyleMeshes
    {
        public VisualStyle Style;
        [Tooltip("-1 = every object; otherwise only objects registered with this variant (a player's department slot)")]
        public int Variant = -1;
        public Mesh[] From = new Mesh[0];
        public Mesh[] To = new Mesh[0];
    }

    /// <summary>Every material of the world and its version in one family (flat, glossy plastic, pixel, PBR…).</summary>
    [Serializable]
    public class MaterialVariants
    {
        public ModelFamily Family;
        public Material[] From = new Material[0];
        public Material[] To = new Material[0];
    }

    /// <summary>
    /// Wear levels of one material (painted metal from clean to rotten): every object picks one by where it stands
    /// (<see cref="WearMath.Pick"/>), so no two cupboards rust the same way. Visual only.
    /// </summary>
    [Serializable]
    public class WearVariants
    {
        public Material From;
        [Tooltip("From the least worn to the most")] public Material[] To = new Material[0];
    }

    /// <summary>Which wear level an object takes (pure, the same on every peer).</summary>
    public static class WearMath
    {
        /// <summary>
        /// The level of one part of an object (a drawer, a door, the carcass): the object's level is the most any of its
        /// parts rusts, each part takes its own level up to that — so rust lands on some parts and leaves others clean.
        /// </summary>
        public static int PartLevel(Vector3 objectPosition, string part, int count)
        {
            int most = Pick(objectPosition, count);
            if (most <= 0) return 0;
            // by the part's name, not where it is: an open drawer keeps its rust (string.GetHashCode differs between runs)
            int h = 17;
            foreach (char ch in part ?? "") h = unchecked(h * 31 + ch);
            return Pick(objectPosition + new Vector3(h % 997, h / 997 % 991, 0.5f), most + 1);
        }

        /// <summary>A level 0..count-1 from the object's position (rounded to centimetres, so peers agree).</summary>
        public static int Pick(Vector3 position, int count)
        {
            if (count <= 1) return 0;
            unchecked
            {
                uint h = 2166136261u;
                foreach (var v in new[] { Mathf.RoundToInt(position.x * 100f), Mathf.RoundToInt(position.y * 100f), Mathf.RoundToInt(position.z * 100f) })
                {
                    h = (h ^ (uint)v) * 16777619u;
                    h ^= h >> 13;
                }
                h *= 0x9E3779B1u;
                return (int)((h >> 8) % (uint)count);
            }
        }
    }

    /// <summary>A model replaced as a whole in one family (a CC0 scanned desk for ours): placed in the model's space.</summary>
    [Serializable]
    public class PropReplacement
    {
        public ModelFamily Family;
        [Tooltip("The Body mesh of our model: where it is, the replacement goes")] public Mesh Key;
        public GameObject Prefab;
        public Vector3 Position;
        public Vector3 Scale = Vector3.one;
        [Tooltip("The replacement brings its own screens: hide our glowing Eye parts too")] public bool HideGlow;
    }
}
