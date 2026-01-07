using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace Godot.InAppPurchases.Core;

/// <summary>
/// Local cache for owned products.
/// Used for offline access and debugging, but platform provider is always source of truth.
/// </summary>
public static class IAPCache
{
    private const string CacheFileName = "user://iap_cache.json";

    private static List<OwnedProduct> _cachedProducts = new();
    private static DateTime? _cacheTimestamp;
    private static bool _loaded;

    /// <summary>
    /// Saves owned products to the cache.
    /// </summary>
    public static void SaveCache(IEnumerable<OwnedProduct> products, DateTime? timestamp = null)
    {
        _cachedProducts = new List<OwnedProduct>(products);
        _cacheTimestamp = timestamp ?? DateTime.UtcNow;

        try
        {
            var cacheData = new CacheData
            {
                Products = _cachedProducts,
                Timestamp = _cacheTimestamp.Value
            };

            var json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            using var file = FileAccess.Open(CacheFileName, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(json);
                IAPLogger.Debug($"Cache saved with {_cachedProducts.Count} products");
            }
            else
            {
                IAPLogger.Warning($"Could not open cache file for writing: {FileAccess.GetOpenError()}");
            }
        }
        catch (Exception ex)
        {
            IAPLogger.Error($"Failed to save cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads owned products from the cache.
    /// </summary>
    public static List<OwnedProduct> LoadCache()
    {
        if (_loaded)
        {
            return new List<OwnedProduct>(_cachedProducts);
        }

        _cachedProducts = new List<OwnedProduct>();
        _cacheTimestamp = null;

        try
        {
            if (!FileAccess.FileExists(CacheFileName))
            {
                _loaded = true;
                return _cachedProducts;
            }

            using var file = FileAccess.Open(CacheFileName, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                IAPLogger.Warning($"Could not open cache file for reading: {FileAccess.GetOpenError()}");
                _loaded = true;
                return _cachedProducts;
            }

            var json = file.GetAsText();
            var cacheData = JsonSerializer.Deserialize<CacheData>(json);

            if (cacheData != null)
            {
                _cachedProducts = cacheData.Products ?? new List<OwnedProduct>();
                _cacheTimestamp = cacheData.Timestamp;
                IAPLogger.Debug($"Cache loaded with {_cachedProducts.Count} products");
            }
        }
        catch (Exception ex)
        {
            IAPLogger.Error($"Failed to load cache: {ex.Message}");
            _cachedProducts = new List<OwnedProduct>();
            _cacheTimestamp = null;
        }

        _loaded = true;
        return new List<OwnedProduct>(_cachedProducts);
    }

    /// <summary>
    /// Gets the timestamp of when the cache was last saved.
    /// </summary>
    public static DateTime? GetCacheTimestamp()
    {
        if (!_loaded)
        {
            LoadCache();
        }
        return _cacheTimestamp;
    }

    /// <summary>
    /// Checks if the cache is still valid based on settings.
    /// </summary>
    public static bool IsCacheValid()
    {
        if (!_loaded)
        {
            LoadCache();
        }

        if (_cacheTimestamp == null)
        {
            return false;
        }

        var validitySeconds = IAPSettings.GetCacheValiditySeconds();
        if (validitySeconds <= 0)
        {
            // 0 means always stale
            return false;
        }

        var age = DateTime.UtcNow - _cacheTimestamp.Value;
        return age.TotalSeconds < validitySeconds;
    }

    /// <summary>
    /// Checks if a product is in the cache.
    /// </summary>
    public static bool IsProductCached(string productId)
    {
        if (!_loaded)
        {
            LoadCache();
        }
        return _cachedProducts.Exists(p => p.ProductId == productId);
    }

    /// <summary>
    /// Gets a cached product by ID.
    /// </summary>
    public static OwnedProduct? GetCachedProduct(string productId)
    {
        if (!_loaded)
        {
            LoadCache();
        }
        return _cachedProducts.Find(p => p.ProductId == productId);
    }

    /// <summary>
    /// Adds or updates a product in the cache.
    /// </summary>
    public static void AddOrUpdateProduct(OwnedProduct product)
    {
        if (!_loaded)
        {
            LoadCache();
        }

        var existing = _cachedProducts.FindIndex(p => p.ProductId == product.ProductId);
        if (existing >= 0)
        {
            _cachedProducts[existing] = product;
        }
        else
        {
            _cachedProducts.Add(product);
        }

        SaveCache(_cachedProducts, DateTime.UtcNow);
    }

    /// <summary>
    /// Removes a product from the cache.
    /// </summary>
    public static void RemoveProduct(string productId)
    {
        if (!_loaded)
        {
            LoadCache();
        }

        _cachedProducts.RemoveAll(p => p.ProductId == productId);
        SaveCache(_cachedProducts, DateTime.UtcNow);
    }

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    public static void ClearCache()
    {
        _cachedProducts.Clear();
        _cacheTimestamp = null;
        _loaded = true;

        try
        {
            if (FileAccess.FileExists(CacheFileName))
            {
                DirAccess.RemoveAbsolute(CacheFileName);
            }
            IAPLogger.Debug("Cache cleared");
        }
        catch (Exception ex)
        {
            IAPLogger.Error($"Failed to clear cache file: {ex.Message}");
        }
    }

    /// <summary>
    /// Forces a reload of the cache from disk.
    /// </summary>
    public static void ReloadCache()
    {
        _loaded = false;
        LoadCache();
    }

    /// <summary>
    /// Internal cache data structure for serialization.
    /// </summary>
    private class CacheData
    {
        public List<OwnedProduct> Products { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}
