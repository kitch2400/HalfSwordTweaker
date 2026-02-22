namespace HalfSwordTweaker.Config;

/// <summary>
/// Provides helper methods for file operations.
/// </summary>
public static class FileHelper
{
    /// <summary>
    /// Gets the path to the Half Sword configuration directory.
    /// </summary>
    /// <returns>The path to the config directory.</returns>
    public static string GetConfigDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "HalfSwordUE5", "Saved", "Config", "Windows");
    }

    /// <summary>
    /// Gets the path to the engine.ini file.
    /// </summary>
    /// <returns>The path to engine.ini.</returns>
    public static string GetEngineIniPath()
    {
        return Path.Combine(GetConfigDirectory(), "engine.ini");
    }

    /// <summary>
    /// Gets the path to the GameUserSettings.ini file.
    /// </summary>
    /// <returns>The path to GameUserSettings.ini.</returns>
    public static string GetGameUserSettingsIniPath()
    {
        return Path.Combine(GetConfigDirectory(), "GameUserSettings.ini");
    }

    /// <summary>
    /// Checks if a file is read-only.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>True if the file is read-only; otherwise, false.</returns>
    public static bool IsFileReadOnly(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        var fileInfo = new FileInfo(filePath);
        return fileInfo.IsReadOnly;
    }

    /// <summary>
    /// Sets or removes the read-only attribute from a file.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="isReadOnly">True to set read-only; false to remove it.</param>
    public static void SetFileReadOnly(string filePath, bool isReadOnly)
    {
        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            fileInfo.IsReadOnly = isReadOnly;
        }
    }

    /// <summary>
    /// Creates a backup of a file.
    /// </summary>
    /// <param name="filePath">The path to the file to backup.</param>
    /// <param name="backupPath">The path for the backup file. If null, a default path is used.</param>
    /// <returns>The path to the backup file.</returns>
    public static string CreateBackup(string filePath, string? backupPath = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        if (string.IsNullOrEmpty(backupPath))
        {
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            backupPath = Path.Combine(directory, $"{fileName}.backup_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
        }

        File.Copy(filePath, backupPath, overwrite: true);
        return backupPath;
    }

    /// <summary>
    /// Ensures that a directory exists.
    /// </summary>
    /// <param name="directoryPath">The path to the directory.</param>
    public static void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

}
