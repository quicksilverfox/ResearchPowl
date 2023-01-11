using Verse;

namespace ResearchPowl
{
    public class ModSettings_ResearchPowl : ModSettings
    {
        public static bool shouldPause, shouldReset, alignToAncestors, searchByDescription, asyncLoadingOnStartup, showIndexOnQueue, disableShortcutManual, verboseDebug, swapZoomMode, dontShowUnallowedTech;
        public static bool useVanillaResearchFinishedMessage = true, dontIgnoreHiddenPrerequisites = true, alwaysDisplayProgress = true, progressTooltip = true, 
            delayLayoutGeneration = true, shouldSeparateByTechLevels = true, placeModTechSeparately = true;
        public static float scrollingSpeedMultiplier = 1f, zoomingSpeedMultiplier = 1f, draggingDisplayDelay = 0.25f;
        public static int largeModTechCount = 5, maxAllowedTechLvl = 7;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref shouldSeparateByTechLevels, "ShouldSeparateByTechLevels", true);
            Scribe_Values.Look(ref shouldPause, "ShouldPauseOnOpen", true);
            Scribe_Values.Look(ref shouldReset, "ShouldResetOnOpen", false);
            Scribe_Values.Look(ref alignToAncestors, "AlignCloserToAncestors", false);
            Scribe_Values.Look(ref placeModTechSeparately, "placeModTechsSeparately", true);
            Scribe_Values.Look(ref largeModTechCount, "MinimumSeparateModTech", 5);
            Scribe_Values.Look(ref maxAllowedTechLvl, "maxAllowedTechLvl", 7);
            Scribe_Values.Look(ref searchByDescription, "SearchByDescription", false);
            Scribe_Values.Look(ref delayLayoutGeneration, "DelayResearchLayoutGeneration", true);
            Scribe_Values.Look(ref asyncLoadingOnStartup, "AsyncLoadingOnStartup", false);
            Scribe_Values.Look(ref progressTooltip, "ProgressTooltip", false);
            Scribe_Values.Look(ref alwaysDisplayProgress, "AlwaysDisplayProgress", false);
            Scribe_Values.Look(ref showIndexOnQueue, "ShowQueuePositionOnQueue", false);
            Scribe_Values.Look(ref disableShortcutManual, "DisableShortcutManual", false);
            Scribe_Values.Look(ref dontIgnoreHiddenPrerequisites, "dontIgnoreHiddenPrerequisites", true);
            Scribe_Values.Look(ref scrollingSpeedMultiplier, "ScrollingSpeedMultiplier", 1);
            Scribe_Values.Look(ref zoomingSpeedMultiplier, "zoomingSpeedMultiplier", 1);
            Scribe_Values.Look(ref draggingDisplayDelay, "draggingDisplayDelay", 0.25f);
            Scribe_Values.Look(ref verboseDebug, "verboseLogging", false);
            Scribe_Values.Look(ref swapZoomMode, "swapZoomMode", false);
            Scribe_Values.Look(ref dontShowUnallowedTech, "dontShowUnallowedTech", false);
            Scribe_Values.Look(ref useVanillaResearchFinishedMessage, "useVanillaResearchFinishedMessage", true);
        }
    }
}