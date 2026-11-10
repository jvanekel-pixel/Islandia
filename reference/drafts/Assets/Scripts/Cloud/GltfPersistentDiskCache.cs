using System;
using System.IO;
using System.Threading.Tasks;
using IslandBuilder.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace IslandBuilder.Cloud
{
    public sealed class GltfPersistentDiskCache : MonoBehaviour
    {
        public static GltfPersistentDiskCache Instance { get; private set; }

        private const string CacheSubfolder = "LocalCache/GLB";
        private string _cacheRootDirectory;

        public string CacheRootDirectory => _cacheRootDirectory;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _cacheRootDirectory = Path.Combine(Application.persistentDataPath, CacheSubfolder);
            if (!Directory.Exists(_cacheRootDirectory))
            {
                Directory.CreateDirectory(_cacheRootDirectory);
            }
        }

        public string GetCachedGlbFilePath(string assetSha256Id)
        {
            string safeId = SanitizeId(assetSha256Id);
            return Path.Combine(_cacheRootDirectory, $"{safeId}.glb");
        }

        public bool TryGetCachedFilePath(string assetSha256Id, out string fullPath, out long fileSizeBytes)
        {
            fullPath = GetCachedGlbFilePath(assetSha256Id);
            if (File.Exists(fullPath))
            {
                var info = new FileInfo(fullPath);
                if (info.Length > 0 && info.Length <= IslandConstants.MaxSingleGlbBytes)
                {
                    fileSizeBytes = info.Length;
                    return true;
                }
            }

            fileSizeBytes = 0L;
            return false;
        }

        /// <summary>
        /// Downloads a sanitized .glb from the Phase 6 Companion Server directly to a temporary disk file
        /// using DownloadHandlerFile (zero managed byte[] heap spike!) and atomically renames it into the cache.
        /// </summary>
        public async Task<(bool success, string localFilePath)> DownloadAndCacheGlbAsync(
            string assetSha256Id,
            string fullDownloadUrl)
        {
            if (TryGetCachedFilePath(assetSha256Id, out string existingPath, out _))
            {
                return (true, existingPath);
            }

            string finalPath = GetCachedGlbFilePath(assetSha256Id);
            string tempPath = $"{finalPath}.downloading";

            try
            {
                using (var req = UnityWebRequest.Get(fullDownloadUrl))
                {
                    // DownloadHandlerFile streams directly to Android flash storage without allocating a 15 MB byte[] in RAM!
                    var fileHandler = new DownloadHandlerFile(tempPath)
                    {
                        removeFileOnAbort = true
                    };
                    req.downloadHandler = fileHandler;
                    req.timeout = 45;

                    var op = req.SendWebRequest();
                    while (!op.isDone)
                    {
                        // Enforce < 15 MB gate mid-download to abort oversized streams immediately
                        if (req.downloadedBytes > (ulong)IslandConstants.MaxSingleGlbBytes)
                        {
                            req.Abort();
                            throw new InvalidOperationException("Aborted download: GLB stream exceeded 15 MB hardware gate.");
                        }
                        await Task.Yield();
                    }

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        throw new IOException($"Network error downloading GLB: {req.error}");
                    }
                }

                var fileInfo = new FileInfo(tempPath);
                if (!fileInfo.Exists || fileInfo.Length == 0 || fileInfo.Length > IslandConstants.MaxSingleGlbBytes)
                {
                    if (fileInfo.Exists) fileInfo.Delete();
                    throw new InvalidOperationException("Downloaded GLB failed post-download size validation.");
                }

                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }
                File.Move(tempPath, finalPath);

                return (true, finalPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GltfPersistentDiskCache] Failed to cache asset '{assetSha256Id}': {ex.Message}");
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                return (false, string.Empty);
            }
        }

        public void EvictCachedAsset(string assetSha256Id)
        {
            string path = GetCachedGlbFilePath(assetSha256Id);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string SanitizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "unknown_asset";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                id = id.Replace(c, '_');
            }
            return id;
        }
    }
}
