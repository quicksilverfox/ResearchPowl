// Constants.cs
// Copyright Karel Kroeze, 2018-2020

using UnityEngine;

namespace ResearchPal
{
    public static class Constants
    {
        public const           double  Epsilon                     = 1e-4;
        public const           float   HubSize                     = 16f;
        public const           float   DetailedModeZoomLevelCutoff = 1.5f;
        public const           float   Margin                      = 6f;
        public const           float   SideMargin                  = 13f;
        public const           float   QueueLabelSize              = 25f;
        public const           float   SmallQueueLabelSize         = 20f;
        public const           float   AbsoluteMaxZoomLevel        = 3f;
        public const           float   ZoomStep                    = .05f;
        public static readonly Vector2 IconSize                    = new Vector2( 18f, 18f );
        public static readonly Vector2 NodeMargins                 = new Vector2( 50f, 10f );
        public static readonly Vector2 NodeSize                    = new Vector2( 200f, 50f );
        public static readonly float   TopBarHeight                = NodeSize.y + 2 * Margin;
        public static readonly Vector2 TechLevelLabelSize          = new Vector2( 200f, 30f );

        public static readonly float DraggingClickDelay = 0.25f;
    }
}