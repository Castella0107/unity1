using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

// Loads jacket images from StreamingAssets/Songs/{songId}/ with an LRU cache.
// Intended for Windows Standalone; uses direct File I/O (not UnityWebRequest).
public class JacketLoader
{
    readonly Dictionary<string, Texture2D> _cache    = new Dictionary<string, Texture2D>();
    readonly LinkedList<string>            _lruOrder = new LinkedList<string>();
    const int MAX_CACHE = 10;

    static readonly string[] Extensions = { "jacket.png", "jacket.jpg", "jacket.jpeg" };

    /// Load jacket for songId. Returns null if not found or on error.
    public async Task<Texture2D> LoadAsync(string songId)
    {
        if (string.IsNullOrEmpty(songId)) return null;

        // Cache hit: refresh LRU and return
        if (_cache.TryGetValue(songId, out var cached))
        {
            _lruOrder.Remove(songId);
            _lruOrder.AddLast(songId);
            return cached;
        }

        string path = FindJacketPath(songId);
        if (path == null) return null;

        try
        {
            byte[] bytes = await Task.Run(() => File.ReadAllBytes(path));

            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(bytes);
            tex.filterMode = FilterMode.Bilinear;

            _cache[songId] = tex;
            _lruOrder.AddLast(songId);
            EnforceCacheLimit();

            return tex;
        }
        catch (Exception e)
        {
            Debug.LogError("[JacketLoader] Load failed for " + songId + ": " + e.Message);
            return null;
        }
    }

    static string FindJacketPath(string songId)
    {
        var root = Path.Combine(Application.streamingAssetsPath, "Songs", songId);
        if (!Directory.Exists(root)) return null;

        foreach (var ext in Extensions)
        {
            var path = Path.Combine(root, ext);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    void EnforceCacheLimit()
    {
        while (_lruOrder.Count > MAX_CACHE)
        {
            string oldest = _lruOrder.First.Value;
            _lruOrder.RemoveFirst();
            if (_cache.TryGetValue(oldest, out var tex))
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
                _cache.Remove(oldest);
            }
        }
    }

    public void ClearCache()
    {
        foreach (var tex in _cache.Values)
            if (tex != null) UnityEngine.Object.Destroy(tex);
        _cache.Clear();
        _lruOrder.Clear();
    }
}
