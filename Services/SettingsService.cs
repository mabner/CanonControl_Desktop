using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CanonControl.Models;

namespace CanonControl.Services;

// service for loading and saving application settings to JSON file
public class SettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CanonControl"
    );

    private static readonly string SettingsFilePath = Path.Combine(
        SettingsDirectory,
        "settings.json"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, // pretty-print JSON for readability
    };

    // load settings from JSON file. returns default settings if file doesn't exist.
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                // return default settings if file doesn't exist
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            // log error and return defaults if loading fails
            Console.WriteLine($"Failed to load settings: {ex.Message}");
            return new AppSettings();
        }
    }

    // save settings to JSON file
    public void Save(AppSettings settings)
    {
        try
        {
            // ensure directory exists
            Directory.CreateDirectory(SettingsDirectory);

            // serialize and save
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            // log error if saving fails
            Console.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    // get the full path where settings are stored
    public string GetSettingsPath() => SettingsFilePath;
}
