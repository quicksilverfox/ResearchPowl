// ResearchNode.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using static ResearchPal.Constants;

namespace ResearchPal
{
    public class ResearchNode : Node
    {
        private static readonly Dictionary<ResearchProjectDef, bool> _buildingPresentCache =
            new Dictionary<ResearchProjectDef, bool>();

        private static readonly Dictionary<ResearchProjectDef, List<ThingDef>> _missingFacilitiesCache =
            new Dictionary<ResearchProjectDef, List<ThingDef>>();

        public ResearchProjectDef Research;

        public ResearchNode( ResearchProjectDef research )
        {
            Research = research;

            // initialize position at vanilla y position, leave x at zero - we'll determine this ourselves
            _pos = new Vector2( 0, research.researchViewY + 1 );
        }

        public bool isMatched = false;

        private bool _isHighlighted = false;


        public override bool Highlighted()
        {
            return _isHighlighted;
        }

        public void Highlighted(bool h) {
            _isHighlighted = h;
        }

        public List<ResearchNode> Parents
        {
            get
            {
                return InNodes.OfType<ResearchNode>()
                    .Concat(
                        InNodes.OfType<DummyNode>().SelectMany( dn => dn.Parent ))
                    .ToList();
            }
        }

        public override Color Color
        {
            get
            {
                if (Highlighted())
                    return GenUI.MouseoverColor;
                if (IsUnmatchedInSearch())
                {
                    return Assets.ColorUnmatched[Research.techLevel];
                }
                if (Completed)
                    return Assets.ColorCompleted[Research.techLevel];
                if (Available)
                    return Assets.ColorCompleted[Research.techLevel];
                return Assets.ColorUnavailable[Research.techLevel];
            }
        }
        public bool mouseHoverHighlight = false;


        public bool IsUnmatchedInSearch()
        {
            return MainTabWindow_ResearchTree.Instance.SearchActive() && !isMatched;
        }
        public bool IsMatchedInSearch()
        {
            return MainTabWindow_ResearchTree.Instance.SearchActive() && isMatched;
        }

        public bool HighlightInEdge(ResearchNode from) {
            return mouseHoverHighlight && (from.mouseHoverHighlight || from.Research.IsFinished);
        }

        public override Color InEdgeColor(ResearchNode from)
        {
            if (HighlightInEdge(from))
                return GenUI.MouseoverColor;
            if (MainTabWindow_ResearchTree.Instance.SearchActive())
            {
                return Assets.ColorUnmatched[Research.techLevel];
            }
            if (Completed)
                return Assets.ColorCompleted[Research.techLevel];
            if (Available)
                return Assets.ColorAvailable[Research.techLevel];
            return Assets.ColorUnavailable[Research.techLevel];
        }

        public List<ResearchNode> Children
        {
            get
            {
                return OutNodes.OfType<ResearchNode>()
                    .Concat(
                        OutNodes.OfType<DummyNode>().SelectMany(dn => dn.Child))
                    .ToList();
            }
        }

        public override string Label => Research.LabelCap;

        public static bool BuildingPresent( ResearchProjectDef research )
        {
            if ( DebugSettings.godMode && Prefs.DevMode )
                return true;

            // try get from cache
            bool result;
            if ( _buildingPresentCache.TryGetValue( research, out result ) )
                return result;

            // do the work manually
            if ( research.requiredResearchBuilding == null )
                result = true;
            else
                result = Find.Maps.SelectMany( map => map.listerBuildings.allBuildingsColonist )
                             .OfType<Building_ResearchBench>()
                             .Any( b => research.CanBeResearchedAt( b, true ) );

            if ( result )
                result = research.Ancestors().All( BuildingPresent );

            // update cache
            _buildingPresentCache.Add( research, result );
            return result;
        }

        public static bool TechprintAvailable( ResearchProjectDef research )
        {
            return research.TechprintRequirementMet;
        }

        public static void ClearCaches()
        {
            _buildingPresentCache.Clear();
            _missingFacilitiesCache.Clear();
        }

        public static implicit operator ResearchNode( ResearchProjectDef def )
        {
            return def.ResearchNode();
        }

        public int Matches( string query )
        {
            var culture = CultureInfo.CurrentUICulture;
            query = query.ToLower( culture );

            if ( Research.LabelCap.RawText.ToLower( culture ).Contains( query ) )
                return 1;
            if (Research.GetUnlockDefsAndDescs()
                         .Any( unlock => unlock.First.LabelCap.RawText.ToLower( culture ).Contains( query )))
                return 2;
            if ((Research.modContentPack?.Name.ToLower(culture) ?? "").Contains(query) ) {
                return 3;
            }
            if (Research.description.ToLower( culture ).Contains( query ))
                return 4;
            return 0;
        }

        public static List<ThingDef> MissingFacilities( ResearchProjectDef research )
        {
            // try get from cache
            List<ThingDef> missing;
            if ( _missingFacilitiesCache.TryGetValue( research, out missing ) )
                return missing;

            // get list of all researches required before this
            var thisAndPrerequisites = research.Ancestors().Where( rpd => !rpd.IsFinished ).ToList();
            thisAndPrerequisites.Add( research );

            // get list of all available research benches
            var availableBenches = Find.Maps.SelectMany( map => map.listerBuildings.allBuildingsColonist )
                                       .OfType<Building_ResearchBench>();
            var availableBenchDefs = availableBenches.Select( b => b.def ).Distinct();
            missing = new List<ThingDef>();

            // check each for prerequisites
            // TODO: We should really build this list recursively so we can re-use results for prerequisites.
            foreach ( var rpd in thisAndPrerequisites )
            {
                if ( rpd.requiredResearchBuilding == null )
                    continue;

                if ( !availableBenchDefs.Contains( rpd.requiredResearchBuilding ) )
                    missing.Add( rpd.requiredResearchBuilding );

                if ( rpd.requiredResearchFacilities.NullOrEmpty() )
                    continue;

                foreach ( var facility in rpd.requiredResearchFacilities )
                    if ( !availableBenches.Any( b => b.HasFacility( facility ) ) )
                        missing.Add( facility );
            }

            // add to cache
            missing = missing.Distinct().ToList();
            _missingFacilitiesCache.Add( research, missing );
            return missing;
        }

        public bool BuildingPresent()
        {
            return BuildingPresent( Research );
        }
        
        public bool TechprintAvailable()
        {
            return TechprintAvailable( Research );
        }

        public override int DefaultPriority()
        {
            return InEdges.Count() + OutEdges.Count();
        }
        public override int LayoutUpperPriority()
        {
            return InEdges.Count();
        }
        public override int LayoutLowerPriority()
        {
            return OutEdges.Count();
        }

        public override int CompareTieBreaker(Node that)
        {
            if (that is DummyNode) {
                return -1;
            }
            var n = that as ResearchNode;
            var c1 = Research.techLevel.CompareTo(n.Research.techLevel);
            if (c1 != 0) return c1;
            return (Research.modContentPack?.Name ?? "").CompareTo(n.Research.modContentPack?.Name ?? "");
        }

        private void DrawProgressBarImpl(Rect rect) {
            GUI.color            =  Assets.ColorAvailable[Research.techLevel];
            rect.xMin += Research.ProgressPercent * rect.width;
            GUI.DrawTexture(rect, BaseContent.WhiteTex);
        }

        private void DrawBackground(bool mouseOver) {
            // researches that are completed or could be started immediately, and that have the required building(s) available
            GUI.color = mouseOver ? GenUI.MouseoverColor : Color;

            if ( mouseOver || Highlighted() )
                GUI.DrawTexture( Rect, Assets.ButtonActive );
            else
                GUI.DrawTexture( Rect, Assets.Button );
        }

        private void DrawProgressBar() {
            // grey out center to create a progress bar effect, completely greying out research not started.
            if ( IsMatchedInSearch()
               || !IsUnmatchedInSearch() && Available
               || Highlighted())
            {
                var progressBarRect = Rect.ContractedBy( 3f );
                DrawProgressBarImpl(progressBarRect);
            }
        }

        private void HandleTooltips() {
            Text.WordWrap = true;
                // attach description and further info to a tooltip
            if (!TechprintAvailable()) {
                TooltipHandler.TipRegion(Rect,
                    ResourceBank.String.MissingTechprints(Research.TechprintsApplied, Research.techprintCount));
            }
            if ( !BuildingPresent() ) {
                TooltipHandler.TipRegion( Rect,
                   ResourceBank.String.MissingFacilities(
                        string.Join(", ",
                            MissingFacilities().Select( td => td.LabelCap )
                                .ToArray())));
            }
            TooltipHandler.TipRegion(Rect, GetResearchTooltipString, Research.GetHashCode());

            if (Settings.progressTooltip && ProgressWorthDisplaying() && !Research.IsFinished) {
                TooltipHandler.TipRegion(Rect, string.Format("Progress: {0}", ProgressString()));
            }
        }

        private void DrawIcons() {
            var unlocks = Research.GetUnlockDefsAndDescs();
            for ( var i = 0; i < unlocks.Count; i++ )
            {
                var iconRect = new Rect(
                    IconsRect.xMax - ( i                + 1 )          * ( IconSize.x + 4f ),
                    IconsRect.yMin + ( IconsRect.height - IconSize.y ) / 2f,
                    IconSize.x,
                    IconSize.y );

                if ( iconRect.xMin - IconSize.x < IconsRect.xMin &&
                        i             + 1          < unlocks.Count )
                {
                    // stop the loop if we're about to overflow and have 2 or more unlocks yet to print.
                    iconRect.x = IconsRect.x + 4f;
                    GUI.DrawTexture( iconRect, Assets.MoreIcon, ScaleMode.ScaleToFit );
                    var tip = string.Join( "\n",
                                            unlocks.GetRange( i, unlocks.Count - i ).Select( p => p.Second )
                                                    .ToArray() );

                    if (RightClick(iconRect) && Find.WindowStack.FloatMenu == null) {
                        var floatMenu = MakeInfoMenuFromDefs(unlocks.Skip(i).Select(p => p.First));
                        Find.WindowStack.Add(floatMenu);
                    }
                    TooltipHandler.TipRegion( iconRect, tip );

                    // new TipSignal( tip, Settings.TipID, TooltipPriority.Pawn ) );
                    break;
                }
                var def = unlocks[i].First;

                // draw icon
                def.DrawColouredIcon( iconRect );

                // tooltip
                TooltipHandler.TipRegion( iconRect, unlocks[i].Second );

                if (RightClick(iconRect)) {
                    Dialog_InfoCard.Hyperlink link = new Dialog_InfoCard.Hyperlink(def);
                    link.OpenDialog();
                }
            }
        }

        private void DrawNodeDetailMode(bool mouseOver) {
            Text.Anchor   = TextAnchor.UpperLeft;
            Text.WordWrap = true;
            Text.Font     = _largeLabel ? GameFont.Tiny : GameFont.Small;
            Widgets.Label( LabelRect, Research.LabelCap );

            GUI.DrawTexture(CostIconRect, !Completed && !Available ? Assets.Lock : Assets.ResearchIcon,
                                ScaleMode.ScaleToFit);

            Color savedColor = GUI.color;
            Color numberColor;
            float numberToDraw;
            if (Settings.alwaysDisplayProgress && ProgressWorthDisplaying() || SwitchToProgress()) {
                if (Research.IsFinished) {
                    numberColor = Color.cyan;
                    numberToDraw = 0;
                } else {
                    numberToDraw = Research.CostApparent - Research.ProgressApparent;
                    numberColor = Color.green;
                }
            } else {
                numberToDraw = Research.CostApparent;
                numberColor = savedColor;
            }
            if (IsUnmatchedInSearch()) {
                numberColor = Color.gray;
            }
            GUI.color = numberColor;
            Text.Anchor = TextAnchor.UpperRight;

            Text.Font   = NumericalFont(numberToDraw);
            Widgets.Label(CostLabelRect, numberToDraw.ToStringByStyle(ToStringStyle.Integer));
            GUI.color = savedColor;

            DrawIcons();
        }

        private string ProgressString() {
            return string.Format("{0} / {1}",
                Research.ProgressApparent.ToStringByStyle(ToStringStyle.Integer),
                Research.CostApparent.ToStringByStyle(ToStringStyle.Integer));
        }

        private bool ProgressWorthDisplaying() {
            return Research.ProgressApparent > 0;
        }

        private void DrawNodeZoomedOut(bool mouseOver) {
            string textToDraw;
            if (SwitchToProgress() && ! Research.IsFinished) {
                textToDraw = ProgressString();
            } else {
                textToDraw = Research.LabelCap;
            }
            Text.Anchor   = TextAnchor.MiddleCenter;
            // Text.Font     = ChooseFont(textToDraw, Rect, GameFont.Medium, true);
            // Text.WordWrap = Text.Font == GameFont.Medium ? false : true;
            Text.Font     = GameFont.Medium;
            Text.WordWrap = true;
            Widgets.Label(Rect, textToDraw);
        }


        private void DrawNode(bool detailedMode, bool mouseOver, int painter) {
            // TryModifySharedState(painter, mouseOver);
            HandleTooltips();

            DrawBackground(mouseOver);
            DrawProgressBar();

            // draw the research label
            if ((Completed || Available) && !IsUnmatchedInSearch())
                GUI.color = Color.white;
            else
                GUI.color = Color.grey;

            if (detailedMode) {
                DrawNodeDetailMode(mouseOver);
            } else {
                DrawNodeZoomedOut(mouseOver);
            }
            Text.WordWrap = true;
        }


        public static GameFont NumericalFont(float number) {
            return number >= 1000000 ? GameFont.Tiny : GameFont.Small;
        }

        public static bool RightClick(Rect rect) {
            return Input.GetMouseButton(1) && Mouse.IsOver(rect);
        }

        private static FloatMenu MakeInfoMenuFromDefs(IEnumerable<Def> defs) {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (var def in defs) {
                Texture2D icon = def.IconTexture();
                // if (def is ThingDef thingDef) {
                //     icon = thingDef.uiIcon;
                // } else if (def is RecipeDef recipeDef) {
                //     icon = recipeDef.UIIconThing.uiIcon;
                // } else if (def is TerrainDef terrainDef) {
                //     icon = terrainDef.uiIcon;
                // }
                Dialog_InfoCard.Hyperlink hyperlink = new Dialog_InfoCard.Hyperlink(def);
             
                options.Add(new FloatMenuOption(def.label, () => hyperlink.OpenDialog(), icon, def.IconColor()));
            }
            return new FloatMenu(options);
        }

        public bool ShouldHighlight() {
            return Mouse.IsOver(Rect);
        }

        public bool SwitchToProgress() {
            return Tree.DisplayProgressState && ProgressWorthDisplaying();
        }

        public bool ShouldHighlight(Vector2 mousePos) {
            return Rect.Contains(mousePos);
        }

        public void HandleMouseEvents() {
            // LMB is queue operations, RMB is info
            if (Event.current.button == 0 && !Research.IsFinished) {
                if (DebugSettings.godMode && Event.current.control) {
                    Queue.FinishS(this);
                    Messages.Message(ResourceBank.String.FinishedResearch(Research.LabelCap), MessageTypeDefOf.SilentInput, false);
                    Queue.Notify_InstantFinished();
                } else if (!Queue.ContainsS(this)) {
                    if (Event.current.shift) {
                        Queue.AppendS(this);
                    } else if (Event.current.alt) {
                        Queue.PrependS(this);
                    } else {
                        Queue.ReplaceS(this);
                    }
                } else {
                    Queue.RemoveS(this);
                }
            } else if (Event.current.button == 1) {
                ResearchTree.JumpToHelp(Research);
            }
        }

        // bool prevOver = false;

        /// <summary>
        ///     Draw the node, including interactions.
        /// </summary>
        public override void Draw(
            Rect visibleRect, int painterId, bool forceDetailedMode = false)
        {
            // Call site should ensure this condition
            // if (!IsVisible(visibleRect)) {
            //     return;
            // }
            var detailedMode = forceDetailedMode ||
                               MainTabWindow_ResearchTree.Instance.ZoomLevel < DetailedModeZoomLevelCutoff;
            var mouseOver = Mouse.IsOver(Rect);

            if (Event.current.type == EventType.Repaint) {
                DrawNode(detailedMode, mouseOver, painterId);
            }

            // Tool tip debugging fail attempt
            // var curOver = Mouse.IsOver(Rect);
            // if (prevOver && !curOver) {
            //     var w = Find.WindowStack.GetWindowAt(UI.GUIToScreenPoint(Event.current.mousePosition)); 
            //     var w2 = Find.WindowStack[Find.WindowStack.Count - 1];
            //     var ms = UI.GUIToScreenPoint(Event.current.mousePosition);
            //     // var w = Find.WindowStack[Find.WindowStack.Count - 1];
            //     // bool curOver = Rect.Contains(Event.current.mousePosition) && ! (b1 && ! b2 || (b3 || b4));
            //     Log.Message("LEAVING {0}, immediate: {1}, rect: {2}, {7} pos: {9} => {3} => {10},  rect2 : {4}, {8} w: {5} h: {6}",
            //         Research.label,
            //         w is ImmediateWindow, w?.windowRect, ms, w2.windowRect,
            //         UI.screenWidth, UI.screenHeight, w.windowRect.Contains(ms),
            //         w2.windowRect.Contains(ms), Event.current.mousePosition,
            //         GenUI.GetMouseAttachedWindowPos(w2.windowRect.width, w2.windowRect.height));
            // }
            // prevOver = curOver;

            // if clicked and not yet finished, queue up this research and all prereqs.
            if (Widgets.ButtonInvisible(Rect) && Available) {
                HandleMouseEvents();
            }
        }

        /// <summary>
        ///     Get recursive list of all incomplete prerequisites
        /// </summary>
        /// <returns>List<Node> prerequisites</Node></returns>
        public List<ResearchNode> MissingPrerequisitesRev()
        {
            var result = MissingPrerequisites();
            result.Reverse();
            return result;
        }

        // inc means "inclusive"
        public List<ResearchNode> MissingPrerequisitesInc() {
            List<ResearchNode> result = new List<ResearchNode>();
            MissingPrerequitesRec(result);
            return result;
        }

        // rev means "reverse"
        public List<ResearchNode> MissingPrerequisites() {
            List<ResearchNode> result = new List<ResearchNode>();
            foreach (var n in DirectPrerequisites().Where(n => ! n.Research.IsFinished)) {
                n.MissingPrerequitesRec(result);
            }
            return result;
        }

        public IEnumerable<ResearchNode> DirectPrerequisites() {
            return InEdges.Select(e => e.InResearch());
        }

        private void MissingPrerequitesRec(List<ResearchNode> acc) {
            if (acc.Contains(this)) {
                return;
            }
            foreach (var n in DirectPrerequisites().Where(n => !n.Research.IsFinished)) {
                n.MissingPrerequitesRec(acc);
            }
            acc.Add(this);
        }

        public override bool Completed => Research.IsFinished;
        public override bool Available => !Research.IsFinished && ( DebugSettings.godMode || (BuildingPresent() && TechprintAvailable()));

        public List<ThingDef> MissingFacilities()
        {
            return MissingFacilities( Research );
        }

        /// <summary>
        ///     Creates text version of research description and additional unlocks/prereqs/etc sections.
        /// </summary>
        /// <returns>string description</returns>
        private string GetResearchTooltipString()
        {
            // start with the descripton
            var text = new StringBuilder();
            text.AppendLine( Research.description );
            text.AppendLine();

            if ( Queue.ContainsS( this ) )
            {
                text.AppendLine( ResourceBank.String.LClickReplaceQueue );
            }
            else
            {
                text.AppendLine( ResourceBank.String.LClickReplaceQueue );
                text.AppendLine( ResourceBank.String.SLClickAddToQueue );
            }
            if ( DebugSettings.godMode )
            {
                text.AppendLine( ResourceBank.String.CLClickDebugInstant );
            }
            if (ResearchTree.HasHelpTreeLoaded) {
                text.AppendLine( ResourceBank.String.RClickForDetails );
            }

            return text.ToString();
        }

        public void DrawAt( Vector2 pos, Rect visibleRect, int painterId, bool forceDetailedMode = false )
        {
            SetRects( pos );
            Draw(visibleRect, painterId, forceDetailedMode);
            SetRects();
        }
    }
}