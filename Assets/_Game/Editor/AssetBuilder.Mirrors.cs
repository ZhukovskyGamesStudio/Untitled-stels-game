using System.Collections.Generic;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        /// <summary>Materials whose answer is their own, whatever the rule says (the car's glass: only darkens what is behind it).</summary>
        static readonly HashSet<string> s_OwnMirror = new() { "CarGlass" };

        /// <summary>
        /// Only what is glossy or metal mirrors its room (<see cref="RoomProbes.Mirrors"/>): every lit material of the game
        /// is measured (<see cref="MaterialGloss"/>) and the matt ones leave the room's picture alone. Runs last, over all
        /// of them, whoever made them — a material made tomorrow follows the rule without knowing of it.
        /// </summary>
        static void OnlyGlossMirrors()
        {
            int matt = 0, all = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat")) continue; // (a material inside an imported model is the importer's: it is not shown as it is)
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!MaterialGloss.Lit(m) || s_OwnMirror.Contains(m.name)) continue;
                var (smoothness, metal) = MaterialGloss.Of(m);
                bool mirrors = RoomProbes.Mirrors(smoothness, metal);
                all++;
                if (!mirrors) matt++;
                if (m.IsKeywordEnabled(MaterialGloss.NoMirrorKeyword) == !mirrors &&
                    (!m.HasProperty(MaterialGloss.MirrorProperty) || Mathf.Approximately(m.GetFloat(MaterialGloss.MirrorProperty), mirrors ? 1f : 0f))) continue;
                if (m.HasProperty(MaterialGloss.MirrorProperty)) m.SetFloat(MaterialGloss.MirrorProperty, mirrors ? 1f : 0f);
                if (mirrors) m.DisableKeyword(MaterialGloss.NoMirrorKeyword);
                else m.EnableKeyword(MaterialGloss.NoMirrorKeyword);
                EditorUtility.SetDirty(m);
            }
            Debug.Log($"[Builder] {all - matt} of {all} lit materials mirror their room");
        }
    }
}
