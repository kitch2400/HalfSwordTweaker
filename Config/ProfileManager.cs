using System.Text.Json;

namespace HalfSwordTweaker.Config;

public class ProfileManager
{
    private readonly string _profilesDirectory;
    private readonly string _appStatePath;
    private readonly Dictionary<string, ProfileData> _bundledProfiles;
    private AppState _appState;

    public string ActiveProfileName => _appState.ActiveProfileName;

    public const string CustomProfileName = "Custom";
    public const string ActivePrefix = "[ACTIVE] ";

    public ProfileManager()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _profilesDirectory = Path.Combine(localAppData, "HalfSwordTweaker", "Profiles");
        _appStatePath = Path.Combine(_profilesDirectory, "appstate.json");
        _bundledProfiles = CreateBundledProfiles();
        _appState = LoadAppState();
    }

    public static string StripActivePrefix(string profileName)
    {
        if (profileName.StartsWith(ActivePrefix))
        {
            return profileName.Substring(ActivePrefix.Length);
        }
        return profileName;
    }

    private AppState LoadAppState()
    {
        if (File.Exists(_appStatePath))
        {
            try
            {
                var json = File.ReadAllText(_appStatePath);
                var state = JsonSerializer.Deserialize<AppState>(json);
                if (state != null)
                {
                    if (!string.IsNullOrEmpty(state.ActiveProfileName) && 
                        !state.ActiveProfileName.StartsWith(ActivePrefix))
                    {
                        return state;
                    }
                }
            }
            catch
            {
            }
        }
        return new AppState { ActiveProfileName = CustomProfileName };
    }

    public void SaveAppState()
    {
        try
        {
            EnsureProfilesDirectoryExists();
            _appState.LastModified = DateTime.Now;
            var json = JsonSerializer.Serialize(_appState, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_appStatePath, json);
        }
        catch
        {
        }
    }

    public void SetActiveProfile(string profileName)
    {
        var cleanName = profileName.StartsWith(ActivePrefix) 
            ? profileName.Substring(ActivePrefix.Length) 
            : profileName;
        _appState.ActiveProfileName = cleanName;
        SaveAppState();
    }

    public bool IsActiveProfile(string profileName)
    {
        var cleanName = profileName.StartsWith(ActivePrefix) 
            ? profileName.Substring(ActivePrefix.Length) 
            : profileName;
        return string.Equals(cleanName, _appState.ActiveProfileName, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] ScalabilityGroupKeys = new[]
    {
        "sg.ViewDistanceQuality", "sg.AntiAliasingQuality", "sg.ShadowQuality",
        "sg.GlobalIlluminationQuality", "sg.ReflectionQuality", "sg.PostProcessQuality",
        "sg.TextureQuality", "sg.EffectsQuality", "sg.FoliageQuality",
        "sg.ShadingQuality", "sg.LandscapeQuality"
    };

    public string FindMatchingProfile(Dictionary<string, string> scalabilityGroups)
    {
        foreach (var kvp in _bundledProfiles)
        {
            if (MatchesProfile(scalabilityGroups, kvp.Value.ScalabilityGroups))
            {
                return kvp.Key;
            }
        }
        return CustomProfileName;
    }

    private static bool MatchesProfile(Dictionary<string, string> current, Dictionary<string, string> profile)
    {
        foreach (var key in ScalabilityGroupKeys)
        {
            var currentValue = current.TryGetValue(key, out var cv) ? cv : null;
            var profileValue = profile.TryGetValue(key, out var pv) ? pv : null;
            
            if (currentValue != profileValue)
            {
                return false;
            }
        }
        return true;
    }

    private static Dictionary<string, ProfileData> CreateBundledProfiles()
    {
        return new Dictionary<string, ProfileData>(StringComparer.OrdinalIgnoreCase)
        {
            ["Low[RECOMMENDED]"] = CreateLowPreset(),
            ["Medium"] = CreateMediumPreset(),
            ["High"] = CreateHighPreset(),
            ["Ultra"] = CreateUltraPreset(),
            ["Cinematic"] = CreateCinematicPreset()
        };
    }

    public bool IsBundledProfile(string profileName)
    {
        return _bundledProfiles.ContainsKey(profileName);
    }

    public bool SaveProfile(string profileName, Dictionary<string, Dictionary<string, string>> engineSettings, Dictionary<string, Dictionary<string, string>> gameUserSettings, Dictionary<string, string> scalabilityGroups)
    {
        try
        {
            EnsureProfilesDirectoryExists();

            var profilePath = Path.Combine(_profilesDirectory, $"{profileName}.json");

            var profileData = new ProfileData
            {
                ProfileName = profileName,
                Timestamp = DateTime.Now,
                EngineSettings = engineSettings,
                GameUserSettings = gameUserSettings,
                ScalabilityGroups = scalabilityGroups
            };

            var json = JsonSerializer.Serialize(profileData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(profilePath, json);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public ProfileData? LoadProfile(string profileName)
    {
        if (_bundledProfiles.TryGetValue(profileName, out var bundledProfile))
        {
            return bundledProfile;
        }

        var profilePath = Path.Combine(_profilesDirectory, $"{profileName}.json");

        if (!File.Exists(profilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(profilePath);
            return JsonSerializer.Deserialize<ProfileData>(json);
        }
        catch
        {
            return null;
        }
    }

    public List<string> GetProfiles()
    {
        var profiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        profiles.Add(CustomProfileName);

        foreach (var bundled in _bundledProfiles.Keys)
        {
            profiles.Add(bundled);
        }

        if (Directory.Exists(_profilesDirectory))
        {
            foreach (var file in Directory.GetFiles(_profilesDirectory, "*.json"))
            {
                var profileName = Path.GetFileNameWithoutExtension(file);
                if (profileName.Equals("appstate", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!_bundledProfiles.ContainsKey(profileName) && 
                    !profileName.Equals(CustomProfileName, StringComparison.OrdinalIgnoreCase))
                {
                    profiles.Add(profileName);
                }
            }
        }

        var sortedProfiles = profiles
            .OrderBy(p => p == CustomProfileName ? -1 : 0)
            .ThenBy(p => p == "Low[RECOMMENDED]" ? 0 : p == "Medium" ? 1 : p == "High" ? 2 : p == "Ultra" ? 3 : p == "Cinematic" ? 4 : 5)
            .ThenBy(p => p)
            .Select(p => IsActiveProfile(p) ? ActivePrefix + p : p)
            .ToList();

        return sortedProfiles;
    }

    private void EnsureProfilesDirectoryExists()
    {
        if (!Directory.Exists(_profilesDirectory))
        {
            Directory.CreateDirectory(_profilesDirectory);
        }
    }

    public static ProfileData CreateLowPreset()
    {
        return new ProfileData
        {
            ProfileName = "Low[RECOMMENDED]",
            Timestamp = DateTime.Now,
            ScalabilityGroups = new Dictionary<string, string>
            {
                ["sg.ResolutionQuality"] = "0",
                ["sg.ViewDistanceQuality"] = "0",
                ["sg.AntiAliasingQuality"] = "0",
                ["sg.ShadowQuality"] = "0",
                ["sg.GlobalIlluminationQuality"] = "0",
                ["sg.ReflectionQuality"] = "0",
                ["sg.PostProcessQuality"] = "0",
                ["sg.TextureQuality"] = "0",
                ["sg.EffectsQuality"] = "0",
                ["sg.FoliageQuality"] = "0",
                ["sg.ShadingQuality"] = "0",
                ["sg.LandscapeQuality"] = "0"
            }
        };
    }

    public static ProfileData CreateMediumPreset()
    {
        return new ProfileData
        {
            ProfileName = "Medium",
            Timestamp = DateTime.Now,
            ScalabilityGroups = new Dictionary<string, string>
            {
                ["sg.ResolutionQuality"] = "0",
                ["sg.ViewDistanceQuality"] = "1",
                ["sg.AntiAliasingQuality"] = "1",
                ["sg.ShadowQuality"] = "1",
                ["sg.GlobalIlluminationQuality"] = "1",
                ["sg.ReflectionQuality"] = "1",
                ["sg.PostProcessQuality"] = "1",
                ["sg.TextureQuality"] = "1",
                ["sg.EffectsQuality"] = "1",
                ["sg.FoliageQuality"] = "1",
                ["sg.ShadingQuality"] = "1",
                ["sg.LandscapeQuality"] = "1"
            }
        };
    }

    public static ProfileData CreateHighPreset()
    {
        return new ProfileData
        {
            ProfileName = "High",
            Timestamp = DateTime.Now,
            ScalabilityGroups = new Dictionary<string, string>
            {
                ["sg.ResolutionQuality"] = "0",
                ["sg.ViewDistanceQuality"] = "2",
                ["sg.AntiAliasingQuality"] = "2",
                ["sg.ShadowQuality"] = "2",
                ["sg.GlobalIlluminationQuality"] = "2",
                ["sg.ReflectionQuality"] = "2",
                ["sg.PostProcessQuality"] = "2",
                ["sg.TextureQuality"] = "2",
                ["sg.EffectsQuality"] = "2",
                ["sg.FoliageQuality"] = "2",
                ["sg.ShadingQuality"] = "2",
                ["sg.LandscapeQuality"] = "2"
            }
        };
    }

    public static ProfileData CreateUltraPreset()
    {
        return new ProfileData
        {
            ProfileName = "Ultra",
            Timestamp = DateTime.Now,
            ScalabilityGroups = new Dictionary<string, string>
            {
                ["sg.ResolutionQuality"] = "0",
                ["sg.ViewDistanceQuality"] = "3",
                ["sg.AntiAliasingQuality"] = "3",
                ["sg.ShadowQuality"] = "3",
                ["sg.GlobalIlluminationQuality"] = "3",
                ["sg.ReflectionQuality"] = "3",
                ["sg.PostProcessQuality"] = "3",
                ["sg.TextureQuality"] = "3",
                ["sg.EffectsQuality"] = "3",
                ["sg.FoliageQuality"] = "3",
                ["sg.ShadingQuality"] = "3",
                ["sg.LandscapeQuality"] = "3"
            }
        };
    }

    public static ProfileData CreateCinematicPreset()
    {
        return new ProfileData
        {
            ProfileName = "Cinematic",
            Timestamp = DateTime.Now,
            ScalabilityGroups = new Dictionary<string, string>
            {
                ["sg.ResolutionQuality"] = "0",
                ["sg.ViewDistanceQuality"] = "4",
                ["sg.AntiAliasingQuality"] = "4",
                ["sg.ShadowQuality"] = "4",
                ["sg.GlobalIlluminationQuality"] = "4",
                ["sg.ReflectionQuality"] = "4",
                ["sg.PostProcessQuality"] = "4",
                ["sg.TextureQuality"] = "4",
                ["sg.EffectsQuality"] = "4",
                ["sg.FoliageQuality"] = "4",
                ["sg.ShadingQuality"] = "4",
                ["sg.LandscapeQuality"] = "4"
            }
        };
    }
}

public class ProfileData
{
    public string ProfileName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, Dictionary<string, string>> EngineSettings { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> GameUserSettings { get; set; } = new();
    public Dictionary<string, string> ScalabilityGroups { get; set; } = new();
}

public class AppState
{
    public string ActiveProfileName { get; set; } = "Current";
    public DateTime LastModified { get; set; } = DateTime.Now;
}
