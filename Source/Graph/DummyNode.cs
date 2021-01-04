// DummyNode.cs
// Copyright Karel Kroeze, 2018-2020

using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace ResearchPal
{
    public class DummyNode : Node
    {
        #region Overrides of Node

        public override string Label => "DUMMY";

        #endregion

        #region Overrides of Node

#if DEBUG_DUMMIES
        public override void Draw()
        {
            // cop out if off-screen
            var screen = new Rect( MainTabWindow_ResearchTree._scrollPosition.x,
                                   MainTabWindow_ResearchTree._scrollPosition.y, Screen.width, Screen.height - 35 );
            if ( Rect.xMin > screen.xMax ||
                 Rect.xMax < screen.xMin ||
                 Rect.yMin > screen.yMax ||
                 Rect.yMax < screen.yMin )
            {
                return;
            }

            Widgets.DrawBox( Rect );
            Widgets.Label( Rect, Label );
        }
#endif

        #endregion

        public List<ResearchNode> Parent
        {
            get
            {
                return InNodes.OfType<ResearchNode>()
                    .Concat(InNodes.OfType<DummyNode>()
                        .SelectMany(n => n.Parent))
                    .ToList();
            }
        }

        public List<ResearchNode> Child
        {
            get
            {
                return OutNodes.OfType<ResearchNode>()
                    .Concat(OutNodes.OfType<DummyNode>().SelectMany(n => n.Child)).ToList();
            }
        }

        public override bool  Completed   => OutNodes.FirstOrDefault()?.Completed   ?? false;
        public override bool  Available   => OutNodes.FirstOrDefault()?.Available   ?? false;
        public override bool  Highlighted => OutNodes.FirstOrDefault()?.Highlighted ?? false;
        public override Color Color       => OutNodes.FirstOrDefault()?.Color       ?? Color.white;
        public override Color EdgeColor   => OutNodes.FirstOrDefault()?.EdgeColor   ?? Color.white;

        public void Merge(DummyNode that) {
            foreach (var n in that.OutNodes) {
                if (! OutNodes.Contains(n)) {
                    _outEdges.Add(new Edge<Node, Node>(this, n));
                }
            }
            foreach (var n in that.InNodes) {
                if (! InNodes.Contains(n)) {
                    _inEdges.Add(new Edge<Node, Node>(n, this));
                }
            }
        }

        public override int CompareTieBreaker(Node that)
        {
            if (that is DummyNode) {
                return 0;
            }
            return 1;
        }

    }
}