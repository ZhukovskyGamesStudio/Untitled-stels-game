using System.IO;
using System.Linq;
using Stealth.Configs;
using Stealth.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Stealth.EditorTools
{
    /// <summary>
    /// Pictures of the rigged humans in every pose, for a look without the game: each department's human, posed by its own
    /// Animator (the humanoid layers and the IK, as in the game) in an empty lit scene, one PNG per pose →
    /// Logs/poses/&lt;who&gt;/NN_&lt;pose&gt;.png (contact sheets: <c>python Tools/sheet.py Logs/poses/&lt;who&gt; 4 3</c>).
    /// Run from the menu Stealth → Human Pose Pictures, or in batch (with graphics, not -nographics):
    /// <c>Unity -batchmode -projectPath . -executeMethod Stealth.EditorTools.HumanPoseSheet.Batch -logFile Logs/poses.log</c>.
    /// </summary>
    public static class HumanPoseSheet
    {
        /// <summary>A pose: the Animator's parameters, whether the hand holds something, and how long the take runs before the picture.</summary>
        struct Shot
        {
            public string Name;
            public float Speed, Climb, Time;
            public bool Crouch, Down, Sit, Hold, NoIK, Close;
            /// <summary>The thing in the hand (an ItemCatalog shape; -1: nothing) and its colour.</summary>
            public int Item, Color;
            public BodyPose Pose;
        }

        static readonly Shot[] Shots =
        {
            new() { Name = "idle", Time = 1.2f, Item = -1 },
            new() { Name = "walk", Speed = 1.5f, Time = 0.9f },
            new() { Name = "run", Speed = 4.5f, Time = 0.75f },
            new() { Name = "crouch", Crouch = true, Time = 1f },
            new() { Name = "crouch_walk", Crouch = true, Speed = 1.3f, Time = 0.9f },
            new() { Name = "hold", Hold = true, Time = 1f, Item = ItemCatalogShape.Radio },
            new() { Name = "walk_hold", Speed = 1.5f, Hold = true, Time = 0.9f, Item = ItemCatalogShape.Tablet },
            new() { Name = "sit", Sit = true, Time = 1f },
            new() { Name = "down", Down = true, Time = 1.5f },
            new() { Name = "climb", Pose = BodyPose.Ladder, Climb = 1f, Time = 0.8f },
            new() { Name = "terminal", Pose = BodyPose.Terminal, Time = 1f },
            new() { Name = "work", Pose = BodyPose.Work, Time = 1f },
            // the right hand close up with each kind of thing it carries
            new() { Name = "hand_screwdriver", Hold = true, Close = true, Time = 1f, Item = ItemCatalogShape.Screwdriver },
            new() { Name = "hand_radio", Hold = true, Close = true, Time = 1f, Item = ItemCatalogShape.Radio },
            new() { Name = "hand_tablet", Hold = true, Close = true, Time = 1f, Item = ItemCatalogShape.Tablet },
            new() { Name = "hand_keyboard", Hold = true, Close = true, Time = 1f, Item = ItemCatalogShape.Keyboard },
            new() { Name = "hand_coffee", Hold = true, Close = true, Time = 1f, Item = ItemCatalogShape.Coffee },
            new() { Name = "hand_medkit", Hold = true, Close = true, Time = 1f, Item = ItemCatalogShape.Medkit },
            new() { Name = "hand_finder", Hold = true, Close = true, Time = 1f, Item = ItemCatalogShape.Finder },
            new() { Name = "hand_loot", Hold = true, Close = true, Time = 1f, Item = 0, Color = 2 },
        };

        /// <summary>The shapes the pictures hold (ItemCatalog's, as constants for the table above).</summary>
        static class ItemCatalogShape
        {
            public const int Screwdriver = Items.ItemCatalog.Screwdriver, Radio = Items.ItemCatalog.Radio, Tablet = Items.ItemCatalog.Tablet,
                Keyboard = Items.ItemCatalog.Keyboard, Coffee = Items.ItemCatalog.Coffee, Medkit = Items.ItemCatalog.Medkit, Finder = Items.ItemCatalog.Finder;
        }

        const int Width = 640, Height = 860;

        [MenuItem("Stealth/Human Pose Pictures")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var humans = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.PlayerHumans;
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "poses");
            var visuals = AssetDatabase.LoadAssetAtPath<Items.ItemVisuals>("Assets/_Game/Data/ItemVisuals.asset");

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40f, 200f, 0f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.45f;
            fill.transform.rotation = Quaternion.Euler(20f, 40f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 2f;
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.22f, 0.25f);
            cam.fieldOfView = 32f;
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            foreach (var prefab in humans.Where(h => h != null))
            {
                string who = prefab.name.Replace("HumanRig_", "").ToLowerInvariant();
                string dir = Path.Combine(root, who);
                Directory.CreateDirectory(dir);
                for (int i = 0; i < Shots.Length; i++)
                {
                    var shot = Shots[i];
                    var go = Object.Instantiate(prefab);
                    var rig = go.GetComponent<HumanRig>();
                    var animator = rig.Animator;
                    var ik = animator.GetComponent<HumanIK>();
                    if (ik != null) ik.enabled = !shot.NoIK;
                    // dressed as in the game: the department's clothes, a team colour, a skin tone
                    var dept = (Department)System.Array.IndexOf(humans, prefab);
                    PlayerLooks.Paint(go.transform, (int)dept, dept);
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Rebind();
                    rig.Pose(shot.Speed, shot.Crouch, shot.Down, 0f, shot.Sit, shot.Pose, shot.Climb, shot.Hold);
                    rig.Settle();
                    animator.SetFloat("Speed", shot.Speed);
                    animator.SetFloat("ClimbSpeed", shot.Climb);
                    // the take runs a while (blends settle), a frame at a time so the IK passes run too
                    for (float t = 0f; t < shot.Time; t += 1f / 30f) animator.Update(1f / 30f);
                    // the thing in the palm, held the way the game holds it (PlayerController.HeldLocal under the anchor that
                    // HumanRig.PlaceHeld puts in the palm)
                    GameObject thing = null;
                    var palm = rig.HandR != null && rig.FingersR != null ? HumanMath.Palm(rig.HandR.position, rig.FingersR.position) : Vector3.up;
                    if (shot.Hold && shot.Item >= 0 && visuals != null)
                        thing = Held(visuals, shot.Item, shot.Color, palm, go.transform.rotation);
                    // a three-quarter view from the front (the body faces +Z); lying, from higher and further; the hand close up
                    var look = shot.Close ? palm : shot.Down ? new Vector3(0f, 0.3f, 0f) : new Vector3(0f, shot.Sit || shot.Crouch ? 0.65f : 0.95f, 0f);
                    cam.transform.position = look + (shot.Close ? new Vector3(0.75f, 0.3f, 0.8f) : shot.Down ? new Vector3(2.4f, 2.6f, 2.6f) : new Vector3(1.7f, 0.45f, 3.3f));
                    cam.transform.LookAt(look);
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(dir, $"{i:00}_{shot.Name}.png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(go);
                    if (thing != null) Object.DestroyImmediate(thing);
                }
                Debug.Log($"[Poses] {who}: {Shots.Length} pictures in {dir}");
            }
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        /// <summary>A held thing as the game shows it: an anchor at the palm facing the body's way, the model or the piece inside.</summary>
        static GameObject Held(Items.ItemVisuals visuals, int shape, int color, Vector3 palm, Quaternion body)
        {
            var anchor = new GameObject("HeldAnchor");
            anchor.transform.SetPositionAndRotation(palm, body);
            var thing = new GameObject("HeldThing");
            thing.transform.SetParent(anchor.transform, false);
            var model = visuals.Models != null && shape < visuals.Models.Length ? visuals.Models[shape] : null;
            Bounds b;
            if (model != null)
            {
                Object.Instantiate(model, thing.transform);
                b = PlayerController.ModelBounds(shape);
            }
            else
            {
                var mesh = visuals.Meshes[Mathf.Clamp(shape, 0, visuals.Meshes.Length - 1)];
                thing.AddComponent<MeshFilter>().sharedMesh = mesh;
                thing.AddComponent<MeshRenderer>().sharedMaterial = visuals.ColorMaterials[Mathf.Clamp(color, 0, visuals.ColorMaterials.Length - 1)];
                b = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.12f);
            }
            PlayerController.HeldLocal(shape, b, out var position, out var rotation, out float scale);
            thing.transform.localScale = Vector3.one * scale;
            thing.transform.SetLocalPositionAndRotation(position, rotation);
            return anchor;
        }

        /// <summary>Batch entry: the pictures, then quit.</summary>
        public static void Batch()
        {
            int code = 0;
            try { Render(); }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
