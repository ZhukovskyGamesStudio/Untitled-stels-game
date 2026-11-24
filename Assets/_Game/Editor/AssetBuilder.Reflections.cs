#if URP_SCREEN_SPACE_REFLECTION
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Stealth.EditorTools
{
    /// <summary>
    /// Screen-space reflections (URP's own, compiled in by the URP_SCREEN_SPACE_REFLECTION define of the player settings):
    /// shiny and metal things mirror what is on the screen, so a dark room stays dark — there is nothing lit to mirror, and
    /// the sky and the reflection probes stay off (LightMath, RenderSettings.reflectionIntensity = 0).
    /// </summary>
    public static partial class AssetBuilder
    {
        /// <summary>The feature on the PC renderer, once.</summary>
        static void AddReflectionFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PcRendererPath);
            if (data == null || data.rendererFeatures.Any(f => f is ScreenSpaceReflectionRendererFeature)) return;
            var feature = ScriptableObject.CreateInstance<ScreenSpaceReflectionRendererFeature>();
            feature.name = "ScreenSpaceReflection";
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
            var so = new SerializedObject(data);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
            so.ApplyModifiedProperties();
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }
    }
}
#endif
