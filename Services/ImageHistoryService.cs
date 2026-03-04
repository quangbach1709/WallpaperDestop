using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace WallpaperDestop.Services
{
    /// <summary>
    /// Service to manage history of used wallpaper image IDs for anti-duplication.
    /// Stores up to 100 most recent image IDs in UsedImageIds.json in LocalAppData.
    /// Uses Queue-like behavior to maintain size limit by removing oldest entries.
    /// </summary>
    public class ImageHistoryService
    {
        private readonly string _historyFilePath;
        private const int MaxHistorySize = 100;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public ImageHistoryService()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(localAppData, "WallpaperDestop");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            
            _historyFilePath = Path.Combine(appFolder, "UsedImageIds.json");
        }

        /// <summary>
        /// Load the list of previously used image IDs from local JSON file.
        /// Returns empty list if file doesn't exist or is corrupted.
        /// </summary>
        public async Task<List<string>> LoadUsedImageIdsAsync()
        {
            try
            {
                if (!File.Exists(_historyFilePath))
                {
                    Debug.WriteLine($"[ImageHistoryService] History file not found, starting with empty list");
                    return new List<string>();
                }

                var jsonContent = await File.ReadAllTextAsync(_historyFilePath);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    Debug.WriteLine($"[ImageHistoryService] History file is empty, starting with empty list");
                    return new List<string>();
                }

                var usedIds = JsonSerializer.Deserialize<List<string>>(jsonContent, JsonOptions) ?? new List<string>();
                Debug.WriteLine($"[ImageHistoryService] Loaded {usedIds.Count} used image IDs from history");
                return usedIds;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageHistoryService] Error loading history: {ex.Message}");
                // Return empty list on any error to avoid blocking the app
                return new List<string>();
            }
        }

        /// <summary>
        /// Save the list of used image IDs to local JSON file.
        /// Automatically trims the list to MaxHistorySize (100) by removing oldest entries.
        /// </summary>
        /// <param name="usedImageIds">List of image IDs to save</param>
        public async Task SaveUsedImageIdsAsync(List<string> usedImageIds)
        {
            try
            {
                // Tối ưu dung lượng: Chỉ giữ tối đa 100 ID gần nhất
                var trimmedIds = usedImageIds.TakeLast(MaxHistorySize).ToList();
                
                if (trimmedIds.Count < usedImageIds.Count)
                {
                    var removedCount = usedImageIds.Count - trimmedIds.Count;
                    Debug.WriteLine($"[ImageHistoryService] Trimmed {removedCount} oldest IDs, keeping {trimmedIds.Count} recent ones");
                }

                var jsonContent = JsonSerializer.Serialize(trimmedIds, JsonOptions);
                await File.WriteAllTextAsync(_historyFilePath, jsonContent);
                
                Debug.WriteLine($"[ImageHistoryService] Saved {trimmedIds.Count} image IDs to history");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageHistoryService] Error saving history: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a new image ID to the history and save to file.
        /// This method loads current history, adds the new ID, trims if necessary, and saves.
        /// </summary>
        /// <param name="imageId">The new image ID to add to history</param>
        public async Task AddImageIdAsync(string imageId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imageId))
                {
                    Debug.WriteLine($"[ImageHistoryService] Cannot add empty or null image ID");
                    return;
                }

                Debug.WriteLine($"[ImageHistoryService] Adding image ID to history: {imageId}");

                // Load current history
                var usedIds = await LoadUsedImageIdsAsync();

                // Add new ID if not already present
                if (!usedIds.Contains(imageId))
                {
                    usedIds.Add(imageId);
                    await SaveUsedImageIdsAsync(usedIds);
                    Debug.WriteLine($"[ImageHistoryService] Successfully added new image ID: {imageId}");
                }
                else
                {
                    Debug.WriteLine($"[ImageHistoryService] Image ID already exists in history: {imageId}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageHistoryService] Error adding image ID: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a specific image ID has been used before.
        /// </summary>
        /// <param name="imageId">Image ID to check</param>
        /// <returns>True if the image has been used before</returns>
        public async Task<bool> IsImageUsedAsync(string imageId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imageId))
                    return false;

                var usedIds = await LoadUsedImageIdsAsync();
                return usedIds.Contains(imageId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageHistoryService] Error checking image ID: {ex.Message}");
                // Return false on error to avoid blocking the app
                return false;
            }
        }

        /// <summary>
        /// Filter out already used images from a list of photos.
        /// Returns the first unused photo, or null if all have been used.
        /// </summary>
        /// <param name="photos">List of photos to filter</param>
        /// <returns>First unused photo or null if all are duplicates</returns>
        public async Task<T?> FindFirstUnusedImageAsync<T>(List<T> photos, Func<T, string> idSelector) where T : class
        {
            try
            {
                if (photos == null || !photos.Any())
                {
                    Debug.WriteLine($"[ImageHistoryService] No photos provided for filtering");
                    return null;
                }

                var usedIds = await LoadUsedImageIdsAsync();
                Debug.WriteLine($"[ImageHistoryService] Filtering {photos.Count} photos against {usedIds.Count} used IDs");

                // Find first photo that hasn't been used before
                var unusedPhoto = photos.FirstOrDefault(photo => 
                {
                    var photoId = idSelector(photo);
                    return !string.IsNullOrWhiteSpace(photoId) && !usedIds.Contains(photoId);
                });

                if (unusedPhoto != null)
                {
                    var unusedId = idSelector(unusedPhoto);
                    Debug.WriteLine($"[ImageHistoryService] Found unused photo: {unusedId}");
                }
                else
                {
                    Debug.WriteLine($"[ImageHistoryService] All {photos.Count} photos have been used before!");
                }

                return unusedPhoto;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageHistoryService] Error filtering photos: {ex.Message}");
                // Return first photo as fallback
                return photos.FirstOrDefault();
            }
        }

        /// <summary>
        /// Get statistics about the image history for debugging/monitoring.
        /// </summary>
        /// <returns>Tuple with count of used IDs and file info</returns>
        public async Task<(int count, bool fileExists, long fileSize)> GetHistoryStatsAsync()
        {
            try
            {
                var usedIds = await LoadUsedImageIdsAsync();
                var fileExists = File.Exists(_historyFilePath);
                var fileSize = fileExists ? new FileInfo(_historyFilePath).Length : 0;

                return (usedIds.Count, fileExists, fileSize);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageHistoryService] Error getting stats: {ex.Message}");
                return (0, false, 0);
            }
        }
    }
}