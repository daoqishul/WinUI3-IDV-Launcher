using System.Text.Json.Serialization;

namespace IdvLauncher.Models;

public sealed class AppSettings
{
    [JsonPropertyName("gamePath")]
    public string GamePath { get; set; } = "";

    [JsonPropertyName("gameArgs")]
    public string GameArgs { get; set; } = "--start_from_launcher=1";

    [JsonPropertyName("theme")]
    public int Theme { get; set; } = 0; // 0 system, 1 dark, 2 light

    [JsonPropertyName("backdropMode")]
    public int BackdropMode { get; set; } = 1; // 0 solid, 1 mica, 2 acrylic

    [JsonPropertyName("squareCorners")]
    public bool SquareCorners { get; set; }

    [JsonPropertyName("rememberWindowBounds")]
    public bool RememberWindowBounds { get; set; } = true;

    [JsonPropertyName("enableTray")]
    public bool EnableTray { get; set; }

    [JsonPropertyName("disclaimerAccepted")]
    public bool DisclaimerAccepted { get; set; }

    [JsonPropertyName("frameDisclaimerAccepted")]
    public bool FrameDisclaimerAccepted { get; set; }

    [JsonPropertyName("sidebarCollapsed")]
    public bool SidebarCollapsed { get; set; }

    [JsonPropertyName("animations")]
    public bool Animations { get; set; } = true;

    [JsonPropertyName("settingsSchema")]
    public int SettingsSchema { get; set; }

    [JsonPropertyName("autoMinimize")]
    public bool AutoMinimize { get; set; }

    [JsonPropertyName("autoClose")]
    public bool AutoClose { get; set; }

    [JsonPropertyName("launchDelay")]
    public int LaunchDelay { get; set; }

    [JsonPropertyName("selectedAccount")]
    public string SelectedAccount { get; set; } = "";

    [JsonPropertyName("announcementPath")]
    public string AnnouncementPath { get; set; } = "";

    [JsonPropertyName("windowW")]
    public int WindowW { get; set; } = 1268;

    [JsonPropertyName("windowH")]
    public int WindowH { get; set; } = 690;

    [JsonPropertyName("windowX")]
    public int WindowX { get; set; } = -1;

    [JsonPropertyName("windowY")]
    public int WindowY { get; set; } = -1;

    [JsonPropertyName("lastPage")]
    public int LastPage { get; set; }

    [JsonPropertyName("enginePath")]
    public string EnginePath { get; set; } = "";

    [JsonPropertyName("enginePort")]
    public int EnginePort { get; set; } = 10717;

    [JsonPropertyName("engineGameId")]
    public string EngineGameId { get; set; } = "h55";

    [JsonPropertyName("useLoginEngine")]
    public bool UseLoginEngine { get; set; } = true;

    [JsonPropertyName("autoScanLogin")]
    public bool AutoScanLogin { get; set; } = true;

    [JsonPropertyName("frameUnlockEnabled")]
    public bool FrameUnlockEnabled { get; set; }

    [JsonPropertyName("frameRateTarget")]
    public int FrameRateTarget { get; set; } = 120;

    [JsonPropertyName("pcEmulatorMode")]
    public int PcEmulatorMode { get; set; }

    [JsonPropertyName("keyConfigPath")]
    public string KeyConfigPath { get; set; } = "";
}
