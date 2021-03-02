// Queue.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using System;
using static ResearchPal.Assets;
using static ResearchPal.Constants;

namespace ResearchPal
{
    public class Queue : WorldComponent
    {
        private static   Queue                    _instance;
        private readonly List<ResearchNode>       _queue = new List<ResearchNode>();
        private          List<ResearchProjectDef> _saveableQueue;

        public Queue(World world) : base(world) {
            _instance = this;
        }

        /// <summary>
        ///     Removes and returns the first node in the queue.
        /// </summary>
        /// <returns></returns>

        public static int NumQueued => _instance._queue.Count - 1;

        public static void DrawLabels( Rect visibleRect )
        {
            Profiler.Start( "Queue.DrawLabels" );
            var i = 1;
            foreach ( var node in _instance._queue )
            {
                if ( node.IsVisible( visibleRect ) )
                {
                    var main       = ColorCompleted[node.Research.techLevel];
                    var background = i > 1 ? ColorUnavailable[node.Research.techLevel] : main;
                    DrawLabel( node.QueueRect, main, background, i );
                }

                i++;
            }

            Profiler.End();
        }

        public static void DrawLabel( Rect canvas, Color main, Color background, int label )
        {
            // draw coloured tag
            GUI.color = main;
            GUI.DrawTexture( canvas, CircleFill );

            // if this is not first in line, grey out centre of tag
            if ( background != main )
            {
                GUI.color = background;
                GUI.DrawTexture( canvas.ContractedBy( 2f ), CircleFill );
            }

            // draw queue number
            GUI.color   = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label( canvas, label.ToString() );
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Require the input to be topologically ordered
        private void UnsafeConcat(IEnumerable<ResearchNode> nodes) {
            foreach (var n in nodes) {
                if (!_queue.Contains(n)) {
                    _queue.Add(n);
                }
            }
        }

        // Require the input to be topologically ordered
        private void UnsafeConcatFront(IEnumerable<ResearchNode> nodes) {
            UnsafeInsert(nodes, 0);
        }

        private void UnsafeInsert(IEnumerable<ResearchNode> nodes, int pos) {
            int i = pos;
            foreach (var n in nodes) {
                if (_queue.IndexOf(n, 0, pos) != -1) {
                    continue;
                }
                _queue.Remove(n);
                _queue.Insert(i++, n);
            }
        }

        public bool CantResearch(ResearchNode node) {
            return !node.GetAvailable();
        }

        private void UnsafeAppend(ResearchNode node) {
            UnsafeConcat(node.MissingPrerequisitesInc());
            UpdateCurrentResearch();
        }

        public bool Append(ResearchNode node) {
            if (_queue.Contains(node) || CantResearch(node)) {
                return false;
            }
            UnsafeAppend(node);
            return true;
        }

        public void Prepend(ResearchNode node) {
            if (CantResearch(node)) {
                return;
            }
            UnsafeConcatFront(node.MissingPrerequisitesInc());
            UpdateCurrentResearch();
        }

        // S means "static"
        static public bool AppendS(ResearchNode node) {
            return _instance.Append(node);
        }

        static public void PrependS(ResearchNode node) {
            _instance.Prepend(node);
        }

        public void Clear() {
            _queue.Clear();
            UpdateCurrentResearch();
        }

        static public void ClearS() {
            _instance.Clear();
        }

        private void MarkShouldRemove(int index, List<ResearchNode> shouldRemove) {
            var node = _queue[index];
            shouldRemove.Add(node);
            for (int i = index + 1; i < _queue.Count(); ++i) {
                if (shouldRemove.Contains(_queue[i])) {
                    continue;
                }
                if (_queue[i].MissingPrerequisites().Contains(node)) {
                    MarkShouldRemove(i, shouldRemove);
                }
            }
        }

        private void UpdateCurrentResearch() {
            Find.ResearchManager.currentProj = _queue.FirstOrDefault()?.Research;
        }

        static private void UpdateCurrentResearchS() {
            _instance.UpdateCurrentResearch();
        }

        public void SanityCheck() {
            List<ResearchNode> finished = new List<ResearchNode>();
            List<ResearchNode> unavailable = new List<ResearchNode>();

            foreach (var n in _queue) {
                if (n.Research.IsFinished) {
                    finished.Add(n);
                } else if (!n.GetAvailable()) {
                    unavailable.Add(n);
                }
            }
            finished.ForEach(n => _queue.Remove(n));
            unavailable.ForEach(n => Remove(n));
            UpdateCurrentResearch();
        }

        static public void SanityCheckS() {
            _instance.SanityCheck();
        }

        public bool Remove(ResearchNode node) {
            if (node.Completed()) {
                return _queue.Remove(node);
            }
            List<ResearchNode> shouldRemove = new List<ResearchNode>();
            var idx = _queue.IndexOf(node);
            if (idx == -1) {
                return false;
            }
            MarkShouldRemove(idx, shouldRemove);
            foreach (var n in shouldRemove) {
                _queue.Remove(n);
            }
            if (idx == 0) {
                UpdateCurrentResearch();
            }
            return true;
        }

        public void Replace(ResearchNode node) {
            _queue.Clear();
            Append(node);
        }

        static public void ReplaceS(ResearchNode node) {
            _instance.Replace(node);
        }

        static public bool RemoveS(ResearchNode node) {
            return _instance.Remove(node);
        }

        public void Finish(ResearchNode node) {
            foreach (var n in node.MissingPrerequisitesInc()) {
                _queue.Remove(n);
                Find.ResearchManager.FinishProject(n.Research);
            }
        }

        static public void FinishS(ResearchNode node) {
            _instance.Finish(node);
        }

        public bool Contains(ResearchNode node) {
            return _queue.Contains(node);
        }

        public static bool ContainsS(ResearchNode node) {
            return _instance._queue.Contains(node);
        }

        public int Count() {
            return _queue.Count();
        }

        public static int CountS() {
            return _instance.Count();
        }

        public ResearchNode this[int n] {
            get {
                return _queue[n];
            }
        } 

        public static ResearchNode AtS(int n) {
            return _instance[n];
        }

        public ResearchNode Current() {
            return _queue.FirstOrDefault();
        }

        public static ResearchNode CurrentS() {
            return _instance.Current();
        }

        public static void TryStartNext( ResearchProjectDef finished )
        {
            var current = CurrentS();

            var finishedNode = _instance._queue.Find(n => n.Research == finished);
            if (finishedNode == null) {
                return;
            }
            RemoveS(finishedNode);
            if (finishedNode != current) {
                return;
            }
            var next = CurrentS()?.Research;
            Find.ResearchManager.currentProj = next;
            DoCompletionLetter(current.Research, next);
        }

        private static void DoCompletionLetter( ResearchProjectDef current, ResearchProjectDef next )
        {
            // message
            string label = "ResearchFinished".Translate( current.LabelCap );
            string text  = current.LabelCap + "\n\n" + current.description;

            if ( next != null )
            {
                text += "\n\n" + ResourceBank.String.NextInQueue(next.LabelCap);
                Find.LetterStack.ReceiveLetter( label, text, LetterDefOf.PositiveEvent );
            }
            else
            {
                text += "\n\n" + ResourceBank.String.NextInQueue("none");
                Find.LetterStack.ReceiveLetter( label, text, LetterDefOf.NeutralEvent );
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // store research defs as these are the defining elements
            if ( Scribe.mode == LoadSaveMode.Saving )
                _saveableQueue = _queue.Select( node => node.Research ).ToList();

            Scribe_Collections.Look( ref _saveableQueue, "Queue", LookMode.Def );

            if ( Scribe.mode == LoadSaveMode.PostLoadInit ) {
                if (Settings.asyncLoadingOnStartup) {
                    Tree.WaitForResearchNodes();
                }
                foreach (var research in _saveableQueue) {
                    // find a node that matches the research - or null if none found
                    var node = research.ResearchNode();

                    if (node != null) {
                        UnsafeAppend(node);
                    }
                }
                UpdateCurrentResearch();
            }
        }

        private void DoMove(ResearchNode node, int from, int to) {
            List<ResearchNode> movingNodes = new List<ResearchNode>();
            to = Math.Max(0, Math.Min(Count(), to));
            if (to > from) {
                movingNodes.Add(node);
                int dest = --to;
                for (int i = from + 1; i <= to; ++i) {
                    if (_queue[i].MissingPrerequisites().Contains(node)) {
                        movingNodes.Add(_queue[i]);
                        --dest;
                    }
                }
                movingNodes.ForEach(n => _queue.Remove(n));
                _queue.InsertRange(dest, movingNodes);
            } else if (to < from) {
                var prerequisites = node.MissingPrerequisites().ToList();
                for (int i = to; i < from; ++i) {
                    if (prerequisites.Contains(_queue[i])) {
                        movingNodes.Add(_queue[i]);
                    }
                }
                movingNodes.Add(node);
                UnsafeInsert(movingNodes, to);
            }
        }

        public void Insert(ResearchNode node, int pos) {
            if (CantResearch(node)) {
                return;
            }
            pos = Math.Max(0, Math.Min(Count(), pos));
            var idx = _queue.IndexOf(node);
            if (idx == pos) return;
            if (idx != -1) {
                DoMove(node, idx, pos);
            } else {
                UnsafeInsert(node.MissingPrerequisitesInc(), pos);
            }
            UpdateCurrentResearch();

        }

        private Vector2 _scroll_pos = new Vector2(0, 0);

        private float Width() {
            var original = DisplayQueueLength() * (NodeSize.x + Margin) - Margin;
            if (Settings.showIndexOnQueue) {
                return original + Constants.QueueLabelSize * 0.5f;
            }
            return original;
        }

        private Rect ViewRect(Rect canvas) {
            return new Rect(0, 0, Mathf.Max(Width(), canvas.width), canvas.height);
        }

        private Vector2 NodePos(int i) {
            return new Vector2(i * (Margin + NodeSize.x), 0);
        }

        private Rect VisibleRect(Rect canvas) {
            return new Rect(_scroll_pos, canvas.size);
        }

        private void ReleaseNodeAt(ResearchNode node, int dropIdx) {
            if (dropIdx == -1) {
                Remove(node);
            }
            var tab = MainTabWindow_ResearchTree.Instance;
            if (_queue.IndexOf(node) == dropIdx) {
                if (tab.DraggingTime() < 0.2f) {
                    node.LeftClick();
                }
            } else {
                if (DraggingFromQueue() && dropIdx > _queue.IndexOf(node)) {
                    ++dropIdx;
                }
                Insert(node, dropIdx);
            }
        } 

        static private bool ReleaseEvent() {
            return DraggingNode()
                && Event.current.type == EventType.MouseUp
                && Event.current.button == 0;
        }

        static private void StopDragging() {
            MainTabWindow_ResearchTree.Instance.StopDragging();
        }

        static private int DropIndex(Rect visibleRect, Vector2 dropPos) {
            Rect relaxedRect = visibleRect;
            relaxedRect.yMin -= NodeSize.y * 0.3f;
            relaxedRect.height += NodeSize.y;
            if (!visibleRect.Contains(dropPos)) {
                return -1;
            }
            return VerticalPosToIdx(dropPos.x);
        }

        private void HandleDragReleaseInside(Rect visibleRect) {
            if (ReleaseEvent()) {
                ReleaseNodeAt(
                    DraggedNode(),
                    DropIndex(visibleRect, Event.current.mousePosition));
                StopDragging();
                ResetNodePositions();
                Event.current.Use();
            }
        }

        private static int VerticalPosToIdx(float pos) {
            return (int) (pos / (Margin + NodeSize.x));
        }

        static private List<int> NormalPositions(int n) {
            List<int> poss = new List<int>();
            for (int i = 0; i < n; ++i) {
                poss.Add(i);
            }
            return poss;
        }

        private List<int> DraggingNodePositions(Rect visibleRect) {
            List<int> poss = new List<int>();
            if (!DraggingNode()) {
                return NormalPositions(Count());
            }
            int draggedIdx = DropIndex(
                visibleRect, Event.current.mousePosition);
            for (int p = 0, i = 0; i < Count();) {
                var node = _queue[i];
                // The dragged node should disappear
                if (DraggingFromQueue() && node == DraggedNode()) {
                    poss.Add(-1);
                    ++i;
                // The space of the queue is occupied
                } else if (draggedIdx == p) {
                    ++p;
                    continue;
                // usual situation
                } else {
                    poss.Add(p);
                    ++p;
                    ++i;
                }
            }
            return poss;
        }

        private void ResetNodePositions() {
            currentPositions = NormalPositions(Count());
        }

        List<int> currentPositions = new List<int>();

        bool nodeDragged = false;

        public void NotifyNodeDragged() {
            nodeDragged = true;
        }
        static public void NotifyNodeDraggedS() {
            _instance.NotifyNodeDragged();
        }

        static private bool DraggingNode() {
            return MainTabWindow_ResearchTree.Instance.DraggingNode();
        }
        static private bool DraggingFromQueue() {
            return MainTabWindow_ResearchTree.Instance.DraggingSource() == Painter.Queue;
        }
        static private ResearchNode DraggedNode() {
            return MainTabWindow_ResearchTree.Instance.DraggedNode();
        }
        private int DisplayQueueLength() {
            if (DraggingNode() && !DraggingFromQueue()) {
                return Count() + 1;
            }
            return Count();
        }

        public void UpdateCurrentPosition(Rect visibleRect) {
            if (!DraggingNode()) {
                if (nodeDragged) {
                    ResetNodePositions();
                } else {
                    TryRefillPositions();
                }
                return;
            } else if (nodeDragged) {
                currentPositions = DraggingNodePositions(visibleRect);
            }
            nodeDragged = false;
        }

        private void TryRefillPositions() {
            if (currentPositions.Count() != Count()) {
                ResetNodePositions();
            }
        }

        private Pair<float, float> TolerantVerticalRange(float ymin, float ymax) {
            return new Pair<float, float>(ymin - NodeSize.x * 0.3f, ymax + NodeSize.y * 0.7f);
        }

        private void HandleReleaseOutside(Rect canvas) {
            var mouse = Event.current.mousePosition;
            if (ReleaseEvent() && !canvas.Contains(mouse)) {
                var vrange = TolerantVerticalRange(canvas.yMin, canvas.yMax);
                if (mouse.y >= vrange.First && mouse.y <= vrange.Second) {
                    if (mouse.x <= canvas.xMin) {
                        ReleaseNodeAt(DraggedNode(), 0);
                    } else if (mouse.x >= canvas.xMax) {
                        ReleaseNodeAt(DraggedNode(), Count());
                    }
                    ResetNodePositions();
                    StopDragging();
                    Event.current.Use();
                } else if (DraggingFromQueue()) {
                    Remove(DraggedNode());
                    ResetNodePositions();
                    StopDragging();
                    Event.current.Use();
                }
            }
        }
        private void HandleScroll(Rect canvas) {
            if (Event.current.isScrollWheel && Mouse.IsOver(canvas)) {
                _scroll_pos.x += Event.current.delta.y * 20;
                Event.current.Use();
            } else if (DraggingNode()) {
                var tab = MainTabWindow_ResearchTree.Instance;
                var nodePos = tab.DraggedNodePos();
                if (  nodePos.y <= canvas.yMin - NodeSize.y
                   || nodePos.y >= canvas.yMax
                   || (  nodePos.x >= canvas.xMin
                      && nodePos.x <= canvas.xMax - NodeSize.x)) {
                    return;
                }
                float baseScroll = 20;
                if (nodePos.x < canvas.xMin) {
                    _scroll_pos.x -= baseScroll * (canvas.xMin - nodePos.x) / NodeSize.x;
                } else if (nodePos.x > canvas.xMax - NodeSize.x) {
                    _scroll_pos.x += baseScroll * (nodePos.x + NodeSize.x - canvas.xMax) / NodeSize.x;
                }
            }
        }

        private void DrawBackground(Rect baseCanvas) {
            GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            GUI.DrawTexture(baseCanvas, BaseContent.GreyTex);
        }

        private Rect CanvasFromBaseCanvas(Rect baseCanvas) {
            var r = baseCanvas.ContractedBy(Constants.Margin);
            r.xMin += Margin;
            r.xMax -= Margin;
            return r;
        }

        List<ResearchNode> temporaryQueue = new List<ResearchNode>();

        private void DrawNodes(Rect visibleRect) {
            temporaryQueue.Clear();
            // when handling event in nodes, the queue itself may change
            // so using a temporary queue to avoid the unmatching DrawAt and SetRect
            foreach (var node in _queue) {
                temporaryQueue.Add(node);
            }
            for (int i = 0; i < temporaryQueue.Count(); ++i) {
                if (currentPositions[i] == -1) {
                    continue;
                }
                var pos = NodePos(currentPositions[i]);
                var node = temporaryQueue[i];
                node.DrawAt(pos, visibleRect, Painter.Queue, true);
            }
            if (Settings.showIndexOnQueue) {
                DrawLabels(visibleRect);
            }
            foreach (var node in temporaryQueue) {
                node.SetRects();
            }
            if (temporaryQueue.Count() != Count()) {
                ResetNodePositions();
            }
        }

        public void Draw(Rect baseCanvas, bool interactible) {

            DrawBackground(baseCanvas);
            var canvas = CanvasFromBaseCanvas(baseCanvas);

            if (CountS() == 0) {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = Color.white;
                Widgets.Label( canvas, ResourceBank.String.NothingQueued );
                Text.Anchor = TextAnchor.UpperLeft;
            }

            HandleReleaseOutside(canvas);
            HandleScroll(canvas);


            _scroll_pos = GUI.BeginScrollView(
                canvas, _scroll_pos, ViewRect(canvas), GUIStyle.none, GUIStyle.none);
            Profiler.Start("Queue.DrawQueue");


            var visibleRect = VisibleRect(canvas);
            HandleDragReleaseInside(visibleRect);
            UpdateCurrentPosition(visibleRect);

            DrawNodes(visibleRect);

            Profiler.End();
            GUI.EndScrollView(false);
        }

        public static void DrawS( Rect canvas, bool interactible )
        {
            _instance.Draw(canvas, interactible);
        }

        public static void Notify_InstantFinished()
        {
            foreach (var node in new List<ResearchNode>(_instance._queue))
                if (node.Research.IsFinished)
                    _instance._queue.Remove(node);

            Find.ResearchManager.currentProj = _instance._queue.FirstOrDefault()?.Research;
        }
    }
}