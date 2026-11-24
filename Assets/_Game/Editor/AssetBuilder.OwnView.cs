using System.Collections.Generic;
using System.Linq;
using Stealth.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.EditorTools
{
    /// <summary>
    /// What a player sees of himself, made from his own person (<see cref="HumanRig"/>): the skin without the head and the
    /// neck (from inside his own head only torn edges of the face were seen, and down the empty collar), and the right arm
    /// posed round a held thing — a still copy of it, sleeve and all, that the first-person view shows in place of the old
    /// clay mitten. Meshes in Art/Models/Humans/View, the arms' prefabs in Prefabs/Player.
    /// </summary>
    public static partial class AssetBuilder
    {
        const string OwnViewDir = ModelsDir + "/Humans/View";

        /// <summary>The bones of the right arm below the shoulder, by the skeleton's names.</summary>
        static bool RightArmBone(string bone) =>
            bone is "upper_arm.R" or "forearm.R" or "hand.R" || HumanSkeleton.Fingers.Any(f => bone.StartsWith(f + ".") && bone.EndsWith(".R"));

        /// <summary>The share of each vertex that hangs on the bones <paramref name="pick"/> names.</summary>
        static float[] WeightOn(SkinnedMeshRenderer r, System.Func<string, bool> pick)
        {
            var on = r.bones.Select(b => b != null && pick(b.name)).ToArray();
            return r.sharedMesh.boneWeights.Select(w =>
            {
                float s = 0f;
                if (w.weight0 > 0f && on[w.boneIndex0]) s += w.weight0;
                if (w.weight1 > 0f && on[w.boneIndex1]) s += w.weight1;
                if (w.weight2 > 0f && on[w.boneIndex2]) s += w.weight2;
                if (w.weight3 > 0f && on[w.boneIndex3]) s += w.weight3;
                return s;
            }).ToArray();
        }

        static Mesh MeshAsset(string name, Mesh fresh)
        {
            EnsureFolder(OwnViewDir);
            string path = $"{OwnViewDir}/{name}.asset";
            fresh.name = name;
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old == null) { AssetDatabase.CreateAsset(fresh, path); return fresh; }
            EditorUtility.CopySerialized(fresh, old); // (the same asset: the prefabs keep their reference)
            old.name = name;
            EditorUtility.SetDirty(old);
            Object.DestroyImmediate(fresh);
            return old;
        }

        /// <summary>Fills in <see cref="HumanRig.Skin"/>, <see cref="HumanRig.OwnSkin"/> and the arms of the view for a rig being built.</summary>
        static void AddOwnView(HumanRig rig, string who)
        {
            var skin = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "AccentHead");
            if (skin == null || !skin.sharedMesh.isReadable) { Debug.LogWarning($"[Builder] human_{who}: no readable AccentHead, no own view"); return; }
            // the arms first: they are posed on a copy of the rig as it is
            rig.ViewFist = BakeViewArm(rig, who, cupped: false);
            rig.ViewCup = BakeViewArm(rig, who, cupped: true);
            // the skin without its head and neck: a second renderer on the same bones, off until the owner looks out of it
            var head = WeightOn(skin, b => b is "head" or "neck");
            var mesh = Object.Instantiate(skin.sharedMesh);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                var kept = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                    if (head[tris[i]] < 0.05f && head[tris[i + 1]] < 0.05f && head[tris[i + 2]] < 0.05f) kept.AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
                mesh.SetTriangles(kept, s);
            }
            var own = Object.Instantiate(skin, skin.transform.parent);
            own.name = "AccentHeadOwn";
            own.sharedMesh = MeshAsset($"own_{who}_skin", mesh);
            own.shadowCastingMode = ShadowCastingMode.Off;
            own.enabled = false;
            rig.Skin = skin;
            rig.OwnSkin = own;
        }

        /// <summary>
        /// The right arm of this person holding a thing the way other players see it carried (the Carry pose, the fist or the
        /// cupped palm), as a still prefab whose origin is where the thing's anchor is (<see cref="HumanRig.HeldSpot"/>, +Z ahead).
        /// </summary>
        static GameObject BakeViewArm(HumanRig source, string who, bool cupped)
        {
            string name = $"ViewArm_{who}_{(cupped ? "cup" : "fist")}";
            return Prefab($"{PrefabsDir}/Player/{name}.prefab", () =>
            {
                var go = Object.Instantiate(source.gameObject);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var rig = go.GetComponent<HumanRig>();
                rig.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                rig.Animator.Rebind();
                rig.Cupped = cupped;
                rig.Pose(0f, false, false, 0f, holding: true);
                rig.Settle();
                for (float t = 0f; t < 1f; t += 1f / 30f) rig.Animator.Update(1f / 30f);
                var toAnchor = Matrix4x4.TRS(rig.HeldSpot(), Quaternion.identity, Vector3.one).inverse;
                var arm = new GameObject(name);
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!r.enabled || r.sharedMesh == null || !r.sharedMesh.isReadable) continue;
                    var weight = WeightOn(r, RightArmBone);
                    if (!weight.Any(w => w > 0.5f)) continue;
                    var posed = new Mesh();
                    r.BakeMesh(posed, true);
                    var m = toAnchor * r.transform.localToWorldMatrix;
                    var v = posed.vertices;
                    var n = posed.normals;
                    var tan = posed.tangents;
                    var uv = r.sharedMesh.uv;
                    var index = new Dictionary<int, int>();
                    var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nu = new List<Vector2>(); var tris = new List<int>();
                    int Vert(int i)
                    {
                        if (index.TryGetValue(i, out int k)) return k;
                        k = nv.Count;
                        index[i] = k;
                        nv.Add(m.MultiplyPoint3x4(v[i]));
                        nn.Add(m.MultiplyVector(n[i]).normalized);
                        var t = m.MultiplyVector(tan[i]).normalized;
                        nt.Add(new Vector4(t.x, t.y, t.z, tan[i].w));
                        nu.Add(uv[i]);
                        return k;
                    }
                    var all = r.sharedMesh.triangles;
                    for (int i = 0; i < all.Length; i += 3)
                        if (weight[all[i]] > 0.5f && weight[all[i + 1]] > 0.5f && weight[all[i + 2]] > 0.5f)
                            for (int c = 0; c < 3; c++) tris.Add(Vert(all[i + c]));
                    Object.DestroyImmediate(posed);
                    if (tris.Count == 0) continue;
                    var mesh = new Mesh();
                    mesh.SetVertices(nv);
                    mesh.SetNormals(nn);
                    mesh.SetTangents(nt);
                    mesh.SetUVs(0, nu);
                    mesh.SetTriangles(tris, 0);
                    mesh.RecalculateBounds();
                    var part = new GameObject(r.name); // (the part's name: PlayerLooks paints it like the body's)
                    part.transform.SetParent(arm.transform, false);
                    part.layer = PlayerController.OwnBodyLayer;
                    part.AddComponent<MeshFilter>().sharedMesh = MeshAsset($"{name}_{r.name}", mesh);
                    var mr = part.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = r.sharedMaterial;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
                Object.DestroyImmediate(go);
                return arm;
            });
        }
    }
}
