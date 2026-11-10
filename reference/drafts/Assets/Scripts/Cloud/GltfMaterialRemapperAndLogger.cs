using System.Collections.Generic;
using GLTFast.Logging;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandBuilder.Cloud
{
    /// <summary>
    /// Wraps GLTFast's ConsoleLogger (ICodeLogger) to intercept and count any extension warnings or errors
    /// during runtime .glb loading on Meta Quest.
    /// </summary>
    public sealed class IslandGltfDiagnosticLogger : ICodeLogger
    {
        private readonly ConsoleLogger _innerConsoleLogger = new ConsoleLogger();

        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public string LastDiagnosticMessage { get; private set; } = string.Empty;

        public void Error(LogCode code, params string[] messages)
        {
            ErrorCount++;
            LastDiagnosticMessage = LogMessages.GetFullMessage(code, messages);
            _innerConsoleLogger.Error(code, messages);
        }

        public void Warning(LogCode code, params string[] messages)
        {
            WarningCount++;
            LastDiagnosticMessage = LogMessages.GetFullMessage(code, messages);
            _innerConsoleLogger.Warning(code, messages);
        }

        public void Info(LogCode code, params string[] messages)
        {
            _innerConsoleLogger.Info(code, messages);
        }

        public void Error(string message)
        {
            ErrorCount++;
            LastDiagnosticMessage = message;
            _innerConsoleLogger.Error(message);
        }

        public void Warning(string message)
        {
            WarningCount++;
            LastDiagnosticMessage = message;
            _innerConsoleLogger.Warning(message);
        }

        public void Info(string message)
        {
            _innerConsoleLogger.Info(message);
        }

        public void Log(LogType logType, LogCode code, params string[] messages)
        {
            if (logType == LogType.Error || logType == LogType.Exception) ErrorCount++;
            else if (logType == LogType.Warning) WarningCount++;
            _innerConsoleLogger.Log(logType, code, messages);
        }
    }

    /// <summary>
    /// Remaps glTFast runtime materials onto our Single-Pass Stereo, GPU-Instanced, AppSW-compliant
    /// IslandBuilder/GltfSinglePassLit shader while counting total triangles and submeshes.
    /// </summary>
    public static class GltfMaterialRemapper
    {
        private static Shader _cachedSinglePassShader;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int RoughnessId = Shader.PropertyToID("_Roughness");
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // glTFast property IDs
        private static readonly int GltfBaseColorTexId = Shader.PropertyToID("baseColorTexture");
        private static readonly int GltfBaseColorFactorId = Shader.PropertyToID("baseColorFactor");
        private static readonly int GltfMetallicRoughnessTexId = Shader.PropertyToID("metallicRoughnessTexture");
        private static readonly int GltfMetallicFactorId = Shader.PropertyToID("metallicFactor");
        private static readonly int GltfRoughnessFactorId = Shader.PropertyToID("roughnessFactor");
        private static readonly int GltfEmissiveTexId = Shader.PropertyToID("emissiveTexture");
        private static readonly int GltfEmissiveFactorId = Shader.PropertyToID("emissiveFactor");

        public static void RemapHierarchyMaterialsAndAuditGeometry(
            GameObject rootObject,
            List<MeshRenderer> renderersScratch,
            List<MeshFilter> filtersScratch,
            out int totalTriangles)
        {
            totalTriangles = 0;
            if (rootObject == null) return;

            if (_cachedSinglePassShader == null)
            {
                _cachedSinglePassShader = Shader.Find("IslandBuilder/GltfSinglePassLit");
            }

            filtersScratch.Clear();
            rootObject.GetComponentsInChildren(true, filtersScratch);
            for (int i = 0; i < filtersScratch.Count; i++)
            {
                Mesh m = filtersScratch[i].sharedMesh;
                if (m != null)
                {
                    totalTriangles += (int)(m.GetIndexCount(0) / 3);
                }
            }

            if (_cachedSinglePassShader == null) return;

            renderersScratch.Clear();
            rootObject.GetComponentsInChildren(true, renderersScratch);

            for (int r = 0; r < renderersScratch.Count; r++)
            {
                MeshRenderer mr = renderersScratch[r];
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;

                Material[] mats = mr.sharedMaterials;
                bool modified = false;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material srcMat = mats[m];
                    if (srcMat == null) continue;

                    // Extract textures & factors from glTFast's generated material before switching shader
                    Texture baseTex = srcMat.HasProperty(GltfBaseColorTexId)
                        ? srcMat.GetTexture(GltfBaseColorTexId)
                        : srcMat.mainTexture;

                    Color baseCol = srcMat.HasProperty(GltfBaseColorFactorId)
                        ? srcMat.GetColor(GltfBaseColorFactorId)
                        : (srcMat.HasProperty(BaseColorId) ? srcMat.GetColor(BaseColorId) : Color.white);

                    Texture mrTex = srcMat.HasProperty(GltfMetallicRoughnessTexId)
                        ? srcMat.GetTexture(GltfMetallicRoughnessTexId)
                        : null;

                    float metallic = srcMat.HasProperty(GltfMetallicFactorId) ? srcMat.GetFloat(GltfMetallicFactorId) : 0.0f;
                    float roughness = srcMat.HasProperty(GltfRoughnessFactorId) ? srcMat.GetFloat(GltfRoughnessFactorId) : 0.65f;

                    Texture emiTex = srcMat.HasProperty(GltfEmissiveTexId) ? srcMat.GetTexture(GltfEmissiveTexId) : null;
                    Color emiCol = srcMat.HasProperty(GltfEmissiveFactorId) ? srcMat.GetColor(GltfEmissiveFactorId) : Color.black;

                    srcMat.shader = _cachedSinglePassShader;
                    srcMat.enableInstancing = true;

                    if (baseTex != null) srcMat.SetTexture(BaseMapId, baseTex);
                    srcMat.SetColor(BaseColorId, baseCol);
                    if (mrTex != null) srcMat.SetTexture(MetallicGlossMapId, mrTex);
                    srcMat.SetFloat(MetallicId, metallic);
                    srcMat.SetFloat(RoughnessId, roughness);
                    if (emiTex != null) srcMat.SetTexture(EmissionMapId, emiTex);
                    srcMat.SetColor(EmissionColorId, emiCol);

                    modified = true;
                }

                if (modified)
                {
                    mr.sharedMaterials = mats;
                }
            }
        }
    }
}
