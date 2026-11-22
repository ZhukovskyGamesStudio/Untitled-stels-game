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
    /// idle pose and in the poses that bend it — and fine details a few centimetres wide: an eye, the lips, the nose, an
    /// ear, the hairline, the nape, the fingers, the thumb, a cuff, the collar, the ankle, a toe. Every view is its own
    /// large picture → Logs/poses/_details/&lt;who&gt;/&lt;detail&gt;_&lt;pose&gt;/&lt;side&gt;.png, plus a small overview
    /// strip &lt;detail&gt;_&lt;pose&gt;.png. The camera cuts away what is in front of the detail and behind it (near and far
    /// planes around it): a thigh no longer hides the palm, the other hand no longer stands behind this one. A whole figure
    /// hides what a close-up shows: a seam, a pinched joint, a mitten of a hand. Menu Stealth → Human Detail Pictures, or
    /// in batch (with graphics): <c>Unity -batchmode -projectPath . -executeMethod Stealth.EditorTools.HumanDetailSheet.Batch</c>
    /// (<c>HUMAN_DETAIL_WHO=backend</c> — one person; <c>HUMAN_DETAIL_ONLY=hand_r,eye</c> — these details;
    /// <c>HUMAN_DETAIL_POSE=idle</c> — these poses; <c>HUMAN_DETAIL_TILE=900</c> — pixels of a view; <c>HUMAN_DETAIL_FLAT=1</c> —
    /// without the normal maps, into _details_flat: is a flaw in the game mesh or in the bake?; <c>HUMAN_DETAIL_PARTS=AccentHead</c> —
    /// only these renderers: whose surface is it?; <c>HUMAN_DETAIL_SHADOW=1</c> — the key light casts shadows;
    /// <c>HUMAN_DETAIL_NOCUT=1</c> — nothing cut away: is that blotch where the near plane slices a finger?;
    /// <c>HUMAN_DETAIL_WEAR=bob,glasses</c> — these wardrobe items tried on, into _details_wear).
    /// </summary>
    public static class HumanDetailSheet
    {
        const int Thumb = 300;
        /// <summary>How far the camera stands from the detail (the near and far planes are set around the detail from here).</summary>
        const float Distance = 2f;

        /// <summary>A pose the details are looked at in.</summary>
        struct Stance
        {
            public string Name;
            public float Speed, Climb, Time;
            public bool Crouch, Sit, Hold;
            /// <summary>No animation at all: the skin as bound (is a flaw in the mesh or in the skinning?).</summary>
            public bool Rest;
            public BodyPose Pose;
        }

        static readonly Stance[] Stances =
        {
            new() { Name = "rest", Rest = true },
            new() { Name = "idle", Time = 1.2f },
            new() { Name = "walk", Speed = 1.5f, Time = 0.9f },
            new() { Name = "run", Speed = 4.5f, Time = 0.75f },
            new() { Name = "crouch", Crouch = true, Time = 1f },
            new() { Name = "sit", Sit = true, Time = 1f },
            new() { Name = "climb", Pose = BodyPose.Ladder, Climb = 1f, Time = 0.8f },
            new() { Name = "hold", Hold = true, Time = 1f },
            new() { Name = "work", Pose = BodyPose.Work, Time = 1f },
        };

        /// <summary>A detail: where it is, how much of the world the picture shows, the sides, the poses.</summary>
        struct Detail
        {
            public string Name;
            /// <summary>The centre: the mean of these bones (a bone listed twice weighs twice).</summary>
            public HumanBodyBones[] Bones;
            /// <summary>Or the centre is this point of the figure as modelled (Blender's metres: +X its left, −Y its front,
            /// +Z up), carried by the head bone the way the skin is — a feature of the face wherever the head turns.</summary>
            public Vector3? OnHead;
            /// <summary>Metres the picture is tall and wide.</summary>
            public float Span;
            /// <summary>Shifts the centre, in the body's space.</summary>
            public Vector3 Offset;
            /// <summary>Views in the hand's own frame (along the fingers, to the thumb, out of the palm) rather than the body's.</summary>
            public bool Hand;
            /// <summary>The sides to look from (names in <see cref="Sides"/> or <see cref="HandSides"/>); null — the default set.</summary>
            public string[] From;
            public string[] Stances;
        }

        const HumanBodyBones LH = HumanBodyBones.LeftHand, RH = HumanBodyBones.RightHand;
        static readonly string[] Face = { "f", "fl", "l", "fr", "r", "hi", "lo" };
        static readonly string[] Ring = { "f", "fl", "l", "bl", "b", "br", "r", "fr" };

        static readonly Detail[] Details =
        {
            new() { Name = "head", Bones = new[] { HumanBodyBones.Head }, Span = 0.36f, Offset = new Vector3(0f, 0.1f, 0.01f), Stances = new[] { "idle" } },
            new() { Name = "eye", OnHead = new Vector3(0.034f, -0.075f, 1.668f), Span = 0.07f, From = Face, Stances = new[] { "idle" } },
            new() { Name = "mouth", OnHead = new Vector3(0f, -0.09f, 1.60f), Span = 0.09f, From = Face, Stances = new[] { "idle", "rest" } },
            new() { Name = "beard", OnHead = new Vector3(0f, -0.07f, 1.575f), Span = 0.13f, From = new[] { "f", "fl", "l", "fr", "lo", "hi" }, Stances = new[] { "idle" } },
            new() { Name = "nose", OnHead = new Vector3(0f, -0.1f, 1.64f), Span = 0.08f, From = Face, Stances = new[] { "idle" } },
            new() { Name = "ear", OnHead = new Vector3(0.075f, 0.0f, 1.655f), Span = 0.09f, From = new[] { "l", "r", "fl", "bl", "f", "b" }, Stances = new[] { "idle" } },
            new() { Name = "hairline", OnHead = new Vector3(0f, -0.04f, 1.74f), Span = 0.18f, From = new[] { "f", "fl", "l", "hi", "bl", "b" }, Stances = new[] { "idle" } },
            new() { Name = "hair", OnHead = new Vector3(0f, 0.0f, 1.71f), Span = 0.28f, From = new[] { "f", "fl", "l", "bl", "b", "hi", "top" }, Stances = new[] { "idle", "run" } },
            new() { Name = "nape", OnHead = new Vector3(0f, 0.06f, 1.58f), Span = 0.16f, From = new[] { "b", "bl", "l", "br" }, Stances = new[] { "idle" } },
            new() { Name = "neck", Bones = new[] { HumanBodyBones.Neck }, Span = 0.34f, Stances = new[] { "idle", "work" } },
            new() { Name = "collar", Bones = new[] { HumanBodyBones.Neck, HumanBodyBones.Chest }, Span = 0.22f, From = Ring, Stances = new[] { "idle" } },
            new() { Name = "chest", Bones = new[] { HumanBodyBones.Chest }, Span = 0.62f, Offset = new Vector3(0f, 0.08f, 0f), Stances = new[] { "idle", "climb" } },
            new() { Name = "shoulder_l", Bones = new[] { HumanBodyBones.LeftUpperArm }, Span = 0.36f, Stances = new[] { "idle", "walk", "climb", "work" } },
            new() { Name = "elbow_l", Bones = new[] { HumanBodyBones.LeftLowerArm }, Span = 0.3f, Stances = new[] { "idle", "run", "climb", "work" } },
            new() { Name = "forearm_l", Bones = new[] { HumanBodyBones.LeftLowerArm, LH }, Span = 0.2f, From = Ring, Stances = new[] { "idle", "rest" } },
            new() { Name = "cuff_l", Bones = new[] { LH, LH, HumanBodyBones.LeftLowerArm }, Span = 0.14f, From = Ring, Stances = new[] { "idle" } },
            new() { Name = "hand_l", Bones = new[] { LH, HumanBodyBones.LeftIndexProximal }, Span = 0.24f, Hand = true, Stances = new[] { "idle", "climb", "work" } },
            new() { Name = "fingers_l", Bones = new[] { HumanBodyBones.LeftMiddleIntermediate }, Span = 0.11f, Hand = true, Stances = new[] { "idle", "work" } },
            new() { Name = "thumb_l", Bones = new[] { HumanBodyBones.LeftThumbIntermediate }, Span = 0.09f, Hand = true, Stances = new[] { "idle" } },
            new() { Name = "hand_r", Bones = new[] { RH, HumanBodyBones.RightIndexProximal }, Span = 0.24f, Hand = true, Stances = new[] { "idle", "hold" } },
            new() { Name = "fingers_r", Bones = new[] { HumanBodyBones.RightMiddleIntermediate }, Span = 0.11f, Hand = true, Stances = new[] { "hold" } },
            new() { Name = "waist", Bones = new[] { HumanBodyBones.Hips }, Span = 0.56f, Offset = new Vector3(0f, 0.04f, 0f), Stances = new[] { "idle", "walk", "crouch", "sit" } },
            new() { Name = "knee_l", Bones = new[] { HumanBodyBones.LeftLowerLeg }, Span = 0.4f, Stances = new[] { "idle", "run", "crouch", "sit" } },
            new() { Name = "ankle_l", Bones = new[] { HumanBodyBones.LeftFoot }, Span = 0.16f, From = Ring, Stances = new[] { "idle" } },
            new() { Name = "foot_l", Bones = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes }, Span = 0.38f, Stances = new[] { "idle", "walk", "crouch" } },
            new() { Name = "toe_l", Bones = new[] { HumanBodyBones.LeftToes }, Span = 0.14f, From = new[] { "f", "fl", "l", "r", "hi", "under" }, Stances = new[] { "idle" } },
        };

        /// <summary>The sides a detail is seen from, in the body's space (it faces +Z): a name, where the camera is, and its up.</summary>
        static readonly (string name, Vector3 from, Vector3 up)[] Sides =
        {
            ("f", Vector3.forward, Vector3.up), ("fl", new Vector3(-0.7f, 0.15f, 0.7f), Vector3.up), ("l", Vector3.left, Vector3.up),
            ("bl", new Vector3(-0.7f, 0.1f, -0.7f), Vector3.up), ("b", Vector3.back, Vector3.up), ("br", new Vector3(0.7f, 0.1f, -0.7f), Vector3.up),
            ("r", Vector3.right, Vector3.up), ("fr", new Vector3(0.7f, 0.15f, 0.7f), Vector3.up),
            ("hi", new Vector3(0f, 0.6f, 0.8f), Vector3.up), ("lo", new Vector3(0f, -0.55f, 0.85f), Vector3.up),
            ("top", Vector3.up, Vector3.forward), ("under", Vector3.down, Vector3.forward),
        };

        static readonly string[] BroadSides = { "f", "fl", "l", "b", "r", "top", "under" };

        [MenuItem("Stealth/Human Detail Pictures")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var humans = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.PlayerHumans;
            string root = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "poses", "_details");
            string onlyWho = System.Environment.GetEnvironmentVariable("HUMAN_DETAIL_WHO");
            var wanted = Set("HUMAN_DETAIL_ONLY");
            var poses = Set("HUMAN_DETAIL_POSE");
            int tile = int.TryParse(System.Environment.GetEnvironmentVariable("HUMAN_DETAIL_TILE"), out int t) ? t : 900;
            bool flat = System.Environment.GetEnvironmentVariable("HUMAN_DETAIL_FLAT") == "1";
            var parts = Set("HUMAN_DETAIL_PARTS");
            var wear = Set("HUMAN_DETAIL_WEAR");
            if (wear != null) root += "_wear";
            bool nocut = System.Environment.GetEnvironmentVariable("HUMAN_DETAIL_NOCUT") == "1";
            if (flat) root += "_flat";

            // an even studio light: a key, a fill from the other side, one from behind — a close-up must not sit in shadow.
            // No cast shadows unless asked (HUMAN_DETAIL_SHADOW=1): the key light's shadow of the nose lay on the lip as a
            // hard-edged grey patch, the same side on every face, and passed for a flaw of the model through a dozen rebuilds
            bool shadows = System.Environment.GetEnvironmentVariable("HUMAN_DETAIL_SHADOW") == "1";
            foreach (var (name, euler, power) in new[] { ("Key", new Vector3(35f, 205f, 0f), 1.15f), ("Fill", new Vector3(20f, 60f, 0f), 0.6f), ("Back", new Vector3(30f, -20f, 0f), 0.7f), ("Under", new Vector3(-50f, 180f, 0f), 0.35f) })
            {
                var light = new GameObject(name).AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = power;
                light.shadows = shadows && name == "Key" ? LightShadows.Soft : LightShadows.None;
                light.transform.rotation = Quaternion.Euler(euler);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.46f, 0.5f);
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.24f, 0.26f, 0.3f);
            cam.orthographic = true;
            var rt = new RenderTexture(tile, tile, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            var tex = new Texture2D(tile, tile, TextureFormat.RGB24, false);

            foreach (var prefab in humans.Where(h => h != null))
            {
                string who = prefab.name.Replace("HumanRig_", "").ToLowerInvariant();
                if (!string.IsNullOrEmpty(onlyWho) && who != onlyWho) continue;
                string dir = Path.Combine(root, who);
                Directory.CreateDirectory(dir);
                int pictures = 0;
                foreach (var stance in Stances)
                {
                    if (poses != null && !poses.Contains(stance.Name)) continue;
                    var details = Details.Where(d => d.Stances.Contains(stance.Name) && (wanted == null || wanted.Contains(d.Name))).ToArray();
                    if (details.Length == 0) continue;
                    var go = Object.Instantiate(prefab);
                    var rig = go.GetComponent<HumanRig>();
                    var animator = rig.Animator;
                    var dept = (Department)System.Array.IndexOf(humans, prefab);
                    // try items on (HUMAN_DETAIL_WEAR=bob,glasses: the ones made for this person)
                    if (wear != null && go.TryGetComponent<HumanWardrobe>(out var wardrobe))
                        foreach (var item in wear)
                        {
                            var piece = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.Wardrobe.FirstOrDefault(w => w != null && w.Who == who && w.Item == item);
                            if (piece != null) wardrobe.Wear(piece);
                            else if (wardrobe.Own.Any(o => o.Item == item)) wardrobe.WearOwn(wardrobe.Own.First(o => o.Item == item).Slot);
                        }
                    PlayerLooks.Paint(go.transform, (int)dept, dept);
                    foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        skin.updateWhenOffscreen = true;
                        if (parts != null && !parts.Contains(skin.name)) skin.enabled = false;
                        if (!flat) continue;
                        foreach (var m in skin.materials)                  // the game mesh's own shading, without the baked normals
                        {
                            m.SetTexture("_BumpMap", null);
                            m.DisableKeyword("_NORMALMAP");
                        }
                    }
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    if (stance.Rest) Unpose(go, animator);
                    else
                    {
                        animator.Rebind();
                        rig.Pose(stance.Speed, stance.Crouch, false, 0f, stance.Sit, stance.Pose, stance.Climb, stance.Hold);
                        rig.Settle();
                        animator.SetFloat("Speed", stance.Speed);
                        animator.SetFloat("ClimbSpeed", stance.Climb);
                        for (float s = 0f; s < stance.Time; s += 1f / 30f) animator.Update(1f / 30f);
                    }

                    foreach (var detail in details)
                    {
                        var centre = CentreOf(detail, go, animator);
                        var views = detail.Hand ? HandSides(animator, detail.Name.EndsWith("_l")) : Sides;
                        var names = detail.From ?? (detail.Hand ? null : BroadSides);
                        var chosen = views.Where(v => names == null || names.Contains(v.name)).ToArray();
                        string sub = Path.Combine(dir, $"{detail.Name}_{stance.Name}");
                        Directory.CreateDirectory(sub);
                        var sheet = new Texture2D(Thumb * chosen.Length, Thumb, TextureFormat.RGB24, false);
                        cam.orthographicSize = detail.Span * 0.5f;
                        // (a hand's fingers stand up to 10 cm off the centre: a tighter cut sliced the nearer ones into light blotches)
                        float depth = Mathf.Max(detail.Span * 0.6f, detail.Hand ? 0.1f : 0.035f);
                        cam.nearClipPlane = nocut ? 0.05f : Distance - depth;
                        cam.farClipPlane = nocut ? 12f : Distance + depth * 1.6f;
                        for (int v = 0; v < chosen.Length; v++)
                        {
                            var from = chosen[v].from.normalized;
                            cam.transform.position = centre + from * Distance;
                            cam.transform.rotation = Quaternion.LookRotation(-from, chosen[v].up);
                            cam.Render();
                            RenderTexture.active = rt;
                            tex.ReadPixels(new Rect(0, 0, tile, tile), 0, 0);
                            tex.Apply();
                            RenderTexture.active = null;
                            File.WriteAllBytes(Path.Combine(sub, chosen[v].name + ".png"), tex.EncodeToPNG());
                            sheet.SetPixels(v * Thumb, 0, Thumb, Thumb, Shrink(tex, Thumb));
                        }
                        sheet.Apply();
                        File.WriteAllBytes(Path.Combine(dir, $"{detail.Name}_{stance.Name}.png"), sheet.EncodeToPNG());
                        Object.DestroyImmediate(sheet);
                        pictures += chosen.Length;
                    }
                    Object.DestroyImmediate(go);
                }
                Debug.Log($"[Details] {who}: {pictures} pictures in {dir}");
            }
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        /// <summary>The bones put back where every skin was bound to them (twice: a parent set after its child moves it).</summary>
        static void Unpose(GameObject go, Animator animator)
        {
            animator.enabled = false;
            go.GetComponent<HumanRig>().enabled = false;
            for (int pass = 0; pass < 2; pass++)
                foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var binds = skin.sharedMesh.bindposes;
                    for (int b = 0; b < skin.bones.Length; b++)
                    {
                        var bind = skin.transform.localToWorldMatrix * binds[b].inverse;
                        skin.bones[b].SetPositionAndRotation(bind.GetPosition(), bind.rotation);
                    }
                }
        }

        static HashSet<string> Set(string variable)
        {
            string value = System.Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrEmpty(value) ? null : new HashSet<string>(value.Split(','));
        }

        static Vector3 CentreOf(Detail detail, GameObject go, Animator animator)
        {
            if (detail.OnHead is Vector3 p) return OnHead(go, animator, p) + detail.Offset;
            var centre = Vector3.zero;
            foreach (var bone in detail.Bones) centre += animator.GetBoneTransform(bone).position;
            return centre / detail.Bones.Length + detail.Offset;
        }

        /// <summary>
        /// A point of the figure as modelled (Blender's metres) where the skin bound to the head bone carries it now: into
        /// the mesh's space (as the FBX from Blender comes in: X, Y, −Z), through the head's bind pose, out by the head as it is.
        /// </summary>
        static Vector3 OnHead(GameObject go, Animator animator, Vector3 blender)
        {
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int i = System.Array.IndexOf(skin.bones, head);
                if (i < 0 || skin.sharedMesh == null) continue;
                var mesh = new Vector3(blender.x, blender.y, -blender.z);
                return head.localToWorldMatrix.MultiplyPoint(skin.sharedMesh.bindposes[i].MultiplyPoint(mesh));
            }
            return head.position;
        }

        static Color[] Shrink(Texture2D source, int size)
        {
            var output = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                output[y * size + x] = source.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);
            return output;
        }

        /// <summary>
        /// The sides of a hand in its own frame, fingers up the picture: the back, the palm, the thumb's edge, the little
        /// finger's edge, from the fingertips down the hand, three-quarter views of the palm and of the back.
        /// </summary>
        static (string name, Vector3 from, Vector3 up)[] HandSides(Animator animator, bool left)
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
                ("back", -palm, along), ("palm", palm, along), ("thumbside", side, along), ("littleside", -side, along), ("tips", along, palm),
                ("palm34", (palm + side * 0.7f + along * 0.5f).normalized, along), ("back34", (-palm - side * 0.7f + along * 0.4f).normalized, along),
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
