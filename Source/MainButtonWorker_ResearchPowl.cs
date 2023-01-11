// Copyright Karel Kroeze, 2018-2020

using RimWorld;
using UnityEngine;

namespace ResearchPowl
{
    public class MainButtonWorker_ResearchTree : MainButtonWorker_ToggleResearchTab
    {
        float margin = Constants.SmallQueueLabelSize - Constants.Margin;
        public override void DoButton( Rect rect )
        {
            base.DoButton( rect );

            var numQueued = Queue._instance._queue.Count - 1;
            if ( numQueued > 0 )
            {
                var queueRect = new Rect(rect.m_Width + rect.m_XMin - margin, rect.m_YMin + (rect.m_Height - Constants.SmallQueueLabelSize) / 2f, Constants.SmallQueueLabelSize, Constants.SmallQueueLabelSize);
                Queue.DrawLabel( queueRect, Color.white, Color.grey, numQueued);
            }
        }
    }
}