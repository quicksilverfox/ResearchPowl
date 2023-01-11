using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using System.Text.RegularExpressions;

namespace ResearchPowl
{
    public class NodeLayers
    {
        public List<NodeLayer> _layers;

        void InitializeWithLists(List<List<Node>> layers)
        {
            _layers = new List<NodeLayer>(layers.Select((layer, idx) => new NodeLayer(idx, layer, this)));
        }

        public NodeLayers(List<List<Node>> layers) {
            InitializeWithLists(layers);
        }

        private NodeLayers(List<Node> nodes) {
            var layers = new List<List<Node>>();
            var length = _layers.Count;
            foreach (var node in nodes)
            {
                var nodeX = node.X;
                if (nodeX > length)
                {
                    for (int i = length; i < nodeX; ++i) layers.Add(new List<Node>());
                }
                layers[nodeX - 1].Add(node);
            }
        }

        public int LayerCount() => _layers.Count;

        public int NodeCount() => _layers.Select(l => l._nodes.Count).Sum();

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
            var layerCount = LayerCount();
            for (int n = 0; n < maxIter; ++n) {
                for (int i = 1; i < layerCount; ++i) {
                    _layers[i].SortByUpperBarycenter();
                }
                for (int i = layerCount - 2; i >= 0; --i) {
                    _layers[i].SortByLowerBarycenter();
                }
            }
        }

        private void NLevelBCMethod(int maxIter1, int maxIter2) {
            NLevelBCPhase1(maxIter1);
            var layerCount = LayerCount();
            for (int k = 0; k < maxIter2; ++k)
            {
                for (int i = layerCount - 2; i >= 0; --i)
                {
                    if ( _layers[i].ReverseLowerBarycenterTies() && ! MathUtil.Ascending(_layers[i + 1]._nodes.Select(n => n.UpperBarycenter())))
                    {
                        NLevelBCPhase1(maxIter1);
                    }
                }
                for (int i = 1; i < layerCount; ++i)
                {
                    if ( _layers[i].ReverseUpperBarycenterTies() && ! MathUtil.Ascending(_layers[i - 1]._nodes.Select(n => n.LowerBarycenter())))
                    {
                        NLevelBCPhase1(maxIter1);
                    }
                }
            }
        }

        private void BruteforceSwapping(int maxIter) {
            int layerCount = LayerCount();
            for (int k = 0; k < maxIter; ++k) {
                for (int i = 1; i < layerCount; ++i) _layers[i].UnsafeBruteforceSwapping();
                for (int i = layerCount - 2; i >= 0; --i) _layers[i].UnsafeBruteforceSwapping();
            }

            foreach (var layer in _layers) layer.RearrangeOrder();
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
            foreach (var layer in _layers) layer.AssignPositionPriorities();

            var length = LayerCount();
            for (int i = 1; i < length; ++i) {
                _layers[i].ImprovePositionAccordingToUpper();
            }
            for (int i = length - 2; i >= 0; --i) {
                _layers[i].ImprovePositionAccordingToLower();
            }
            for (int i = 1; i < length; ++i) {
                _layers[i].ImprovePositionAccordingToUpper();
            }
            if (! ModSettings_ResearchPowl.alignToAncestors) {
                for (int i = LayerCount() - 2; i >= 0; --i) {
                    _layers[i].ImprovePositionAccordingToLower();
                }
            }
            AlignSegments(3);
        }

        public void MoveVertically(float f)
        {
            foreach (var l in _layers) l.MoveVertically(f);
        }
        public float BottomPosition(int l) {
            return _layers[l].BottomPosition();
        }
        public float TopPosition(int l) {
            return _layers[l].TopPosition();
        }
        public IEnumerable<Node> AllNodes()
        {
            foreach (var item in _layers)
            {
                foreach (var item2 in item) yield return item2;
            }
        }
        static List<List<Node>> EmptyNewLayers(int n)
        {
            var result = new List<List<Node>>();
            for (int i = 0; i < n; ++i)
            {
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

        static void AlignNode(Node node) {
            bool aligned = false;
            do {
                var segment = node.LocalSegment();
                aligned = segment.Align();
            } while (aligned);
        }

        public void AlignSegments(int maxIter)
        {
            for (int n = 0; n < maxIter; ++n)
            {
                if (ModSettings_ResearchPowl.alignToAncestors)
                {
                    for (int i = 0; i < LayerCount(); ++i)
                    {
                        foreach (var item in Layer(i)._nodes) AlignNode(item);
                    }
                }
                else
                {
                    for (int i = LayerCount() - 1; i >= 0; --i)
                    {
                        foreach (var item in Layer(i)._nodes) AlignNode(item);
                    }
                }
            }
        }

        static string GroupingByMods(Node node) {
            if (node is ResearchNode)
            {
                var n = node as ResearchNode;
                if (n.Research.modContentPack == null) Log.Debug("Research {0} does not belong to any mod?", n.Label);
                var name = n.Research.modContentPack?.Name ?? "__Vanilla";
                //Is an official mod?
                if (name == ModContentPack.LudeonPackageIdAuthor) return "__Vanilla";
                //Is a VE mod?
                else if ( (new Regex("^Vanilla (.*)Expanded( - .*)?$")).IsMatch(name) || (new Regex("VFE")).IsMatch(name)) return "__VanillaExpanded";
                return name;
            }
            else if (node is DummyNode) return GroupingByMods(node.OutNodes()[0]);
            return "";
        }
        
        public List<NodeLayers> SplitLargeMods()
        {
            var result = new List<List<List<Node>>>();
            var vanilla = EmptyNewLayers(LayerCount());
            result.Add(vanilla);
            var list = AllNodes().GroupBy(n => GroupingByMods(n)).ToList();
            foreach (var group in list)
            {
                var ns = new List<Node>(group);
                var techCount = ns.OfType<ResearchNode>().Count();
                if (group.Key == "__Vanilla" || techCount < ModSettings_ResearchPowl.largeModTechCount)
                {
                    MergeDataFromTo(ns, vanilla);
                }
                else
                {
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
        public List<Node> _nodes;
        public int _layer;
        public NodeLayers _layers;

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

        public bool AdjustY() {
            bool changed = false;
            var length = _nodes.Count;
            for (int i = 0; i < length; ++i) {
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
            if (IsBottomLayer()) return 0;
            int sum = 0;
            var length = _nodes.Count;
            for (int i = 0; i < length - 1; ++i)
            {
                var list = new List<Node>(_nodes[i].OutNodes().Where(n => n.layer == LowerLayer()));
                for (int j = i + 1; j < length; ++j)
                {
                    var list2 = new List<Node>(_nodes[j].OutNodes().Where(n => n.layer == LowerLayer()));
                    foreach (var ni in list)
                    {
                        foreach (var nj in list2) if (ni.ly > nj.ly) ++sum;
                    }
                }
            }
            return sum;
        }

        public int UpperCrossings() {
            if (IsTopLayer()) return 0;
            int sum = 0;
            var lenght = _nodes.Count;
            for (int i = 0; i < lenght - 1; ++i)
            {
                var list = _nodes[i].InNodes().Where(n => n.layer == UpperLayer()).ToList();
                for (int j = i + 1; j < lenght; ++j)
                {
                    var list2 = _nodes[j].InNodes().Where(n => n.layer == UpperLayer()).ToList();
                    foreach (var ni in list)
                    {
                        foreach (var nj in list2) if (ni.ly > nj.ly) ++sum;
                    }
                }
            }
            return sum;
        }

        private void SwapNodeY(int i, int j)
        {
            int temp = _nodes[i].ly;
            _nodes[i].ly = _nodes[j].ly;
            _nodes[j].ly = temp;
        }

        public void UnsafeBruteforceSwapping()
        {
            int nodes = _nodes.Count;
            for (int i = 0; i < nodes - 1; ++i)
            {
                for (int j = i + 1; j < nodes; ++j)
                {
                    Node ni = _nodes[i], nj = _nodes[j];
                    int c1 = ni.Crossings() + nj.Crossings();
                    int l1 = ni.EdgeLengthSquare() + nj.EdgeLengthSquare();
                    SwapNodeY(i, j);
                    int c2 = ni.Crossings() + nj.Crossings();
                    int l2 = ni.EdgeLengthSquare() + nj.EdgeLengthSquare();
                    if (c2 < c1 || c2 == c1 && l2 < l1) continue;
                    SwapNodeY(i, j);
                }
            }
        }

        public void RearrangeOrder()
        {
            _nodes.SortBy(n => n.ly);
            AdjustY();
        }

        public IEnumerator<Node> GetEnumerator()
        {
            return _nodes.GetEnumerator();
        }

        public bool SortByUpperBarycenter()
        {
            foreach (var n in _nodes) n.doubleCache = n.UpperBarycenter();
            return SortBy((n1, n2) => n1.doubleCache.CompareTo(n2.doubleCache));
        }
        public bool SortByLowerBarycenter()
        {
            foreach (var n in _nodes) n.doubleCache = n.LowerBarycenter();
            return SortBy((n1, n2) => n1.doubleCache.CompareTo(n2.doubleCache));
        }
        void ReverseSegment(int i, int j) {
            for (--j; i < j; ++i, --j)
            {
                Node temp = _nodes[i];
                _nodes[i] = _nodes[j];
                _nodes[j] = temp;
            }
        }

        public bool ReverseLowerBarycenterTies()
        {
            var length = _nodes.Count;
            for (int i = 0; i < length - 1; )
            {
                var bi = _nodes[i].LowerBarycenter();
                int j = i + 1;
                //for (; j < length && MathUtil.FloatEqual(bi, _nodes[j].LowerBarycenter()); ++j) continue;
                ReverseSegment(i, j);
                i = j;
            }
            return AdjustY();
        }

        public bool ReverseUpperBarycenterTies()
        {
            var length = _nodes.Count;
            for (int i = 0; i < length - 1; )
            {
                var bi = _nodes[i].UpperBarycenter();
                int j = i + 1;
                //for (; j < length && MathUtil.FloatEqual(bi, _nodes[j].UpperBarycenter()); ++j) continue;
                ReverseSegment(i, j);
                i = j;
            }
            return AdjustY();
        }

        public void ApplyGridCoordinates() {
            var length = _nodes.Count;
            for (int i = 0; i < length; ++i) {
                var n = _nodes[i];
                n.X = _layer + 1;
                n.Y = i + 1;
            }
        }
        public void AssignPositionPriorities()
        {
            List<Node> ordering = new List<Node>(_nodes.OrderBy(n => n.DefaultPriority()));
            var length = ordering.Count;
            for (int i = 0; i < length; ++i) {
                ordering[i].assignedPriority = i;
            }
        }
        public void ImprovePositionAccordingToLower()
        {
            var list = new List<Node>(_nodes.OrderByDescending(n => n.LayoutPriority()));
            foreach (var n in list)
            {
                float c = (float) Math.Round(n.LowerPositionBarycenter());
                if (MathUtil.FloatEqual(c, n.Yf)) continue;
                
                if (c < n.Yf) n.PushUpTo(c);
                else n.PushDownTo(c);
            }
        }
        public void ImprovePositionAccordingToUpper()
        {
            var list = new List<Node>(_nodes.OrderByDescending(n => n.LayoutPriority()));
            foreach (var n in list)
            {
                float c = (float) Math.Round(n.UpperPositionBarycenter());
                if (MathUtil.FloatEqual(c, n.Yf)) continue;
                if (c < n.Yf) n.PushUpTo(c);
                else n.PushDownTo(c);
            }
        }
        public float TopPosition()
        {
            if (_nodes.Count == 0) return 99999;
            return _nodes[0].Yf;
        }

        public float BottomPosition()
        {
            if (_nodes.Count == 0) return -99999;
            return _nodes[_nodes.Count - 1].Yf;
        }

        public void MoveVertically(float f)
        {
            foreach (var n in _nodes) n.Yf = n.Yf + f;
        }

        public bool IsTopLayer() => _layer == 0;
        public bool IsBottomLayer() => _layer >= _layers.LayerCount();
        public NodeLayer UpperLayer() => IsTopLayer() ? null : _layers.Layer(_layer - 1);
        public NodeLayer LowerLayer() => IsBottomLayer() ? null : _layers.Layer(_layer + 1);

    }

    static class MathUtil {
        public static bool SignDiff(int x, int y)
        {
            return x < 0 && y > 0 || x > 0 && y < 0;
        }
        public static bool SignDiff(float x, float y)
        {
            return x < 0 && y > 0 || x > 0 && y < 0;
        }

        public static bool FloatEqual(double x, double y)
        {
            return Math.Abs(x - y) < 0.00001;
        }

        public static bool Ascending(IEnumerable<double> xs)
        {
            return xs.Zip(xs.Skip(1), (a, b) => new {a, b}).All(p => p.a <= p.b);
        }
    }

    static class NodeUtil {
        static float MinimumVerticalDistance = 1;
        
        public static int LowerCrossings(this Node n1) {
            int sum = 0;
            var list1 = new List<Node>(n1.layer._nodes.Where(n => n != n1));
            var list2 = n1.LocalOutNodes();
            foreach (var n2 in list1)
            {
                var list3 = n2.LocalOutNodes();
                foreach (var m1 in list2)
                {
                    foreach (var m2 in list3) if (MathUtil.SignDiff(n1.ly - n2.ly, m1.ly - m2.ly)) ++sum;
                }
            }
            return sum;
        }

        public static int UpperCrossings(this Node n1) {
            int sum = 0;
            var list1 = new List<Node>(n1.layer._nodes.Where(n => n != n1));
            var list2 = n1.LocalInNodes();
            foreach (var n2 in list1) {
                var list3 = n2.LocalInNodes();
                foreach (var m1 in list2)
                {
                    foreach (var m2 in list3) if (MathUtil.SignDiff(n1.ly - n2.ly, m1.ly - m2.ly)) ++sum;
                }
            }
            return sum;
        }

        public static int UpperEdgeLengthSquare(this Node node) {
            var tmp = node.LocalInNodes();
            var length = tmp.Count;
            int sum = 0;
            for (int i = 0; i < length; ++i)
            {
                var tmp2 = tmp[i];
                sum += (node.ly - tmp2.ly) * (node.ly - tmp2.ly);
            }
            return sum;
        }
        public static int LowerEdgeLengthSquare(this Node node)
        {
            var tmp = node.LocalOutNodes();
            var length = tmp.Count;
            int sum = 0;
            for (int i = 0; i < length; ++i)
            {
                var tmp2 = tmp[i];
                sum += (node.ly - tmp2.ly) * (node.ly - tmp2.ly);
            }
            return sum;
        }

        public static int EdgeLengthSquare(this Node node)
        {
            return node.UpperEdgeLengthSquare() + node.LowerEdgeLengthSquare();
        }

        public static int Crossings(this Node n)
        {
            return LowerCrossings(n) + UpperCrossings(n);
        }

        public static double LowerBarycenter(this Node node)
        {
            List<Node> outs = node.LocalOutNodes();
            if (outs.Count == 0) return node.ly;
            return outs.Sum(n => n.ly) / (double) outs.Count;
        }
        public static double UpperBarycenter(this Node node)
        {
            List<Node> ins = node.LocalInNodes();
            if (ins.Count == 0) return node.ly;
            return ins.Sum(n => n.ly) / (double) ins.Count;
        }

        public static float LowerPositionBarycenter(this Node node)
        {
            List<Node> outs = node.LocalOutNodes();
            if (outs.Count == 0) return node.Yf;
            return outs.Sum(n => n.Yf) / outs.Count;
        }

        public static float UpperPositionBarycenter(this Node node)
        {
            List<Node> ins = node.LocalInNodes();
            if (ins.Count == 0) return node.Yf;
            return ins.Sum(n => n.Yf) / ins.Count;
        }

        public static Node MovingUpperbound(this Node node)
        {
            for (int i = node.ly - 1; i >= 0; --i)
            {
                if (node.layer[i].LayoutPriority() > node.LayoutPriority()) return node.layer[i];
            }
            return null;
        }
        public static Node MovingLowerbound(this Node node)
        {
            for (int i = node.ly + 1; i < node.layer._nodes.Count; ++i)
            {
                if (node.layer[i].LayoutPriority() > node.LayoutPriority()) return node.layer[i];
            }
            return null;
        }
        public static void PushUpTo(this Node node, float target) {
            Node blocker = node.MovingUpperbound();
            var layer = node.layer;
            if (blocker == null) node.Yf = target;
            else node.Yf = Math.Max(blocker.Yf + (node.ly - blocker.ly) * MinimumVerticalDistance, target);
            
            for (int i = node.ly - 1; i > (blocker?.ly ?? -1) && layer[i].Yf > layer[i + 1].Yf - MinimumVerticalDistance; --i)
            {
                layer[i].Yf = layer[i + 1].Yf - MinimumVerticalDistance;
            }
        }

        public static void PushDownTo(this Node node, float target) {
            Node blocker = node.MovingLowerbound();
            var layer = node.layer;
            if (blocker == null) node.Yf = target;
            else node.Yf = Math.Min(blocker.Yf - (blocker.ly - node.ly) * MinimumVerticalDistance, target);
            for ( int i = node.ly + 1; i < (blocker?.ly ?? layer._nodes.Count) && layer[i].Yf < layer[i - 1].Yf + MinimumVerticalDistance; ++i)
            {
                layer[i].Yf = layer[i - 1].Yf + MinimumVerticalDistance;
            }
        }

        public static List<Node> LocalOutNodes(this Node node) {
            
            var list = new List<Node>(node.OutNodes());
            var length = list.Count;
            var workingList = new List<Node>();
            for (int i = 0; i < length; i++)
            {
                var tmp = list[i];
                if (tmp.layer == node.layer.LowerLayer()) workingList.Add(tmp);
            }
            return workingList;
        }
        public static List<Node> LocalInNodes(this Node node)
        {
            var list = new List<Node>(node.OutNodes());
            var length = list.Count;
            var workingList = new List<Node>();
            for (int i = 0; i < length; i++)
            {
                var tmp = list[i];
                if (tmp.layer == node.layer.UpperLayer()) workingList.Add(tmp);
            }
            return workingList;
        }
        public static NodeSegment LocalSegment(this Node node) {
            List<Node> segment = new List<Node>();
            segment.Add(node);
            for (var outs = node.LocalOutNodes(); outs.Count == 1 && MathUtil.FloatEqual(outs[0].Yf, node.Yf); outs = outs[0].LocalOutNodes())
            {
                segment.Add(outs[0]);
            }
            for (var ins = node.LocalInNodes(); ins.Count == 1 && MathUtil.FloatEqual(ins[0].Yf, node.Yf); ins = ins[0].LocalOutNodes())
            {
                segment.Insert(0, ins[0]);
            }
            return new NodeSegment(segment);
        }
    }

    public class NodeSegment
    {
        List<Node> _nodes;

        public NodeSegment(List<Node> nodes)
        {
            _nodes = nodes;
        }
        float UpperMaximumEmptySpace()
        {
            float result = 99999;
            foreach (var n in _nodes)
            {
                if (n.ly <= 0) continue;
                var layer = n.layer;
                result = Math.Min(result, n.Yf - layer[n.ly - 1].Yf - 1);
            }
            return result;
        }
        float LowerMaximumEmptySpace()
        {
            float result = 99999;
            foreach (var n in _nodes)
            {
                var layer = n.layer;
                if (n.ly >= layer._nodes.Count - 1) continue;
                result = Math.Min(result, layer[n.ly + 1].Yf - n.Yf - 1);
            }
            return result;
        }
        float SelectAppropriateMovement(IEnumerable<Node> alignTo, float pos)
        {
            var dys = alignTo.Select(n => n.Yf - pos).ToArray();
            float dymax = dys.Max(), dymin = dys.Min();
            if (MathUtil.SignDiff(dymax, dymin)) return (float) Math.Round(dys.Average());
            return dymin;
        }
        float? ForwardAlignTarget()
        {
            var outs = _nodes[_nodes.Count - 1].LocalOutNodes();
            if (outs.Count == 0) return null;
            return SelectAppropriateMovement(outs, _nodes[0].Yf);
        }
        float? BackwardAlignTarget()
        {
            var ins = _nodes[0].LocalInNodes();
            if (ins.Count == 0) return null;
            return SelectAppropriateMovement(ins, _nodes[0].Yf);
        }
        float DetermineMovement(float? attempt, out bool aligned)
        {
            aligned = false;
            if (attempt == null) return 0;
            if (attempt > 0)
            {
                var lm = LowerMaximumEmptySpace();
                if (lm >= attempt.Value)
                {
                    aligned = true;
                    return attempt.Value;
                }
                return lm;
            }
            if (attempt < 0)
            {
                var um = -UpperMaximumEmptySpace();
                if (um <= attempt.Value)
                {
                    aligned = true;
                    return attempt.Value;
                }
            }
            return 0;
        }
        float DetermineMovement(float? left, float? right, out bool aligned)
        {
            if (left == null) return DetermineMovement(right, out aligned);
            if (right == null) return DetermineMovement(left, out aligned);
            if (MathUtil.SignDiff(left.Value, right.Value))
            {
                var res = DetermineMovement((float) Math.Round((left.Value - right.Value) / 2), out aligned);
                aligned = false;
                return res;
            }
            if (Math.Abs(left.Value) < Math.Abs(right.Value)) return DetermineMovement(left.Value, out aligned);
            return DetermineMovement(right.Value, out aligned);
        }
        public bool Align()
        {
            bool aligned;
            float? left = BackwardAlignTarget(), right = ForwardAlignTarget();
            float movement = DetermineMovement(left, right, out aligned);
            foreach (var n in _nodes) n.Yf = n.Yf + movement; //was MoveVertically(movement);
            return aligned;
        }
    }
}
