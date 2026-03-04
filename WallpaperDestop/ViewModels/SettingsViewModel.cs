using System.Windows.Input;
using WallpaperDestop.Helpers;
using WallpaperDestop.Models;
using WallpaperDestop.Services;

namespace WallpaperDestop.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private AppSettings _settings;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _settings = _settingsService.Load();

        // Initialize fields from saved settings
        _apiKey = _settings.UnsplashApiKey;
        _startWithWindows = _settings.StartWithWindows;
        _photoCount = _settings.PhotoCount;
        _searchQuery = _settings.SearchQuery;

        // Sync the checkbox with the actual registry state
        _startWithWindows = StartupService.IsRegistered;

        SaveCommand = new RelayCommand(Save);
    }

    // ── Properties ──────────────────────────────────────────────

    private string _apiKey = string.Empty;
    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (SetProperty(ref _apiKey, value))
                OnPropertyChanged(nameof(IsApiKeyValid));
        }
    }

    private bool _startWithWindows;
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    private int _photoCount = 10;
    public int PhotoCount
    {
        get => _photoCount;
        set => SetProperty(ref _photoCount, Math.Clamp(value, 5, 30));
    }

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    private string _saveStatus = string.Empty;
    public string SaveStatus
    {
        get => _saveStatus;
        set => SetProperty(ref _saveStatus, value);
    }

    public bool IsApiKeyValid => !string.IsNullOrWhiteSpace(ApiKey) && ApiKey.Length > 10;

    // ── Commands ────────────────────────────────────────────────

    public ICommand SaveCommand { get; }

    // ── Handlers ────────────────────────────────────────────────

    private void Save()
    {
        try
        {
            _settings.UnsplashApiKey = ApiKey.Trim();
            _settings.StartWithWindows = StartWithWindows;
            _settings.PhotoCount = PhotoCount;
            _settings.SearchQuery = SearchQuery.Trim();

            _settingsService.Save(_settings);

            // Update Windows startup registration
            StartupService.SetStartup(StartWithWindows);

            SaveStatus = "Settings saved successfully!";
        }
        catch (Exception ex)
        {
            SaveStatus = $"Error saving: {ex.Message}";
        }
    }
}
