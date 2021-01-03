using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using System.Text;

namespace ResearchPal
{
    public class NodeLayers
    {
        private static List<NodeLayer> _layers;

        private void InitializeWithLists(List<List<Node>> layers) {
            _layers = layers.Select((layer, idx) => new NodeLayer(idx, layer, this)).ToList();
        }

        public NodeLayers(List<List<Node>> layers) {
            InitializeWithLists(layers);
        }

        public static NodeLayers InitializeWithLegacyLogic(List<Node> nodes) {
            return new NodeLayers(nodes);
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
            NLevelBCMethod(5, 3);
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
    }

    public class NodeLayer
    {
        private List<Node> _nodes;
        private int _layer;
        private NodeLayers _layers;

        public List<Node> Nodes() {
            return _nodes;
        }

        public NodeLayer(int layer, List<Node> nodes, NodeLayers layers)
        {
            _layer = layer;
            _layers = layers;
            _nodes = nodes;
            foreach (var n in _nodes)
            {
                n.layer = this;
            }
            AdjustY();
        }

        public Node this[int i] {
            get {
                return _nodes[i];
            }
        }

        public int Count() {
            return _nodes.Count();
        }

        private bool AdjustY()
        {
            bool changed = false;
            for (int i = 0; i < _nodes.Count(); ++i) {
                changed = changed || _nodes[i].ly != i;
                _nodes[i].ly = i;
            }
            return changed;
        }

        public bool SortBy(Func<Node, Node, int> f)
        {
            _nodes.SortStable(f);
            return AdjustY();
        }

        public int LowerCrossings()
        {
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

        public int UpperCrossings()
        {
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
            for (int i = 0; i < Count(); ++i) {
                for (int j = 0; j < Count(); ++j) {
                    int c1 = _nodes[i].Crossings() + _nodes[j].Crossings();
                    SwapNodeY(i, j);
                    int c2 = _nodes[i].Crossings() + _nodes[j].Crossings();
                    if (c1 <= c2) SwapNodeY(i, j);
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
                n.sortingWeight = n.UpperBarycenter();
            }
            _nodes.SortBy(n => n.sortingWeight);
            return AdjustY();
        }
        public bool SortByLowerBarycenter() {
            foreach (var n in _nodes) {
                n.sortingWeight = n.LowerBarycenter();
            }
            _nodes.SortBy(n => n.sortingWeight);
            return AdjustY();
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
            return _nodes.First().Yf;
        }

        public float BottomPosition() {
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
            return xs.Zip(xs.Skip(1), (a, b) => new {a, b}).All(p => p.a < p.b);
        }
    }

    static class NodeUtil {

        public static int LowerCrossings(this Node n1) {
            int sum = 0;
            foreach (var n2 in n1.layer.Nodes().Where(n => n != n1)) {
                foreach (var m1 in n1.OutNodes.Where(n => n.layer == n1.layer.LowerLayer())) {
                    foreach (var m2 in n2.OutNodes.Where(n => n.layer == n1.layer.LowerLayer())) {
                        if (MathUtil.SignDiff(n1.ly - n2.ly, m1.ly - m2.ly)) ++sum;
                    }
                }
            }
            return sum;
        }

        public static int UpperCrossings(this Node n1) {
            int sum = 0;
            foreach (var n2 in n1.layer.Nodes().Where(n => n != n1)) {
                foreach (var m1 in n1.InNodes.Where(n => n.layer == n1.layer.UpperLayer())) {
                    foreach (var m2 in n2.InNodes.Where(n => n.layer == n1.layer.UpperLayer())) {
                        if (MathUtil.SignDiff(n1.ly - n2.ly, m1.ly - m2.ly)) ++sum;
                    }
                }
            }
            return sum;
        }

        public static int Crossings(this Node n) {
            return LowerCrossings(n) + UpperCrossings(n);
        }

        public static double LowerBarycenter(this Node node) {
            List<Node> outs = node.OutNodes.Where(n => n.layer == node.layer.LowerLayer()).ToList();
            if (outs.Count() == 0) {
                return node.ly;
            }
            return outs.Sum(n => n.ly) / (double) outs.Count();
        }
        public static double UpperBarycenter(this Node node) {
            List<Node> ins = node.InNodes.Where(n => n.layer == node.layer.UpperLayer()).ToList();
            if (ins.Count() == 0) {
                return node.ly;
            }
            return ins.Sum(n => n.ly) / (double) ins.Count();
        }

        public static float LowerPositionBarycenter(this Node node) {
            List<Node> outs = node.OutNodes.Where(n => n.layer == node.layer.LowerLayer()).ToList();
            if (outs.Count() == 0) {
                return node.Yf;
            }
            return outs.Sum(n => n.Yf) / outs.Count();
        }

        public static float UpperPositionBarycenter(this Node node) {
            List<Node> ins = node.InNodes.Where(n => n.layer == node.layer.UpperLayer()).ToList();
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
    }
}
