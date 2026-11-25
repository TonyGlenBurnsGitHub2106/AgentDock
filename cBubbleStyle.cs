using System;
using System.Collections.Generic;

namespace AgentDockStandalone {

    // How the messages in Run conversation are drawn (Story 6846): the shape of the bubbles,
    // their outlines, fills, shadows and tails, from Plain (no bubbles at all) to Fancy. The
    // colors come from the theme (cComposeTheme), the letters from the font (cConversationFont);
    // a style is only the shape. Picked in Settings -> Bubble Style (or the Theme Viewer) for every
    // pane, and per pane in Settings -> Pane Theme (cPaneThemes). Tony, 2026-11-25: "Need to be
    // able to go from plain to fancy", then "Need about a dozen more bubble options".
    internal sealed class cBubbleStyle {

        public string Name;
        public string Description;
        public int    PromptRadius;
        public int    AnswerRadius;
        public float  EdgeWidth;          // 0: no outline
        public bool   Fill = true;        // false: no fill, on the view's background
        public bool   Gradient;           // the fill shades from light at the top to the color
        public bool   Shadow;             // a soft shadow under each bubble
        public bool   HardShadow;         // a solid offset shadow, like a comic panel
        public bool   Tail;               // a little point toward the speaker's side
        public bool   Flat;               // Plain: no bubbles, full-width rows with a bar at the left
        public bool   Dashed;             // the outline is dashed
        public bool   DoubleEdge;         // a second outline just inside the first
        public bool   Glow;               // a soft glow of the outline color around the bubble
        public bool   Accent;             // a thick bar inside the bubble on the speaker's side
        public bool   HeaderBand;         // "You" / "Agent" in a colored band across the bubble's top
        public bool   Solid;              // filled with the outline color, text in black or white
        public bool   Underline;          // no box: a line under each message, full width
        public bool   InkEdge;            // the outline in the text color (Comic)
        public int    Pad = 9;            // inside a bubble, around the text
        public int    Gap = 10;           // between one message and the next

        // Classic is the look Run conversation had before styles (1.0.35 to 1.0.39).
        public const string CNST_DEFAULT = "Classic";

        // From plain to fancy, in that order on the menus.
        private static readonly cBubbleStyle[] maAll = new cBubbleStyle[] {
            new cBubbleStyle { Name = "Plain", Description = "No bubbles: each message is a row with a colored bar at its left.",
                               Flat = true, Fill = false },
            new cBubbleStyle { Name = "Notebook", Description = "No boxes: each message underlined, like lined paper.",
                               Underline = true, Fill = false, Pad = 7 },
            new cBubbleStyle { Name = "Minimal", Description = "Square blocks of color, no outlines." },
            new cBubbleStyle { Name = "Compact", Description = "Small, tight bubbles, more messages on screen.",
                               PromptRadius = 8, AnswerRadius = 3, EdgeWidth = 1F, Pad = 5, Gap = 5 },
            new cBubbleStyle { Name = "Boxed", Description = "Square blocks with a thin outline.", EdgeWidth = 1F },
            new cBubbleStyle { Name = "Classic", Description = "Your prompts rounder, the answers squarer, outlined.",
                               PromptRadius = 14, AnswerRadius = 4, EdgeWidth = 1.25F },
            new cBubbleStyle { Name = "Roomy", Description = "Rounded and outlined, with plenty of space around the text.",
                               PromptRadius = 12, AnswerRadius = 12, EdgeWidth = 1F, Pad = 15, Gap = 16 },
            new cBubbleStyle { Name = "Outline", Description = "Outlines only, no fill.",
                               PromptRadius = 10, AnswerRadius = 10, EdgeWidth = 2F, Fill = false },
            new cBubbleStyle { Name = "Dashed", Description = "Rounded, with a dashed outline.",
                               PromptRadius = 8, AnswerRadius = 8, EdgeWidth = 1.5F, Dashed = true },
            new cBubbleStyle { Name = "Soft", Description = "Round bubbles with no outline.", PromptRadius = 16, AnswerRadius = 16 },
            new cBubbleStyle { Name = "Pill", Description = "Fully rounded ends, like pills.", PromptRadius = 99, AnswerRadius = 99, Pad = 11 },
            new cBubbleStyle { Name = "Accent", Description = "A thick bar of color on the speaker's side of each bubble.",
                               PromptRadius = 6, AnswerRadius = 6, Accent = true },
            new cBubbleStyle { Name = "Double", Description = "Two outlines, one inside the other.",
                               PromptRadius = 10, AnswerRadius = 10, EdgeWidth = 1.25F, DoubleEdge = true, Pad = 11 },
            new cBubbleStyle { Name = "Chat", Description = "Round bubbles with a little tail, like a phone.",
                               PromptRadius = 16, AnswerRadius = 16, Tail = true },
            new cBubbleStyle { Name = "Solid", Description = "Bold bubbles filled with the outline color, with tails.",
                               PromptRadius = 14, AnswerRadius = 14, Solid = true, Tail = true },
            new cBubbleStyle { Name = "Card", Description = "Cards with the speaker in a colored band across the top, and a shadow.",
                               PromptRadius = 8, AnswerRadius = 8, EdgeWidth = 1F, HeaderBand = true, Shadow = true, Gap = 13 },
            new cBubbleStyle { Name = "Shadowed", Description = "Rounded and outlined, lifted off the page by a shadow.",
                               PromptRadius = 10, AnswerRadius = 10, EdgeWidth = 1F, Shadow = true, Gap = 13 },
            new cBubbleStyle { Name = "Glow", Description = "Rounded bubbles in a soft glow of their outline color.",
                               PromptRadius = 14, AnswerRadius = 14, EdgeWidth = 1F, Glow = true, Gap = 14 },
            new cBubbleStyle { Name = "Neon", Description = "Glowing outlines with no fill. Best on a dark theme.",
                               PromptRadius = 12, AnswerRadius = 12, EdgeWidth = 2F, Glow = true, Fill = false, Gap = 14 },
            new cBubbleStyle { Name = "Comic", Description = "Ink outlines, a hard shadow and tails, like a comic strip.",
                               PromptRadius = 12, AnswerRadius = 12, EdgeWidth = 2.5F, InkEdge = true, HardShadow = true, Tail = true, Gap = 14 },
            new cBubbleStyle { Name = "Fancy", Description = "Shaded fill, shadow, tail and a bold outline.",
                               PromptRadius = 18, AnswerRadius = 18, EdgeWidth = 2F, Gradient = true, Shadow = true, Tail = true, Gap = 13 },
        };

        public static IEnumerable<cBubbleStyle> All { get { return maAll; } }

        // The style of that name, or Classic when the name is unknown.
        public static cBubbleStyle Named(string astrName) {
            foreach (cBubbleStyle s in maAll)
                if (String.Equals(s.Name, astrName, StringComparison.OrdinalIgnoreCase)) return s;
            foreach (cBubbleStyle s in maAll)
                if (s.Name == CNST_DEFAULT) return s;
            return maAll[0];
        }

        // True when a style of that name exists.
        public static bool Exists(string astrName) {
            foreach (cBubbleStyle s in maAll)
                if (String.Equals(s.Name, astrName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
