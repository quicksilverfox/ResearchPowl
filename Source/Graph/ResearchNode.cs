// ResearchNode.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using static ResearchPowl.Constants;

namespace ResearchPowl
{
	public enum Painter { Tree = 0, Queue = 1, Drag = 2 }
	public class ResearchNode : Node
	{
		static readonly Dictionary<ResearchProjectDef, bool> _buildingPresentCache = new Dictionary<ResearchProjectDef, bool>();
		static readonly Dictionary<ResearchProjectDef, List<ThingDef>> _missingFacilitiesCache = new Dictionary<ResearchProjectDef, List<ThingDef>>();
		public bool isMatched, _available;
		private List<Def> _unlocks;
		private Painter _currentPainter;

		public ResearchProjectDef Research;
		public ResearchNode(ResearchProjectDef research)
		{
			Research = research;

			// initialize position at vanilla y position, leave x at zero - we'll determine this ourselves
			_pos = new Vector2( 0, research.researchViewY + 1 );
		}
		public bool PainterIs(Painter p)
		{
			return p == _currentPainter;
		}

		private List<Def> Unlocks()
		{
			if (_unlocks == null) _unlocks = Research.GetUnlockDefs();
			return _unlocks;
		}

		void UpdateAvailable()
		{
			_available = GetAvailable();
		}

		private HighlightReasonSet _highlightReasons = new HighlightReasonSet();

		public override bool Highlighted()
		{
			return _highlightReasons.Highlighted();
		}
		public void Highlight(Highlighting.Reason r)
		{
			_highlightReasons.Highlight(r);
		}
		Color HighlightColor()
		{
			return Highlighting.Color(
				_highlightReasons.Current(), Research.techLevel);
		}
		public bool Unhighlight(Highlighting.Reason r)
		{
			return _highlightReasons.Unhighlight(r);
		}
		public IEnumerable<Highlighting.Reason> HighlightReasons()
		{
			return _highlightReasons.Reasons();
		}
		public override Color Color
		{
			get
			{
				if (Research.IsFinished && (!IsUnmatchedInSearch() || Highlighted())) {
					return Assets.ColorCompleted[Research.techLevel];
				}
				if (Highlighted()) {
					return HighlightColor();
				}
				if (IsUnmatchedInSearch()) {
					return Assets.ColorUnmatched[Research.techLevel];
				}
				if (Available()) {
					return Assets.ColorCompleted[Research.techLevel];
				}
				return Assets.ColorUnavailable[Research.techLevel];
			}
		}
		public bool IsUnmatchedInSearch()
		{
			// return highlightReasons.Contains(HL.Reason.SearchUnmatched);
			return MainTabWindow_ResearchTree.Instance.SearchActive() && !isMatched;
		}
		public bool IsMatchedInSearch()
		{
			// return highlightReasons.Contains(HL.Reason.SearchMatched);
			return MainTabWindow_ResearchTree.Instance.SearchActive() && isMatched;
		}
		public bool HighlightInEdge(ResearchNode from)
		{
			foreach (var r1 in HighlightReasons()) {
				foreach (var r2 in from.HighlightReasons()) {
					if (Highlighting.Similar(r1, r2)) {
						return true;
					}
				}
			}
			return false;
		}
		public override Color InEdgeColor(ResearchNode from)
		{
			if (HighlightInEdge(from))
				return Assets.NormalHighlightColor;
			if (MainTabWindow_ResearchTree.Instance.SearchActive())
			{
				return Assets.ColorUnmatched[Research.techLevel];
			}
			if (Research.IsFinished)
				return Assets.ColorEdgeCompleted[Research.techLevel];
			if (Available())
				return Assets.ColorAvailable[Research.techLevel];
			return Assets.ColorUnavailable[Research.techLevel];
		}
		public List<ResearchNode> Children()
		{
			List<ResearchNode> workingList = new List<ResearchNode>();
            List<Node> list = new List<Node>(OutNodes);
            var length = list.Count;
            for (int i = 0; i < length; i++)
            {
                var node = list[i];
                if (node is DummyNode dNode) workingList.AddRange(dNode.Child());
            }
            return workingList; 
		}
		public override string Label => Research.LabelCap;
		public static void ClearCaches()
		{
			_buildingPresentCache.Clear();
			_missingFacilitiesCache.Clear();
		}
		public int Matches( string query )
		{
			var culture = CultureInfo.CurrentUICulture;
			query = query.ToLower( culture );

			if ( Research.LabelCap.RawText.ToLower( culture ).Contains( query ) )
				return 1;
			if (Unlocks().Any(
				unlock => unlock.LabelCap.RawText.ToLower( culture ).Contains( query )))
				return 2;
			if ((Research.modContentPack?.Name.ToLower(culture) ?? "").Contains(query) ) {
				return 3;
			}
			if (ModSettings_ResearchPowl.searchByDescription) {
				if (Research.description.ToLower(culture).Contains(query))
					return 4;
			}
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
		public override int DefaultPriority()
		{
			return InEdges.Count() + OutEdges.Count();
		}
		void DrawProgressBarImpl(Rect rect)
		{
			//GUI.color = Assets.ColorAvailable[Research.techLevel];
			rect.xMin += Research.ProgressPercent * rect.width;
			//GUI.DrawTexture(rect, BaseContent.WhiteTex);
			FastGUI.DrawTextureFast(rect, BaseContent.WhiteTex, Assets.ColorAvailable[Research.techLevel]);
		}
		void DrawBackground(bool mouseOver)
		{
			// researches that are completed or could be started immediately, and that have the required building(s) available
			//GUI.color = Color;

			if (mouseOver)
				//GUI.DrawTexture(Rect, Assets.ButtonActive);
				FastGUI.DrawTextureFast(Rect, Assets.ButtonActive, Color);
			else
				//GUI.DrawTexture(Rect, Assets.Button);
				FastGUI.DrawTextureFast(Rect, Assets.Button, Color);
		}
		void DrawProgressBar()
		{
			// grey out center to create a progress bar effect, completely greying out research not started.
			if ( IsMatchedInSearch()
			   || !IsUnmatchedInSearch() && _available
			   || Highlighted())
			{
				var progressBarRect = Rect.ContractedBy( 3f );
				DrawProgressBarImpl(progressBarRect);
			}
		}
		void HandleTooltips()
		{
			if (PainterIs(Painter.Drag)) return;
			Text.WordWrap = true;

			if (!ModSettings_ResearchPowl.disableShortcutManual)
			{
				TooltipHandler.TipRegion(Rect, ShortcutManualTooltip, Research.GetHashCode() + 2);
			}
			// attach description and further info to a tooltip
			if (!Research.TechprintRequirementMet)
			{
				TooltipHandler.TipRegion(Rect, "InsufficientTechprintsApplied".Translate(Research.TechprintsApplied, Research.TechprintCount));
			}
			if (!Research.PlayerHasAnyAppropriateResearchBench)
			{
				TooltipHandler.TipRegion(Rect, ResourceBank.String.MissingFacilities( string.Join(", ", MissingFacilities().Select( td => td.LabelCap ).ToArray())));
			}
			if (!CompatibilityHooks.PassCustomUnlockRequirements(Research))
			{
				foreach (var prompt in CompatibilityHooks.CustomUnlockRequirementPrompts(Research))
				{
					TooltipHandler.TipRegion(Rect, prompt);
				}
			}
			if (Research.techLevel > Faction.OfPlayer.def.techLevel)
			{
				TooltipHandler.TipRegion(Rect, TechLevelTooLowTooltip, Research.GetHashCode() + 3);
			}

			if (!Research.PlayerMechanitorRequirementMet)
			{
				var tmp = "MissingRequiredMechanitor".Translate();
				TooltipHandler.TipRegion(Rect, tmp);
			}
			if (!Research.StudiedThingsRequirementsMet)
			{
				var tmp = (from t in Research.requiredStudied select "NotStudied".Translate(t.LabelCap).ToString()).ToLineList("", false);
				TooltipHandler.TipRegion(Rect, tmp);
			}

			TooltipHandler.TipRegion(Rect, GetResearchTooltipString, Research.GetHashCode());

			if (ModSettings_ResearchPowl.progressTooltip && ProgressWorthDisplaying() && !Research.IsFinished)
			{
				TooltipHandler.TipRegion(Rect, string.Format("Progress: {0}", ProgressString()));
			}
		}
		string ShortcutManualTooltip()
		{
			if (Event.current.shift) {
				StringBuilder builder = new StringBuilder();
				if (PainterIs(Painter.Queue)) {
					builder.AppendLine(ResourceBank.String.LClickRemoveFromQueue);
				} else {
					if (Available()) {
						builder.AppendLine(ResourceBank.String.LClickReplaceQueue);
						builder.AppendLine(ResourceBank.String.SLClickAddToQueue);
						builder.AppendLine(ResourceBank.String.ALClickAddToQueue);
					}
					if (DebugSettings.godMode) {
						builder.AppendLine(ResourceBank.String.CLClickDebugInstant);
					}
				}
				if (Available()) {
					builder.AppendLine(ResourceBank.String.Drag);
				}
				builder.AppendLine(ResourceBank.String.RClickHighlight);
				builder.AppendLine(ResourceBank.String.RClickIcon);
				return builder.ToString();
			} else {
				return ResourceBank.String.ShiftForShortcutManual;
			}
		}
		string TechLevelTooLowTooltip()
		{
			var techlevel = Faction.OfPlayer.def.techLevel;
			return ResourceBank.String.TechLevelTooLow(
				techlevel, Research.CostFactor(techlevel), (int) Research.baseCost);
		}
		IEnumerable<ResearchProjectDef> OtherLockedPrerequisites(IEnumerable<ResearchProjectDef> ps)
		{
			if (ps == null) return new List<ResearchProjectDef>();
			return ps.Where(p => !p.IsFinished && p != Research);
		}
		string OtherPrereqTooltip(List<ResearchProjectDef> ps)
		{
			if (ps.NullOrEmpty()) {
				return "";
			}
			return ResourceBank.String.OtherPrerequisites(
				String.Join(", ", ps.Distinct().Select(p => p.LabelCap)));
		}
		string UnlockItemTooltip(Def def)
		{
			string unlockTooltip = "";
			string otherPrereqTooltip = "";
			if (def is TerrainDef) {
				unlockTooltip += ResourceBank.String.AllowsBuildingX(def.LabelCap);
				otherPrereqTooltip +=
					OtherPrereqTooltip(OtherLockedPrerequisites(
						(def as TerrainDef).researchPrerequisites).ToList());
			} else if (def is RecipeDef) {
				unlockTooltip += ResourceBank.String.AllowsCraftingX(def.LabelCap);
				otherPrereqTooltip +=
					OtherPrereqTooltip(OtherLockedPrerequisites(
						(def as RecipeDef).researchPrerequisites).ToList());
			} else if (def is ThingDef) {
				ThingDef thing = def as ThingDef;
				List<ResearchProjectDef> plantPrerequisites =
					thing.plant?.sowResearchPrerequisites ?? new List<ResearchProjectDef>();
				if (plantPrerequisites.Contains(Research)) {
					unlockTooltip += ResourceBank.String.AllowsPlantingX(def.LabelCap);
					otherPrereqTooltip +=
						OtherPrereqTooltip(
							OtherLockedPrerequisites(plantPrerequisites).ToList());
				} else {
					unlockTooltip += ResourceBank.String.AllowsBuildingX(def.LabelCap);
					OtherPrereqTooltip(OtherLockedPrerequisites(
						(def as BuildableDef).researchPrerequisites).ToList());
				}
			} else {
				unlockTooltip += ResourceBank.String.AllowGeneralX(def.LabelCap);
			}
			string tooltip = otherPrereqTooltip == ""
				? unlockTooltip
				: unlockTooltip + "\n\n" + otherPrereqTooltip;
			return tooltip;
		}
		FloatMenu MakeInfoMenuFromDefs(IEnumerable<Def> defs)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (var def in defs) {
				Texture2D icon = def.IconTexture();
				Dialog_InfoCard.Hyperlink hyperlink = new Dialog_InfoCard.Hyperlink(def);
			 
				options.Add(new FloatMenuOption(
					def.label, () => hyperlink.ActivateHyperlink(), icon, def.IconColor(),
					MenuOptionPriority.Default,
					rect => TooltipHandler.TipRegion(
						rect, () => UnlockItemTooltip(def),
						def.GetHashCode() + Research.GetHashCode())));
			}
			return new FloatMenu(options);
		}
		void IconActions(bool draw)
		{
			// handle only right click
			if (!draw && !(Event.current.type == EventType.MouseDown && Event.current.button == 1)) {
				return;
			}
			var unlocks = Unlocks();
			for (var i = 0; i < unlocks.Count; ++i) {
				var iconRect = new Rect(
					IconsRect.xMax - ( i                + 1 )          * ( IconSize.x + 4f ),
					IconsRect.yMin + ( IconsRect.height - IconSize.y ) / 2f,
					IconSize.x,
					IconSize.y );

				if ( iconRect.xMin - IconSize.x < IconsRect.xMin
				   &&   i          + 1          < unlocks.Count ) {
					// stop the loop if we're about to overflow and have 2 or more unlocks yet to print.
					iconRect.x = IconsRect.x + 4f;

					if (draw) {
						FastGUI.DrawTextureFast(iconRect, Assets.MoreIcon, Assets.colorWhite);
						//GUI.DrawTexture(iconRect, Assets.MoreIcon, ScaleMode.ScaleToFit);
						if (!PainterIs(Painter.Drag)) {
							var tip = string.Join(
								"\n",
								unlocks.GetRange(i, unlocks.Count - i).Select(p => p.LabelCap).ToArray());
							TooltipHandler.TipRegion( iconRect, tip );
						}
					} else if
						(!draw && Mouse.IsOver(iconRect)
						&& Find.WindowStack.FloatMenu == null) {
						var floatMenu = MakeInfoMenuFromDefs(unlocks.Skip(i));
						Find.WindowStack.Add(floatMenu);
						Event.current.Use();
					}
					break;
				}
				var def = unlocks[i];

				if (draw) {
					def.DrawColouredIcon( iconRect );
					if (! PainterIs(Painter.Drag)) {
						TooltipHandler.TipRegion(
							iconRect, () => UnlockItemTooltip(def),
							def.GetHashCode() + Research.GetHashCode());
					}
				} else if (Mouse.IsOver(iconRect)) {
					Dialog_InfoCard.Hyperlink link = new Dialog_InfoCard.Hyperlink(def);
					link.ActivateHyperlink();
					Event.current.Use();
					break;
				}
			}
		}
		void DrawNodeDetailMode(bool mouseOver)
		{
			Text.Anchor   = TextAnchor.UpperLeft;
			Text.WordWrap = true;
			Text.Font     = _largeLabel ? GameFont.Tiny : GameFont.Small;
			Widgets.Label( LabelRect, Research.LabelCap );

			FastGUI.DrawTextureFast(CostIconRect, !Research.IsFinished && !Available() ? Assets.Lock : Assets.ResearchIcon, Assets.colorWhite);

			Color savedColor = GUI.color;
			Color numberColor;
			float numberToDraw;
			if (ModSettings_ResearchPowl.alwaysDisplayProgress && ProgressWorthDisplaying() || SwitchToProgress()) {
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
			if (IsUnmatchedInSearch() && (! Highlighted())) {
				numberColor = Color.gray;
			}
			GUI.color = numberColor;
			Text.Anchor = TextAnchor.UpperRight;

			Text.Font   = NumericalFont(numberToDraw);
			Widgets.Label(CostLabelRect, numberToDraw.ToStringByStyle(ToStringStyle.Integer));
			GUI.color = savedColor;

			IconActions(true);
		}
		string ProgressString()
		{
			return string.Format("{0} / {1}",
				Research.ProgressApparent.ToStringByStyle(ToStringStyle.Integer),
				Research.CostApparent.ToStringByStyle(ToStringStyle.Integer));
		}
		bool ProgressWorthDisplaying()
		{
			return Research.ProgressApparent > 0;
		}
		void DrawNodeZoomedOut(bool mouseOver)
		{
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
		bool ShouldGreyOutText()
		{
			return !
				(  (Research.IsFinished || Available())
				&& (Highlighted() || !IsUnmatchedInSearch()));
		}
		void DrawNode(bool detailedMode, bool mouseOver)
		{
			HandleTooltips();

			DrawBackground(mouseOver);
			DrawProgressBar();

			// draw the research label
			if (ShouldGreyOutText()) GUI.color = Color.grey;
			else GUI.color = Color.white;

			if (detailedMode) DrawNodeDetailMode(mouseOver);
			else DrawNodeZoomedOut(mouseOver);
			Text.WordWrap = true;
		}
		public static GameFont NumericalFont(float number)
		{
			return number >= 1000000 ? GameFont.Tiny : GameFont.Small;
		}
		public bool SwitchToProgress()
		{
			return Tree.DisplayProgressState && ProgressWorthDisplaying();
		}
		public bool MouseOver(Vector2 mousePos)
		{
			return Rect.Contains(mousePos);
		}
		void HandleDragging(bool mouseOver)
		{
			var evt = Event.current;
			if (! mouseOver || Event.current.shift || Event.current.alt) {
				return;
			}
			if (evt.type == EventType.MouseDown && evt.button == 0 && Available()) {
				MainTabWindow_ResearchTree.Instance.StartDragging(this, _currentPainter);
				if (PainterIs(Painter.Queue)) {
					Queue.NotifyNodeDraggedS();
				}
				Highlight(Highlighting.Reason.Focused);
				Event.current.Use();
			} else if (evt.type == EventType.MouseUp && evt.button == 0 && PainterIs(Painter.Tree)) {
				var tab = MainTabWindow_ResearchTree.Instance;
				if (tab.draggedNode == this && tab.DraggingTime() < Constants.DraggingClickDelay) {
					LeftClick();
					tab.StopDragging();
					Event.current.Use();
				}
			}
		}
		private bool DetailMode()
		{
			return PainterIs(Painter.Queue) || PainterIs(Painter.Drag) || MainTabWindow_ResearchTree.Instance.ZoomLevel < DetailedModeZoomLevelCutoff;
		}
		/// <summary>
		///     Draw the node, including interactions.
		/// </summary>
		public override void Draw(Rect visibleRect, Painter painter)
		{
			_currentPainter = painter;
			var mouseOver = Mouse.IsOver(Rect);

			if (Event.current.type == EventType.Repaint)
			{
				UpdateAvailable();
				DrawNode(DetailMode(), mouseOver);
			}

			if (PainterIs(Painter.Drag)) return;
			if (DetailMode()) IconActions(false);
			HandleDragging(mouseOver);

			// if clicked and not yet finished, queue up this research and all prereqs.
			if (!MainTabWindow_ResearchTree.Instance.IsDraggingNode() && Widgets.ButtonInvisible(Rect))
			{
				if (Event.current.button == 0) LeftClick();
				else if (Event.current.button == 1) RightClick();
			}
		}
		public bool LeftClick() {
			if (Research.IsFinished || !Available()) return false;

			if (DebugSettings.godMode && Event.current.control)
			{
				Queue.FinishS(this);
				Messages.Message(ResourceBank.String.FinishedResearch(Research.LabelCap), MessageTypeDefOf.SilentInput, false);
				Queue.Notify_InstantFinished();
			}
			else if (!Queue.ContainsS(this))
			{
				if (Event.current.shift) Queue.AppendS(this);
				else if (Event.current.alt) Queue.PrependS(this);
				else Queue.ReplaceS(this);
			}
			else Queue.RemoveS(this);

			return true;
		}
		bool RightClick()
		{
			SoundDefOf.Click.PlayOneShotOnCamera();
			Tree.HandleFixedHighlight(this);
			if (_currentPainter == Painter.Queue) MainTabWindow_ResearchTree.Instance.CenterOn(this);

			return true;
		}
		// inc means "inclusive"
		public List<ResearchNode> MissingPrerequisitesInc()
		{
			List<ResearchNode> result = new List<ResearchNode>();
			MissingPrerequitesRec(result);
			return result;
		}
		public List<ResearchNode> MissingPrerequisites()
		{
			List<ResearchNode> result = new List<ResearchNode>();
			if (!Research.PrerequisitesCompleted)
			{
				foreach (var n in DirectPrerequisites().Where(n => ! n.Research.IsFinished))
				{
					n.MissingPrerequitesRec(result);
				}
			}
			return result;
		}
		public IEnumerable<ResearchNode> DirectPrerequisites()
		{
			foreach (var n in InEdges) yield return n.InResearch();
		}
		public IEnumerable<ResearchNode> DirectChildren()
		{
			foreach (var n in OutEdges) yield return n.OutResearch();
		}
		void MissingPrerequitesRec(List<ResearchNode> acc)
		{
			if (acc.Contains(this)) return;
			if (!Research.PrerequisitesCompleted)
			{
				foreach (var n in DirectPrerequisites().Where(n => !n.Research.IsFinished)) n.MissingPrerequitesRec(acc);
			}
			acc.Add(this);
		}
		public bool GetAvailable()
		{
			return !Research.IsFinished && (DebugSettings.godMode || (
				(Research.requiredResearchBuilding == null || Research.PlayerHasAnyAppropriateResearchBench) && 
				Research.TechprintRequirementMet && 
				Research.PlayerMechanitorRequirementMet && 
				Research.StudiedThingsRequirementsMet && 
				MainTabWindow_ResearchTree.AllowedTechlevel(Research.techLevel) && 
				CompatibilityHooks.PassCustomUnlockRequirements(Research)
			));
		}
		public bool Available() {
			return _available;
		}
		public List<ThingDef> MissingFacilities()
		{
			return MissingFacilities( Research );
		}
		/// <summary>
		///     Creates text version of research description and additional unlocks/prereqs/etc sections.
		/// </summary>
		/// <returns>string description</returns>
		string GetResearchTooltipString()
		{
			// start with the descripton
			var text = new StringBuilder();
			text.AppendLine(Research.description);
			text.AppendLine();
			text.Append(ResourceBank.String.TechLevelOfResearch + Research.techLevel.ToStringHuman().CapitalizeFirst());

			return text.ToString();
		}
		public void DrawAt(Vector2 pos, Rect visibleRect, Painter painter, bool deferRectReset = false)
		{
			SetRects(pos);
			if (IsVisible(visibleRect)) Draw(visibleRect, painter);
			if (!deferRectReset) SetRects();
		}
	}
}