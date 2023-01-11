// Node.cs
// Copyright Karel Kroeze, 2019-2020

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static ResearchPowl.Constants;

namespace ResearchPowl
{
    public class Node
    {
        public List<Edge<Node, Node>> _inEdges = new List<Edge<Node, Node>>();
        protected bool _largeLabel, _rectsSet;
        public List<Edge<Node, Node>> _outEdges = new List<Edge<Node, Node>>();
        protected Vector2 _pos = Vector2.zero;
        protected Rect _queueRect, _rect, _labelRect, _costLabelRect, _costIconRect, _iconsRect, _lockRect;
        protected Vector2 _topLeft = Vector2.zero, _right = Vector2.zero, _left = Vector2.zero;

        public List<Node> Descendants()
        {
            List<Node> workingList = new List<Node>(OutNodes());
            foreach (var item in OutNodes()) workingList.AddRange(item.Descendants());
            return workingList;
        }

        public List<Node> OutNodes()
        {
            var workingList = new List<Node>();
            foreach (var item in _outEdges) workingList.Add(item._out);
            return workingList;
        }
        public List<Node> InNodes()
        {
            var workingList = new List<Node>();
            foreach (var item in _inEdges) workingList.Add(item._in);
            return workingList;
        }

        public Rect CostIconRect
        {
            get
            {
                if ( !_rectsSet )
                    SetRects();

                return _costIconRect;
            }
        }

        public Rect CostLabelRect
        {
            get
            {
                if ( !_rectsSet )
                    SetRects();

                return _costLabelRect;
            }
        }

        public virtual Color Color     => Color.white;
        public virtual Color InEdgeColor(ResearchNode from)
        {
            return Color;
        }

        public Rect IconsRect
        {
            get
            {
                if (!_rectsSet) SetRects();
                return _iconsRect;
            }
        }

        public Rect LabelRect
        {
            get
            {
                if ( !_rectsSet )
                    SetRects();

                return _labelRect;
            }
        }

        /// <summary>
        ///     Middle of left node edge
        /// </summary>
        public Vector2 Left
        {
            get
            {
                if ( !_rectsSet )
                    SetRects();

                return _left;
            }
        }

        /// <summary>
        ///     Tag UI Rect
        /// </summary>
        public Rect QueueRect
        {
            get
            {
                if ( !_rectsSet )
                    SetRects();

                return _queueRect;
            }
        }

        public Rect LockRect
        {
            get
            {
                if ( !_rectsSet )
                    SetRects();

                return _lockRect;
            }
        }

        /// <summary>
        ///     Static UI rect for this node
        /// </summary>
        public Rect Rect
        {
            get
            {
                if (!_rectsSet) SetRects();
                return _rect;
            }
        }

        /// <summary>
        ///     Middle of right node edge
        /// </summary>
        public Vector2 Right
        {
            get
            {
                if ( !_rectsSet )
                    SetRects();

                return _right;
            }
        }

        public Vector2 Center => ( Left + Right ) / 2f;

        public virtual int X
        {
            get => (int) _pos.x;
            set
            {
                if ( value < 0 )
                    throw new ArgumentOutOfRangeException( nameof( value ) );
                if ( Math.Abs( _pos.x - value ) < Epsilon )
                    return;

                _pos.x = value;

                // update caches
                _rectsSet       = false;
                // Tree.Size.x     = Tree.Nodes().Max( n => n.X );
                Tree.OrderDirty = true;
            }
        }

        public virtual int Y
        {
            get => (int) _pos.y;
            set
            {
                if ( value < 0 )
                    throw new ArgumentOutOfRangeException( nameof( value ) );
                if ( Math.Abs( _pos.y - value ) < Epsilon )
                    return;

                _pos.y = value;

                // update caches
                _rectsSet       = false;
                // Tree.Size.z     = Tree.Nodes().Max( n => n.Y );
                Tree.OrderDirty = true;
            }
        }

        public virtual Vector2 Pos => new Vector2( X, Y );

        public virtual float Yf
        {
            get => _pos.y;
            set
            {
                if ( Math.Abs( _pos.y - value ) < Epsilon )
                    return;

                _pos.y = value;

                // update caches
                // Tree.Size.z     = Tree.Nodes.Max( n => n.Y ) + 1;
                Tree.OrderDirty = true;
            }
        }

        public virtual string Label { get; }
        public virtual bool Highlighted()
        {
            return false;
        }

        public List<Node> MissingPrerequisiteNodes()
        {
            List<Node> results = new List<Node>();
            foreach (var n in InNodes())
            {
                if (n is ResearchNode rn)
                {
                    if (! rn.Research.IsFinished) 
                    {
                        results.Add(n);
                        results.AddRange(n.MissingPrerequisiteNodes());
                    }
                }
                else if (n is DummyNode dn)
                {
                    var temp = dn.MissingPrerequisiteNodes();
                    if (temp.Count != 0)
                    {
                        results.Add(dn);
                        results.AddRange(temp);
                    }
                }
            }
            return results;
        }

        protected internal virtual float SetDepth( int min = 1 )
        {
            // calculate desired position
            var isRoot  = InNodes().NullOrEmpty();
            var desired = isRoot ? 1 : InNodes().Max( n => n.X ) + 1;
            var depth   = Mathf.Max( desired, min );

            // update
            X = depth;
            return depth;
        }

        public override string ToString()
        {
            return Label + _pos;
        }

        public void SetRects()
        {
            // origin
            _topLeft = new Vector2(
                ( X  - 1 ) * ( NodeSize.x + NodeMargins.x ),
                ( Yf - 1 ) * ( NodeSize.y + NodeMargins.y ) );

            SetRects( _topLeft );
        }

        public void SetRects( Vector2 topLeft )
        {
            // main rect
            _rect = new Rect( topLeft.x,
                              topLeft.y,
                              NodeSize.x,
                              NodeSize.y );

            // left and right edges
            _left  = new Vector2( _rect.xMin, _rect.yMin + _rect.height / 2f );
            _right = new Vector2( _rect.xMax, _left.y );

            // queue rect
            _queueRect = new Rect( _rect.xMax - QueueLabelSize * 0.6f,
                                   _rect.yMin + ( _rect.height - QueueLabelSize ) / 2f, QueueLabelSize,
                                   QueueLabelSize );

            // label rect
            _labelRect = new Rect( _rect.xMin             + 6f,
                                   _rect.yMin             + 3f,
                                   _rect.width * 2f / 3f  - 6f,
                                   _rect.height * 2f / 3f);

            // research cost rect
            _costLabelRect = new Rect( _rect.xMin                  + _rect.width * 2f / 3f,
                                       _rect.yMin                  + 3f,
                                       _rect.width * 1f / 3f - 16f - 3f,
                                       _rect.height * .5f          - 3f );

            // research icon rect
            _costIconRect = new Rect( _costLabelRect.xMax,
                                      _rect.yMin + ( _costLabelRect.height - 16f ) / 2,
                                      16f,
                                      16f );

            // icon container rect
            _iconsRect = new Rect( _rect.xMin,
                                   _rect.yMin + _rect.height * .5f,
                                   _rect.width,
                                   _rect.height * .5f );

            // lock icon rect
            _lockRect = new Rect( 0f, 0f, 32f, 32f );
            _lockRect = _lockRect.CenteredOnXIn( _rect );
            _lockRect = _lockRect.CenteredOnYIn( _rect );

            // see if the label is too big
            _largeLabel = Text.CalcHeight( Label, _labelRect.width ) > _labelRect.height;

            // done
            _rectsSet = true;
        }

        public static GameFont ChooseFont(
            string s, Rect rect, GameFont largest,
            bool wordWrap = false, GameFont smallest = GameFont.Tiny) {
            if (largest == GameFont.Tiny)
                return largest;
            var savedFont = Text.Font;
            var savedTextWrap = Text.WordWrap;
            GameFont result;
            Text.WordWrap = wordWrap;
            for (; largest > smallest; --largest) {
                Text.Font = largest;
                if (Text.CalcHeight(s, rect.width) <= rect.height) {
                    break;
                }
            }
            result = largest;

            Text.Font = savedFont;
            Text.WordWrap = savedTextWrap;
            return result;
        }

        public static GameFont SmallOrTiny(string s, Rect rect) {
            return ChooseFont(s, rect, GameFont.Small);
        }

        public virtual bool IsVisible( Rect visibleRect )
        {
            var nodeRect = Rect;
            return !(
            nodeRect.m_YMin > visibleRect.yMin + visibleRect.m_Height || 
            visibleRect.yMin + visibleRect.m_Height < visibleRect.m_YMin ||
            nodeRect.m_XMin > visibleRect.xMin + visibleRect.m_Width || 
            visibleRect.xMin + visibleRect.m_Width < visibleRect.m_XMin);
        }

        public virtual void Draw(Rect visibleRect, Painter painter)
        {
        }

        public int assignedPriority = int.MinValue;

        public virtual int DefaultPriority() {
            return int.MaxValue;
        }

        public int LayoutPriority() {
            if (assignedPriority != int.MinValue) {
                return assignedPriority;
            }
            return DefaultPriority();
        }

        public virtual int LayoutUpperPriority() {
            return int.MaxValue;
        }
        public virtual int LayoutLowerPriority() {
            return int.MaxValue;
        }

        public int lx;
        public int ly;

        public NodeLayer layer;

        public double doubleCache;
        public int intCache1;
        public int intCache2;

        public virtual int CompareTieBreaker(Node that) {
            return 0;
        }
    }

}