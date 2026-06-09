using UnityEngine;
using UnityEngine.Rendering;
using System.Text;

public class QualityDebugger : MonoBehaviour
{
    void Start()
    {
        int qualityIndex = QualitySettings.GetQualityLevel();
        string qualityName = QualitySettings.names[qualityIndex];

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("===== QUALITY INFO =====");
        sb.AppendLine($"Quality Level: {qualityName} (Index {qualityIndex})");
        sb.AppendLine($"VSync Count: {QualitySettings.vSyncCount}");
        sb.AppendLine($"Anti Aliasing (MSAA): {QualitySettings.antiAliasing}x");
        sb.AppendLine($"Shadow Quality: {QualitySettings.shadows}");
        sb.AppendLine($"Shadow Resolution: {QualitySettings.shadowResolution}");
        sb.AppendLine($"Shadow Distance: {QualitySettings.shadowDistance}");
        sb.AppendLine($"LOD Bias: {QualitySettings.lodBias}");
        sb.AppendLine($"Anisotropic Filtering: {QualitySettings.anisotropicFiltering}");
        sb.AppendLine($"Texture Quality (Mipmap Limit): {QualitySettings.globalTextureMipmapLimit}");
        sb.AppendLine($"Realtime Reflection Probes: {QualitySettings.realtimeReflectionProbes}");
        sb.AppendLine($"Soft Particles: {QualitySettings.softParticles}");
        sb.AppendLine($"Skin Weights: {QualitySettings.skinWeights}");

        // Active Render Pipeline
        RenderPipelineAsset activePipeline = GraphicsSettings.currentRenderPipeline;
        sb.AppendLine($"Active Render Pipeline: {(activePipeline ? activePipeline.name : "Built-in")}");

        // Pipeline assigned to this quality level
        RenderPipelineAsset levelPipeline = QualitySettings.GetRenderPipelineAssetAt(qualityIndex);
        sb.AppendLine($"Pipeline Assigned To This Quality Level: {(levelPipeline ? levelPipeline.name : "Built-in")}");

        sb.AppendLine("========================");

        Debug.Log(sb.ToString());
    }
}