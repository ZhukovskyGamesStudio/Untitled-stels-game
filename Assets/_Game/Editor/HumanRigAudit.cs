using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Stealth.EditorTools
{
    /// <summary>
    /// How far the takes the game plays are from the takes that were authored (Tools/Blender/humanlib/anim.py): the shared
    /// file imported once more as it is (generic: the bones' own curves — the truth) next to every person playing the
    /// game's clips, bone by bone → Logs/human_audit.txt, and pictures of both side by side (the truth on the left) from
    /// the front and from the side → Logs/poses/_audit; it also writes the authored takes as numbers for the test
    /// (Tests/EditMode/HumanTakesReference.csv: run it after the takes are exported anew). Menu Stealth → Human Rig Audit, or in batch (with graphics):
    /// <c>-executeMethod Stealth.EditorTools.HumanRigAudit.Batch</c>.
    /// </summary>
    public static class HumanRigAudit
    {
        const string Source = "Assets/_Game/Art/Models/Humans/human_backend.fbx";
        const string TempDir = "Assets/_HumanAudit";
        const string Truth = TempDir + "/human_truth.fbx";
        static string s_Game;
        /// <summary>The authored takes as numbers (under Assets): the test's reference.</summary>
        public const string ReferenceFile = "_Game/Tests/EditMode/HumanTakesReference.csv";
        static readonly string[] People = { "backend", "qa", "sysadmin", "designer" };
        static readonly float[] Moments = { 0.05f, 0.3f, 0.55f, 0.8f };

        [MenuItem("Stealth/Human Rig Audit")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(TempDir)) AssetDatabase.CreateFolder("Assets", "_HumanAudit");
            AssetDatabase.DeleteAsset(Truth);
            AssetDatabase.CopyAsset(Source, Truth);
            var imp = (ModelImporter)AssetImporter.GetAtPath(Truth);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.motionNodeName = "";
            imp.SaveAndReimport();
            imp.clipAnimations = imp.defaultClipAnimations.Select(c =>
            {
                c.name = c.takeName.Contains('|') ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                c.loopTime = true;
                return c;
            }).ToArray();
            imp.SaveAndReimport();

            // HUMAN_AUDIT_TRY=1: the game's side is a fresh copy configured by the builder's code as it is now
            s_Game = Source;
            if (System.Environment.GetEnvironmentVariable("HUMAN_AUDIT_TRY") == "1")
            {
                s_Game = TempDir + "/human_try.fbx";
                AssetDatabase.CopyAsset(Source, s_Game);
                AssetBuilder.ConfigureHumanImport((ModelImporter)AssetImporter.GetAtPath(s_Game), force: true);
            }
            try
            {
                Compare();
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempDir);
            }
        }

        static AnimationClip[] Clips(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).OrderBy(c => c.name).ToArray();

        static void Compare()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40f, 200f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.22f, 0.25f);
            cam.fieldOfView = 30f;
            const int W = 960, H = 860;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            string pictures = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "poses", "_audit");
            if (Directory.Exists(pictures)) Directory.Delete(pictures, true);
            Directory.CreateDirectory(pictures);

            var truthAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Truth);
            var truthClips = Clips(Truth);
            var gameClips = Clips(s_Game);
            var log = new StringBuilder();
            log.AppendLine("clip | person | worst bone: position cm (relative to the model), turn deg | hips cm | then every bone over 2 cm or 12 deg");

            // the authored takes as numbers, for the test that holds the game's clips to them (HumanRigTests): every bone's
            // place and turn in the model's space at a few moments of every take
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var reference = new StringBuilder("# written by Stealth → Human Rig Audit (Tools/human_audit.sh): the authored takes; clip,moment,bone,px,py,pz,qx,qy,qz,qw\n");
                var model = Object.Instantiate(truthAsset);
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var bones = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.bones).Where(b => b != null).Distinct().OrderBy(b => b.name).ToArray();
                foreach (var clip in truthClips)
                    foreach (float moment in Moments)
                    {
                        clip.SampleAnimation(model, moment * clip.length);
                        foreach (var b in bones)
                        {
                            var at = model.transform.InverseTransformPoint(b.position);
                            var turn = Quaternion.Inverse(model.transform.rotation) * b.rotation;
                            reference.Append(string.Format(inv, "{0},{1:0.###},{2},{3:0.#####},{4:0.#####},{5:0.#####},{6:0.#####},{7:0.#####},{8:0.#####},{9:0.#####}\n",
                                clip.name, moment, b.name, at.x, at.y, at.z, turn.x, turn.y, turn.z, turn.w));
                        }
                    }
                Object.DestroyImmediate(model);
                File.WriteAllText(Path.Combine(Application.dataPath, ReferenceFile), reference.ToString());
            }
            // the rest pose's hands close up (the figure faces -Z in the file): from its front and from its outer side
            {
                var rest = Object.Instantiate(truthAsset);
                rest.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                AssetBuilder.ToBindPose(rest);
                foreach (var r in rest.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true; // (their bounds are the import pose's)
                foreach (var hand in rest.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("hand.")))
                {
                    var at = hand.position + Vector3.down * 0.08f;
                    foreach (var (view, from) in new[] { ("front", new Vector3(0f, 0.05f, -0.9f)), ("outside", new Vector3(Mathf.Sign(hand.position.x) * 0.9f, 0.05f, 0f)), ("whole", new Vector3(0f, 0.9f, -4.2f)) })
                    {
                        cam.transform.position = at + from;
                        cam.transform.LookAt(at);
                        cam.Render();
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                        tex.Apply();
                        RenderTexture.active = null;
                        File.WriteAllBytes(Path.Combine(pictures, $"_rest_{hand.name}_{view}.png"), tex.EncodeToPNG());
                    }
                }
                Object.DestroyImmediate(rest);
            }
            foreach (var who in People)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(s_Game != Source ? s_Game : $"Assets/_Game/Art/Models/Humans/human_{who}.fbx");
                if (s_Game != Source && who != People[0]) break;
                if (asset == null) continue;
                foreach (var truthClip in truthClips)
                {
                    var gameClip = gameClips.FirstOrDefault(c => c.name == truthClip.name);
                    if (gameClip == null) { log.AppendLine($"{truthClip.name} | {who} | NO GAME CLIP"); continue; }
                    float worstCm = 0f, worstDeg = 0f, hipsCm = 0f;
                    string worstBone = "", worstTurn = "";
                    var perBone = new System.Collections.Generic.Dictionary<string, (float cm, float deg)>();
                    for (int m = 0; m < Moments.Length; m++)
                    {
                        float t = Moments[m] * truthClip.length;
                        var a = Object.Instantiate(truthAsset);
                        var b = Object.Instantiate(asset);
                        a.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        b.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        truthClip.SampleAnimation(a, t);
                        gameClip.SampleAnimation(b, t);
                        var bonesA = a.GetComponentsInChildren<Transform>(true).ToDictionary(x => x.name, x => x);
                        foreach (var tb in b.GetComponentsInChildren<Transform>(true))
                        {
                            if (!bonesA.TryGetValue(tb.name, out var ta) || tb == b.transform) continue;
                            if (tb.GetComponent<Renderer>() != null) continue;
                            float cm = Vector3.Distance(a.transform.InverseTransformPoint(ta.position), b.transform.InverseTransformPoint(tb.position)) * 100f;
                            float deg = Quaternion.Angle(Quaternion.Inverse(a.transform.rotation) * ta.rotation, Quaternion.Inverse(b.transform.rotation) * tb.rotation);
                            if (tb.name == "hips") hipsCm = Mathf.Max(hipsCm, cm);
                            if (cm > worstCm) { worstCm = cm; worstBone = tb.name; }
                            if (deg > worstDeg) { worstDeg = deg; worstTurn = tb.name; }
                            perBone.TryGetValue(tb.name, out var was);
                            perBone[tb.name] = (Mathf.Max(was.cm, cm), Mathf.Max(was.deg, deg));
                        }
                        // pictures: the truth on the left, the game's on the right — the first person only
                        if (who == People[0] && (m == 1 || m == 3))
                        {
                            a.transform.position = new Vector3(-0.55f, 0f, 0f);
                            b.transform.position = new Vector3(0.55f, 0f, 0f);
                            // (the import turns the figure's front to -Z)
                            bool low = truthClip.name == "Down";
                            var look = new Vector3(0f, low ? 0.3f : 0.9f, 0f);
                            foreach (var (view, from) in new[] { ("front", new Vector3(0f, 0.3f, -4.4f)), ("side", new Vector3(-4.4f, 0.3f, -0.6f)), ("top", new Vector3(0f, 4.6f, -0.4f)) })
                            {
                                if (view == "side") { a.transform.position = new Vector3(0f, 0f, 0.8f); b.transform.position = new Vector3(0f, 0f, -0.8f); }
                                else { a.transform.position = new Vector3(-0.55f, 0f, 0f); b.transform.position = new Vector3(0.55f, 0f, 0f); }
                                if (view == "top" && !low) continue;
                                cam.transform.position = look + from;
                                cam.transform.LookAt(look);
                                cam.Render();
                                RenderTexture.active = rt;
                                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                                tex.Apply();
                                RenderTexture.active = null;
                                File.WriteAllBytes(Path.Combine(pictures, $"{truthClip.name}_{m}_{view}.png"), tex.EncodeToPNG());
                            }
                        }
                        Object.DestroyImmediate(a);
                        Object.DestroyImmediate(b);
                    }
                    log.Append($"{truthClip.name,-11} | {who,-8} | {worstBone} {worstCm:0.0} cm, {worstTurn} {worstDeg:0} deg | hips {hipsCm:0.0} cm |");
                    foreach (var kv in perBone.Where(k => k.Value.cm > 1.5f || k.Value.deg > 4f).OrderByDescending(k => k.Value.cm))
                        log.Append($" {kv.Key} {kv.Value.cm:0.0}/{kv.Value.deg:0}");
                    log.AppendLine();
                }
            }
            string file = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "human_audit.txt");
            File.WriteAllText(file, log.ToString());
            Debug.Log($"[HumanAudit] {file}\n{log}");
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(floor);
        }

        /// <summary>Batch entry: the audit, then quit.</summary>
        public static void Batch()
        {
            int code = 0;
            try { Run(); }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
