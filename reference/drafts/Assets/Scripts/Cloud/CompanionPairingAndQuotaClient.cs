using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using IslandBuilder.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace IslandBuilder.Cloud
{
    [Serializable]
    public struct SanitizedCloudAssetDescriptor
    {
        public string assetSha256Id;
        public string displayName;
        public string glbUrl;
        public long fileSizeBytes;
        public long estimatedRuntimeMemoryBytes;
        public long timestampUtc;
    }

    [Serializable]
    public struct PairingRegistrationResponse
    {
        public string pairingCode;
        public int expiresInSeconds;
    }

    public sealed class CompanionPairingAndQuotaClient : MonoBehaviour
    {
        public static CompanionPairingAndQuotaClient Instance { get; private set; }

        [Header("Cloud Companion Endpoint")]
        [SerializeField] private string cloudServerBaseUrl = "http://localhost:8080";

        private string _activePairingCode = "------";
        private float _pairingCodeExpiryRealtime;
        private long _currentAllocatedImportBytes;

        private readonly Dictionary<string, SanitizedCloudAssetDescriptor> _validatedAssets =
            new Dictionary<string, SanitizedCloudAssetDescriptor>(64);

        public string ActivePairingCode => _activePairingCode;
        public long CurrentAllocatedImportBytes => _currentAllocatedImportBytes;

        public long MaxImportMemoryBudgetBytes =>
            IslandRemediationLightingAndSaveManager.Instance != null
                ? IslandRemediationLightingAndSaveManager.Instance.MaxImportedModelsMemoryBytes
                : IslandConstants.Quest2ImportedPoolBytes;

        /// <summary>
        /// Fired when a sanitized KTX2/Draco .glb passes both the <15MB file gate and cumulative RAM budget.
        /// Phase 7's IslandGltfLoader subscribes to this event to stream the model with TimeBudgetPerFrameDeferAgent.
        /// </summary>
        public event Action<SanitizedCloudAssetDescriptor> OnSanitizedAssetApprovedForLoad;
        public event Action<string, int> OnPairingCodeRefreshed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Requests a fresh 10-minute expiring 6-digit pairing code from the Phase 6 Companion Server.
        /// </summary>
        public async Task<string> RequestNewExpiringPairingCodeAsync()
        {
            string tierName = IslandRemediationLightingAndSaveManager.Instance != null
                ? IslandRemediationLightingAndSaveManager.Instance.DetectedTier.ToString()
                : "Quest3";

            string url = $"{cloudServerBaseUrl.TrimEnd('/')}/api/pairing/register";
            string jsonBody = $"{{\"deviceTier\":\"{tierName}\"}}";

            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;

                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    await Task.Yield();
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[CompanionPairingClient] Could not reach cloud server ({req.error}).");
                    return string.Empty;
                }

                var resp = JsonUtility.FromJson<PairingRegistrationResponse>(req.downloadHandler.text);
                _activePairingCode = resp.pairingCode;
                _pairingCodeExpiryRealtime = Time.realtimeSinceStartup + resp.expiresInSeconds;

                OnPairingCodeRefreshed?.Invoke(_activePairingCode, resp.expiresInSeconds);
                Debug.Log($"<color=#00FF88>[CompanionPairingClient] Pairing Code: {_activePairingCode} (Expires in {resp.expiresInSeconds}s)</color>");
                return _activePairingCode;
            }
        }

        /// <summary>
        /// P1 #10 Memory Gate Validator: Verifies that a cloud-transcoded .glb satisfies both:
        ///   1. The < 15 MB per-file Phase 6 Gate
        ///   2. The cumulative 128 MB (Quest 2) / 256 MB (Quest 3/3S) Imported Model Memory Pool
        /// </summary>
        public bool TryApproveSanitizedAsset(SanitizedCloudAssetDescriptor descriptor, out string rejectionReason)
        {
            if (descriptor.fileSizeBytes <= 0 || descriptor.fileSizeBytes > IslandConstants.MaxSingleGlbBytes)
            {
                rejectionReason = $"File size ({descriptor.fileSizeBytes / (1024 * 1024)} MB) exceeds the 15 MB per-GLB hardware gate.";
                Debug.LogError($"[CompanionQuota FAIL] {rejectionReason}");
                return false;
            }

            // If this SHA-256 asset is already loaded in memory, reuse its instanced mesh at 0 extra texture RAM cost!
            if (_validatedAssets.ContainsKey(descriptor.assetSha256Id))
            {
                rejectionReason = string.Empty;
                OnSanitizedAssetApprovedForLoad?.Invoke(descriptor);
                return true;
            }

            long estimatedBytes = Math.Max(descriptor.fileSizeBytes * 2L, descriptor.estimatedRuntimeMemoryBytes);
            if (_currentAllocatedImportBytes + estimatedBytes > MaxImportMemoryBudgetBytes)
            {
                rejectionReason =
                    $"Cumulative Imported Model Memory Cap reached ({_currentAllocatedImportBytes / (1024 * 1024)} MB / " +
                    $"{MaxImportMemoryBudgetBytes / (1024 * 1024)} MB). Delete unused custom models before importing more.";
                Debug.LogWarning($"[CompanionQuota BLOCKED] {rejectionReason}");
                return false;
            }

            _currentAllocatedImportBytes += estimatedBytes;
            _validatedAssets[descriptor.assetSha256Id] = descriptor;
            rejectionReason = string.Empty;

            Debug.Log($"<color=#00FF88>[CompanionQuota PASS] Approved '{descriptor.displayName}' ({descriptor.fileSizeBytes / 1024} KB). " +
                      $"Pool Usage: {_currentAllocatedImportBytes / (1024 * 1024)} / {MaxImportMemoryBudgetBytes / (1024 * 1024)} MB.</color>");

            OnSanitizedAssetApprovedForLoad?.Invoke(descriptor);
            return true;
        }

        public void ReleaseImportedAssetMemory(string assetSha256Id)
        {
            if (_validatedAssets.TryGetValue(assetSha256Id, out SanitizedCloudAssetDescriptor desc))
            {
                long estimatedBytes = Math.Max(desc.fileSizeBytes * 2L, desc.estimatedRuntimeMemoryBytes);
                _currentAllocatedImportBytes = Math.Max(0L, _currentAllocatedImportBytes - estimatedBytes);
                _validatedAssets.Remove(assetSha256Id);
            }
        }
    }
}
