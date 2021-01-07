using UnityEngine;
using Verse;
using static ResearchPal.ResourceBank.String;

namespace ResearchPal
{
  public class Settings : ModSettings
    {
        #region tuning parameters

        public static bool shouldPause;
        public static bool shouldReset;
        public static bool shouldSeparateByTechLevels;

        public static bool alignToAncestors = false;

        public static bool placeModTechSeparately = true;

        public static int largeModTechCount = 5;

        public static bool delayLayoutGeneration = false;

        #endregion tuning parameters

        public static void DoSettingsWindowContents(Rect rect)
        {
            Listing_Standard list = new Listing_Standard(GameFont.Small);
            list.ColumnWidth = rect.width / 2;
            list.Begin(rect);

            list.CheckboxLabeled(ShouldSeparateByTechLevels, ref shouldSeparateByTechLevels,
                                 ShouldSeparateByTechLevelsTip);
            list.CheckboxLabeled(AlignCloserToAncestors, ref alignToAncestors, AlignCloserToAncestorsTip);
            list.CheckboxLabeled(PlaceModTechSeparately, ref placeModTechSeparately, PlaceModTechSeparatelyTip);
            if (placeModTechSeparately) {
                list.Label(MinimumSeparateModTech, -1, MinimumSeparateModTechTip);
                string buffer = largeModTechCount.ToString();
                list.IntEntry(ref largeModTechCount, ref buffer);
            }
            list.Gap();

            list.CheckboxLabeled(ShouldPauseOnOpen, ref shouldPause,
                                  ShouldPauseOnOpenTip);
            list.CheckboxLabeled(ShouldResetOnOpen, ref shouldReset,
                                  ShouldResetOnOpenTip);
            list.CheckboxLabeled(
                DelayLayoutGeneration,
                ref delayLayoutGeneration,
                DelayLayoutGenerationTip);
            list.End();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref shouldSeparateByTechLevels, "ShouldSeparateByTechLevels", false);
            Scribe_Values.Look(ref shouldPause, "ShouldPauseOnOpen", true);
            Scribe_Values.Look(ref shouldReset, "ShouldResetOnOpen", false);
            Scribe_Values.Look(ref alignToAncestors, "AlignCloserToAncestors", false);
            Scribe_Values.Look(ref placeModTechSeparately, "placeModTechsSeparately", true);
            Scribe_Values.Look(ref largeModTechCount, "MinimumSeparateModTech", 5);
            Scribe_Values.Look(ref delayLayoutGeneration, "DelayResearchLayoutGeneration", false);
        }
    }
}