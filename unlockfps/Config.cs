using System.Collections.ObjectModel;
using System.ComponentModel;

namespace UnlockFps;

public partial class LaunchOptions : INotifyPropertyChanged
{
    public string? GamePath { get; set; }
    public string? SRGamePath { get; set; }

    public bool IsWindowBorderless { get; set; }
    public bool Fullscreen { get; set; } = true;
    public bool IsExclusiveFullscreen { get; set; }
    public bool UseCustomResolution { get; set; }
    public int CustomResolutionX { get; set; } = 1920;
    public int CustomResolutionY { get; set; } = 1080;
    public bool UseMobileUI { get; set; }

    public int MonitorId { get; set; } = 1;

    public bool SuspendLoad { get; set; }
    public ObservableCollection<string> DllList { get; set; } = new();

    //251230
    public bool EnableGensinAdvancedSet { get; set; } = false;
}

public partial class Config : INotifyPropertyChanged
{
    public LaunchOptions LaunchOptions { get; set; } = new();

    public bool AutoLaunch { get; set; }
    public bool AutoClose { get; set; } = true;
    public bool UsePowerSave { get; set; }
    public int FpsTarget { get; set; } = 240;
    public int FpsPowerSave { get; set; } = 10;
    public int ProcessPriority { get; set; } = 3;
    public bool ShowDebugConsole { get; set; }
    public bool WindowQueryUseEvent { get; set; } = true;

    //250830
    public bool UmlimitedFpsGenshin { get; set; } = false;
    public bool UmlimitedFpsStarRail { get; set; } = false;
    public bool ForceUmlimitedFps { get; set; } = false;
    public bool GameSelection { get; set; } = false;
    public bool UseSystemTheme { get; set; } = false;
    public string? SRCustomParam { get; set; }
    public bool UsePowerSaveSR { get; set; }
    public int FpsTargetSR { get; set; } = 240;
    public int FpsTargetTemp { get; set; } = 240;
    public bool ApplySRLoadDll { get; set; } = false;
    public int ProcessPrioritySR { get; set; } = 3;

    //251102
    public bool UseHDRGenshin { get; set; } = false;
    //251106
    public bool EnableFOVGenshin { get; set; } = false;
    public int FOVTargetGenshin { get; set; } = 45;
    //251122
    public bool EnableFOVGenshinFix { get; set; } = false;
    //251205
    public bool EnableStarRailMbUIWt { get; set; } = false;
    public bool EnableGenshinMbUIWt { get; set; } = false;
    public bool EnableFOVStarRail { get; set; } = false;
    public int FOVTargetStarRail { get; set; } = 45;
    public bool EnableFOVStarRailFix { get; set; } = false;
    public int SRFovDepth { get; set; } = 250;
    public int SRFovSpeed { get; set; } = 20;
    public int SRFovRuntime { get; set; } = 5000;
    public bool DisablePlayerPerspectiveBlur { get; set; } = false;
    public bool DisplayGenshinFog { get; set; } = false;

    //251230
    public bool RemoveQuestBannerGensin { get; set; } = false;
    public bool RemoveDamageTextGensin { get; set; } = false;
    public bool DisableEventCameraMoveGensin { get; set; } = false;
    public bool RemoveTeamProgressGensin { get; set; } = false;
    public bool RedirectCombineEntryGensin { get; set; } = false;

    //260102
    public bool DisableAdvanceSetForceFps { get; set; } = false;

    //260116
    //public bool EnableGenshinQuickCrafting { get; set; } = false;
    //public bool EnableGenshinQuickCraftingTab { get; set; } = false;
    public bool EnableGenshinQuickCraftingFocus { get; set; } = false;

    //260118
    public bool EnableGenshinCraftingFocusOnly { get; set; } = false;

    //260120
    public string SRFOVHookAddress { get; set; } = string.Empty;

    // 260131
    public bool EnableGenshinQuickCraftingKey { get; set; } = false;
    public bool HideGenshinUID { get; set; } = false;

    // 260214
    public bool EnableSRGraphicOptionSet { get; set; } = false;
    public bool SRCustomFullScreen { get; set; } = false;
    public bool EnableSRCustomResolutionSet { get; set; } = false;
    public int SRCustomResolutionWidth { get; set; } = 1920;
    public int SRCustomResolutionHeight { get; set; } = 1080;
    public int DetectSRCustomResolutionMode { get; set; } = 0;

    // 260225
    public bool EnableGenshinRemoveGrass { get; set; } = false;

    // 260307
    public bool ForceUmlimitedFpsV2 { get; set; } = false;
}