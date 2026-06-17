using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnlockFps.Services;

public class ConfigService
{
    private const string ConfigName = "fps_config.json";

    public Config Config { get; private set; } = new();

    public string JsonLoadError { get; private set; } = string.Empty;

    public ConfigService()
    {
        Load();
        StandardizeValues();
    }

    private void Load()
    {
        if (!File.Exists(ConfigName))
            return;

        var json = File.ReadAllText(ConfigName);
        //Config = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.Config)!;

        try
        {
            Config = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.Config)!;
        }
        catch (Exception e)
        {
            //Console.WriteLine($"Failed to load config: {e}");
            Console.WriteLine("Failed to load config");
            //JsonLoadError = $"Failed to load config: {e.Message}";
            JsonLoadError = "Failed to load config file" + Environment.NewLine + "Your config file doesn't appear to be in the correct format" + Environment.NewLine + "It will be reset to default";
        }
    }

    private async void Button_Click(object? sender)
    {
        //var alertWindow = new AlertWindow();
        //alertWindow.Text = "That's not the right place";
        //alertWindow.Title = "Notification";
        //alertWindow.IsError = false; // ⭐ 关键设置：启用第二个图标
        //await alertWindow.ShowDialog(this);
    }


    private void StandardizeValues()
    {
        if (Config.LaunchOptions == null!)
        {
            Config.LaunchOptions = new LaunchOptions();
        }

        if (!string.IsNullOrWhiteSpace(Config.LaunchOptions.GamePath))
        {
            Config.LaunchOptions.GamePath = File.Exists(Config.LaunchOptions.GamePath)
                ? Path.GetFullPath(Config.LaunchOptions.GamePath)
                : null;
        }

        Config.FpsTarget = Math.Clamp(Config.FpsTarget, 20, 720);
        Config.ProcessPriority = Math.Clamp(Config.ProcessPriority, 0, 5);
        Config.LaunchOptions.CustomResolutionX = Math.Clamp(Config.LaunchOptions.CustomResolutionX, 200, 7680);
        Config.LaunchOptions.CustomResolutionY = Math.Clamp(Config.LaunchOptions.CustomResolutionY, 200, 4320);
        Config.LaunchOptions.MonitorId = Math.Clamp(Config.LaunchOptions.MonitorId, 1, 100);
        Config.FpsPowerSave = Math.Clamp(Config.FpsPowerSave, 1, 30);

        if (Config.LaunchOptions.DllList == null!)
        {
            Config.LaunchOptions.DllList = new ObservableCollection<string>();
        }
        else
        {
            Config.LaunchOptions.DllList = new ObservableCollection<string>(
                Config.LaunchOptions.DllList
                    .Where(k => !string.IsNullOrWhiteSpace(k) && File.Exists(k))
                    .Select(Path.GetFullPath)
            );
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Config, ConfigJsonContext.Default.Config);
        File.WriteAllText(ConfigName, json);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(Config))]
internal partial class ConfigJsonContext : JsonSerializerContext;