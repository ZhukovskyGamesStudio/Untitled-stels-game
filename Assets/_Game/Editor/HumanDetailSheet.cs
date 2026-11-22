using System.Collections.Generic;
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
    /// Close-ups of the rigged humans, a detail at a time: the head, the neck, a shoulder, an elbow, each hand, the waist,
    /// a knee, a foot — every one from several sides in an orthographic view (no perspective to hide a proportion), in the
    /// idle pose and in the poses that bend it. One PNG per detail and pose, its views side by side →
    /// Logs/poses/_details/&lt;who&gt;/&lt;detail&gt;_&lt;pose&gt;.png. A whole figure hides what a close-up shows: a seam, a
    /// pinched joint, a mitten of a hand. Menu Stealth → Human Detail Pictures, or in batch (with graphics):
    /// <c>Unity -batchmode -projectPath . -executeMethod Stealth.EditorTools.HumanDetailSheet.Batch</c>
    /// (<c>HUMAN_DETAIL_WHO=backend</c> — one person; <c>HUMAN_DETAIL_ONLY=hand_r,head</c> — these details).
    /// </summary>
    public static class HumanDetailSheet
    {
        const int Tile = 420;

        /// <summary>A pose the details are looked at in.</summary>
        struct Stance
        {
            public string Name;
            public float Speed, Climb, Time;
            public bool Crouch, Sit, Hold;
            public BodyPose Pose;
        }

        static readonly Stance[] Stances =
        {
            new() { Name = "idle", Time = 1.2f },
            new() { Name = "walk", Speed = 1.5f, Time = 0.9f },
            new() { Name = "run", Speed = 4.5f, Time = 0.75f },
            new() { Name = "crouch", Crouch = true, Time = 1f },
            new() { Name = "sit", Sit = true, Time = 1f },
            new() { Name = "climb", Pose = BodyPose.Ladder, Climb = 1f, Time = 0.8f },
            new() { Name = "hold", Hold = true, Time = 1f },
            new() { Name = "work", Pose = BodyPose.Work, Time = 1f },
        };

        /// <summary>A detail: the bones it lies between, how much of the world the picture shows, the sides, the poses.</summary>
        struct Detail
        {
            public string Name;
            public HumanBodyBones[] Bones;
            /// <summary>Metres the picture is tall and wide.</summary>
            public float Span;
            /// <summary>Shifts the centre, in the body's space.</summary>
            public Vector3 Offset;
            /// <summary>Views in the hand's own frame (along the fingers, to the thumb, out of the palm) rather than the body's.</summary>
            public bool Hand;
            public string[] Stances;
        }

        const HumanBodyBones LH = HumanBodyBones.LeftHand, RH = HumanBodyBones.RightHand;

        static readonly Detail[] Details =
        {
            new() { Name = "head", Bones = new[] { HumanBodyBones.Head }, Span = 0.36f, Offset = new Vector3(0f, 0.1f, 0.01f), Stances = new[] { "idle" } },
            new() { Name = "neck", Bones = new[] { HumanBodyBones.Neck }, Span = 0.34f, Stances = new[] { "idle", "work" } },
            new() { Name = "chest", Bones = new[] { HumanBodyBones.Chest }, Span = 0.62f, Offset = new Vector3(0f, 0.08f, 0f), Stances = new[] { "idle", "climb" } },
            new() { Name = "shoulder_l", Bones = new[] { HumanBodyBones.LeftUpperArm }, Span = 0.36f, Stances = new[] { "idle", "walk", "climb", "work" } },
            new() { Name = "elbow_l", Bones = new[] { HumanBodyBones.LeftLowerArm }, Span = 0.3f, Stances = new[] { "idle", "run", "climb", "work" } },
            new() { Name = "hand_l", Bones = new[] { LH, HumanBodyBones.LeftIndexProximal }, Span = 0.24f, Hand = true, Stances = new[] { "idle", "climb", "work" } },
            new() { Name = "hand_r", Bones = new[] { RH, HumanBodyBones.RightIndexProximal }, Span = 0.24f, Hand = true, Stances = new[] { "idle", "hold" } },
            new() { Name = "waist", Bones = new[] { HumanBodyBones.Hips }, Span = 0.56f, Offset = new Vector3(0f, 0.04f, 0f), Stances = new[] { "idle", "walk", "crouch", "sit" } },
            new() { Name = "knee_l", Bones = new[] { HumanBodyBones.LeftLowerLeg }, Span = 0.4f, Stances = new[] { "idle", "run", "crouch", "sit" } },
            new() { Name = "foot_l", Bones = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes }, Span = 0.38f, Stances = new[] { "idle", "walk", "crouch" } },
        };

        /// <summary>The sides a detail is seen from, in the body's space (it faces +Z): where the camera is, and its up.</summary>
        static readonly (Vector3 from, Vector3 up)[] Sides =
        {
            (Vector3.forward, Vector3.up), (new Vector3(-0.7f, 0.15f, 0.7f), Vector3.up), (Vector3.left, Vector3.up),
            (Vector3.back, Vector3.up), (Vector3.right, Vector3.up), (Vector3.up, Vector3.forward), (Vector3.down, Vector3.forward),
        };

        [MenuItem("Stealth/Human Detail Pictures")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var humans = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.PlayerHumans;
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "poses", "_details");
            string onlyWho = System.Environment.GetEnvironmentVariable("HUMAN_DETAIL_WHO");
            string onlyDetails = System.Environment.GetEnvironmentVariable("HUMAN_DETAIL_ONLY");
            var wanted = string.IsNullOrEmpty(onlyDetails) ? null : new HashSet<string>(onlyDetails.Split(','));

            // an even studio light: a key, a fill from the other side, one from behind — a close-up must not sit in shadow
            foreach (var (name, euler, power) in new[] { ("Key", new Vector3(35f, 205f, 0f), 1.15f), ("Fill", new Vector3(20f, 60f, 0f), 0.6f), ("Back", new Vector3(30f, -20f, 0f), 0.7f), ("Under", new Vector3(-50f, 180f, 0f), 0.35f) })
            {
                var light = new GameObject(name).AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = power;
                light.shadows = name == "Key" ? LightShadows.Soft : LightShadows.None;
                light.transform.rotation = Quaternion.Euler(euler);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.46f, 0.5f);
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.24f, 0.26f, 0.3f);
            cam.orthographic = true;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 12f;
            var rt = new RenderTexture(Tile, Tile, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(Tile, Tile, TextureFormat.RGB24, false);

            foreach (var prefab in humans.Where(h => h != null))
            {
                string who = prefab.name.Replace("HumanRig_", "").ToLowerInvariant();
                if (!string.IsNullOrEmpty(onlyWho) && who != onlyWho) continue;
                string dir = Path.Combine(root, who);
                Directory.CreateDirectory(dir);
                int pictures = 0;
                foreach (var stance in Stances)
                {
                    var details = Details.Where(d => d.Stances.Contains(stance.Name) && (wanted == null || wanted.Contains(d.Name))).ToArray();
                    if (details.Length == 0) continue;
                    var go = Object.Instantiate(prefab);
                    var rig = go.GetComponent<HumanRig>();
                    var animator = rig.Animator;
                    var dept = (Department)System.Array.IndexOf(humans, prefab);
                    PlayerLooks.Paint(go.transform, (int)dept, dept);
                    foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Rebind();
                    rig.Pose(stance.Speed, stance.Crouch, false, 0f, stance.Sit, stance.Pose, stance.Climb, stance.Hold);
                    rig.Settle();
                    animator.SetFloat("Speed", stance.Speed);
                    animator.SetFloat("ClimbSpeed", stance.Climb);
                    for (float t = 0f; t < stance.Time; t += 1f / 30f) animator.Update(1f / 30f);

                    foreach (var detail in details)
                    {
                        var centre = Vector3.zero;
                        foreach (var bone in detail.Bones) centre += animator.GetBoneTransform(bone).position;
                        centre = centre / detail.Bones.Length + detail.Offset;
                        var views = detail.Hand ? HandSides(animator, detail.Bones[0] == LH) : Sides;
                        var sheet = new Texture2D(Tile * views.Length, Tile, TextureFormat.RGB24, false);
                        cam.orthographicSize = detail.Span * 0.5f;
                        for (int v = 0; v < views.Length; v++)
                        {
                            var from = views[v].from.normalized;
                            cam.transform.position = centre + from * 3f;
                            cam.transform.rotation = Quaternion.LookRotation(-from, views[v].up);
                            cam.Render();
                            RenderTexture.active = rt;
                            tex.ReadPixels(new Rect(0, 0, Tile, Tile), 0, 0);
                            RenderTexture.active = null;
                            sheet.SetPixels(v * Tile, 0, Tile, Tile, tex.GetPixels());
                        }
                        sheet.Apply();
                        File.WriteAllBytes(Path.Combine(dir, $"{detail.Name}_{stance.Name}.png"), sheet.EncodeToPNG());
                        Object.DestroyImmediate(sheet);
                        pictures++;
                    }
                    Object.DestroyImmediate(go);
                }
                Debug.Log($"[Details] {who}: {pictures} pictures in {dir}");
            }
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// The sides of a hand in its own frame, fingers up the picture: the back, the palm, the thumb's edge, the little
        /// finger's edge, from the fingertips down the hand, and a three-quarter view of the palm.
        /// </summary>
        static (Vector3 from, Vector3 up)[] HandSides(Animator animator, bool left)
        {
            var hand = animator.GetBoneTransform(left ? LH : RH);
            var fingers = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var thumb = animator.GetBoneTransform(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            var along = (fingers.position - hand.position).normalized;
            var palm = Vector3.Cross(along, thumb.position - hand.position).normalized;
            if (!left) palm = -palm;
            var side = Vector3.Cross(palm, along).normalized;
            return new[]
            {
                (-palm, along), (palm, along), (side, along), (-side, along), (along, palm), ((palm + side * 0.7f + along * 0.5f).normalized, along),
            };
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
