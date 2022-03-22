using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;


namespace BipedFix
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class BipedFix : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        public static ConfigEntry<bool> UIFixes;
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
                                 (float)Display.main.systemWidth, // Set default to display width so we don't leave an unsupported resolution as default
                                "Set desired resolution width.");

            DesiredResolutionY = Config.Bind("General",
                                "ResolutionHeight",
                                (float)Display.main.systemHeight, // Set default to display height so we don't leave an unsupported resolution as default
                                "Set desired resolution height.");

            Fullscreen = Config.Bind("General",
                                "Fullscreen",
                                true,
                                "Set to true for fullscreen or false for windowed.");

            UIFixes = Config.Bind("Tweaks",
                                "UIFixes",
                                true,
                                "Fixes user interface issues at wider than 16:9 aspect ratios.");

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
        private static readonly int VSyncFrames = BipedFix.ToggleVSync.Value ? 1 : 0;
        public static float DefaultAspectRatio = 1.77777777778f; // 1920/1080 Based on DefaultReferenceResolution that is hardcoded.
        public static float NewAspectRatio = BipedFix.DesiredResolutionX.Value / BipedFix.DesiredResolutionY.Value;
        public static float AspectMultiplier = NewAspectRatio / DefaultAspectRatio ;
        public static Vector2 DefaultReferenceResolution = new(1920, 1080);
        public static Vector2 NewReferenceResolution = new(AspectMultiplier * 1920 , 1080);


        // Set screen resolution
        [HarmonyPatch(typeof(Biped.ScreenResolutionUtils), nameof(Biped.ScreenResolutionUtils.ApplyResolution))]
        [HarmonyPostfix]
        public static void SetResolution()
        {
            Screen.SetResolution((int)BipedFix.DesiredResolutionX.Value, (int)BipedFix.DesiredResolutionY.Value, BipedFix.Fullscreen.Value); // No exclusive full screen til Unity 2018.1
            BipedFix.Log.LogInfo($"Screen resolution set to = {(int)BipedFix.DesiredResolutionX.Value}x{(int)BipedFix.DesiredResolutionY.Value}");
        }

        // Unlock framerate 1
        [HarmonyPatch(typeof(Biped.Game), "Awake")]
        [HarmonyPostfix]
        public static void UnlockFramerate1()
        {
            Application.targetFrameRate = BipedFix.UnlockedFPS.Value;
            QualitySettings.vSyncCount = VSyncFrames; // Set Vsync status
            BipedFix.Log.LogInfo($"1 - Changed target frame rate to {Application.targetFrameRate}");
        }

        // Unlock framerate 2
        [HarmonyPatch(typeof(Biped.Level), "Awake")]
        [HarmonyPostfix]
        public static void UnlockFramerate2()
        {
            Application.targetFrameRate = BipedFix.UnlockedFPS.Value;
            BipedFix.Log.LogInfo($"2 - Changed target frame rate to {Application.targetFrameRate}");
        }

        // Unlock framerate 3
        [HarmonyPatch(typeof(Biped.Level), "OnDestroy")]
        [HarmonyPostfix]
        public static void UnlockFramerate3()
        {
            Application.targetFrameRate = BipedFix.UnlockedFPS.Value;
            BipedFix.Log.LogInfo($"3 - Changed target frame rate to {Application.targetFrameRate}");
        }

        // Fix misaligned hint bubbles
        [HarmonyPatch(typeof(Biped.HintBubbleDialogHandler), nameof(Biped.HintBubbleDialogHandler.Init))]
        [HarmonyPostfix]
        public static void UpdateHintReferenceResolution(Biped.HintBubbleDialogHandler __instance)
        {
            if (BipedFix.UIFixes.Value)
            {
                var canvasScaler = GameObject.Find("DialogCanvas(Clone)").GetComponent<CanvasScaler>();
                canvasScaler.referenceResolution = NewReferenceResolution;
                BipedFix.Log.LogInfo($"Changed hint bubble canvas reference resolution to {canvasScaler.referenceResolution}");  
            }
        }

        // Fix game main UI
        [HarmonyPatch(typeof(Biped.BipedGameUI), nameof(Biped.BipedGameUI.Start))]
        [HarmonyPostfix]
        public static void UpdateGameMainUIReferenceResolution()
        {
            if (BipedFix.UIFixes.Value)
            {
                var canvasScaler = GameObject.Find("GameMainUI").GetComponent<CanvasScaler>();
                canvasScaler.referenceResolution = NewReferenceResolution;
                BipedFix.Log.LogInfo($"Changed game UI reference resolution to {canvasScaler.referenceResolution}");
            }
        }

        // WIP Move saving prefab (this is a bad fix but it's better than default)
        [HarmonyPatch(typeof(Biped.SavingNode), "Start")]
        [HarmonyPostfix]
        public static void MoveSavingUI()
        {
            if (BipedFix.UIFixes.Value)
            {
                var Prefab_Saving = GameObject.Find("GameMainUI/GamingUICoopMode/Saving/Prefab_Saving").GetComponent<RectTransform>();
                Prefab_Saving.localPosition = new Vector3(749 / AspectMultiplier, -104, 0);
                BipedFix.Log.LogInfo($"Saving local pos changed to = {Prefab_Saving.localPosition}");
            }
        }

        // Fix cinematic letterboxing
        [HarmonyPatch(typeof(Biped.GameMainUI), "Awake")]
        [HarmonyPostfix]
        public static void AdjustLetterboxing()
        {
            if (BipedFix.UIFixes.Value)
            {
                var CinematicUI = GameObject.Find("GameMainUI/GamingUICoopMode/CinematicUI").GetComponent<RectTransform>();
                CinematicUI.localScale = new Vector3(1 * AspectMultiplier, 1, 1); // Multiply letterbox 
                BipedFix.Log.LogInfo($"Cutscene local scale set to = {CinematicUI.localScale}");
            }    
        }

        // Fix UI mask
        [HarmonyPatch(typeof(Biped.GameMainUI), "Awake")]
        [HarmonyPostfix]
        public static void AdjustUIMask()
        {
            if (BipedFix.UIFixes.Value)
            {
                var UI_Mask = GameObject.Find("GameMainUI/GamingUICoopMode/UI_Mask").GetComponent<RectTransform>();
                UI_Mask.localScale = new Vector3(1 * AspectMultiplier, 1, 1);
                BipedFix.Log.LogInfo($"UI_Mask local scale set to = {UI_Mask.localScale}");
            }
        }

        // Fix title idle video aspect ratio
        [HarmonyPatch(typeof(Biped.TitleVideo), "GetRandomVideoUrl")]
        [HarmonyPostfix]
        public static void TitleVideoAR()
        {
            if (BipedFix.UIFixes.Value)
            {
                var TitleVideoPlayer = GameObject.Find("TitleVideo/Player").GetComponent<RectTransform>();
                TitleVideoPlayer.localScale = new Vector3(1 / AspectMultiplier, 1, 1);
                BipedFix.Log.LogInfo($"Title video local scale set to = {TitleVideoPlayer.localScale}");
            }
        }

        // Fix game video aspect ratio
        [HarmonyPatch(typeof(Biped.DefaultVideoPlayer), "DoPlay")]
        [HarmonyPostfix]
        public static void GameMenuVideoAR()
        {
            if (BipedFix.UIFixes.Value)
            {
                var GameMenuVideo = GameObject.Find("GameMainUI/VideoUI/Player").GetComponent<RectTransform>();
                GameMenuVideo.localScale = new Vector3(1 / AspectMultiplier, 1, 1);
                BipedFix.Log.LogInfo($"Game menu video local scale set to = {GameMenuVideo.localScale}");
            }   
        }
    }
}