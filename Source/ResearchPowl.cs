// ResearchTree.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Threading;
using Verse.Sound;
using UnityEngine;
using static ResearchPowl.ResourceBank.String;
using static ResearchPowl.ModSettings_ResearchPowl;

namespace ResearchPowl
{
    public class ResearchPowl : Mod
    {
        static MainButtonDef modHelp;
        static MethodInfo helpWindow_JumpTo;

        public ResearchPowl( ModContentPack content ) : base( content )
        {
            new Harmony(this.Content.PackageIdPlayerFacing).PatchAll();
			base.GetSettings<ModSettings_ResearchPowl>();

            if (!ModSettings_ResearchPowl.delayLayoutGeneration)
            {
                if (ModSettings_ResearchPowl.asyncLoadingOnStartup) LongEventHandler.QueueLongEvent(StartLoadingWorker, "ResearchPowl.BuildingResearchTreeAsync", false, null);
                else LongEventHandler.QueueLongEvent(Tree.InitializeLayout, "ResearchPowl.BuildingResearchTree", false, null);
            }

            LongEventHandler.ExecuteWhenFinished(InitializeHelpSuport);
        }
        public static Thread initializeWorker = null;
        static void StartLoadingWorker() {
            initializeWorker = new Thread(Tree.InitializeLayout);
            Log.Message("Initialization start on background");
            initializeWorker.Start();
        }
        public override string SettingsCategory() { return "ResearchPal".Translate(); }
        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Rect rectLeftColumn = inRect.LeftPart(0.46f).Rounded();
            Rect rectRightColumn = inRect.RightPart(0.46f).Rounded();

            Listing_Standard listLeft = new Listing_Standard(GameFont.Small);
            listLeft.ColumnWidth = rectLeftColumn.width;
            listLeft.Begin(rectLeftColumn);

            if (listLeft.ButtonText(ResetTreeLayout))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                if (Tree.ResetLayout()) Messages.Message(LayoutRegenerated, MessageTypeDefOf.CautionInput, false);
            }

            if (Prefs.DevMode) listLeft.CheckboxLabeled(DontIgnoreHiddenPrerequisites, ref dontIgnoreHiddenPrerequisites, DontIgnoreHiddenPrerequisitesTip);
            listLeft.CheckboxLabeled(ShouldSeparateByTechLevels, ref shouldSeparateByTechLevels, ShouldSeparateByTechLevelsTip);
            listLeft.CheckboxLabeled(AlignCloserToAncestors, ref alignToAncestors, AlignCloserToAncestorsTip);
            listLeft.CheckboxLabeled(PlaceModTechSeparately, ref placeModTechSeparately, PlaceModTechSeparatelyTip);
            if (placeModTechSeparately)
            {
                listLeft.Label(MinimumSeparateModTech, -1, MinimumSeparateModTechTip);
                string buffer = largeModTechCount.ToString();
                listLeft.IntEntry(ref largeModTechCount, ref buffer);
            }
            listLeft.CheckboxLabeled(SearchByDescription, ref searchByDescription, SearchByDescriptionTip);
            listLeft.Gap();

            listLeft.CheckboxLabeled(ShouldPauseOnOpen, ref shouldPause, ShouldPauseOnOpenTip);
            listLeft.CheckboxLabeled(ShouldResetOnOpen, ref shouldReset, ShouldResetOnOpenTip);
            if (!asyncLoadingOnStartup || delayLayoutGeneration) listLeft.CheckboxLabeled(DelayLayoutGeneration, ref delayLayoutGeneration, DelayLayoutGenerationTip);
            if (!delayLayoutGeneration) listLeft.CheckboxLabeled(AsyncLoadingOnStartup, ref asyncLoadingOnStartup, AsyncLoadingOnStartupTip);
            listLeft.Gap();

            listLeft.CheckboxLabeled(ProgressTooltip, ref progressTooltip, ProgressTooltipTip);
            listLeft.CheckboxLabeled(AlwaysDisplayProgress, ref alwaysDisplayProgress, AlwaysDisplayProgressTip);
            listLeft.CheckboxLabeled(ShowIndexOnQueue, ref showIndexOnQueue, ShowIndexOnQueueTip);
            listLeft.CheckboxLabeled(DisableShortcutManual, ref disableShortcutManual);
            listLeft.CheckboxLabeled(SwapZoomMode, ref swapZoomMode, SwapZoomModeTip);

            listLeft.Gap();

            if (Prefs.DevMode) listLeft.CheckboxLabeled("ResearchPal.VerboseLogging".Translate(), ref verboseDebug, "ResearchPal.VerboseLoggingTip".Translate());

            listLeft.Gap();

            listLeft.CheckboxLabeled( ResourceBank.String.useVanillaResearchFinishedMessage, ref ModSettings_ResearchPowl.useVanillaResearchFinishedMessage, useVanillaResearchFinishedMessageTip);

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
        void InitializeHelpSuport()
        {
            var type = GenTypes.GetTypeInAnyAssembly("HelpTab.IHelpDefView");
            if (type != null)
            {
                modHelp = DefDatabase<MainButtonDef>.GetNamed("ModHelp", false);
                helpWindow_JumpTo = type.GetMethod("JumpTo", new Type[] { typeof(Def) });
            }
        }
    }
}