// DummyNode.cs
// Copyright Karel Kroeze, 2018-2020

using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using static ResearchPowl.Assets;
using static ResearchPowl.Constants;
using Verse;
using RimWorld;

namespace ResearchPowl
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
        public override bool Highlighted() {
            return OutResearch().HighlightInEdge(InResearch());
        }

        public ResearchNode OutResearch() {
            return OutEdges.First().OutResearch();
        }

        public ResearchNode InResearch() {
            return InEdges.First().InResearch();
        }

        public TechLevel OutTechLevel() {
            return OutResearch().Research.techLevel;
        }

        public override Color Color {
            get {
                return OutResearch().InEdgeColor(InResearch());
            }
        }

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