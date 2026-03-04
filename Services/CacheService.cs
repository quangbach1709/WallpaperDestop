using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WallpaperDestop.Models;

namespace WallpaperDestop.Services
{
    public class CacheData
    {
        public DateTime Timestamp { get; set; }
        public List<UnsplashPhoto> Photos { get; set; } = new List<UnsplashPhoto>();
        public int CurrentPage { get; set; } = 1;
    }

    public class CacheService
    {
        private readonly string _cacheFilePath;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(12);

        public CacheService()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(localAppData, "WallpaperDestop");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            
            _cacheFilePath = Path.Combine(appFolder, "UnsplashCache.json");
        }

        /// <summary>
        /// Checks if cached data exists and is still valid (within 12 hours)
        /// </summary>
        public bool IsCacheValid()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                    return false;

                var cacheData = LoadCacheData();
                if (cacheData == null)
                    return false;

                return DateTime.Now - cacheData.Timestamp < _cacheExpiry;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Loads cached photos from local JSON file
        /// </summary>
        public async Task<CacheData?> LoadCacheAsync()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                    return null;

                var jsonContent = await File.ReadAllTextAsync(_cacheFilePath);
                if (string.IsNullOrWhiteSpace(jsonContent))
                    return null;

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<CacheData>(jsonContent, options);
            }
            catch (Exception ex)
            {
                // Log error if needed
                System.Diagnostics.Debug.WriteLine($"Error loading cache: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves photos to local JSON cache with current timestamp
        /// </summary>
        public async Task SaveCacheAsync(List<UnsplashPhoto> photos, int currentPage = 1)
        {
            try
            {
                var cacheData = new CacheData
                {
                    Timestamp = DateTime.Now,
                    Photos = photos ?? new List<UnsplashPhoto>(),
                    CurrentPage = currentPage
                };

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                var jsonContent = JsonSerializer.Serialize(cacheData, options);
                await File.WriteAllTextAsync(_cacheFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                // Log error if needed
                System.Diagnostics.Debug.WriteLine($"Error saving cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates cache with new photos (appending to existing ones)
        /// </summary>
        public async Task UpdateCacheWithNewPhotosAsync(List<UnsplashPhoto> newPhotos, int currentPage)
        {
            try
            {
                var existingCache = await LoadCacheAsync();
                var allPhotos = new List<UnsplashPhoto>();

                if (existingCache?.Photos != null)
                {
                    allPhotos.AddRange(existingCache.Photos);
                }

                // Add new photos (avoid duplicates by ID)
                foreach (var newPhoto in newPhotos)
                {
                    if (!allPhotos.Exists(p => p.Id == newPhoto.Id))
                    {
                        allPhotos.Add(newPhoto);
                    }
                }

                await SaveCacheAsync(allPhotos, currentPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the cache file
        /// </summary>
        public Task ClearCacheAsync()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    File.Delete(_cacheFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing cache: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets cache file info for debugging
        /// </summary>
        public (bool exists, DateTime? lastModified, long size) GetCacheInfo()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                    return (false, null, 0);

                var fileInfo = new FileInfo(_cacheFilePath);
                return (true, fileInfo.LastWriteTime, fileInfo.Length);
            }
            catch
            {
                return (false, null, 0);
            }
        }

        private CacheData? LoadCacheData()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                    return null;

                var jsonContent = File.ReadAllText(_cacheFilePath);
                if (string.IsNullOrWhiteSpace(jsonContent))
                    return null;

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<CacheData>(jsonContent, options);
            }
            catch
            {
                return null;
            }
        }
    }
}