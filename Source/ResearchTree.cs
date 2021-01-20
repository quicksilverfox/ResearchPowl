// ResearchTree.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Threading;

namespace ResearchPal
{
    public class ResearchTree : Mod
    {
        public ResearchTree( ModContentPack content ) : base( content )
        {
            new Harmony("rimworld.ResearchPal").PatchAll();

            GetSettings<Settings>();

            if (! Settings.delayLayoutGeneration) {
                if (Settings.asyncLoadingOnStartup) {
                    LongEventHandler.QueueLongEvent(
                        StartLoadingWorker, "ResearchPal.BuildingResearchTreeAsync", false, null);
                } else {
                    LongEventHandler.QueueLongEvent(
                        Tree.InitializeLayout, "ResearchPal.BuildingResearchTree", false, null);
                }
            }


            LongEventHandler.ExecuteWhenFinished(InitializeHelpSuport);
        }

        public static Thread initializeWorker = null;

        static void StartLoadingWorker() {
            initializeWorker = new Thread(Tree.InitializeLayout);
            Log.Message("Initialization start on background");
            initializeWorker.Start();
        }

        #region Overrides of Mod

        public override string SettingsCategory() { return "ResearchPal".Translate(); }
        public override void DoSettingsWindowContents(UnityEngine.Rect inRect) { Settings.DoSettingsWindowContents(inRect); }

        #endregion

        #region HelpTree Support

        static MainButtonDef modHelp;
        static MethodInfo helpWindow_JumpTo;
        static bool helpTreeLoaded;

        void InitializeHelpSuport()
        {
            var type = GenTypes.GetTypeInAnyAssembly("HelpTab.IHelpDefView");
            if (type != null)
            {
                modHelp = DefDatabase<MainButtonDef>.GetNamed("ModHelp", false);
                helpWindow_JumpTo = type.GetMethod("JumpTo", new Type[] { typeof(Def) });

                helpTreeLoaded = true;
            }
        }

        public static void JumpToHelp(Def def)
        {
            if (helpTreeLoaded)
            {
                helpWindow_JumpTo.Invoke(modHelp.TabWindow, new object[] { def });
            }
        }

        public static bool HasHelpTreeLoaded
        {
            get
            {
                return helpTreeLoaded;
            }
        }

        #endregion
    }
}