// MainTabWindow_ResearchTree.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using static ResearchPowl.Constants;

namespace ResearchPowl
{
	public class MainTabWindow_ResearchTree : MainTabWindow
	{
		internal static Vector2 _scrollPosition = Vector2.zero, _mousePosition = -Vector2.zero, absoluteMousePos;
		public Vector2 draggedPosition;
		bool _dragging, _viewRect_InnerDirty = true, _viewRectDirty = true, _searchActive;
		float _zoomLevel = 1f, startDragging, lastSearchChangeTime = 0;
		public static float SearchResponseDelay = 0.3f;
		string _prevQuery = "", _curQuery = "";
		Rect _treeRect, _baseViewRect, _baseViewRect_Inner, _viewRect, _viewRect_Inner, searchRect;
		List<ResearchNode> _searchResults;
		IntVec2 currentTreeSize = new IntVec2(0, 0);
		Matrix4x4 originalMatrix;
		public ResearchNode draggedNode = null;
		public Painter draggingSource;

		public Rect VisibleRect => new Rect(_scrollPosition.x, _scrollPosition.y, ViewRect_Inner().width, ViewRect_Inner().height );
		public float ScaledMargin => Constants.Margin * ZoomLevel / Prefs.UIScale;

		public bool SearchActive() { return _searchActive;}

		public MainTabWindow_ResearchTree()
		{
			closeOnClickedOutside = false;
			Instance              = this;
		}
		public static MainTabWindow_ResearchTree Instance { get; private set; }
		public float ZoomLevel
		{
			get => _zoomLevel;
			set
			{
				_zoomLevel           = Mathf.Clamp( value, 1f, MaxZoomLevel() );
				_viewRectDirty       = true;
				_viewRect_InnerDirty = true;
			}
		}
		public Rect ViewRect()
		{
			if ( _viewRectDirty )
			{
				_viewRect = new Rect(
					_baseViewRect.xMin   * ZoomLevel,
					_baseViewRect.yMin   * ZoomLevel,
					_baseViewRect.width  * ZoomLevel,
					_baseViewRect.height * ZoomLevel
				);
				_viewRectDirty = false;
			}

			return _viewRect;
		}
		public Rect ViewRect_Inner()
		{
			if ( _viewRect_InnerDirty )
			{
				_viewRect_Inner      = _viewRect.ContractedBy( Margin * ZoomLevel );
				_viewRect_InnerDirty = false;
			}

			return _viewRect_Inner;
		}
		public Rect TreeRect()
		{
			if (currentTreeSize != Tree.Size)
			{
				ResetTreeRect();
				currentTreeSize = Tree.Size;
			}
			return _treeRect;
		}
		void ResetTreeRect()
		{
			var width  = Tree.Size.x * ( NodeSize.x + NodeMargins.x );
			var height = Tree.Size.z * ( NodeSize.y + NodeMargins.y );
			_treeRect = new Rect( 0f, 0f, width, height );
		}
		// special rules for tech-level availability
		public static bool AllowedTechlevel(TechLevel level)
		{
			if ((int)level > ModSettings_ResearchPowl.maxAllowedTechLvl) return false;
			//Hard-coded mod hooks
			if (Find.Storyteller.def.defName == "VFEM_Maynard") return level >= TechLevel.Animal && level <= TechLevel.Medieval;
			return true;
		}
		internal float MaxZoomLevel()
		{
			// get the minimum zoom level at which the entire tree fits onto the screen, or a static maximum zoom level.
			var fitZoomLevel = Mathf.Max( TreeRect().width  / _baseViewRect_Inner.width, TreeRect().height / _baseViewRect_Inner.height );
			return Mathf.Min(fitZoomLevel, AbsoluteMaxZoomLevel);
		}
		public override void PreOpen()
		{
			base.PreOpen();
			Tree.WaitForInitialization();
			
			//Set Rects 
			var ymin = TopBarHeight + StandardMargin + SideMargin;
			// tree view rects, have to deal with UIScale and ZoomLevel manually.
			_baseViewRect = new Rect(
				StandardMargin / Prefs.UIScale, ymin,
				(Screen.width - StandardMargin) / Prefs.UIScale,
				(Screen.height - MainButtonDef.ButtonHeight - StandardMargin - Constants.Margin) /
				Prefs.UIScale - ymin);
			_baseViewRect_Inner = _baseViewRect.ContractedBy( Constants.Margin / Prefs.UIScale );

			//Windowrect, set to topleft (for some reason vanilla alignment overlaps bottom buttons).
			windowRect.x = 0f;
			windowRect.y = 0f;
			windowRect.width = UI.screenWidth;
			windowRect.height = UI.screenHeight - MainButtonDef.ButtonHeight;

			forcePause = ModSettings_ResearchPowl.shouldPause;
			if (ModSettings_ResearchPowl.shouldReset)
			{
				ResetSearch();
				_scrollPosition = Vector2.zero;
				ZoomLevel = 1f;
			}

			//Clear node availability caches
			ResearchNode.ClearCaches();
			Queue.SanityCheckS();

			closeOnClickedOutside = _dragging = false;
		}
		public override void DoWindowContents( Rect canvas )
		{
			GUI.EndClip();
			GUI.EndClip(); // some window black magic by fluffy

			absoluteMousePos = Event.current.mousePosition;
			var topRect = new Rect(canvas.xMin + SideMargin, canvas.yMin + StandardMargin, canvas.width - StandardMargin, TopBarHeight );
			DrawTopBar(topRect);

			ApplyZoomLevel();

			// draw background
			//GUI.DrawTexture( ViewRect, Assets.SlightlyDarkBackground );
			FastGUI.DrawTextureFast(ViewRect(), Assets.SlightlyDarkBackground, Assets.colorWhite);

			// draw the actual tree
			_scrollPosition = GUI.BeginScrollView( ViewRect(), _scrollPosition, TreeRect() );
			GUI.BeginGroup(new Rect(ScaledMargin, ScaledMargin, TreeRect().width  - ScaledMargin * 2f, TreeRect().height - ScaledMargin * 2f));

			Tree.Draw( VisibleRect );
			Queue.DrawLabels( VisibleRect );
			
			//Handle LeftoverNode Release
			if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && IsDraggingNode())
			{
				StopDragging();
				Event.current.Use();
			}
			
			//Handle StopFixed Highlights
			if (Event.current.type == EventType.MouseDown && (Event.current.button == 0 || Event.current.button == 1 && !Event.current.shift) && Tree.StopFixedHighlights())
			{
				SoundDefOf.Click.PlayOneShotOnCamera();
			}

			//Handle unfocus
			if (Event.current.type == EventType.MouseDown && !searchRect.Contains(absoluteMousePos))
			{
				UI.UnfocusCurrentControl();
			}
			
			//Handle Zoom, handle zoom only with shift
			if (Event.current.isScrollWheel && ((ModSettings_ResearchPowl.swapZoomMode && Event.current.shift) || (!ModSettings_ResearchPowl.swapZoomMode && !Event.current.shift)))
			{
				// absolute position of mouse on research tree
				var absPos = Event.current.mousePosition;

				// relative normalized position of mouse on visible tree
				var relPos = ( Event.current.mousePosition - _scrollPosition ) / ZoomLevel;

				// update zoom level
				ZoomLevel += Event.current.delta.y * ZoomStep * ZoomLevel * ModSettings_ResearchPowl.zoomingSpeedMultiplier;

				// we want to keep the _normalized_ relative position the same as before zooming
				_scrollPosition = absPos - relPos * ZoomLevel;
				Event.current.Use();
			}

			GUI.EndGroup();
			GUI.EndScrollView(false);

			//ResetZoomLevel
			GUI.matrix = originalMatrix;
			HandleNodeDragging();
			ApplyZoomLevel();

			//Handle dragging, middle mouse or holding down shift for panning
			if (Event.current.button == 2 || Event.current.shift && Event.current.button == 0)
			{
				if (Event.current.type == EventType.MouseDown)
				{
					_dragging = true;
					_mousePosition = Event.current.mousePosition;
					Event.current.Use();
				}
				if (Event.current.type == EventType.MouseUp)
				{
					_dragging = false;
					_mousePosition = Vector2.zero;
				}
				if (Event.current.type == EventType.MouseDrag)
				{
					var _currentMousePosition = Event.current.mousePosition;
					_scrollPosition += _mousePosition - _currentMousePosition;
					_mousePosition = _currentMousePosition;
					Event.current.Use();
				}
			}
			// scroll wheel vertical, switch to horizontal with alt
			if (Event.current.isScrollWheel && ((ModSettings_ResearchPowl.swapZoomMode && !Event.current.shift) || (!ModSettings_ResearchPowl.swapZoomMode && Event.current.shift)))
			{
				float delta = Event.current.delta.y * 15 * ModSettings_ResearchPowl.scrollingSpeedMultiplier;
				if (Event.current.alt) _scrollPosition.x += delta;
				else _scrollPosition.y += delta;
				
				Event.current.Use();
			}
			
			//HandleDolly
			var dollySpeed = 10f;
			if (KeyBindingDefOf.MapDolly_Left.IsDown) _scrollPosition.x -= dollySpeed;
			if (KeyBindingDefOf.MapDolly_Right.IsDown) _scrollPosition.x += dollySpeed;
			if (KeyBindingDefOf.MapDolly_Up.IsDown) _scrollPosition.y -= dollySpeed;
			if (KeyBindingDefOf.MapDolly_Down.IsDown) _scrollPosition.y += dollySpeed;

			//Reset zoom level
			UI.ApplyUIScale();
			GUI.BeginClip( windowRect );
			GUI.BeginClip( new Rect( 0f, 0f, UI.screenWidth, UI.screenHeight ) );

			//Cleanup
			GUI.color   = Color.white;
			Text.Anchor = TextAnchor.UpperLeft;
		}
		void ApplyZoomLevel()
		{
			originalMatrix = GUI.matrix;
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Prefs.UIScale / ZoomLevel, Prefs.UIScale / ZoomLevel, 1f));
		}
		public bool IsDraggingNode()
		{
			return draggedNode != null;
		}
		public void StartDragging(ResearchNode node, Painter painter)
		{
			Log.Debug("Start dragging node: {0}", node.Research.label);
			draggedNode = node;
			draggingSource = painter;
			draggedPosition = UI.GUIToScreenPoint(node.Rect.position);
			startDragging = Time.time;
		}
		public void StopDragging()
		{
			draggedNode?.Unhighlight(Highlighting.Reason.Focused);
			draggedNode = null;
		}
		public void HandleNodeDragging()
		{
			if (!IsDraggingNode()) return;
			var evt = Event.current;
			if (evt.type == EventType.MouseDrag && evt.button == 0)
			{
				draggedPosition += evt.delta;
				Queue.NotifyNodeDraggedS();
				evt.Use();
			}
			if (draggingSource == Painter.Tree && DraggingTime() > ModSettings_ResearchPowl.draggingDisplayDelay)
			{
				var pos = absoluteMousePos;
				pos.x -= NodeSize.x * 0.5f;
				pos.y -= NodeSize.y * 0.5f;
				draggedNode.DrawAt(pos, windowRect, Painter.Drag);
			}
			else draggedNode.DrawAt(draggedPosition, windowRect, Painter.Drag);
		}
		public float DraggingTime()
		{
			return !IsDraggingNode() ? 0 : Time.time - startDragging;
		}
		void DrawTopBar(Rect canvas)
		{
			Rect searchRect2 = new Rect(canvas) { width = 200f };
			Rect queueRect  = new Rect(canvas) { xMin = canvas.xMin + 200f, xMax = canvas.xMax - 130f };

			FastGUI.DrawTextureFast(searchRect2, Assets.SlightlyDarkBackground, Assets.colorWhite);
			DrawSearchBar(searchRect2.ContractedBy(Constants.Margin));
			Queue.DrawS(queueRect, !_dragging);
		}
		bool CancelSearchButton(Rect canvas)
		{
			var iconRect = new Rect(canvas.xMax - Constants.Margin - 12f, 0f, 12f,12f ).CenteredOnYIn(canvas);
			var texture = Assets.closeXSmall;
			return Widgets.ButtonImage(iconRect, texture, false);
		}
		void DrawSearchBar(Rect canvas)
		{
			searchRect = new Rect(canvas.xMin, 0f, canvas.width, 30f).CenteredOnYIn(canvas);

			if (CancelSearchButton(canvas)) ResetSearch();

			UpdateTextField();
			OnSearchFieldChanged();
		}
		void UpdateTextField()
		{
			var curQuery = Widgets.TextField(searchRect, _curQuery);
			if (curQuery != _curQuery)
			{
				lastSearchChangeTime = Time.realtimeSinceStartup;
				_curQuery = curQuery;
			}
		}
		void OnSearchFieldChanged()
		{
			if ( _curQuery == _prevQuery || Time.realtimeSinceStartup - lastSearchChangeTime < SearchResponseDelay) return;

			_prevQuery = _curQuery;
			ClearPreviousSearch();

			if (_curQuery.Length <= 2) return;

			_searchActive = true;
			// open float menu with search results, if any.
			var options = new List<FloatMenuOption>();

			var list = Tree.ResearchNodes();
			List<(int, ResearchNode)> workingList = new List<(int, ResearchNode)>();
			int length = list.Count;
			for (int i = 0; i < length; i++)
			{
				var node = list[i];
				var search = node.Matches(_curQuery);
				if (search > 0) workingList.Add((search, node));
			}
			workingList.SortBy(x => x.Item1);
			List<ResearchNode> _searchResults = new List<ResearchNode>();
			foreach (var item in workingList) _searchResults.Add(item.Item2);

			Log.Debug("Search activate: {0}", _curQuery);
			Log.Debug("Search result: {0}", Queue.DebugQueueSerialize(_searchResults));

			foreach (var result in _searchResults)
			{
				result.isMatched = true;
				options.Add(new FloatMenuOption(result.Label, action: () => ClickAndCenterOn(result), mouseoverGuiAction: rect => CenterOn(result)));
			}

			if (!options.Any()) options.Add(new FloatMenuOption(ResourceBank.String.NoResearchFound, null));

			Find.WindowStack.Add(new FloatMenu_Fixed(options, UI.GUIToScreenPoint(new Vector2(searchRect.xMin, searchRect.yMax))));
		}
		void ResetSearch()
		{
			_curQuery = "";
			_prevQuery = "";
			ClearPreviousSearch();
		}
		void ClearPreviousSearch()
		{
			Find.WindowStack.FloatMenu?.Close(false);
			_searchActive = false;
			if (_searchResults != null)
			{
				foreach (var result in _searchResults) result.isMatched = false;                
				_searchResults = null;
			}
		}
		public void ClickAndCenterOn(ResearchNode node)
		{
			CenterOn(node);
			UI.UnfocusCurrentControl();
		}
		public void CenterOn(ResearchNode node)
		{
			var position = new Vector2((NodeSize.x + NodeMargins.x) * (node.X - .5f), (NodeSize.y + NodeMargins.y) * (node.Y - .5f) );

			position -= new Vector2(UI.screenWidth, UI.screenHeight) / 2f;

			position.x = Mathf.Clamp( position.x, 0f, TreeRect().width  - ViewRect().width );
			position.y = Mathf.Clamp( position.y, 0f, TreeRect().height - ViewRect().height );
			_scrollPosition = position;
		}
	}
}