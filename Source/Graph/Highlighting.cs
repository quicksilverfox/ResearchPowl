using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace ResearchPowl
{

public static class Highlighting {
    public enum Reason {
        Unknown = 0,
        FixedSecondary = 1,
        // SearchUnmatched = 3,
        HoverSecondary = 3,
        // SearchMatched = 5,
        HoverPrimary = 4,
        FixedPrimary = 6,
        Focused = 7
    }

    public static int Priority(Reason reason) {
        return (int) reason;
    }

    public static bool Similar(Reason r1, Reason r2) {
        if (r1 > r2) {
            Reason temp = r1;
            r1 = r2;
            r2 = temp;
        }
        return r1 == Reason.FixedSecondary && r2 == Reason.FixedPrimary
            || r1 == Reason.HoverSecondary && r2 == Reason.HoverPrimary
            || r1 == r2;
    }

    public static bool CauseEdgeHighlight(Reason r) {
        Reason[] causeHighlights =
            { Reason.FixedPrimary, Reason.FixedSecondary,
                Reason.HoverPrimary, Reason.HoverSecondary};
        return causeHighlights.Contains(r);
    }

    public static Color Color(Reason reason, TechLevel techLevel) {
        if (reason == Reason.FixedSecondary) {
            return Assets.NormalHighlightColor;
        }
        if (reason == Reason.FixedPrimary) {
            return Assets.FixedPrimaryColor;
        }
        if (reason == Reason.HoverSecondary) {
            return Assets.NormalHighlightColor;
        }
        if (reason == Reason.HoverPrimary) {
            return Assets.HoverPrimaryColor;
        }
        if (reason == Reason.Focused) {
            return Assets.FixedPrimaryColor;
        }
        // if (reason == Reason.SearchUnmatched) {
        //     return Assets.ColorUnmatched[techLevel];
        // }
        return Assets.NormalHighlightColor;
    }

    public static bool Stackable(Reason r) {
        Reason[] stackable = {Reason.FixedSecondary};
        return stackable.Contains(r);
    }
}

public class HighlightReasonSet {
    private List<Pair<Highlighting.Reason, int>> _reasons =
        new List<Pair<Highlighting.Reason, int>>();

    public bool Highlighted() {
        return _reasons.Any();
    }

    public bool HighlightedAs(Highlighting.Reason reason) {
        return _reasons.Any(p => p.First == reason);
    }

    public bool HighlightedAbove(Highlighting.Reason reason) {
        return _reasons.Any(p => p.First >= reason);
    }

    public Highlighting.Reason Current() {
        return _reasons.Select(p => p.First).MaxBy(Highlighting.Priority);
    }

    public IEnumerable<Highlighting.Reason> Reasons() {
        return _reasons.Select(p => p.First);
    }

    public bool Highlight(Highlighting.Reason r) {
        for (int i = 0; i < _reasons.Count(); ++i) {
            var p = _reasons[i];
            if (p.First == r) {
                if (!Highlighting.Stackable(r)) {
                    return false;
                }
                _reasons[i] =
                    new Pair<Highlighting.Reason, int>(r, p.Second + 1);
                return true;
            }
        }
        _reasons.Add(new Pair<Highlighting.Reason, int>(r, 1));
        return true;
    } 

    public bool Unhighlight(Highlighting.Reason r) {
        for (int i = 0; i < _reasons.Count(); ++i) {
            var p = _reasons[i];
            if (p.First == r) {
                if (p.Second == 1) {
                    _reasons.RemoveAt(i);
                } else {
                    _reasons[i] =
                        new Pair<Highlighting.Reason, int>(r, p.Second - 1);
                }
                return true;
            }
        }
        return false;
    }
}

public class RelatedNodeHighlightSet {
    private ResearchNode _causer;
    private List<ResearchNode> _relatedNodes;

    private Highlighting.Reason _causerReason;
    private Highlighting.Reason _relatedReason;

    bool _activated = false;

    private RelatedNodeHighlightSet(
        ResearchNode causer, Highlighting.Reason causerR,
        Highlighting.Reason relatedR) {
        _causer = causer;
        _causerReason = causerR;
        _relatedReason = relatedR;
    }

    public bool Activated() {
        return _activated;
    }

    public ResearchNode Causer() {
        return _causer;
    }

    public static List<ResearchNode> RelatedNodes(ResearchNode node) {
        return RelatedPrerequisites(node).Concat(node.DirectChildren()).ToList();
    }

    private static IEnumerable<ResearchNode> RelatedPrerequisites(ResearchNode node) {
        return node.DirectPrerequisites().Concat(
            node.DirectPrerequisites()
                .Where(n => ! n.Completed())
                .SelectMany(RelatedPrerequisites));
    }

    public static RelatedNodeHighlightSet HoverOn(ResearchNode node) {
        var instance = new RelatedNodeHighlightSet(
            node, Highlighting.Reason.HoverPrimary,
            Highlighting.Reason.HoverSecondary); 
        instance._relatedNodes = RelatedNodes(node);
        return instance;
    }

    public static RelatedNodeHighlightSet FixHighlight(ResearchNode node) {
        var instance = new RelatedNodeHighlightSet(
            node, Highlighting.Reason.FixedPrimary,
            Highlighting.Reason.FixedSecondary);
        instance._relatedNodes = RelatedNodes(node);
        return instance;
    }

    public bool ShouldContinue(Vector2 mouse) {
        return _causer.MouseOver(mouse);
    }
    public bool Stop() {
        if (!Activated()) {
            return false;
        }
        _causer.Unhighlight(_causerReason);
        _relatedNodes.ForEach(n => n.Unhighlight(_relatedReason));
        return true;
    }
    public bool Start() {
        if (Activated()) {
            return false;
        }
        _activated = true;
        _causer.Highlight(_causerReason);
        _relatedNodes.ForEach(n => n.Highlight(_relatedReason));
        return true;
    }
    public bool TryStop(Vector2 mouse) {
        if (ShouldContinue(mouse)) {
            return false;
        }
        Stop();
        return true;
    }

}

}
