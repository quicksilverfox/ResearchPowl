// Tree.cs
// Copyright Karel Kroeze, 2020-2020

//using Multiplayer.API;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using static ResearchPal.Constants;

namespace ResearchPal
{
    public static class Tree
    {
        public static volatile bool Initialized = false;
        public static  IntVec2                         Size = IntVec2.Zero;
        public static bool shouldSeparateByTechLevels;

        private static List<Node>                      _nodes;
        private static List<Edge<Node, Node>>          _edges;
        private static List<TechLevel>                 _relevantTechLevels;
        private static Dictionary<TechLevel, IntRange> _techLevelBounds;

        public static bool OrderDirty;

        private static List<List<Node>> _layers;
        private static List<Node> _singletons;

        private static List<ResearchNode> _researchNodes;
        public static bool DisplayProgressState = false;

        public static Dictionary<TechLevel, IntRange> TechLevelBounds
        {
            get
            {
                if ( _techLevelBounds == null )
                    throw new Exception( "TechLevelBounds called before they are set." );
                return _techLevelBounds;
            }
        }

        public static List<TechLevel> RelevantTechLevels
        {
            get
            {
                if ( _relevantTechLevels == null )
                    _relevantTechLevels = Enum.GetValues( typeof( TechLevel ) )
                                              .Cast<TechLevel>()
                                               // filter down to relevant tech levels only.
                                              .Where(
                                                   tl => DefDatabase<ResearchProjectDef>.AllDefsListForReading.Any(
                                                       rp => rp.techLevel ==
                                                             tl ) )
                                              .ToList();
                return _relevantTechLevels;
            }
        }

        public static List<Node> Nodes
        {
            get
            {
                if ( _nodes == null )
                    PopulateNodes();

                return _nodes;
            }
        }

        public static List<ResearchNode> ResearchNodes() {
            if (_researchNodes == null || _researchNodes.Count() == 0) {
                _researchNodes = Nodes.OfType<ResearchNode>().ToList();
            }
            return _researchNodes;
        }

        public static IEnumerable<Node> NonSingletons => Nodes.Where(n => _singletons.IndexOf(n) == -1);

        public static List<Edge<Node, Node>> Edges
        {
            get
            {
                if ( _edges == null )
                    throw new Exception( "Trying to access edges before they are initialized." );

                return _edges;
            }
        }

        private static List<List<Node>> Layering(List<Node> nodes) {
            var layers = new List<List<Node>>();
            foreach (var node in Nodes)
            {
                if (node.X > layers.Count()) {
                    for (int i = layers.Count(); i < node.X; ++i) {
                        layers.Add(new List<Node>());
                    }
                }
                layers[node.X - 1].Add(node);
            }
            return layers;
        }

        private static bool SignDiff(float f1, float f2) {
            return f1 < 0 && f2 > 0 || f1 > 0 && f2 < 0;
        }

        private static float mainGraphUpperbound = 1;

        private static List<Node> ProcessSingletons(List<List<Node>> layers) {
            if (shouldSeparateByTechLevels) {
                return new List<Node>();
            }
            var singletons = layers[0]
                .Where(n => n is ResearchNode && n.OutEdges.Count() == 0)
                .OrderBy(n => (n as ResearchNode).Research.techLevel)
                .ToList();
            layers[0] = layers[0].Where(n => n.OutEdges.Count() > 0).ToList();

            foreach (var g in singletons.GroupBy(n => (n as ResearchNode).Research.techLevel)) {
                PlaceSingletons(g, layers.Count() - 1);
            }

            return singletons;
        }

        private static void PlaceSingletons(IEnumerable<Node> singletons, int colNum) {
            int x = 0, y = (int) mainGraphUpperbound;
            foreach (var n in singletons) {
                n.X = x + 1; n.Y = y;
                y += (x + 1) / colNum;
                x = (x + 1) % colNum;
            }
            mainGraphUpperbound = x == 0 ? y : y + 1;
        }

        private static void MergeDummiesByParents(List<Node> layer, List<DummyNode> dummies) {
            for (int i = 0; i < dummies.Count() - 1; ) {
                DummyNode node = dummies[i];
                List<Node> parents = node.InNodes;
                parents.Sort();
                int j = i + 1;
                for (; j < dummies.Count(); ++j) {
                    var parents2 = dummies[j].InNodes;
                    parents2.Sort();
                    if (!parents.SequenceEqual(parents2)) break;
                    node.Merge(dummies[j]);
                    Nodes.Remove(dummies[j]);
                    layer.Remove(dummies[j]);
                }
                i = j;
            }
        }
        private static void MergeDummiesByChildren(List<Node> layer, List<DummyNode> dummies) {
            for (int i = 0; i < dummies.Count() - 1; ) {
                DummyNode node = dummies[i];
                List<Node> children = node.OutNodes;
                children.SortBy(n => n.GetHashCode());
                int j = i + 1;
                for (; j < dummies.Count(); ++j) {
                    var children2 = dummies[j].OutNodes;
                    children2.SortBy(n => n.GetHashCode());
                    if (!children.SequenceEqual(children2)) break;
                    node.Merge(dummies[j]);
                    Nodes.Remove(dummies[j]);
                    layer.Remove(dummies[j]);
                }
                i = j;
            }
        }

        private static void CollapseAdjacentDummyNodes() {
            for (int i = 0; i < _layers.Count(); ++i) {
                var dummies = _layers[i].OfType<DummyNode>().ToList();
                MergeDummiesByParents(_layers[i], dummies);
            }
            for (int i = _layers.Count() - 1; i >= 0; --i) {
                var dummies = _layers[i].OfType<DummyNode>().ToList();
                MergeDummiesByChildren(_layers[i], dummies);
            }
        }

        public static void LegacyPreprocessing() {
            var layers = Layering(Nodes);
            var singletons = ProcessSingletons(layers);
            _layers = layers;
            _singletons = singletons;
        }

        public static void MainAlgorithm(List<List<Node>> data) {
            NodeLayers layers = new NodeLayers(data);
            // var layerss = new List<NodeLayers>();
            // layerss.Add(layers);
            List<NodeLayers> modsSplit = null;
            if (Settings.placeModTechSeparately) {
                modsSplit = layers.SplitLargeMods();
            } else {
                modsSplit = new List<NodeLayers>();
                modsSplit.Add(layers);
            }

            var allLayers = modsSplit
                .OrderBy(l => l.NodeCount())
                .SelectMany(
                    ls => ls
                        .SplitConnectiveComponents()
                        .OrderBy(l => l.NodeCount()))
                .ToList();
            allLayers.ForEach(l => OrgainzeLayers(l));
            PositionAllLayers(allLayers);
        }

        public static void OrgainzeLayers(NodeLayers layers) {
            layers.MinimizeCrossings();
            layers.ApplyGridCoordinates();
            layers.ImproveNodePositionsInLayers();
        }

        private static void FitLayersInBounds(NodeLayers layers, float[] topBounds) {
            float dy = -99999;
            for (int i = 0; i < layers.LayerCount(); ++i) {
                dy = Math.Max(dy, topBounds[i] - layers.TopPosition(i));
            }
            layers.MoveVertically(dy);
            for (int i = 0; i < layers.LayerCount(); ++i) {
                topBounds[i] = Math.Max(topBounds[i], layers.BottomPosition(i) + 1);
            }
        }

        public static void PositionAllLayers(IEnumerable<NodeLayers> layerss) {
            float[] topBounds = new float[_layers.Count()];
            for (int i = 0; i < topBounds.Count(); ++i) {
                topBounds[i] = mainGraphUpperbound;
            }
            foreach (var layers in layerss) {
                FitLayersInBounds(layers, topBounds);
            }
            mainGraphUpperbound = topBounds.Max();
        }

        public static void WaitForInitialization() {
            if (! Tree.Initialized) {
                if (Settings.delayLayoutGeneration) {
                    Tree.Initialize();
                } else if (Settings.asyncLoadingOnStartup) {
                    while (! Tree.Initialized) continue;
                }
            }
        }


        public static void Initialize()
        {
            shouldSeparateByTechLevels = Settings.shouldSeparateByTechLevels;

            // setup
            // Log.Message(ResourceBank.String.PreparingTree_Setup);
            CheckPrerequisites();
            CreateEdges();
            HorizontalPositions();
            NormalizeEdges();
// Legacy Logic Above

            LegacyPreprocessing();
            MainAlgorithm(_layers);

            // CollapseAdjacentDummyNodes();

            RemoveEmptyRows();
            Tree.Size.z = (int) (Nodes.Max(n => n.Yf) + 0.01) + 1;

            Log.Message("Research layout initialized");
            Initialized = true;
// Legacy Logic Below

// #if DEBUG
//             DebugStatus();
// #endif
//             // crossing reduction
//             Log.Message(ResourceBank.String.PreparingTree_CrossingReduction);
//             Collapse();
//             MinimizeCrossings();
// #if DEBUG
//             DebugStatus();
// #endif
//             // layout
//             Log.Message(ResourceBank.String.PreparingTree_Layout);
//             MinimizeEdgeLength();
//             SquashOrphans();
            // RemoveEmptyRows();
// #if DEBUG
//             DebugStatus();
// #endif
//             // done!
//             // we're ready
//             Log.Message(ResourceBank.String.PreparingTree_RestoreQueue);
        }

        private static void RemoveEmptyRows()
        {
            Log.Debug( "Removing empty rows" );
            Profiler.Start();
            var z = Nodes.Max(n => n.Yf);
            for (var y = 1; y < z;) {
                var row = Row( y );
                if ( row.NullOrEmpty() ) {
                    var ns = Nodes.Where(n => n.Yf > y).ToList();
                    if (ns.Count() == 0) {
                        break;
                    }
                    ns.ForEach(n => n.Yf = n.Yf - 1);
                }
                else
                    ++y;
            }

            Profiler.End();
        }

        static void HorizontalPositions() {
            Log.Debug("Assigning horizontal positions.");
            Profiler.Start();

            if (shouldSeparateByTechLevels) {
                HorizontalPositionsByTechLevels();
            } else {
                HorizontalPositionsByDensity();
            }

            Profiler.End();
        }

        static void HorizontalPositionsByTechLevels() {
            _techLevelBounds = new Dictionary<TechLevel, IntRange>();
            float leftBound = 1;
            foreach (var group in
                ResearchNodes()
                    .GroupBy(n => n.Research.techLevel)
                    .OrderBy(g => g.Key)) {
                var updateOrder = FilteredTopoSort(
                    group, n => n.Research.techLevel == group.Key);
                float newLeftBound  = leftBound;
                foreach (var node in updateOrder) {
                    newLeftBound = Math.Max(newLeftBound, node.SetDepth((int)leftBound));
                }
                _techLevelBounds[group.Key] = new IntRange((int)leftBound - 1, (int)newLeftBound);
                leftBound = newLeftBound + 1;
            }
        }

        static void HorizontalPositionsByDensity() {
            var updateOrder = TopologicalSort(ResearchNodes());
            foreach (var node in updateOrder) {
                node.SetDepth(1);
            }
        }
        
        static private List<ResearchNode> TopologicalSort(IEnumerable<ResearchNode> nodes) {
            return FilteredTopoSort(nodes, n => true);
        }

        static private List<ResearchNode> FilteredTopoSort(
            IEnumerable<ResearchNode> nodes, Func<ResearchNode, bool> p) {
            List<ResearchNode> result = new List<ResearchNode>();
            HashSet<ResearchNode> visited = new HashSet<ResearchNode>();
            foreach (var node in nodes) {
                if (node.OutNodes.OfType<ResearchNode>().Where(p).Any()) {
                    continue;
                }
                FilteredTopoSortRec(node, p, result, visited);
            }
            // result.Reverse();
            return result;
        }

        static private void FilteredTopoSortRec(
            ResearchNode cur, Func<ResearchNode, bool> p,
            List<ResearchNode> result, HashSet<ResearchNode> visited) {
            if (visited.Contains(cur)) {
                return;
            }
            foreach (var next in cur.InNodes.OfType<ResearchNode>().Where(p)) {
                FilteredTopoSortRec(next, p, result, visited);
            }
            result.Add(cur);
            visited.Add(cur);
        }

        private static void NormalizeEdges()
        {
            Log.Debug( "Normalizing edges." );
            Profiler.Start();
            foreach (var edge in new List<Edge<Node, Node>>(Edges.Where(e => e.Span > 1)))
            {
                Log.Trace( "\tCreating dummy chain for {0}", edge );

                // remove and decouple long edge
                Edges.Remove( edge );
                edge.In.OutEdges.Remove( edge );
                edge.Out.InEdges.Remove( edge );
                var cur     = edge.In;
                var yOffset = ( edge.Out.Yf - edge.In.Yf ) / edge.Span;

                // create and hook up dummy chain
                for ( var x = edge.In.X + 1; x < edge.Out.X; x++ )
                {
                    var dummy = new DummyNode();
                    dummy.X  = x;
                    dummy.Yf = edge.In.Yf + yOffset * ( x - edge.In.X );
                    var dummyEdge = new Edge<Node, Node>( cur, dummy );
                    cur.OutEdges.Add( dummyEdge );
                    dummy.InEdges.Add( dummyEdge );
                    _nodes.Add( dummy );
                    Edges.Add( dummyEdge );
                    cur = dummy;
                    Log.Trace( "\t\tCreated dummy {0}", dummy );
                }

                // hook up final dummy to out node
                var finalEdge = new Edge<Node, Node>( cur, edge.Out );
                cur.OutEdges.Add( finalEdge );
                edge.Out.InEdges.Add( finalEdge );
                Edges.Add( finalEdge );
            }

            Profiler.End();
        }
        private static void CreateEdges()
        {
            Log.Debug( "Creating edges." );
            Profiler.Start();
            // create links between nodes
            if ( _edges.NullOrEmpty() ) _edges = new List<Edge<Node, Node>>();

            foreach ( var node in Nodes.OfType<ResearchNode>() )
            {
                if ( node.Research.prerequisites.NullOrEmpty() )
                    continue;
                foreach ( var prerequisite in node.Research.prerequisites )
                {
                    ResearchNode prerequisiteNode = prerequisite;
                    if ( prerequisiteNode == null )
                        continue;
                    var edge = new Edge<Node, Node>( prerequisiteNode, node );
                    Edges.Add( edge );
                    node.InEdges.Add( edge );
                    prerequisiteNode.OutEdges.Add( edge );
                    Log.Trace( "\tCreated edge {0}", edge );
                }
            }

            Profiler.End();
        }

        private static void CheckPrerequisites()
        {
            // check prerequisites
            Log.Debug( "Checking prerequisites." );
            Profiler.Start();

            var nodes = new Queue<ResearchNode>(ResearchNodes());
            // remove redundant prerequisites
            while ( nodes.Count > 0 )
            {
                var node = nodes.Dequeue();
                if ( node.Research.prerequisites.NullOrEmpty() )
                    continue;

                var ancestors = node.Research.prerequisites?.SelectMany( r => r.Ancestors() ).ToList();
                var redundant = ancestors.Intersect( node.Research.prerequisites );
                if ( redundant.Any() )
                {
                    // Log.Warning( "\tredundant prerequisites for {0}: {1}", node.Research.LabelCap,
                                //  string.Join( ", ", redundant.Select( r => r.LabelCap ).ToArray() ) );
                    foreach ( var redundantPrerequisite in redundant )
                        node.Research.prerequisites.Remove( redundantPrerequisite );
                }
            }

            // fix bad techlevels
            nodes = new Queue<ResearchNode>(ResearchNodes());
            while ( nodes.Count > 0 )
            {
                var node = nodes.Dequeue();
                if ( !node.Research.prerequisites.NullOrEmpty() )
                    // warn and fix badly configured techlevels
                    if ( node.Research.prerequisites.Any( r => r.techLevel > node.Research.techLevel ) )
                    {
                        Log.Warning( "\t{0} has a lower techlevel than (one of) it's prerequisites",
                                     node.Research.label );
                        node.Research.techLevel = node.Research.prerequisites.Max( r => r.techLevel );

                        // re-enqeue all descendants
                        foreach ( var descendant in node.Descendants.OfType<ResearchNode>() )
                            nodes.Enqueue( descendant );
                    }
            }

            Profiler.End();
        }

        private static void FixPrerequisites(ResearchProjectDef d) {
            if (d.prerequisites == null) {
                d.prerequisites = d.hiddenPrerequisites;
            } else if (d.hiddenPrerequisites != null) {
                d.prerequisites = d.prerequisites.Concat(d.hiddenPrerequisites).ToList();
            }
        }

        private static void PopulateNodes()
        {
            Log.Debug( "Populating nodes." );
            Profiler.Start();

            var projects = DefDatabase<ResearchProjectDef>.AllDefsListForReading;

            if (Settings.dontIgnoreHiddenPrerequisites) {
                projects.ForEach(FixPrerequisites);
            }

            // find hidden nodes (nodes that have themselves as a prerequisite)
            var hidden = projects.Where( p => p.prerequisites?.Contains( p ) ?? false );

            // find locked nodes (nodes that have a hidden node as a prerequisite)
            var locked = projects.Where( p => p.Ancestors().Intersect( hidden ).Any() );

            // populate all nodes
            _nodes = new List<Node>( DefDatabase<ResearchProjectDef>.AllDefsListForReading
                                                                    .Except( hidden )
                                                                    .Except( locked )
                                                                    .Select( def => new ResearchNode( def ) as Node ) );
            Log.Debug( "\t{0} nodes", _nodes.Count );
            Profiler.End();
        }

        private static void Collapse()
        {
            Log.Debug( "Collapsing nodes." );
            Profiler.Start();
            var pre = Size;
            for ( var l = 1; l <= Size.x; l++ )
            {
                var nodes = Layer( l, true );
                var Y     = 1;
                foreach ( var node in nodes )
                    node.Y = Y++;
            }

            Log.Debug( "{0} -> {1}", pre, Size );
            Profiler.End();
        }

        [Conditional( "DEBUG" )]
        internal static void DebugDraw()
        {
            foreach ( var v in Nodes )
            {
                foreach ( var w in v.OutNodes ) Widgets.DrawLine( v.Right, w.Left, Color.white, 1 );
            }
        }

        private static RelatedNodeHighlightSet hoverHighlightSet;

        static List<ResearchNode> FindHighlightsFrom(ResearchNode node) {
            return node.MissingPrerequisites()
                .Concat(node.Children.Where(c => !c.Completed()))
                .ToList();
        }

        static void OverrideHighlight(ResearchNode node) {
            hoverHighlightSet?.Stop();
            hoverHighlightSet = RelatedNodeHighlightSet.HoverOn(node);
            hoverHighlightSet.Start();
        }

        static void HandleHoverHighlight(ResearchNode node, Vector2 mousePos) {
            if (node.ShouldHighlight(mousePos)) {
                Log.Message("Highlighting {0}", node.Label);
                OverrideHighlight(node);
            }
        }

        static bool ContinueHoverHighlight(Vector2 mouse) {
            if (hoverHighlightSet == null) {
                return false;
            }
            if (hoverHighlightSet.TryStop(mouse)) {
                Log.Message("Unhighlighting {0}", hoverHighlightSet.Causer().Label);
                hoverHighlightSet = null;
                return false;
            }
            return true;
        }

        public static void Draw( Rect visibleRect )
        {
            Profiler.Start( "Tree.Draw" );
            if (shouldSeparateByTechLevels)
            {
                Profiler.Start("techlevels");
                foreach (var techlevel in RelevantTechLevels)
                    DrawTechLevel(techlevel, visibleRect);
                Profiler.End();
            }

            Profiler.Start( "edges" );
            foreach ( var edge in Edges.OrderBy( e => e.DrawOrder ) )
                edge.Draw( visibleRect );
            Profiler.End();

            TryModifySharedState();

            Profiler.Start( "nodes" );
            var evt = new Event(Event.current);
            var drawnNodes = ResearchNodes().Where(n => n.IsVisible(visibleRect));
            bool hoverHighlight = ContinueHoverHighlight(evt.mousePosition);
            foreach (var node in drawnNodes) {
                if (! hoverHighlight) {
                    HandleHoverHighlight(node, evt.mousePosition);
                }
                node.Draw(visibleRect, 0, false);
            }
            Profiler.End();
        }
        private static void TryModifySharedState() {
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) {
                DisplayProgressState = true;
            } else if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift)) {
                DisplayProgressState = false;
            }
            // if (Event.current.type == EventType.KeyDown) {
            //     if (Event.current.keyCode == KeyCode.LeftShift || Event.current.keyCode == KeyCode.RightShift) {
            //         _displayProgressState = true;
            //     }
            // } else if (Event.current.type == EventType.KeyUp) {
            //     if (Event.current.keyCode == KeyCode.LeftShift || Event.current.keyCode == KeyCode.RightShift) {
            //         _displayProgressState = false;
            //     }
            // }
        }

        public static void DrawTechLevel( TechLevel techlevel, Rect visibleRect )
        {
            // determine positions
            var xMin = ( NodeSize.x + NodeMargins.x ) * TechLevelBounds[techlevel].min - NodeMargins.x / 2f;
            var xMax = ( NodeSize.x + NodeMargins.x ) * TechLevelBounds[techlevel].max - NodeMargins.x / 2f;

            GUI.color   = Assets.TechLevelColor;
            Text.Anchor = TextAnchor.MiddleCenter;

            // lower bound
            if ( TechLevelBounds[techlevel].min > 0 && xMin > visibleRect.xMin && xMin < visibleRect.xMax )
            {
                // line
                Widgets.DrawLine( new Vector2( xMin, visibleRect.yMin ), new Vector2( xMin, visibleRect.yMax ),
                                  Assets.TechLevelColor, 1f );

                // label
                var labelRect = new Rect(
                    xMin + TechLevelLabelSize.y / 2f - TechLevelLabelSize.x / 2f,
                    visibleRect.center.y             - TechLevelLabelSize.y / 2f,
                    TechLevelLabelSize.x,
                    TechLevelLabelSize.y );

                VerticalLabel( labelRect, techlevel.ToStringHuman() );
            }

            // upper bound
            if ( TechLevelBounds[techlevel].max < Size.x && xMax > visibleRect.xMin && xMax < visibleRect.xMax )
            {
                // label
                var labelRect = new Rect(
                    xMax - TechLevelLabelSize.y / 2f - TechLevelLabelSize.x / 2f,
                    visibleRect.center.y             - TechLevelLabelSize.y / 2f,
                    TechLevelLabelSize.x,
                    TechLevelLabelSize.y );

                VerticalLabel( labelRect, techlevel.ToStringHuman() );
            }

            GUI.color   = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void VerticalLabel( Rect rect, string text )
        {
            // store the scaling matrix
            var matrix = GUI.matrix;

            // rotate and then apply the scaling
            GUI.matrix = Matrix4x4.identity;
            GUIUtility.RotateAroundPivot( -90f, rect.center );
            GUI.matrix = matrix * GUI.matrix;

            Widgets.Label( rect, text );

            // restore the original scaling matrix
            GUI.matrix = matrix;
        }

        public static List<Node> Layer( int depth, bool ordered = false )
        {
            if ( ordered && OrderDirty )
            {
                _nodes     = Nodes.OrderBy( n => n.X ).ThenBy( n => n.Y ).ToList();
                OrderDirty = false;
            }

            return Nodes.Where( n => n.X == depth ).ToList();
        }

        public static List<Node> Row( int Y )
        {
            return Nodes.Where( n => n.Y == Y ).ToList();
        }

        public new static string ToString()
        {
            var text = new StringBuilder();

            for ( var l = 1; l <= Nodes.Max( n => n.X ); l++ )
            {
                text.AppendLine( $"Layer {l}:" );
                var layer = Layer( l, true );

                foreach ( var n in layer )
                {
                    text.AppendLine( $"\t{n}" );
                    text.AppendLine( "\t\tAbove: " +
                                     string.Join( ", ", n.InNodes.Select( a => a.ToString() ).ToArray() ) );
                    text.AppendLine( "\t\tBelow: " +
                                     string.Join( ", ", n.OutNodes.Select( b => b.ToString() ).ToArray() ) );
                }
            }

            return text.ToString();
        }

        public static void DebugStatus()
        {
            Log.Message( "duplicated positions:\n " +
                         string.Join(
                             "\n",
                             Nodes.Where( n => Nodes.Any( n2 => n != n2 && n.X == n2.X && n.Y == n2.Y ) )
                                  .Select( n => n.X + ", " + n.Y + ": " + n.Label ).ToArray() ) );
            Log.Message( "out-of-bounds nodes:\n" +
                         string.Join(
                             "\n", Nodes.Where( n => n.X < 1 || n.Y < 1 ).Select( n => n.ToString() ).ToArray() ) );
            Log.Trace( ToString() );
        }
    }
}