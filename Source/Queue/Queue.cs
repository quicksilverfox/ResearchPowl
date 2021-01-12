// Queue.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
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
            int i = 0;
            foreach (var n in nodes) {
                _queue.Remove(n);
                _queue.Insert(i++, n);
            }
        }

        public bool CantResearch(ResearchNode node) {
            return node.Research.IsFinished || !node.GetAvailable();
        }

        public void Append(ResearchNode node) {
            if (_queue.Contains(node) || CantResearch(node)) {
                return;
            }
            UnsafeConcat(node.MissingPrerequisitesInc());
            UpdateCurrentResearch();
        }

        public void Prepend(ResearchNode node) {
            if (CantResearch(node)) {
                return;
            }
            UnsafeConcatFront(node.MissingPrerequisitesInc());
            UpdateCurrentResearch();
        }

        // S means "static"
        static public void AppendS(ResearchNode node) {
            _instance.Append(node);
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

        public bool Remove(ResearchNode node) {
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

        public static void TryStartNext( ResearchProjectDef finished )
        {
            var current = _instance._queue.FirstOrDefault()?.Research;
            Log.Debug( "TryStartNext: current; {0}, finished; {1}", current, finished );
            if ( finished != _instance._queue.FirstOrDefault()?.Research )
            {
                _instance._queue.Remove(finished);
                return;
            }

            _instance._queue.RemoveAt(0);
            var next = _instance._queue.FirstOrDefault()?.Research;
            Log.Debug( "TryStartNext: next; {0}", next );
            Find.ResearchManager.currentProj = next;
            DoCompletionLetter(current, next);
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
                if (_saveableQueue.Any()) {
                    Tree.WaitForInitialization();
                }
                foreach (var research in _saveableQueue) {
                    // find a node that matches the research - or null if none found
                    var node = research.ResearchNode();

                    if (node != null) {
                        Log.Debug( "Adding {0} to queue", node.Research.LabelCap );
                        Append(node);
                    } else {
                        Log.Debug( "Could not find node for {0}", research.LabelCap );
                    }
                }
                UpdateCurrentResearch();
            }
        }

        public static void DrawQueue( Rect canvas, bool interactible )
        {
            Profiler.Start( "Queue.DrawQueue" );
            if (CountS() == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = TechLevelColor;
                Widgets.Label( canvas, ResourceBank.String.NothingQueued );
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color   = Color.white;
                return;
            }

            var pos = canvas.min;
            for (var i = 0; i < CountS() && pos.x + NodeSize.x < canvas.xMax; ++i)
            {
                var node = AtS(i);
                var rect = new Rect(
                    pos.x      - Margin,
                    pos.y      - Margin,
                    NodeSize.x + 2 * Margin,
                    NodeSize.y + 2 * Margin
                );
                node.DrawAt(pos, rect, 1, true);
                if (interactible && Mouse.IsOver(rect))
                    MainTabWindow_ResearchTree.Instance.CenterOn(node);
                pos.x += NodeSize.x + Margin;
            }

            Profiler.End();
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