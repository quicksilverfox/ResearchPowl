using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using System.Text.RegularExpressions;

namespace ResearchPal
{
    public class NodeLayers
    {
        private List<NodeLayer> _layers;

        private void InitializeWithLists(List<List<Node>> layers) {
            _layers = layers.Select((layer, idx) => new NodeLayer(idx, layer, this)).ToList();
        }

        public NodeLayers(List<List<Node>> layers) {
            InitializeWithLists(layers);
        }

        public static NodeLayers InitializeWithLegacyLogic(List<Node> nodes) {
            return new NodeLayers(nodes);
        }

        public void Merge(NodeLayers layers) {
            for (int i = 0; i < LayerCount(); ++i) {
                Layer(i).Merge(layers.Layer(i));
            }
        }

        private NodeLayers(List<Node> nodes) {
            var layers = new List<List<Node>>();
            foreach (var node in nodes)
            {
                if (node.X > _layers.Count()) {
                    for (int i = _layers.Count(); i < node.X; ++i) {
                        layers.Add(new List<Node>());
                    }
                }
                layers[node.X - 1].Add(node);
            }
        }

        public int LayerCount() => _layers.Count();

        public int NodeCount() => _layers.Select(l => l.Count()).Sum();

        public NodeLayer Layer(int n) {
            return _layers[n];
        }

        public int Crossings() {
            int sum = 0;
            for (int i = 0; i < LayerCount() - 1; ++i) {
                sum += Layer(i).LowerCrossings();
            }
            return sum;
        }

        private void NLevelBCPhase1(int maxIter) {
            for (int n = 0; n < maxIter; ++n) {
                for (int i = 1; i < LayerCount(); ++i) {
                    Layer(i).SortByUpperBarycenter();
                }
                for (int i = LayerCount() - 2; i >= 0; --i) {
                    Layer(i).SortByLowerBarycenter();
                }
            }
        }

        private void NLevelBCMethod(int maxIter1, int maxIter2) {
            NLevelBCPhase1(maxIter1);
            for (int k = 0; k < maxIter2; ++k) {
                for (int i = LayerCount() - 2; i >= 0; --i) {
                    if ( Layer(i).ReverseLowerBarycenterTies()
                        && ! MathUtil.Ascending(
                            Layer(i + 1).Nodes().Select(
                                n => n.UpperBarycenter()))) {
                        NLevelBCPhase1(maxIter1);
                    }
                }
                for (int i = 1; i < LayerCount(); ++i) {
                    if ( Layer(i).ReverseUpperBarycenterTies()
                        && ! MathUtil.Ascending(
                            Layer(i - 1).Nodes().Select(
                                n => n.LowerBarycenter()))) {
                        NLevelBCPhase1(maxIter1);
                    }
                }
            }
        }

        private void BruteforceSwapping(int maxIter) {
            for (int k = 0; k < maxIter; ++k) {
                for (int i = 1; i < LayerCount(); ++i) {
                    Layer(i).UnsafeBruteforceSwapping();
                }
                for (int i = _layers.Count() - 2; i >= 0; --i) {
                    Layer(i).UnsafeBruteforceSwapping();
                }
            }
            foreach (var layer in _layers) {
                layer.RearrangeOrder();
            }
        }

        public void MinimizeCrossings()
        {
            NLevelBCMethod(4, 3);
            BruteforceSwapping(3);
        }

        public void ApplyGridCoordinates() {
            foreach (var layer in _layers) {
                layer.ApplyGridCoordinates();
            }
        }

        public void ImproveNodePositionsInLayers() {
            _layers.ForEach(layer => layer.AssignPositionPriorities());
            for (int i = 1; i < LayerCount(); ++i) {
                Layer(i).ImprovePositionAccordingToUpper();
            }
            for (int i = LayerCount() - 2; i >= 0; --i) {
                Layer(i).ImprovePositionAccordingToLower();
            }
            for (int i = 1; i < LayerCount(); ++i) {
                Layer(i).ImprovePositionAccordingToUpper();
            }
        }

        public void MoveVertically(float f) {
            _layers.ForEach(l => l.MoveVertically(f));
        }

        public void AlignTopAt(float f) {
            float top = _layers.Select(l => l.TopPosition()).Min();
            MoveVertically(f - top);
        }

        private void AdjustLayerData() {
            _layers.ForEach(l => l.AdjustY());
        }

        public float BottomPosition() {
            return _layers.Select(l => l.BottomPosition()).Max();
        }
        public float BottomPosition(int l) {
            return _layers[l].BottomPosition();
        }
        public float TopPosition() {
            return _layers.Select(l => l.TopPosition()).Max();
        }
        public float TopPosition(int l) {
            return _layers[l].TopPosition();
        }

        public IEnumerable<Node> AllNodes() {
            return _layers.SelectMany(l => l.Nodes());
        }

        private static List<List<Node>> EmptyNewLayers(int n) {
            var result = new List<List<Node>>();
            for (int i = 0; i < n; ++i) {
                result.Add(new List<Node>());
            }
            return result;
        }

        private static void MergeDataFromTo(Node n, List<List<Node>> data) {
            data[n.lx].Add(n);
        }

        private static void MergeDataFromTo(IEnumerable<Node> ns, List<List<Node>> data) {
            foreach (var n in ns) {
                MergeDataFromTo(n, data);
            }
        }

        private void DFSConnectiveComponents(
            Node cur, List<List<Node>> data, HashSet<Node> visited) {
            if (cur == null) return;
            visited.Add(cur);
            data[cur.lx].Add(cur);
            foreach (var n in cur.LocalInNodes()) {
                if (! visited.Contains(n)) {
                    DFSConnectiveComponents(n, data, visited);
                }
            }
            foreach (var n in cur.LocalOutNodes()) {
                if (! visited.Contains(n)) {
                    DFSConnectiveComponents(n, data, visited);
                }
            }
        }

        public List<NodeLayers> SplitConnectiveComponents() {
            HashSet<Node> visited = new HashSet<Node>();
            List<NodeLayers> result = new List<NodeLayers>();
            foreach (var node in AllNodes()) {
                if (! visited.Contains(node)) {
                    var data = EmptyNewLayers(LayerCount());
                    DFSConnectiveComponents(node, data, visited);
                    result.Add(new NodeLayers(data));
                }
            }
            return result;
        }

        private static string GroupingByMods(Node node) {
            if (node is ResearchNode) {
                var n = node as ResearchNode;
                if (n.Research.modContentPack == null) {
                    Log.Warning("Research {0} do not belongs to any mod?", n.Label);
                }
                var name = n.Research.modContentPack?.Name ?? "__Vanilla";
                if (name == "Royalty" || name == "Core") {
                    return "__Vanilla";
                } else if (
                       (new Regex("^Vanilla (.*)Expanded( - .*)?$")).IsMatch(name)
                    || (new Regex("VFE")).IsMatch(name)) {
                    return "__VanillaExpanded";
                }
                return name;
            } else if (node is DummyNode) {
                return GroupingByMods(node.OutNodes.First());
            }
            return "";
        }
        
        public List<NodeLayers> SplitLargeMods() {
            var result = new List<List<List<Node>>>();
            var vanilla = EmptyNewLayers(LayerCount());
            result.Add(vanilla);
            foreach (var group in AllNodes().GroupBy(n => GroupingByMods(n))) {
                var ns = group.ToList();
                var techCount = ns.OfType<ResearchNode>().Count();
                Log.Message("Mod {0} has {1} techs", group.Key, techCount);
                if (  group.Key == "__Vanilla"
                   || techCount < Settings.largeModTechCount) {
                    MergeDataFromTo(ns, vanilla);
                } else {
                    var newMod = EmptyNewLayers(LayerCount());
                    MergeDataFromTo(ns, newMod);
                    result.Add(newMod);
                }
            }
            return result.Select(d => new NodeLayers(d)).ToList();
        }
    }

    public class NodeLayer
    {
        private List<Node> _nodes;
        public int _layer;
        public NodeLayers _layers;

        public List<Node> Nodes() {
            return _nodes;
        }

        public NodeLayer(int layer, List<Node> nodes, NodeLayers layers) {
            _layer = layer;
            _layers = layers;
            _nodes = nodes;
            foreach (var n in _nodes) {
                n.layer = this;
                n.lx = layer;
            }
            AdjustY();
        }

        public Node this[int i] {
            get {
                return _nodes[i];
            }
        }

        public void Merge(NodeLayer that) {
            foreach (var n in that.Nodes()) {
                n.layer = this;
                n.lx = _layer;
            }
            _nodes.Concat(that.Nodes());
            AdjustY();
        }

        public int Count() {
            return _nodes.Count();
        }

        public bool AdjustY() {
            bool changed = false;
            for (int i = 0; i < _nodes.Count(); ++i) {
                changed = changed || _nodes[i].ly != i;
                _nodes[i].ly = i;
            }
            return changed;
        }

        public bool SortBy(Func<Node, Node, int> f) {
            _nodes.SortStable(f);
            return AdjustY();
        }

        public int LowerCrossings() {
            if (IsBottomLayer())
                return 0;
            int sum = 0;
            for (int i = 0; i < _nodes.Count() - 1; ++i) {
                for (int j = i + 1; j < _nodes.Count(); ++j) {
                    foreach (var ni in _nodes[i].OutNodes.Where(n => n.layer == LowerLayer())) {
                        foreach (var nj in _nodes[j].OutNodes.Where(n => n.layer == LowerLayer())) {
                            if (ni.ly > nj.ly) ++sum;
                        }
                    }
                }
            }
            return sum;
        }

        public int UpperCrossings() {
            if (IsTopLayer())
                return 0;
            int sum = 0;
            for (int i = 0; i < _nodes.Count() - 1; ++i) {
                for (int j = i + 1; j < _nodes.Count(); ++j) {
                    foreach (var ni in _nodes[i].InNodes.Where(n => n.layer == UpperLayer())) {
                        foreach (var nj in _nodes[j].InNodes.Where(n => n.layer == UpperLayer())) {
                            if (ni.ly > nj.ly) ++sum;
                        }
                    }
                }
            }
            return sum;
        }

        private void SwapNodeY(int i, int j) {
            int temp = _nodes[i].ly;
            _nodes[i].ly = _nodes[j].ly;
            _nodes[j].ly = temp;
        }

        public void UnsafeBruteforceSwapping() {
            for (int i = 0; i < Count() - 1; ++i) {
                for (int j = i + 1; j < Count(); ++j) {
                    Node ni = _nodes[i], nj = _nodes[j];
                    int c1 = ni.Crossings() + nj.Crossings();
                    int l1 = ni.EdgeLengthSquare() + nj.EdgeLengthSquare();
                    SwapNodeY(i, j);
                    int c2 = ni.Crossings() + nj.Crossings();
                    int l2 = ni.EdgeLengthSquare() + nj.EdgeLengthSquare();
                    if (c2 < c1 || c2 == c1 && l2 < l1)
                        continue;
                    SwapNodeY(i, j);
                }
            }
        }

        public void RearrangeOrder() {
            _nodes.SortBy(n => n.ly);
            AdjustY();
        }

        public IEnumerator<Node> GetEnumerator() {
            return _nodes.GetEnumerator();
        }

        public bool SortByUpperBarycenter() {
            foreach (var n in _nodes) {
                n.doubleCache = n.UpperBarycenter();
            }
            return SortBy((n1, n2) => n1.doubleCache.CompareTo(n2.doubleCache));
        }
        public bool SortByLowerBarycenter() {
            foreach (var n in _nodes) {
                n.doubleCache = n.LowerBarycenter();
            }
            return SortBy((n1, n2) => n1.doubleCache.CompareTo(n2.doubleCache));
        }

        private void ReverseSegment(int i, int j) {
            for (--j; i < j; ++i, --j) {
                Node temp = _nodes[i];
                _nodes[i] = _nodes[j];
                _nodes[j] = temp;
            }
        }

        public bool ReverseLowerBarycenterTies() {
            for (int i = 0; i < _nodes.Count() - 1; ) {
                var bi = _nodes[i].LowerBarycenter();
                int j = i + 1;
                for (; j < _nodes.Count()
                     && MathUtil.FloatEqual(bi, _nodes[j].LowerBarycenter())
                     ; ++j) continue;
                ReverseSegment(i, j);
                i = j;
            }
            return AdjustY();
        }

        public bool ReverseUpperBarycenterTies() {
            for (int i = 0; i < _nodes.Count() - 1; ) {
                var bi = _nodes[i].UpperBarycenter();
                int j = i + 1;
                for (; j < _nodes.Count()
                     && MathUtil.FloatEqual(bi, _nodes[j].UpperBarycenter())
                     ; ++j) continue;
                ReverseSegment(i, j);
                i = j;
            }
            return AdjustY();
        }

        public void ApplyGridCoordinates() {
            for (int i = 0; i < Count(); ++i) {
                var n = _nodes[i];
                n.X = _layer + 1;
                n.Y = i + 1;
            }
        }

        public void AssignPositionPriorities() {
            List<Node> ordering = _nodes.OrderBy(n => n.DefaultPriority()).ToList();
            for (int i = 0; i < ordering.Count(); ++i) {
                ordering[i].assignedPriority = i;
            }
        }

        public void ImprovePositionAccordingToLower() {
            foreach (var n in _nodes.OrderByDescending(n => n.LayoutPriority())) {
                float c = (float) Math.Round(n.LowerPositionBarycenter());
                if (MathUtil.FloatEqual(c, n.Yf)) {
                    continue;
                }
                if (c < n.Yf) {
                    n.PushUpTo(c);
                } else {
                    n.PushDownTo(c);
                }
            }
        }
        public void ImprovePositionAccordingToUpper() {
            foreach (var n in _nodes.OrderByDescending(n => n.LayoutPriority())) {
                float c = (float) Math.Round(n.UpperPositionBarycenter());
                if (MathUtil.FloatEqual(c, n.Yf)) {
                    continue;
                }
                if (c < n.Yf) {
                    n.PushUpTo(c);
                } else {
                    n.PushDownTo(c);
                }
            }
        }

        public float TopPosition() {
            if (Count() == 0) {
                return 99999;
            }
            return _nodes.First().Yf;
        }

        public float BottomPosition() {
            if (Count() == 0) {
                return -99999;
            }
            return _nodes.Last().Yf;
        }

        public void MoveVertically(float f) {
            _nodes.ForEach(n => n.Yf = n.Yf + f);
        }

        public bool IsTopLayer() => _layer == 0;
        public bool IsBottomLayer() => _layer >= _layers.LayerCount();
        public NodeLayer UpperLayer() => IsTopLayer() ? null : _layers.Layer(_layer - 1);
        public NodeLayer LowerLayer() => IsBottomLayer() ? null : _layers.Layer(_layer + 1);

    }

    static class MathUtil {
        public static bool SignDiff(int x, int y) {
            return x < 0 && y > 0 || x > 0 && y < 0;
        }

        public static bool FloatEqual(double x, double y) {
            return Math.Abs(x - y) < 0.00001;
        }

        public static bool Ascending(IEnumerable<double> xs) {
            return xs.Zip(xs.Skip(1), (a, b) => new {a, b}).All(p => p.a <= p.b);
        }
    }

    static class NodeUtil {

        public static int LowerCrossings(this Node n1) {
            int sum = 0;
            foreach (var n2 in n1.layer.Nodes().Where(n => n != n1)) {
                foreach (var m1 in n1.LocalOutNodes()) {
                    foreach (var m2 in n2.LocalOutNodes()) {
                        if (MathUtil.SignDiff(n1.ly - n2.ly, m1.ly - m2.ly)) ++sum;
                    }
                }
            }
            return sum;
        }

        public static int UpperCrossings(this Node n1) {
            int sum = 0;
            foreach (var n2 in n1.layer.Nodes().Where(n => n != n1)) {
                foreach (var m1 in n1.LocalInNodes()) {
                    foreach (var m2 in n2.LocalInNodes()) {
                        if (MathUtil.SignDiff(n1.ly - n2.ly, m1.ly - m2.ly)) ++sum;
                    }
                }
            }
            return sum;
        }

        public static int UpperEdgeLengthSquare(this Node node) {
            return node.LocalInNodes().Select(n => (node.ly - n.ly) * (node.ly - n.ly)).Sum();
        }
        public static int LowerEdgeLengthSquare(this Node node) {
            return node.LocalOutNodes().Select(n => (node.ly - n.ly) * (node.ly - n.ly)).Sum();
        }

        public static int EdgeLengthSquare(this Node node) {
            return node.UpperEdgeLengthSquare() + node.LowerEdgeLengthSquare();
        }

        public static int Crossings(this Node n) {
            return LowerCrossings(n) + UpperCrossings(n);
        }

        public static double LowerBarycenter(this Node node) {
            List<Node> outs = node.LocalOutNodes().ToList();
            if (outs.Count() == 0) {
                return node.ly;
            }
            return outs.Sum(n => n.ly) / (double) outs.Count();
        }
        public static double UpperBarycenter(this Node node) {
            List<Node> ins = node.LocalInNodes().ToList();
            if (ins.Count() == 0) {
                return node.ly;
            }
            return ins.Sum(n => n.ly) / (double) ins.Count();
        }

        public static float LowerPositionBarycenter(this Node node) {
            List<Node> outs = node.LocalOutNodes().ToList();
            if (outs.Count() == 0) {
                return node.Yf;
            }
            return outs.Sum(n => n.Yf) / outs.Count();
        }

        public static float UpperPositionBarycenter(this Node node) {
            List<Node> ins = node.LocalInNodes().ToList();
            if (ins.Count() == 0) {
                return node.Yf;
            }
            return ins.Sum(n => n.Yf) / ins.Count();
        }

        public static Node MovingUpperbound(this Node node) {
            for (int i = node.ly - 1; i >= 0; --i) {
                if (node.layer[i].LayoutPriority() > node.LayoutPriority()) {
                    return node.layer[i];
                }
            }
            return null;
        }
        public static Node MovingLowerbound(this Node node) {
            for (int i = node.ly + 1; i < node.layer.Count(); ++i) {
                if (node.layer[i].LayoutPriority() > node.LayoutPriority()) {
                    return node.layer[i];
                }
            }
            return null;
        }

        static float MinimumVerticalDistance = 1;

        public static void PushUpTo(this Node node, float target) {
            Node blocker = node.MovingUpperbound();
            var layer = node.layer;
            if (blocker == null) {
                node.Yf = target;
            } else {
                node.Yf = Math.Max(
                    blocker.Yf + (node.ly - blocker.ly) * MinimumVerticalDistance, target);
            }
            
            for ( int i = node.ly - 1
                ; i > (blocker?.ly ?? -1)
                    && layer[i].Yf > layer[i + 1].Yf - MinimumVerticalDistance
                ; --i) {
                layer[i].Yf = layer[i + 1].Yf - MinimumVerticalDistance;
            }
        }

        public static void PushDownTo(this Node node, float target) {
            Node blocker = node.MovingLowerbound();
            var layer = node.layer;
            if (blocker == null) {
                node.Yf = target;
            } else {
                node.Yf = Math.Min(
                    blocker.Yf - (blocker.ly - node.ly) * MinimumVerticalDistance, target);
            }
            for ( int i = node.ly + 1
                ; i < (blocker?.ly ?? layer.Count())
                    && layer[i].Yf < layer[i - 1].Yf + MinimumVerticalDistance
                ; ++i) {
                layer[i].Yf = layer[i - 1].Yf + MinimumVerticalDistance;
            }
        }

        public static IEnumerable<Node> LocalOutNodes(this Node node) {
            return node.OutNodes.Where(n => {
                return n.layer == node.layer.LowerLayer();
            });
        }
        public static IEnumerable<Node> LocalInNodes(this Node node) {
            return node.InNodes.Where(n => {
                return n.layer == node.layer.UpperLayer(); });
        }
        public static IEnumerable<Edge<Node, Node>> LocalOutEdges(this Node node) {
            return node.OutEdges.Where(e => e.Out.layer == node.layer.LowerLayer());
        }
        public static IEnumerable<Edge<Node, Node>> LocalInEdges(this Node node) {
            return node.InEdges.Where(e => e.In.layer == node.layer.LowerLayer());
        }
    }
}
