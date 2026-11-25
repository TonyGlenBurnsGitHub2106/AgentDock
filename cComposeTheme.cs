using System;
using System.Collections.Generic;
using System.Drawing;

namespace AgentDockStandalone {

    // The look of the compose box (Story 6845): its background, the text box, the buttons and
    // lists, and the two kinds of bubble in Run conversation. Picked from Settings -> Compose Box
    // Theme and remembered (agentdock.ini ComposeTheme). "Windows" is the plain system look, the
    // way the box looked before themes.
    internal sealed class cComposeTheme {

        public readonly string Name;
        public readonly bool   IsSystem;      // Windows: leave the controls in their system colors
        public readonly Color  Back;          // the box behind everything
        public readonly Color  Fore;          // labels
        public readonly Color  InputBack;     // the text box, lists and the conversation
        public readonly Color  InputFore;
        public readonly Color  ButtonBack;
        public readonly Color  ButtonFore;
        public readonly Color  Border;        // button outlines and the bars between parts
        public readonly Color  PromptFill;    // your prompts
        public readonly Color  PromptEdge;
        public readonly Color  PromptText;
        public readonly Color  AnswerFill;    // the agent's answers
        public readonly Color  AnswerEdge;
        public readonly Color  AnswerText;
        public readonly Color  Caption;       // the small "You" / "Agent" over each bubble

        private cComposeTheme(string astrName, bool ablnSystem, string astrBack, string astrFore,
                              string astrInputBack, string astrInputFore, string astrButtonBack,
                              string astrButtonFore, string astrBorder,
                              string astrPromptFill, string astrPromptEdge, string astrPromptText,
                              string astrAnswerFill, string astrAnswerEdge, string astrAnswerText,
                              string astrCaption) {
            Name = astrName; IsSystem = ablnSystem;
            Back = Hex(astrBack); Fore = Hex(astrFore);
            InputBack = Hex(astrInputBack); InputFore = Hex(astrInputFore);
            ButtonBack = Hex(astrButtonBack); ButtonFore = Hex(astrButtonFore); Border = Hex(astrBorder);
            PromptFill = Hex(astrPromptFill); PromptEdge = Hex(astrPromptEdge); PromptText = Hex(astrPromptText);
            AnswerFill = Hex(astrAnswerFill); AnswerEdge = Hex(astrAnswerEdge); AnswerText = Hex(astrAnswerText);
            Caption = Hex(astrCaption);
        }

        private static Color Hex(string astr) {
            int n = Convert.ToInt32(astr.TrimStart('#'), 16);
            return Color.FromArgb((n >> 16) & 0xFF, (n >> 8) & 0xFF, n & 0xFF);
        }

        public const string CNST_DEFAULT = "Windows";

        //                       name             back       fore       inBack     inFore     btnBack    btnFore    border
        //                                        promptFill promptEdge promptTxt  answerFill answerEdge answerTxt  caption
        private static readonly cComposeTheme[] maAll = new cComposeTheme[] {
            new cComposeTheme("Windows", true,    "#F0F0F0", "#000000", "#FFFFFF", "#000000", "#E1E1E1", "#000000", "#A0A0A0",
                                                  "#DCEBFF", "#5B8DD6", "#0B2545", "#F2F2F2", "#9A9A9A", "#1E1E1E", "#6D6D6D"),
            new cComposeTheme("Daylight", false,  "#F7F9FC", "#1F2937", "#FFFFFF", "#111827", "#E5EAF2", "#111827", "#B8C2D1",
                                                  "#D7E8FF", "#3B82F6", "#0F2A55", "#FFFFFF", "#C3CAD5", "#1F2937", "#6B7280"),
            new cComposeTheme("Dark", false,      "#1E1E1E", "#D4D4D4", "#252526", "#E6E6E6", "#333337", "#E6E6E6", "#4A4A4F",
                                                  "#0E3A5F", "#3794FF", "#E8F1FF", "#2D2D30", "#5A5A5F", "#E0E0E0", "#9A9A9A"),
            new cComposeTheme("Midnight", false,  "#0F1626", "#C9D4E5", "#151E31", "#E3EAF5", "#1E2A42", "#E3EAF5", "#33415E",
                                                  "#23376B", "#6E8EF7", "#EEF2FF", "#1B2538", "#3A4A68", "#D9E2F1", "#7F8CA6"),
            new cComposeTheme("Nord", false,      "#2E3440", "#D8DEE9", "#3B4252", "#ECEFF4", "#434C5E", "#ECEFF4", "#4C566A",
                                                  "#4C6A8A", "#88C0D0", "#ECEFF4", "#3B4252", "#5E81AC", "#E5E9F0", "#A3ACBE"),
            new cComposeTheme("Dracula", false,   "#282A36", "#F8F8F2", "#21222C", "#F8F8F2", "#44475A", "#F8F8F2", "#6272A4",
                                                  "#4B3A6E", "#BD93F9", "#F8F8F2", "#343746", "#50FA7B", "#F8F8F2", "#9AA0C0"),
            new cComposeTheme("Solarized Light", false, "#EEE8D5", "#586E75", "#FDF6E3", "#073642", "#E4DCC4", "#073642", "#B9B19A",
                                                  "#D9E8EF", "#268BD2", "#073642", "#FDF6E3", "#B58900", "#3E5059", "#93A1A1"),
            new cComposeTheme("Solarized Dark", false, "#002B36", "#93A1A1", "#073642", "#EEE8D5", "#0B4351", "#EEE8D5", "#2A5662",
                                                  "#0F4C66", "#268BD2", "#EEE8D5", "#073642", "#859900", "#E0DCC8", "#7C9196"),
            new cComposeTheme("Forest", false,    "#1F2A22", "#D3DED5", "#26332A", "#E8F0EA", "#314236", "#E8F0EA", "#445A4B",
                                                  "#2F5A3C", "#7BC47F", "#EEF8EF", "#2B372E", "#8A7A52", "#E5EAE3", "#93A596"),
            new cComposeTheme("Ocean", false,     "#E6F3F7", "#12343F", "#FFFFFF", "#0D2A33", "#CFE7EE", "#0D2A33", "#8CB9C6",
                                                  "#BFE6F2", "#1B8AA8", "#062A35", "#FFFFFF", "#8FB8A8", "#163A33", "#5E7F89"),
            new cComposeTheme("Sepia", false,     "#F1E7D3", "#4A3B2A", "#FBF5E8", "#3B2E20", "#E6D8BC", "#3B2E20", "#BFA983",
                                                  "#EAD9B5", "#A0712E", "#3B2A12", "#FFFBF2", "#B8A890", "#3E3427", "#8A7760"),
            new cComposeTheme("Rose", false,      "#FBEFF2", "#4A2A33", "#FFFFFF", "#3A1F27", "#F4DCE3", "#3A1F27", "#D9AAB8",
                                                  "#F9D3DE", "#D0577A", "#4A1A2A", "#FFFFFF", "#C9B4BC", "#3A2A30", "#9C7A85"),
            new cComposeTheme("Lavender", false,  "#F2EFFA", "#352C4F", "#FFFFFF", "#2A2140", "#E3DDF3", "#2A2140", "#B8ADD8",
                                                  "#DDD3F7", "#7C5CD6", "#2A1A5E", "#FFFFFF", "#BCB6CC", "#2E2A3A", "#857DA0"),
            new cComposeTheme("Slate", false,     "#2B2F36", "#C8CDD5", "#33383F", "#E4E7EB", "#3D434B", "#E4E7EB", "#535A64",
                                                  "#3D5A80", "#98C1D9", "#F0F5FA", "#3A3F47", "#E0A458", "#E4E7EB", "#8F96A0"),
            new cComposeTheme("Amber Terminal", false, "#120D05", "#FFB000", "#1A1307", "#FFC34D", "#2A1E0A", "#FFB000", "#5A3F10",
                                                  "#3A2A0C", "#FFB000", "#FFD27A", "#1A1307", "#8A6418", "#FFC34D", "#B98A2E"),
            new cComposeTheme("Green Terminal", false, "#07130A", "#33FF66", "#0B1A0F", "#5CFF85", "#11281A", "#33FF66", "#1F5A30",
                                                  "#0F3A1C", "#33FF66", "#B8FFC9", "#0B1A0F", "#1F7A3A", "#7DFF9E", "#2FAE52"),
            new cComposeTheme("Sunset", false,    "#2A1A2E", "#F3D9C9", "#33213A", "#FBE8DC", "#46304E", "#FBE8DC", "#6A4A6E",
                                                  "#7A3B4E", "#FF8A65", "#FFF1EA", "#3A2840", "#F2B544", "#F7E3D5", "#B9969F"),
            new cComposeTheme("High Contrast", false, "#000000", "#FFFFFF", "#000000", "#FFFFFF", "#000000", "#FFFF00", "#FFFFFF",
                                                  "#000000", "#00FFFF", "#00FFFF", "#000000", "#FFFF00", "#FFFFFF", "#FFFFFF"),
        };

        public static IEnumerable<cComposeTheme> All { get { return maAll; } }

        // A dark theme: its text boxes and lists are dark.
        public bool IsDark {
            get { return !IsSystem && (0.299 * InputBack.R + 0.587 * InputBack.G + 0.114 * InputBack.B) < 110; }
        }

        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string astrApp, string astrIdList);

        // Windows draws scroll bars itself, light. In a dark theme they get Windows' own dark
        // look (the one Explorer uses), and go back to normal otherwise. Older Windows ignores it.
        public static void ThemeScrollBars(System.Windows.Forms.Control actl, cComposeTheme aTheme) {
            if (!actl.IsHandleCreated) {
                actl.HandleCreated += delegate { ThemeScrollBars(actl, aTheme); };
                return;
            }
            try { SetWindowTheme(actl.Handle, aTheme.IsDark ? "DarkMode_Explorer" : null, null); } catch (Exception) { }
        }

        // The theme of that name, or Windows when the name is unknown.
        public static cComposeTheme Named(string astrName) {
            foreach (cComposeTheme t in maAll)
                if (String.Equals(t.Name, astrName, StringComparison.OrdinalIgnoreCase)) return t;
            return maAll[0];
        }
    }
}
