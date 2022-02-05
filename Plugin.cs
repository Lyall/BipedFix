using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;


namespace BipedFix
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        public static ConfigEntry<bool> HintBubbleFix;
        public static ConfigEntry<bool> CutsceneFix;
        public static ConfigEntry<bool> SavingDialogFix;
        public static ConfigEntry<bool> TitleVideoFix;
        public static ConfigEntry<bool> GameMenuVideoFix;
        public static ConfigEntry<int> UnlockedFPS;
        public static ConfigEntry<bool> ToggleVSync;
        public static ConfigEntry<bool> Fullscreen;
        public static ConfigEntry<float> DesiredResolutionX;
        public static ConfigEntry<float> DesiredResolutionY;

        private void Awake()
        {
            Log = Logger;

            // Plugin startup logic
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            DesiredResolutionX = Config.Bind("General",
                                "ResolutionWidth",
                                 3440f,
                                "Set desired resolution width.");

            DesiredResolutionY = Config.Bind("General",
                                "ResolutionHeight",
                                1440f,
                                "Set desired resolution height.");

            Fullscreen = Config.Bind("General",
                                "Fullscreen",
                                true,
                                "Set to true for fullscreen or false for windowed.");

            HintBubbleFix = Config.Bind("Tweaks",
                                "HintBubbleFix",
                                true,
                                "Fixes misaligned hint bubbles at non 16:9 aspect ratios.");

            CutsceneFix = Config.Bind("Tweaks",
                                "CutsceneFix",
                                true,
                                "Fixes cutscene letterboxing at non 16:9 aspect ratios.");
            
            SavingDialogFix = Config.Bind("Tweaks",
                                "SavingDialogFix",
                                true,
                                "Fixes saving dialog box at non 16:9 aspect ratios.");

            TitleVideoFix = Config.Bind("Tweaks",
                                "TitleVideoAspectRatioFix",
                                true,
                                "Fixes title video aspect ratio.");

            GameMenuVideoFix = Config.Bind("Tweaks",
                                "GameMenuVideoAspectRatioFix",
                                true,
                                "Fixes game menu video aspect ratio.");

            UnlockedFPS = Config.Bind("General",
                                "UnlockedFPS",
                                120,
                                "Set the desired framerate limit.");

            ToggleVSync = Config.Bind("General",
                                "EnableVSync",
                                true,
                                "Enable VSync");

            Harmony.CreateAndPatchAll(typeof(Patches));

        }
    }

    [HarmonyPatch]
    public class Patches
    {
        private static readonly int VSyncFrames = Plugin.ToggleVSync.Value ? 1 : 0;
        public static float DefaultAspectRatio = 1.77777777778f; // 1920/1080 Based on DefaultReferenceResolution that is hardcoded.
        public static float NewAspectRatio = Plugin.DesiredResolutionX.Value / Plugin.DesiredResolutionY.Value;
        public static float AspectMultiplier = NewAspectRatio / DefaultAspectRatio;
        public static Vector2 DefaultReferenceResolution = new(1920, 1080);
        public static Vector2 NewReferenceResolution = new(1920 * AspectMultiplier, 1080);

        // Set screen resolution
        [HarmonyPatch(typeof(Biped.ScreenResolutionUtils), nameof(Biped.ScreenResolutionUtils.ApplyResolution))]
        [HarmonyPostfix]
        public static void SetResolution()
        {
            Screen.SetResolution((int)Plugin.DesiredResolutionX.Value, (int)Plugin.DesiredResolutionY.Value, Plugin.Fullscreen.Value); // No exclusive full screen til Unity 2018.1
            Plugin.Log.LogInfo($"Screen resolution set to = {(int)Plugin.DesiredResolutionX.Value}x{(int)Plugin.DesiredResolutionY.Value}");
        }

        // Unlock framerate 1
        [HarmonyPatch(typeof(Biped.Game), "Awake")]
        [HarmonyPostfix]
        public static void UnlockFramerate1()
        {
            Application.targetFrameRate = Plugin.UnlockedFPS.Value;
            QualitySettings.vSyncCount = VSyncFrames; // Set Vsync status
            Plugin.Log.LogInfo($"1 - Changed target frame rate to {Application.targetFrameRate}");
        }

        // Unlock framerate 2
        [HarmonyPatch(typeof(Biped.Level), "Awake")]
        [HarmonyPostfix]
        public static void UnlockFramerate2()
        {
            Application.targetFrameRate = Plugin.UnlockedFPS.Value;
            Plugin.Log.LogInfo($"2 - Changed target frame rate to {Application.targetFrameRate}");
        }

        // Unlock framerate 3
        [HarmonyPatch(typeof(Biped.Level), "OnDestroy")]
        [HarmonyPostfix]
        public static void UnlockFramerate3()
        {
            Application.targetFrameRate = Plugin.UnlockedFPS.Value;
            Plugin.Log.LogInfo($"3 - Changed target frame rate to {Application.targetFrameRate}");
        }

        // Fix misaligned hint bubbles
        [HarmonyPatch(typeof(Biped.HintBubbleDialogHandler), nameof(Biped.HintBubbleDialogHandler.Init))]
        [HarmonyPostfix]
        public static void UpdateHintReferenceResolution(Biped.HintBubbleDialogHandler __instance)
        {
            if (Plugin.HintBubbleFix.Value && AspectMultiplier > 1)
            {
                var canvasScaler = GameObject.Find("DialogCanvas(Clone)").GetComponent<CanvasScaler>();
                canvasScaler.referenceResolution = NewReferenceResolution;
                Plugin.Log.LogInfo($"Changed hint bubble canvas reference resolution to {canvasScaler.referenceResolution}");
            }
        }

        // Fix cinematic letterboxing
        [HarmonyPatch(typeof(Biped.GameMainUI), "Awake")]
        [HarmonyPostfix]
        public static void AdjustLetterboxing()
        {
            if (Plugin.CutsceneFix.Value && AspectMultiplier > 1)
            {
                var CinematicUI = GameObject.Find("GameMainUI/GamingUICoopMode/CinematicUI").GetComponent<RectTransform>();
                CinematicUI.localScale = new Vector3(1 * AspectMultiplier, 1, 1); // Multiply letterbox 
                Plugin.Log.LogInfo($"Cutscene local scale set to = {CinematicUI.localScale}");
            }
        }

        // Cull BG_Saving and BG_Saved when they are "offscreen". Right now this doesn't address the positioning of prefab_saving which would be a better fix.
        [HarmonyPatch(typeof(Biped.SavingNode), "Start")]
        [HarmonyPostfix]
        public static void CullSavingUI()
        {
            if (Plugin.SavingDialogFix.Value && AspectMultiplier > 1)
            {
                var BG_Saving = GameObject.Find("GameMainUI/GamingUICoopMode/Saving/Prefab_Saving/BG_Saving").GetComponent<CanvasRenderer>();
                BG_Saving.cull = true;
                var Img_Circle_Big = GameObject.Find("GameMainUI/GamingUICoopMode/Saving/Prefab_Saving/Img_Circle_Big").GetComponent<CanvasRenderer>();
                Img_Circle_Big.cull = true;
                var Img_Circle_Small = GameObject.Find("GameMainUI/GamingUICoopMode/Saving/Prefab_Saving/Img_Circle_Small").GetComponent<CanvasRenderer>();
                Img_Circle_Small.cull = true;
                var Img_CircleBG = GameObject.Find("GameMainUI/GamingUICoopMode/Saving/Prefab_Saving/Img_CircleBG").GetComponent<CanvasRenderer>();
                Img_CircleBG.cull = true;
                var Text_Saved = GameObject.Find("GameMainUI/GamingUICoopMode/Saving/Prefab_Saving/Text_Saved").GetComponent<CanvasRenderer>();
                Text_Saved.cull = true;

                Plugin.Log.LogInfo("Saving icons culled = " + BG_Saving.cull + Img_Circle_Big.cull + Img_Circle_Small.cull + Img_CircleBG.cull + Text_Saved.cull);
            }
        }

        // Fix title idle video aspect ratio
        [HarmonyPatch(typeof(Biped.TitleVideo), "GetRandomVideoUrl")]
        [HarmonyPostfix]
        public static void TitleVideoAR()
        {
            if (Plugin.TitleVideoFix.Value && AspectMultiplier > 1)
            {
                var TitleVideoPlayer = GameObject.Find("TitleVideo/Player").GetComponent<RectTransform>();
                TitleVideoPlayer.localScale = new Vector3(1 / AspectMultiplier, 1, 1);
                Plugin.Log.LogInfo($"Title video local scale set to = {TitleVideoPlayer.localScale}");
            }
        }

        // Fix game video aspect ratio
        [HarmonyPatch(typeof(Biped.DefaultVideoPlayer), "DoPlay")]
        [HarmonyPostfix]
        public static void GameMenuVideoAR()
        {
            if (Plugin.GameMenuVideoFix.Value && AspectMultiplier > 1)
            {
                var GameMenuVideo = GameObject.Find("GameMainUI/VideoUI/Player").GetComponent<RectTransform>();
                GameMenuVideo.localScale = new Vector3(1 / AspectMultiplier, 1, 1);
                Plugin.Log.LogInfo($"Game menu video local scale set to = {GameMenuVideo.localScale}");
            }
        }
    }
}