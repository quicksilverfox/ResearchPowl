using UnityEngine;
using Verse;
using RimWorld;
using Verse.Sound;
using static ResearchPowl.ResourceBank.String;
using HarmonyLib;
using System;

namespace ResearchPowl
{
    public class ModSettings_ResearchPowl : ModSettings
    {
        public static bool shouldPause, shouldReset, alignToAncestors, searchByDescription, asyncLoadingOnStartup, showIndexOnQueue, disableShortcutManual, verboseDebug, swapZoomMode, dontShowUnallowedTech;
        public static bool useVanillaResearchFinishedMessage = true, dontIgnoreHiddenPrerequisites = true, alwaysDisplayProgress = true, progressTooltip = true, 
            delayLayoutGeneration = true, shouldSeparateByTechLevels = true, placeModTechSeparately = true;
        public static float scrollingSpeedMultiplier = 1f, zoomingSpeedMultiplier = 1f, draggingDisplayDelay = 0.25f;
        public static int largeModTechCount = 5, maxAllowedTechLvl = 7;
        private static Vector2 currentScrollPosition = new Vector2(0, 0);

        public static void DoSettingsWindowContents(Rect windowRect)
        {
            Rect rectLeftColumn = windowRect.LeftPart(0.46f).Rounded();
            Rect rectRightColumn = windowRect.RightPart(0.46f).Rounded();

            Listing_Standard listLeft = new Listing_Standard(GameFont.Small);
            listLeft.ColumnWidth = rectLeftColumn.width;
            listLeft.Begin(rectLeftColumn);

            if (listLeft.ButtonText(ResetTreeLayout)) {
                SoundDefOf.Click.PlayOneShotOnCamera();
                if (Tree.ResetLayout()) {
                    Messages.Message(
                        LayoutRegenerated, MessageTypeDefOf.CautionInput, false);
                }
            }

            if (Prefs.DevMode) listLeft.CheckboxLabeled(DontIgnoreHiddenPrerequisites, ref dontIgnoreHiddenPrerequisites, DontIgnoreHiddenPrerequisitesTip);
            listLeft.CheckboxLabeled(ShouldSeparateByTechLevels, ref shouldSeparateByTechLevels, ShouldSeparateByTechLevelsTip);
            listLeft.CheckboxLabeled(AlignCloserToAncestors, ref alignToAncestors, AlignCloserToAncestorsTip);
            listLeft.CheckboxLabeled(PlaceModTechSeparately, ref placeModTechSeparately, PlaceModTechSeparatelyTip);
            if (placeModTechSeparately) {
                listLeft.Label(MinimumSeparateModTech, -1, MinimumSeparateModTechTip);
                string buffer = largeModTechCount.ToString();
                listLeft.IntEntry(ref largeModTechCount, ref buffer);
            }
            listLeft.CheckboxLabeled(SearchByDescription, ref searchByDescription, SearchByDescriptionTip);
            listLeft.Gap();

            listLeft.CheckboxLabeled(ShouldPauseOnOpen, ref shouldPause, ShouldPauseOnOpenTip);
            listLeft.CheckboxLabeled(ShouldResetOnOpen, ref shouldReset, ShouldResetOnOpenTip);
            if (!asyncLoadingOnStartup || delayLayoutGeneration) {
                listLeft.CheckboxLabeled(DelayLayoutGeneration, ref delayLayoutGeneration, DelayLayoutGenerationTip);
            }
            if (!delayLayoutGeneration) {
                listLeft.CheckboxLabeled(AsyncLoadingOnStartup, ref asyncLoadingOnStartup, AsyncLoadingOnStartupTip);
            }
            listLeft.Gap();

            listLeft.CheckboxLabeled(ProgressTooltip, ref progressTooltip, ProgressTooltipTip);
            listLeft.CheckboxLabeled(AlwaysDisplayProgress, ref alwaysDisplayProgress, AlwaysDisplayProgressTip);
            listLeft.CheckboxLabeled(ShowIndexOnQueue, ref showIndexOnQueue, ShowIndexOnQueueTip);
            listLeft.CheckboxLabeled(DisableShortcutManual, ref disableShortcutManual);
            listLeft.CheckboxLabeled(SwapZoomMode, ref swapZoomMode, SwapZoomModeTip);

            listLeft.Gap();

            if (Prefs.DevMode) listLeft.CheckboxLabeled("ResearchPal.VerboseLogging".Translate(), ref verboseDebug, "ResearchPal.VerboseLoggingTip".Translate());

            listLeft.Gap();

            listLeft.CheckboxLabeled( ResourceBank.String.useVanillaResearchFinishedMessage, ref useVanillaResearchFinishedMessage, useVanillaResearchFinishedMessageTip);

            listLeft.End();


            Listing_Standard listRight = new Listing_Standard(GameFont.Small);
            listRight.ColumnWidth = rectRightColumn.width;
            listRight.Begin(rectRightColumn);

            listRight.Label("ResearchPal.ScrollSpeedMultiplier".Translate() + string.Format(" {0:0.00}", scrollingSpeedMultiplier), -1, "ResearchPal.ScrollSpeedMultiplierTip".Translate());
            scrollingSpeedMultiplier = listRight.Slider(scrollingSpeedMultiplier, 0.1f, 5);
            listRight.Label( "ResearchPal.ZoomingSpeedMultiplier".Translate() + string.Format(" {0:0.00}", zoomingSpeedMultiplier), -1, "ResearchPal.ZoomingSpeedMultiplierTip".Translate());
            zoomingSpeedMultiplier = listRight.Slider(zoomingSpeedMultiplier, 0.1f, 5);
            listRight.Label( "ResearchPal.DraggingDisplayDelay".Translate() + string.Format(": {0:0.00}s", draggingDisplayDelay), -1, "ResearchPal.DraggingDisplayDelayTip".Translate());
            draggingDisplayDelay = listRight.Slider(draggingDisplayDelay, 0, 1);
            TechLevel tmp = (TechLevel)maxAllowedTechLvl;
            listRight.Label("ResearchPal.MaxAllowedTechLvl".Translate(tmp.ToStringHuman().CapitalizeFirst()), -1f, "ResearchPal.MaxAllowedTechLvlTip".Translate());
            maxAllowedTechLvl = (int)listRight.Slider(maxAllowedTechLvl, 1, 7);
            if (maxAllowedTechLvl < 7) listRight.CheckboxLabeled("ResearchPal.DontShowUnallowedTech".Translate(), ref dontShowUnallowedTech);
            else dontShowUnallowedTech = false;
            listRight.End();
        }

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