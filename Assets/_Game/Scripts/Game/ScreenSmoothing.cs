using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Stealth.Game
{
    /// <summary>
    /// Applies the player's choice of smoothing and vertical sync (<see cref="GameSettings"/>, rules —
    /// <see cref="SmoothingRules"/>) to every game camera right before it renders: whichever camera is on — the menu's, the
    /// player's own — follows the setting at once, with nothing to wire in the prefabs. Picture only.
    /// </summary>
    public static class ScreenSmoothing
    {
        static readonly int FinalPassOffId = Shader.PropertyToID("_FxaaOff"), ShapesOffId = Shader.PropertyToID("_ShapesOff");

        static int s_Sync = -1;
        static RenderObjects s_WorldText;
        static bool s_Looked;

        /// <summary>The pass that draws world-space text after the style's filter (the renderer's "WorldText" feature).</summary>
        static RenderObjects WorldText()
        {
            if (s_Looked) return s_WorldText;
            s_Looked = true;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)
                foreach (var data in asset.rendererDataList)
                {
                    if (data == null) continue;
                    foreach (var f in data.rendererFeatures)
                        if (f is RenderObjects objects && f.name == "WorldText") s_WorldText = objects;
                }
            return s_WorldText;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            s_Sync = -1;
            s_Looked = false;
            s_WorldText = null;
            RenderPipelineManager.beginContextRendering -= Apply;
            RenderPipelineManager.beginContextRendering += Apply;
        }

        /// <summary>The style on screen lets the frames be blended before its filter (<see cref="SmoothingRules"/>).</summary>
        public static bool TemporalFits(StyleScreen screen) =>
            screen == null || (screen.OutlineStrength <= 0f && screen.HatchStrength <= 0f && screen.PixelSize <= 1.01f && !screen.OneBit);

        static void Apply(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!Application.isPlaying) return;
            GameSettings.EnsureLoaded();
            // the look the player chose: the light of the style before or after its rework, the models' own maps on or off
            VisualStyles.OldLook = !GameSettings.NewLighting;
            Shader.SetGlobalFloat(ShapesOffId, GameSettings.NewTextures ? 0f : 1f);
            ShapeLibrary.Detail = GameSettings.NewTextures;
            // the texture size: every texture's top levels skipped, the models' baked maps packed from the same level
            int skip = TextureSizeRules.Skip(GameSettings.TextureSize);
            if (QualitySettings.globalTextureMipmapLimit != skip) QualitySettings.globalTextureMipmapLimit = skip;
            ShapeLibrary.Skip = skip;
            var style = VisualStyles.Current;
            bool fits = TemporalFits(style != null ? style.Screen : null);
            var choice = GameSettings.Smoothing;
            var mode = SmoothingRules.Camera(choice, fits);
            Shader.SetGlobalFloat(FinalPassOffId, SmoothingRules.FinalPass(choice, fits) ? 0f : 1f);
            var tuning = Tuning.Player.Picture;
            // Text drawn after post-processing would be drawn with the frame's sub-pixel shake and nothing to blend it
            // away: under the temporal filter its pass gets the camera's own projection, without the shake.
            var text = WorldText();
            if (text != null) text.settings.cameraSettings.overrideCamera = false;
            foreach (var cam in cameras)
            {
                if (cam == null || cam.cameraType != CameraType.Game || cam.targetTexture != null) continue;
                if (!cam.TryGetComponent<UniversalAdditionalCameraData>(out var data) || data.renderType != CameraRenderType.Base) continue;
                var wanted = mode switch
                {
                    CameraSmoothing.Temporal => AntialiasingMode.TemporalAntiAliasing,
                    CameraSmoothing.Smaa => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                    _ => AntialiasingMode.None,
                };
                if (data.antialiasing != wanted) data.antialiasing = wanted;
                if (mode == CameraSmoothing.Smaa) data.antialiasingQuality = AntialiasingQuality.High;
                if (mode != CameraSmoothing.Temporal) continue;
                if (text != null)
                {
                    var steady = text.settings.cameraSettings;
                    steady.overrideCamera = true;
                    steady.restoreCamera = true;
                    steady.offset = Vector4.zero;
                    steady.cameraFieldOfView = cam.fieldOfView;
                }
                ref var taa = ref data.taaSettings;
                taa.quality = (TemporalAAQuality)tuning.Quality;
                taa.baseBlendFactor = tuning.History;
                taa.varianceClampScale = tuning.Clamp;
                taa.contrastAdaptiveSharpening = tuning.Sharpen;
                taa.mipBias = tuning.MipBias;
            }

            // an autopilot's window runs as fast as it can, whatever the player chose
            int sync = GameSettings.VSync && !DevFlags.SkipPicking ? 1 : 0;
            if (sync == s_Sync) return;
            s_Sync = sync;
            QualitySettings.vSyncCount = sync;
            Application.targetFrameRate = sync == 1 ? -1 : SmoothingRules.UnsyncedRate;
        }
    }
}
