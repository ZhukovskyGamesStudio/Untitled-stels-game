using System.Collections;
using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>
        /// Light must not leak: a closed box (wall and slab prefabs, no windows) in the lit lobby, a plant inside, a plant and
        /// a lit desk lamp on its roof, a floor lamp right outside. Inside the frame must be black; outside — not.
        /// </summary>
        IEnumerator DarkBoxScenario()
        {
            DevFlags.SkipGracePeriod = true;
            DevFlags.Peaceful = true;
            var art = GameManager.Instance.Art;
            var c = ActiveLevel.Layout.HatchPosition + new Vector3(0f, 0f, 2.6f);
            const float inner = 1.6f, height = 2.3f, t = LevelMetrics.WallThickness;
            var parts = new List<GameObject>();
            GameObject Piece(GameObject prefab, Vector3 center, Vector3 size)
            {
                var go = Instantiate(prefab, center, Quaternion.identity);
                go.transform.localScale = size;
                parts.Add(go);
                return go;
            }
            float h = inner / 2f + t / 2f;
            Piece(art.WallBlock, c + new Vector3(0, height / 2f, h), new Vector3(inner + 2 * t, height, t));
            Piece(art.WallBlock, c + new Vector3(0, height / 2f, -h), new Vector3(inner + 2 * t, height, t));
            Piece(art.WallBlock, c + new Vector3(h, height / 2f, 0), new Vector3(t, height, inner));
            Piece(art.WallBlock, c + new Vector3(-h, height / 2f, 0), new Vector3(t, height, inner));
            Piece(art.FloorSlab, c + new Vector3(0, -0.05f, 0), new Vector3(inner + 2 * t, 0.2f, inner + 2 * t));
            Piece(art.FloorSlab, c + new Vector3(0, height + t / 2f, 0), new Vector3(inner + 2 * t, t, inner + 2 * t));
            parts.Add(Instantiate(art.Plant, c + new Vector3(0.45f, 0.05f, 0.45f), Quaternion.identity));             // inside
            parts.Add(Instantiate(art.Plant, c + new Vector3(-0.4f, height + t, 0f), Quaternion.identity));           // on the roof
            parts.Add(Instantiate(art.DeskLamp, c + new Vector3(0.4f, height + t, 0f), Quaternion.Euler(0, 180f, 0))); // lit lamp on the roof
            parts.Add(Instantiate(art.FloorLamp, c + new Vector3(h + 0.45f, 0f, 0f), Quaternion.identity));           // right outside
            Physics.SyncTransforms();

            // The lobby's ceiling lamp from below: the panels must glow.
            var lampPos = ActiveLevel.Layout.HatchPosition + Vector3.up * LevelMetrics.WallHeight;
            LookAtFrom(new Pose(ActiveLevel.Layout.HatchPosition + new Vector3(-2.2f, 0.05f, -1.2f), Quaternion.LookRotation(Vector3.forward)), lampPos);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("lamp_from_below");
            foreach (var rl in FindObjectsByType<RoomLight>())
            {
                if (Vector3.Distance(rl.transform.position, lampPos) > 1f) continue;
                var block = new MaterialPropertyBlock();
                foreach (var p in rl.Panels)
                {
                    p.GetPropertyBlock(block);
                    Log($"lamp panel {p.name}: enabled={p.enabled} visible={p.isVisible} mat={p.sharedMaterial?.name} emission(block)={block.GetColor("_EmissionColor")} " +
                        $"emission(mat)={p.sharedMaterial?.GetColor("_EmissionColor")} keyword={p.sharedMaterial?.IsKeywordEnabled("_EMISSION")} pos={p.bounds.center}");
                }
                Log($"lamp light: enabled={rl.Lamp.enabled} intensity={rl.Lamp.intensity:0.00} mask={rl.Lamp.cullingMask} shadows={rl.Lamp.shadows}");
            }

            // Inside, looking at the plant in the corner.
            var inside = c + new Vector3(-0.35f, 0.05f, -0.35f);
            LookAtFrom(new Pose(inside, Quaternion.LookRotation(Flat(new Vector3(0.8f, 0, 0.8f)))), c + new Vector3(0.45f, 0.6f, 0.45f));
            yield return new WaitForSeconds(1.5f); // fog settles to the indoor colour
            var (maxIn, meanIn) = MeasureFrame();
            float lightIn = Perception.IlluminationOf(Me);
            yield return Shot("inside");

            // Which light leaks? Switch the suspects off one group at a time.
            var moon = FindObjectsByType<Light>();
            void Only(System.Func<Light, bool> off, bool on)
            {
                foreach (var l in moon) if (off(l)) l.enabled = on;
                foreach (var r in FindObjectsByType<RoomLight>()) if (off(r.Lamp)) r.enabled = on;
            }
            var groups = new (string name, System.Func<Light, bool> pick)[]
            {
                ("moon", l => l.type == LightType.Directional),
                ("room lamps", l => l.GetComponentInParent<RoomLight>() != null),
                ("floor/desk lamps", l => l.transform.parent != null && (l.transform.parent.name.Contains("FloorLamp") || l.transform.parent.name.Contains("DeskLamp"))),
                ("everything else", l => l.type != LightType.Directional && l.GetComponentInParent<RoomLight>() == null),
            };
            foreach (var (name, pick) in groups)
            {
                Only(pick, false);
                yield return null;
                var (mx, mn) = MeasureFrame();
                Log($"darkbox: without {name}: max {mx:0.000} mean {mn:0.0000}");
                Only(pick, true);
            }
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            yield return null;
            var (fx, fm) = MeasureFrame();
            Log($"darkbox: without fog: max {fx:0.000} mean {fm:0.0000}");
            RenderSettings.fog = fog;

            // Control: outside the box, looking at it.
            var outside = c + new Vector3(-2.2f, 0.05f, -2.2f);
            LookAtFrom(new Pose(outside, Quaternion.LookRotation(Flat(c - outside))), c + Vector3.up * 1.2f);
            yield return new WaitForSeconds(0.5f);
            var (maxOut, meanOut) = MeasureFrame();
            yield return Shot("outside");

            bool black = maxIn < 0.02f;
            Log($"darkbox: inside max {maxIn:0.000} mean {meanIn:0.0000} light on player {lightIn:0.000} → {(black ? "BLACK" : "LIGHT LEAKS")}; " +
                $"outside max {maxOut:0.000} mean {meanOut:0.000}");
            foreach (var p in parts) Destroy(p);
        }

        /// <summary>Renders the player's camera (no UI) and returns the brightest pixel and the mean brightness, 0..1.</summary>
        static (float max, float mean) MeasureFrame()
        {
            var cam = Me.Camera;
            var rt = RenderTexture.GetTemporary(320, 180, 24);
            var old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = old;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            float max = 0f, sum = 0f;
            var px = tex.GetPixels();
            foreach (var p in px)
            {
                float l = 0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b;
                max = Mathf.Max(max, l);
                sum += l;
            }
            Destroy(tex);
            return (max, sum / px.Length);
        }
    }
}
