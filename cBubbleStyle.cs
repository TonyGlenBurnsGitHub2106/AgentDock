using System;
using System.Collections.Generic;

namespace AgentDockStandalone {

    // How the messages in Run conversation are drawn (Story 6846): the shape of the bubbles,
    // their outlines, fills, shadows and tails, from Plain (no bubbles at all) to Fancy. The
    // colors come from the theme (cComposeTheme); a style is only the shape. Picked in Settings ->
    // Bubble Style for every pane, and per pane in Settings -> Pane Theme (cPaneThemes).
    internal sealed class cBubbleStyle {

        public readonly string Name;
        public readonly string Description;
        public readonly int    PromptRadius;
        public readonly int    AnswerRadius;
        public readonly float  EdgeWidth;     // 0: no outline
        public readonly bool   Fill;          // false: outline only, on the view's background
        public readonly bool   Gradient;      // the fill shades from light at the top to the color
        public readonly bool   Shadow;        // a soft shadow under each bubble
        public readonly bool   Tail;          // a little point toward the speaker's side
        public readonly bool   Flat;          // Plain: no bubbles, full-width rows with a bar at the left

        private cBubbleStyle(string astrName, string astrDescription, int aintPromptRadius, int aintAnswerRadius,
                             float afltEdge, bool ablnFill, bool ablnGradient, bool ablnShadow, bool ablnTail,
                             bool ablnFlat) {
            Name = astrName; Description = astrDescription;
            PromptRadius = aintPromptRadius; AnswerRadius = aintAnswerRadius;
            EdgeWidth = afltEdge; Fill = ablnFill; Gradient = ablnGradient;
            Shadow = ablnShadow; Tail = ablnTail; Flat = ablnFlat;
        }

        // Classic is the look Run conversation had before styles (1.0.35 to 1.0.39).
        public const string CNST_DEFAULT = "Classic";

        // From plain to fancy, in that order on the menu.
        private static readonly cBubbleStyle[] maAll = new cBubbleStyle[] {
            //                name        description                                          pR  aR  edge   fill   grad   shadow tail   flat
            new cBubbleStyle("Plain",    "No bubbles: each message is a row with a colored bar at its left.",
                                                                                                0,  0, 0F,    false, false, false, false, true),
            new cBubbleStyle("Minimal",  "Square blocks of color, no outlines.",                0,  0, 0F,    true,  false, false, false, false),
            new cBubbleStyle("Boxed",    "Square blocks with a thin outline.",                  0,  0, 1F,    true,  false, false, false, false),
            new cBubbleStyle("Classic",  "Your prompts rounder, the answers squarer, outlined.", 14, 4, 1.25F, true,  false, false, false, false),
            new cBubbleStyle("Outline",  "Outlines only, no fill.",                             10, 10, 2F,   false, false, false, false, false),
            new cBubbleStyle("Soft",     "Round bubbles with no outline.",                      16, 16, 0F,   true,  false, false, false, false),
            new cBubbleStyle("Chat",     "Round bubbles with a little tail, like a phone.",     16, 16, 0F,   true,  false, false, true,  false),
            new cBubbleStyle("Shadowed", "Rounded and outlined, lifted off the page by a shadow.", 10, 10, 1F, true, false, true,  false, false),
            new cBubbleStyle("Fancy",    "Shaded fill, shadow, tail and a bold outline.",       18, 18, 2F,   true,  true,  true,  true,  false),
        };

        public static IEnumerable<cBubbleStyle> All { get { return maAll; } }

        // The style of that name, or Classic when the name is unknown.
        public static cBubbleStyle Named(string astrName) {
            foreach (cBubbleStyle s in maAll)
                if (String.Equals(s.Name, astrName, StringComparison.OrdinalIgnoreCase)) return s;
            return Named(CNST_DEFAULT);
        }

        // True when a style of that name exists.
        public static bool Exists(string astrName) {
            foreach (cBubbleStyle s in maAll)
                if (String.Equals(s.Name, astrName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
