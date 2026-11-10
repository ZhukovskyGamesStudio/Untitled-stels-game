using System.Collections.Generic;
using Stealth.Configs;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The architecture of the current visual style in the built house: instantiates the kit pieces planned by
    /// <see cref="ArchitecturePlanner"/> under the level root and paints them with the style's materials (each child by
    /// its role: Body, Accent, Eye). Rebuilt whenever the style or the level changes; destroyed with the level.
    /// </summary>
    public static class StyleDecor
    {
        static readonly List<GameObject> s_Pieces = new();
        static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        static MaterialPropertyBlock s_Block;

        public static int Count => s_Pieces.Count;

        public static void Clear()
        {
            foreach (var go in s_Pieces)
                if (go != null) Object.Destroy(go);
            s_Pieces.Clear();
        }

        public static void Rebuild(StyleGeometry g, StyleKit kit)
        {
            Clear();
            if (g == null || kit == null || !g.HasArchitecture || ActiveLevel.Root == null || ActiveLevel.Shell == null) return;
            var root = ActiveLevel.Root.transform;
            s_Block ??= new MaterialPropertyBlock();
            foreach (var p in ArchitecturePlanner.Plan(ActiveLevel.Shell, ActiveLevel.Layout, g))
            {
                bool finish = p.Kind is DecorKind.FloorFinish or DecorKind.WallFinish;
                var prefab = finish ? kit.Finish : (int)p.Kind < kit.Pieces.Length ? kit.Pieces[(int)p.Kind] : null;
                if (prefab == null) continue;
                var go = Object.Instantiate(prefab, p.Position, p.Rotation, root);
                go.transform.localScale = p.Scale;
                s_Pieces.Add(go);
                if (finish)
                {
                    var r = go.GetComponentInChildren<MeshRenderer>();
                    bool floor = p.Kind == DecorKind.FloorFinish;
                    float tile = Mathf.Max(0.05f, floor ? g.FloorTile : g.WallTile);
                    r.sharedMaterial = floor ? g.Floor : g.Wall;
                    s_Block.Clear();
                    s_Block.SetVector(BaseMapSt, new Vector4(p.Surface.x / tile, p.Surface.y / tile, p.Surface.z / tile, p.Surface.w / tile));
                    r.SetPropertyBlock(s_Block);
                    continue;
                }
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                {
                    var m = MaterialFor(p.Kind, r.gameObject.name, g);
                    if (m != null) r.sharedMaterial = m;
                }
            }
        }

        /// <summary>The style material for a child of a kit piece (pure).</summary>
        public static Material MaterialFor(DecorKind kind, string child, StyleGeometry g)
        {
            if (child.StartsWith("Eye")) return g.Glow ?? g.Trim;
            bool accent = child.StartsWith("Accent");
            Material m = kind switch
            {
                DecorKind.Wainscot => accent ? g.Panel : g.Trim,
                DecorKind.Pipes or DecorKind.PipeClamp => g.Pipe,
                DecorKind.Pilaster or DecorKind.Arch => accent ? g.Trim : g.Column,
                DecorKind.Beam => g.Beam,
                DecorKind.IBeam => accent ? g.Pipe : g.Beam,
                DecorKind.Coffer => accent ? g.Trim : g.Beam,
                DecorKind.Tray or DecorKind.TrayHanger => accent ? g.Trim : g.Pipe,
                _ => g.Trim,
            };
            return m ?? g.Trim;
        }
    }
}
