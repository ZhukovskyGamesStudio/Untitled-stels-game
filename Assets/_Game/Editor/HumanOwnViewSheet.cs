using System.IO;
using System.Linq;
using Stealth.Configs;
using Stealth.Items;
using Stealth.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Stealth.EditorTools
{
    /// <summary>
    /// Pictures of what a player sees of himself, without the game: each department's human hung under a first-person camera
    /// the way the game does it (<see cref="HumanMath.OwnShift"/>, the skin without a head, the right arm folded away and the
    /// view's own arm round a thing), looking ahead and down, standing, crouched and walking →
    /// Logs/poses/_own/&lt;who&gt;_&lt;pose&gt;.png. Menu Stealth → Human Own View Pictures, or in batch with graphics:
    /// <c>Unity -batchmode -projectPath . -executeMethod Stealth.EditorTools.HumanOwnViewSheet.Batch</c>.
    /// </summary>
    public static class HumanOwnViewSheet
    {
        struct Shot
        {
            public string Name;
            public float Pitch, Speed, Eyes;
            public bool Crouch, Torch;
            public int Item;
        }

        [MenuItem("Stealth/Human Own View Pictures")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var humans = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.PlayerHumans;
            var visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>("Assets/_Game/Data/ItemVisuals.asset");
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab").GetComponent<PlayerController>();
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "poses", "_own");
            Directory.CreateDirectory(dir);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, 160f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.42f, 0.45f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 2f;
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.22f, 0.25f);
            cam.fieldOfView = player.Camera.fieldOfView;
            cam.nearClipPlane = player.Camera.nearClipPlane;
            var rt = new RenderTexture(960, 600, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(960, 600, TextureFormat.RGB24, false);
            var shots = new[]
            {
                new Shot { Name = "ahead", Item = -1, Eyes = 1.6f },
                new Shot { Name = "down40", Pitch = 40f, Item = -1, Eyes = 1.6f },
                new Shot { Name = "down85", Pitch = 85f, Item = -1, Eyes = 1.6f },
                new Shot { Name = "walk_down60", Pitch = 60f, Speed = 1.5f, Item = -1, Eyes = 1.6f },
                new Shot { Name = "run_ahead", Pitch = 10f, Speed = 4.5f, Item = -1, Eyes = 1.6f },
                new Shot { Name = "crouch_ahead", Crouch = true, Item = -1, Eyes = 0.95f },
                new Shot { Name = "crouch_down60", Pitch = 60f, Crouch = true, Item = -1, Eyes = 0.95f },
                new Shot { Name = "torch_ahead", Torch = true, Item = -1, Eyes = 1.6f },
                new Shot { Name = "torch_down50", Pitch = 50f, Torch = true, Item = -1, Eyes = 1.6f },
                new Shot { Name = "screwdriver", Item = ItemCatalog.Screwdriver, Eyes = 1.6f },
                new Shot { Name = "radio", Item = ItemCatalog.Radio, Eyes = 1.6f },
                new Shot { Name = "tablet", Item = ItemCatalog.Tablet, Eyes = 1.6f },
                new Shot { Name = "medkit", Item = ItemCatalog.Medkit, Eyes = 1.6f },
                new Shot { Name = "finder", Item = ItemCatalog.Finder, Eyes = 1.6f },
                new Shot { Name = "key", Item = ItemCatalog.Key, Eyes = 1.6f },
            };
            var only = System.Environment.GetEnvironmentVariable("WHO");
            foreach (var prefab in humans.Where(h => h != null))
            {
                string who = prefab.name.Replace("HumanRig_", "").ToLowerInvariant();
                if (!string.IsNullOrEmpty(only) && who != only) continue;
                var dept = (Department)System.Array.IndexOf(humans, prefab);
                foreach (var shot in shots)
                {
                    var go = Object.Instantiate(prefab);
                    var rig = go.GetComponent<HumanRig>();
                    PlayerLooks.Paint(go.transform, (int)dept, dept);
                    rig.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    rig.Animator.Rebind();
                    rig.Pose(shot.Speed, shot.Crouch, false, 0f);
                    rig.Settle();
                    rig.Animator.SetFloat("Speed", shot.Speed);
                    for (float t = 0f; t < 0.9f; t += 1f / 30f) rig.Animator.Update(1f / 30f);
                    // as PlayerController.UpdateOwnLook and HumanRig.FirstPerson do
                    var eyes = new Vector3(0f, shot.Eyes, 0f);
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == rig.OwnSkin) r.enabled = true;
                        else if (HumanMath.OnTheHead(r.name) || r == rig.Skin) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    }
                    rig.Hips.position += HumanMath.OwnShift(eyes, rig.HeadBone.position, Vector3.forward, 1f, planted: true);
                    bool holds = shot.Torch || shot.Item >= 0;
                    if (holds) rig.Animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localScale = Vector3.one * 1e-4f;
                    var head = new GameObject("Head").transform;
                    head.SetPositionAndRotation(eyes, Quaternion.Euler(shot.Pitch, 0f, 0f));
                    cam.transform.SetPositionAndRotation(head.position, head.rotation);
                    if (holds)
                    {
                        var at = shot.Torch ? player.ViewFlashlight.transform : player.HeldThing.transform.parent;
                        var anchor = new GameObject("Anchor").transform;
                        anchor.SetParent(head, false);
                        anchor.SetLocalPositionAndRotation(at.localPosition, at.localRotation);
                        bool cup = shot.Item >= 0 && ItemCatalog.HeldPoseOf(shot.Item).Compact;
                        var arm = Object.Instantiate(cup ? rig.ViewCup : rig.ViewFist, anchor, false);
                        PlayerLooks.Paint(arm.transform, (int)dept, dept);
                        if (shot.Torch)
                        {
                            var torch = Object.Instantiate(player.ViewFlashlight, head, false);
                            torch.SetActive(true);
                            foreach (var r in torch.GetComponentsInChildren<Renderer>(true))
                                if (r.name.StartsWith("Body") || r.name.StartsWith("AccentSleeve")) r.enabled = false;
                        }
                        else
                        {
                            var model = visuals.Models != null && shot.Item < visuals.Models.Length ? visuals.Models[shot.Item] : null;
                            if (model != null)
                            {
                                var thing = Object.Instantiate(model, anchor, false);
                                PlayerController.HeldLocal(shot.Item, PlayerController.ModelBounds(shot.Item), out var p, out var q, out float s);
                                thing.transform.localScale = Vector3.one * s;
                                thing.transform.SetLocalPositionAndRotation(p, q);
                            }
                        }
                    }
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 960, 600), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(dir, $"{who}_{shot.Name}.png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(go);
                    Object.DestroyImmediate(head.gameObject);
                }
            }
            Debug.Log($"[OwnView] pictures in {dir}");
        }

        public static void Batch()
        {
            try { Render(); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); return; }
            EditorApplication.Exit(0);
        }
    }
}
