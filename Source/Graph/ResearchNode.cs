// ResearchNode.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq;
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
		static Dictionary<ResearchProjectDef, string[]> _missingFacilitiesCache = new Dictionary<ResearchProjectDef, string[]>();
		public bool isMatched, _available;
		public ResearchProjectDef Research;
		List<Def> _unlocks;
		Painter _currentPainter;
		HighlightReasonSet _highlightReasons = new HighlightReasonSet();
		public static readonly Dictionary<Def, List<Def>> _unlocksCache = new Dictionary<Def, List<Def>>();

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
		List<Def> Unlocks()
		{
			if (_unlocks == null) _unlocks = GetUnlockDefs(Research);
			return _unlocks;

			List<Def> GetUnlockDefs(ResearchProjectDef research)
			{
				if ( _unlocksCache.ContainsKey( research ) )
					return _unlocksCache[research];

				var unlocks = new List<Def>();

				//Was GetThingsUnlocked()
				var thingDefs = DefDatabase<ThingDef>.AllDefsListForReading;
				var length = thingDefs.Count;
				for (int i = 0; i < length; i++)
				{
					var def = thingDefs[i];
					if (def.researchPrerequisites?.Contains(research) ?? false && def.IconTexture() != null) unlocks.Add(def);
				}

				//Was GetTerrainUnlocked()
				var terrainDefs = DefDatabase<TerrainDef>.AllDefsListForReading;
				length = terrainDefs.Count;
				for (int i = 0; i < length; i++)
				{
					var def = terrainDefs[i];
					if (def.researchPrerequisites?.Contains(research) ?? false && def.IconTexture() != null) unlocks.Add(def);
				}

				//Was GetRecipesUnlocked()
				var recipeDefs = DefDatabase<RecipeDef>.AllDefsListForReading;
				length = recipeDefs.Count;
				for (int i = 0; i < length; i++)
				{
					var def = recipeDefs[i];
					if ((def.researchPrerequisite == research || def.researchPrerequisites != null && def.researchPrerequisites.Contains(research)) && 
						def.IconTexture() != null) unlocks.Add(def);
				}

				var plantDefs = DefDatabase<ThingDef>.AllDefsListForReading;
				length = plantDefs.Count;
				for (int i = 0; i < length; i++)
				{
					var def = plantDefs[i];
					if (def.plant?.sowResearchPrerequisites?.Contains(research) ?? false && def.IconTexture() != null) unlocks.Add(def);
				}

				// get unlocks for all descendant research, and remove duplicates.
				_unlocksCache.Add(research, unlocks);
				return unlocks;
			}
		}
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
		IEnumerable<Highlighting.Reason> HighlightReasons()
		{
			return _highlightReasons.Reasons();
		}
		public override Color Color
		{
			get
			{
				bool isUnmatchedInSearch = IsUnmatchedInSearch();
				bool highlighted = Highlighted();
				
				//Is it already researched and not being searched for?
				if (Research.IsFinished && (!isUnmatchedInSearch || highlighted)) return Assets.ColorCompleted[Research.techLevel];
				//Is it being highlighted?
				if (highlighted) return HighlightColor();
				//Is not what we're searching for?
				if (isUnmatchedInSearch) return Assets.ColorUnmatched[Research.techLevel];
				//Is it available for research?
				if (_available) return Assets.ColorCompleted[Research.techLevel];
				//Otherwise assume unavailable
				return Assets.ColorUnavailable[Research.techLevel];
			}
		}
		public bool IsUnmatchedInSearch()
		{
			return MainTabWindow_ResearchTree.Instance._searchActive && !isMatched;
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
			if (MainTabWindow_ResearchTree.Instance._searchActive)
			{
				return Assets.ColorUnmatched[Research.techLevel];
			}
			if (Research.IsFinished) return Assets.ColorEdgeCompleted[Research.techLevel];
			if (_available) return Assets.ColorAvailable[Research.techLevel];
			return Assets.ColorUnavailable[Research.techLevel];
		}
		public List<ResearchNode> Children()
		{
			List<ResearchNode> workingList = new List<ResearchNode>();
            List<Node> list = new List<Node>(OutNodes());
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
		string[] MissingFacilities(ResearchProjectDef research = null)
		{
			if (research == null) research = Research;
			// try get from cache
			if ( _missingFacilitiesCache.TryGetValue( research, out string[] missing ) ) return missing;

			// get list of all researches required before this
			var thisAndPrerequisites = new List<ResearchProjectDef>();
			foreach (var item in research.Ancestors()) if (item.IsFinished) thisAndPrerequisites.Add(item);
			thisAndPrerequisites.Add(research);

			// get list of all available research benches
			List<Building_ResearchBench> availableBenches = new List<Building_ResearchBench>();
			List<ThingDef> otherBenches = new List<ThingDef>();
			List<ThingDef> availableBenchDefs = new List<ThingDef>();
			foreach (var map in Find.Maps)
			{
				var length = map.listerBuildings.allBuildingsColonist.Count;
				for (int i = 0; i < length; i++)
				{
					var building = map.listerBuildings.allBuildingsColonist[i];
					if (building.def.thingClass == typeof(Building_ResearchBench))
					{
						availableBenches.Add(building as Building_ResearchBench);
						if (!availableBenchDefs.Contains(building.def)) availableBenchDefs.Add(building.def);
					}
					else if (!otherBenches.Contains(building.def)) otherBenches.Add(building.def);
				}
			}
			var workingList = new List<string>();

			// check each for prerequisites
			foreach (var rpd in thisAndPrerequisites)
			{
				//Does this research have any building or facilty requirements
				if (rpd.requiredResearchBuilding == null && rpd.requiredResearchFacilities == null) continue;

				if (rpd.requiredResearchBuilding != null && !availableBenchDefs.Contains(rpd.requiredResearchBuilding)) workingList.Add(rpd.requiredResearchBuilding.LabelCap);

				//Any facilities?
				if (rpd.requiredResearchFacilities.NullOrEmpty())
					continue;

				//Add missing facilities
				foreach (var facility in rpd.requiredResearchFacilities)
				{
					//Is a research bench linked to the facility?
					foreach (var bench in availableBenches) if (HasFacility(bench, facility)) goto facilityFound;
					//Or is it a standalone facility?
					foreach (var bench in otherBenches) if (bench == facility) goto facilityFound;
					//Not found, add
					workingList.Add(facility.LabelCap);
					facilityFound:;
				}
			}

			// add to cache
			var missingFacilities = workingList.Distinct().ToArray();
			_missingFacilitiesCache.Add( research, missingFacilities );
			return missingFacilities;

			bool HasFacility(Building_ResearchBench building, ThingDef facility )
			{
				var comp = building.GetComp<CompAffectedByFacilities>();
				if ( comp == null )
					return false;

				if ( comp.LinkedFacilitiesListForReading.Select( f => f.def ).Contains( facility ) )
					return true;

				return false;
			}
		}
		public override int DefaultPriority()
		{
			return _inEdges.Count + _outEdges.Count;
		}
		void DrawProgressBar()
		{
			// grey out center to create a progress bar effect, completely greying out research not started.
			if ( (MainTabWindow_ResearchTree.Instance._searchActive && isMatched) || !IsUnmatchedInSearch() && _available || Highlighted())
			{
				var progressBarRect = Rect.ContractedBy(3f);

				//was DrawProgressBarImpl(progressBarRect);
				progressBarRect.xMin += Research.ProgressPercent * progressBarRect.width;
				FastGUI.DrawTextureFast(progressBarRect, BaseContent.WhiteTex, Assets.ColorAvailable[Research.techLevel]);
			}
		}
		
		bool hasFacilitiesCache = false;
		int frames = 119;
		void HandleTooltips()
		{
			if (PainterIs(Painter.Drag)) return;
			Text.WordWrap = true;

			if (!ModSettings_ResearchPowl.disableShortcutManual)
			{
				TooltipHandler.TipRegion(Rect, ShortcutManualTooltip, Research.shortHash + 2);
			}
			// attach description and further info to a tooltip
			if (!Research.TechprintRequirementMet)
			{
				TooltipHandler.TipRegion(Rect, "InsufficientTechprintsApplied".Translate(Research.TechprintsApplied, Research.TechprintCount));
			}
			if (++frames == 120)
			{
				frames = 0;
				hasFacilitiesCache = Research.requiredResearchBuilding == null || Research.PlayerHasAnyAppropriateResearchBench;
			}
			if (!hasFacilitiesCache || (Research.requiredResearchFacilities != null && Research.requiredResearchBuilding == null))
			{
				var facilityString = MissingFacilities();
				if (!facilityString.NullOrEmpty()) TooltipHandler.TipRegion(Rect, ResourceBank.String.MissingFacilities( string.Join(", ", facilityString)));
			}
			if (!CompatibilityHooks.PassCustomUnlockRequirements(Research))
			{
				foreach (var prompt in CompatibilityHooks.CustomUnlockRequirementPrompts(Research))
				{
					TooltipHandler.TipRegion(Rect, prompt);
				}
			}
			if (Research.techLevel > Current.gameInt.worldInt.factionManager.ofPlayer.def.techLevel)
			{
				TooltipHandler.TipRegion(Rect, TechLevelTooLowTooltip, Research.shortHash + 3);
			}
			if (!Research.PlayerMechanitorRequirementMet)
			{
				var tmp = "MissingRequiredMechanitor".Translate();
				TooltipHandler.TipRegion(Rect, tmp);
			}
			if (!Research.StudiedThingsRequirementsMet)
			{
				var workingList = new List<string>();
				foreach (var item in Research.requiredStudied) workingList.Add("NotStudied".Translate(item.LabelCap).ToString());
				TooltipHandler.TipRegion(Rect, workingList.ToLineList("", false));
			}

			TooltipHandler.TipRegion(Rect, GetResearchTooltipString, Research.shortHash);

			if (ModSettings_ResearchPowl.progressTooltip && ProgressWorthDisplaying() && !Research.IsFinished)
			{
				TooltipHandler.TipRegion(Rect, string.Format("Progress: {0}", ProgressString()));
			}
		}
		string ShortcutManualTooltip()
		{
			if (Event.current.shift)
			{
				StringBuilder builder = new StringBuilder();
				if (PainterIs(Painter.Queue)) builder.AppendLine(ResourceBank.String.LClickRemoveFromQueue);
				else
				{
					if (_available)
					{
						builder.AppendLine(ResourceBank.String.LClickReplaceQueue);
						builder.AppendLine(ResourceBank.String.SLClickAddToQueue);
						builder.AppendLine(ResourceBank.String.ALClickAddToQueue);
					}
					if (DebugSettings.godMode) builder.AppendLine(ResourceBank.String.CLClickDebugInstant);
				}
				if (_available) builder.AppendLine(ResourceBank.String.Drag);

				builder.AppendLine(ResourceBank.String.RClickHighlight);
				builder.AppendLine(ResourceBank.String.RClickIcon);
				return builder.ToString();
			}
			return ResourceBank.String.ShiftForShortcutManual;
		}
		string TechLevelTooLowTooltip()
		{
			var techlevel = Faction.OfPlayer.def.techLevel;
			return ResourceBank.String.TechLevelTooLow(techlevel, Research.CostFactor(techlevel), (int) Research.baseCost);
		}
		IEnumerable<ResearchProjectDef> OtherLockedPrerequisites(List<ResearchProjectDef> ps)
		{
			if (ps == null) yield break;
			foreach (var item in ps)
			{
				if (!item.IsFinished && item != Research) yield return item;
			}
		}
		string OtherPrereqTooltip(List<ResearchProjectDef> ps)
		{
			if (ps.NullOrEmpty()) return "";
			return ResourceBank.String.OtherPrerequisites(String.Join(", ", ps.Distinct().Select(p => p.LabelCap)));
		}
		string UnlockItemTooltip(Def def)
		{
			string unlockTooltip = "";
			string otherPrereqTooltip = "";
			if (def is TerrainDef terrainDef)
			{
				unlockTooltip += ResourceBank.String.AllowsBuildingX(def.LabelCap);
				otherPrereqTooltip += OtherPrereqTooltip(new List<ResearchProjectDef>(OtherLockedPrerequisites(terrainDef.researchPrerequisites)));
			}
			else if (def is RecipeDef recipeDef)
			{
				unlockTooltip += ResourceBank.String.AllowsCraftingX(def.LabelCap);
				otherPrereqTooltip += OtherPrereqTooltip(new List<ResearchProjectDef>(OtherLockedPrerequisites(recipeDef.researchPrerequisites)));
			}
			else if (def is ThingDef thingDef)
			{
				List<ResearchProjectDef> plantPrerequisites = thingDef.plant?.sowResearchPrerequisites ?? new List<ResearchProjectDef>();
				if (plantPrerequisites.Contains(Research))
				{
					unlockTooltip += ResourceBank.String.AllowsPlantingX(def.LabelCap);
					otherPrereqTooltip += OtherPrereqTooltip(new List<ResearchProjectDef>(OtherLockedPrerequisites(plantPrerequisites)));
				}
				else
				{
					unlockTooltip += ResourceBank.String.AllowsBuildingX(def.LabelCap);
					OtherPrereqTooltip(new List<ResearchProjectDef>(OtherLockedPrerequisites(((BuildableDef)def).researchPrerequisites)));
				}
			}
			else unlockTooltip += ResourceBank.String.AllowGeneralX(def.LabelCap);
			
			return otherPrereqTooltip == ""
				? unlockTooltip
				: unlockTooltip + "\n\n" + otherPrereqTooltip;
		}
		FloatMenu MakeInfoMenuFromDefs(List<Def> defs, int skip = 0)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();

			var length = defs.Count;
			for (int i = skip; i < length; i++)
			{
				var def = defs[i];
			
				Texture2D icon = def.IconTexture();
				Dialog_InfoCard.Hyperlink hyperlink = new Dialog_InfoCard.Hyperlink(def);
			 
				options.Add(new FloatMenuOption(
					def.label, () => hyperlink.ActivateHyperlink(), icon, def.IconColor(),
					MenuOptionPriority.Default,
					rect => TooltipHandler.TipRegion(rect, () => UnlockItemTooltip(def), def.shortHash + Research.shortHash)));
			}
			return new FloatMenu(options);
		}
		void IconActions(bool draw)
		{
			// handle only right click
			if (!draw && !(Event.current.type == EventType.MouseDown && Event.current.button == 1)) return;

			var unlocks = Unlocks();
			var length = unlocks.Count;
			for (var i = 0; i < length; ++i)
			{
				var thisIconRect = IconsRect;
				var iconRect = new Rect(
					thisIconRect.xMax - ( i + 1 ) * ( IconSize.x + 4f ),
					thisIconRect.yMin + ( thisIconRect.height - IconSize.y ) / 2f,
					IconSize.x,
					IconSize.y );

				if (iconRect.xMin - IconSize.x < thisIconRect.xMin && i + 1 < unlocks.Count)
				{
					// stop the loop if we're about to overflow and have 2 or more unlocks yet to print.
					iconRect.x = thisIconRect.x + 4f;

					if (draw)
					{
						FastGUI.DrawTextureFast(iconRect, Assets.MoreIcon, Assets.colorWhite);

						if (!PainterIs(Painter.Drag))
						{
							var tip = string.Join("\n", unlocks.GetRange(i, unlocks.Count - i).Select(p => p.LabelCap).ToArray());
							TooltipHandler.TipRegion( iconRect, tip );
						}
					}
					else if (!draw && Mouse.IsOver(iconRect) && Find.WindowStack.FloatMenu == null)
					{
						var floatMenu = MakeInfoMenuFromDefs(unlocks, i);
						Find.WindowStack.Add(floatMenu);
						Event.current.Use();
					}
					break;
				}
				var def = unlocks[i];

				if (draw)
				{
					DrawColouredIcon(def, iconRect);
					if (!PainterIs(Painter.Drag))
					{
						TooltipHandler.TipRegion(iconRect, () => UnlockItemTooltip(def), def.shortHash + Research.shortHash);
					}
				}
				else if (Mouse.IsOver(iconRect))
				{
					Dialog_InfoCard.Hyperlink link = new Dialog_InfoCard.Hyperlink(def);
					link.ActivateHyperlink();
					Event.current.Use();
					break;
				}
			}

			void DrawColouredIcon(Def def, Rect canvas)
			{
				FastGUI.DrawTextureFast(canvas, def.IconTexture(), Assets.colorWhite);
				GUI.color = Color.white;
			}
		}
		void DrawNodeDetailMode(bool mouseOver)
		{
			Text.Anchor = TextAnchor.UpperLeft;
			Text.WordWrap = true;
			Text.Font = _largeLabel ? GameFont.Tiny : GameFont.Small;
			Widgets.Label( LabelRect, Research.LabelCap );

			FastGUI.DrawTextureFast(CostIconRect, !Research.IsFinished && !_available ? Assets.Lock : Assets.ResearchIcon, Assets.colorWhite);

			Color savedColor = GUI.color;
			Color numberColor;
			float numberToDraw;
			if (ModSettings_ResearchPowl.alwaysDisplayProgress && ProgressWorthDisplaying() || SwitchToProgress())
			{
				if (Research.IsFinished)
				{
					numberColor = Color.cyan;
					numberToDraw = 0;
				}
				else
				{
					numberToDraw = Research.CostApparent - Research.ProgressApparent;
					numberColor = Color.green;
				}
			}
			else
			{
				numberToDraw = Research.CostApparent;
				numberColor = savedColor;
			}
			if (IsUnmatchedInSearch() && (!Highlighted())) numberColor = Color.gray;

			GUI.color = numberColor;
			Text.Anchor = TextAnchor.UpperRight;

			Text.Font = NumericalFont(numberToDraw);
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
			return !( (Research.IsFinished || _available) && (Highlighted() || !IsUnmatchedInSearch()));
		}
		void DrawNode(bool detailedMode, bool mouseOver)
		{
			HandleTooltips();

			//was DrawBackground(mouseOver);
			if (mouseOver) FastGUI.DrawTextureFast(Rect, Assets.ButtonActive, Color);
			else FastGUI.DrawTextureFast(Rect, Assets.Button, Color);

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
			if (! mouseOver || Event.current.shift || Event.current.alt) return;
			if (evt.type == EventType.MouseDown && evt.button == 0 && _available)
			{
				MainTabWindow_ResearchTree.Instance.StartDragging(this, _currentPainter);
				if (PainterIs(Painter.Queue)) Queue.NotifyNodeDraggedS();
				Highlight(Highlighting.Reason.Focused);
				Event.current.Use();
			}
			else if (evt.type == EventType.MouseUp && evt.button == 0 && PainterIs(Painter.Tree))
			{
				var tab = MainTabWindow_ResearchTree.Instance;
				if (tab.draggedNode == this && tab.DraggingTime() < Constants.DraggingClickDelay)
				{
					LeftClick();
					tab.StopDragging();
					Event.current.Use();
				}
			}
		}
		bool DetailMode()
		{
			return PainterIs(Painter.Queue) || PainterIs(Painter.Drag) || MainTabWindow_ResearchTree.Instance._zoomLevel < DetailedModeZoomLevelCutoff;
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
				_available = GetAvailable();
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
			if (Research.IsFinished || !_available) return false;

			if (DebugSettings.godMode && Event.current.control)
			{
				Queue._instance.Finish(this);
				Messages.Message(ResourceBank.String.FinishedResearch(Research.LabelCap), MessageTypeDefOf.SilentInput, false);
				Queue.Notify_InstantFinished();
			}
			else if (!Queue._instance._queue.Contains(this))
			{
				if (Event.current.shift)
				{
					Queue._instance.Append(this);
					Queue.NewUndoState();
				}
				else if (Event.current.alt)
				{
					Queue._instance.Prepend(this);
            		Queue.NewUndoState();
				}
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
				foreach (var n in DirectPrerequisites()) if (!n.Research.IsFinished) n.MissingPrerequitesRec(result);
			}
			return result;
		}
		public IEnumerable<ResearchNode> DirectPrerequisites()
		{
			foreach (var n in _inEdges) yield return n.InResearch();
		}
		void MissingPrerequitesRec(List<ResearchNode> acc)
		{
			if (acc.Contains(this)) return;
			if (!Research.PrerequisitesCompleted)
			{
				foreach (var n in DirectPrerequisites()) if (!n.Research.IsFinished) n.MissingPrerequitesRec(acc);
			}
			acc.Add(this);
		}
		public bool GetAvailable()
		{
			return !Research.IsFinished && (DebugSettings.godMode || (
				((Research.requiredResearchBuilding == null) || Research.PlayerHasAnyAppropriateResearchBench) && 
				Research.TechprintRequirementMet && 
				Research.PlayerMechanitorRequirementMet && 
				Research.StudiedThingsRequirementsMet && 
				AllowedTechlevel(Research.techLevel) && 
				CompatibilityHooks.PassCustomUnlockRequirements(Research)
			));

			// special rules for tech-level availability
			bool AllowedTechlevel(TechLevel level)
			{
				if ((int)level > ModSettings_ResearchPowl.maxAllowedTechLvl) return false;
				//Hard-coded mod hooks
				if (Current.gameInt?.storyteller.def.defName == "VFEM_Maynard") return level >= TechLevel.Animal && level <= TechLevel.Medieval;
				return true;
			}
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