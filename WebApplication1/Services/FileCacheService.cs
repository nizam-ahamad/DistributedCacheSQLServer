using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class FileCacheService : IDistributedCache, ICacheService
    {
        private readonly string _cacheDirectory;
        private readonly ILogger<FileCacheService> _logger;
        private readonly int _defaultExpirationMinutes;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public FileCacheService(
            ILogger<FileCacheService> logger,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _defaultExpirationMinutes = configuration.GetValue<int>("CacheSettings:DefaultExpirationMinutes", 10);
            
            // Get cache directory from configuration or use default
            var cacheFolder = configuration.GetValue<string>("CacheSettings:FileCacheDirectory") ?? "CacheData";
            _cacheDirectory = Path.Combine(environment.ContentRootPath, cacheFolder);
            
            // Ensure cache directory exists
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
                _logger.LogInformation("Cache directory created at: {CacheDirectory}", _cacheDirectory);
            }
        }

        #region IDistributedCache Implementation

        public byte[]? Get(string key)
        {
            return GetAsync(key).GetAwaiter().GetResult();
        }

        public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                var filePath = GetFilePath(key);

                if (!File.Exists(filePath))
                {
                    _logger.LogInformation("Cache miss for key: {Key} - File not found", key);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath, token);
                var cacheEntry = JsonSerializer.Deserialize<FileCacheEntry<byte[]>>(json);

                if (cacheEntry == null)
                {
                    _logger.LogWarning("Cache entry is null for key: {Key}", key);
                    return null;
                }

                // Check if expired
                if (DateTime.UtcNow > cacheEntry.ExpiresAt)
                {
                    _logger.LogInformation("Cache expired for key: {Key}", key);
                    
                    // Delete the file directly instead of calling RemoveAsync to avoid deadlock
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        _logger.LogInformation("Expired cache file deleted for key: {Key}", key);
                    }
                    
                    return null;
                }

                _logger.LogInformation("Cache hit for key: {Key}, expires at: {ExpiresAt}", key, cacheEntry.ExpiresAt);
                return cacheEntry.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache for key: {Key}", key);
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            SetAsync(key, value, options).GetAwaiter().GetResult();
        }

        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                var filePath = GetFilePath(key);
                
                // Calculate expiration time
                var expirationTime = options.AbsoluteExpirationRelativeToNow 
                    ?? TimeSpan.FromMinutes(_defaultExpirationMinutes);

                var cacheEntry = new FileCacheEntry<byte[]>
                {
                    Data = value,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = options.AbsoluteExpiration?.UtcDateTime 
                        ?? DateTime.UtcNow.Add(expirationTime)
                };

                var json = JsonSerializer.Serialize(cacheEntry, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });

                await File.WriteAllTextAsync(filePath, json, token);
                
                _logger.LogInformation("Cache set for key: {Key} at path: {FilePath}, expires at: {ExpiresAt}", 
                    key, filePath, cacheEntry.ExpiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache for key: {Key}", key);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Refresh(string key)
        {
            RefreshAsync(key).GetAwaiter().GetResult();
        }

        public async Task RefreshAsync(string key, CancellationToken token = default)
        {
            // For file-based cache, refresh is not applicable as we use absolute expiration
            // But we can implement it to extend the expiration time if needed
            await Task.CompletedTask;
            _logger.LogInformation("Refresh called for key: {Key} (no-op for file cache)", key);
        }

        public void Remove(string key)
        {
            RemoveAsync(key).GetAwaiter().GetResult();
        }

        public async Task RemoveAsync(string key, CancellationToken token = default)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                var filePath = GetFilePath(key);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Cache removed for key: {Key} at path: {FilePath}", key, filePath);
                }
                else
                {
                    _logger.LogInformation("Cache file not found for key: {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache for key: {Key}", key);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        #endregion

        #region ICacheService Implementation (Generic Methods)

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var bytes = await GetAsync(key, CancellationToken.None);
                
                if (bytes == null || bytes.Length == 0)
                {
                    return null;
                }

                var json = Encoding.UTF8.GetString(bytes);
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting typed cache for key: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                var bytes = Encoding.UTF8.GetBytes(json);

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(_defaultExpirationMinutes)
                };

                await SetAsync(key, bytes, options, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting typed cache for key: {Key}", key);
            }
        }

        Task ICacheService.RemoveAsync(string key)
        {
            return RemoveAsync(key, CancellationToken.None);
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var bytes = await GetAsync(key, CancellationToken.None);
                return bytes != null && bytes.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
                return false;
            }
        }

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var cachedData = await GetAsync<T>(key);
            
            if (cachedData != null)
            {
                return cachedData;
            }

            _logger.LogInformation("Executing factory for key: {Key}", key);
            var data = await factory();
            
            if (data != null)
            {
                await SetAsync(key, data, expiration);
            }

            return data;
        }

        public async Task<CacheResult<T>> GetOrSetWithSourceAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var cachedData = await GetAsync<T>(key);
            
            if (cachedData != null)
            {
                _logger.LogInformation("Data retrieved from cache for key: {Key}", key);
                return new CacheResult<T>
                {
                    Data = cachedData,
                    Source = "cache",
                    IsFromCache = true
                };
            }

            _logger.LogInformation("Cache miss - Executing factory for key: {Key}", key);
            var data = await factory();
            
            if (data != null)
            {
                await SetAsync(key, data, expiration);
                return new CacheResult<T>
                {
                    Data = data,
                    Source = "repository",
                    IsFromCache = false
                };
            }

            return new CacheResult<T>
            {
                Data = null,
                Source = "none",
                IsFromCache = false
            };
        }

        #endregion

        #region Helper Methods

        // Helper method to generate safe file names from cache keys
        private string GetFilePath(string key)
        {
            // Create a hash of the key to ensure safe file names
            var safeFileName = GenerateSafeFileName(key);
            return Path.Combine(_cacheDirectory, $"{safeFileName}.json");
        }

        private string GenerateSafeFileName(string key)
        {
            // Use SHA256 to create a consistent hash for the key
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            var hash = Convert.ToHexString(hashBytes).ToLower();
            
            // Keep original key for readability (if safe) + hash for uniqueness
            var safeKey = new string(key.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            return $"{safeKey}_{hash.Substring(0, 16)}";
        }

        // Cleanup expired cache files (optional background task)
        public async Task CleanupExpiredCacheAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var files = Directory.GetFiles(_cacheDirectory, "*.json");
                var deletedCount = 0;

                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var cacheEntry = JsonSerializer.Deserialize<FileCacheEntry<object>>(json);

                        if (cacheEntry != null && DateTime.UtcNow > cacheEntry.ExpiresAt)
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing cache file: {File}", file);
                    }
                }

                _logger.LogInformation("Cleanup completed. Deleted {Count} expired cache files", deletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        #endregion
    }
}