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
            // Story 6846 (Tony: "Need a lot more color options"): light ones first, then dark.
            new cComposeTheme("Paper", false,     "#FAFAF7", "#2B2B2B", "#FFFFFF", "#1F1F1F", "#EDEDE8", "#1F1F1F", "#C9C9C0",
                                                  "#EFEFEA", "#8A8A80", "#1F1F1F", "#FFFFFF", "#D0D0C8", "#2B2B2B", "#8A8A80"),
            new cComposeTheme("GitHub Light", false, "#F6F8FA", "#1F2328", "#FFFFFF", "#1F2328", "#EAEEF2", "#1F2328", "#D0D7DE",
                                                  "#DDF4FF", "#54AEFF", "#0A3069", "#FFFFFF", "#D0D7DE", "#1F2328", "#656D76"),
            new cComposeTheme("Catppuccin Latte", false, "#EFF1F5", "#4C4F69", "#FFFFFF", "#4C4F69", "#E6E9EF", "#4C4F69", "#BCC0CC",
                                                  "#DCE0F7", "#7287FD", "#303446", "#FFFFFF", "#CCD0DA", "#4C4F69", "#8C8FA1"),
            new cComposeTheme("Gruvbox Light", false, "#F2E5BC", "#3C3836", "#FBF1C7", "#3C3836", "#EBDBB2", "#3C3836", "#BDAE93",
                                                  "#E3D7A8", "#B57614", "#3C3836", "#FBF1C7", "#79740E", "#3C3836", "#7C6F64"),
            new cComposeTheme("Mint", false,      "#EAF7F1", "#1D3B30", "#FFFFFF", "#15302A", "#D3EFE2", "#15302A", "#99CDB6",
                                                  "#C7EEDC", "#2E9E73", "#0E3326", "#FFFFFF", "#A9CFC0", "#1D3B30", "#5F8A7A"),
            new cComposeTheme("Peach", false,     "#FFF1E8", "#4A2E20", "#FFFFFF", "#3E2418", "#FFE0CC", "#3E2418", "#F1B89A",
                                                  "#FFD9C2", "#E07A47", "#4A2210", "#FFFFFF", "#E6C3B0", "#3E2A20", "#A57A64"),
            new cComposeTheme("Sky", false,       "#EEF6FF", "#1C3350", "#FFFFFF", "#132A45", "#DCEBFF", "#132A45", "#A9C7EE",
                                                  "#CFE4FF", "#3F88E8", "#0D2A52", "#FFFFFF", "#B9CBE3", "#1C3350", "#6884A8"),
            new cComposeTheme("Lemon", false,     "#FFFBE6", "#3D3510", "#FFFFFF", "#33290A", "#FFF3B8", "#33290A", "#E6D272",
                                                  "#FFF0A0", "#C9A400", "#3A2E00", "#FFFFFF", "#DDD3A0", "#3D3510", "#8F8040"),
            new cComposeTheme("Coffee", false,    "#EDE0D4", "#3B2A20", "#F7EFE7", "#2F2118", "#E0CFC0", "#2F2118", "#B79C86",
                                                  "#DCC5B0", "#7F5539", "#2F1B0E", "#FBF6F1", "#C3AE9C", "#3B2A20", "#8A7060"),
            new cComposeTheme("Ice", false,       "#F0F7FA", "#22333B", "#FFFFFF", "#1B2B32", "#E1EEF3", "#1B2B32", "#B4CDD8",
                                                  "#DFF3FA", "#5AB4D6", "#0F3240", "#FFFFFF", "#C6D6DD", "#22333B", "#7090A0"),
            new cComposeTheme("Cherry Blossom", false, "#FDF0F4", "#4B2236", "#FFFFFF", "#3F1B2D", "#F9DDE7", "#3F1B2D", "#E7A9BF",
                                                  "#FAD2E0", "#D9467A", "#4F1028", "#FFFFFF", "#DDB9C6", "#3F2733", "#A07586"),
            new cComposeTheme("Monokai", false,   "#272822", "#F8F8F2", "#1E1F1C", "#F8F8F2", "#3E3D32", "#F8F8F2", "#5B5A4E",
                                                  "#3B3A25", "#E6DB74", "#F8F8F2", "#2D2E27", "#A6E22E", "#F8F8F2", "#90908A"),
            new cComposeTheme("Gruvbox Dark", false, "#282828", "#EBDBB2", "#1D2021", "#EBDBB2", "#3C3836", "#EBDBB2", "#504945",
                                                  "#3C3420", "#FABD2F", "#FBF1C7", "#32302F", "#8EC07C", "#EBDBB2", "#A89984"),
            new cComposeTheme("One Dark", false,  "#282C34", "#ABB2BF", "#21252B", "#D7DAE0", "#2F343E", "#D7DAE0", "#3E4452",
                                                  "#2B3A55", "#61AFEF", "#E6EEF9", "#2C313A", "#98C379", "#D7DAE0", "#7F848E"),
            new cComposeTheme("Tokyo Night", false, "#1A1B26", "#A9B1D6", "#16161E", "#C0CAF5", "#24283B", "#C0CAF5", "#3B4261",
                                                  "#283457", "#7AA2F7", "#E3E9FF", "#1F2335", "#BB9AF7", "#C0CAF5", "#737AA2"),
            new cComposeTheme("Catppuccin Mocha", false, "#1E1E2E", "#CDD6F4", "#181825", "#CDD6F4", "#313244", "#CDD6F4", "#45475A",
                                                  "#2E3250", "#89B4FA", "#E6ECFF", "#24273A", "#F5C2E7", "#CDD6F4", "#9399B2"),
            new cComposeTheme("GitHub Dark", false, "#0D1117", "#C9D1D9", "#010409", "#E6EDF3", "#21262D", "#E6EDF3", "#30363D",
                                                  "#0C2D6B", "#388BFD", "#E6EDF3", "#161B22", "#3FB950", "#E6EDF3", "#8B949E"),
            new cComposeTheme("Cobalt", false,    "#193549", "#E1EFFF", "#122738", "#FFFFFF", "#1F4662", "#FFFFFF", "#2D5A7B",
                                                  "#0D3A58", "#FFC600", "#FFFFFF", "#15324A", "#3AD900", "#E1EFFF", "#8FB3D0"),
            new cComposeTheme("Ayu Mirage", false, "#1F2430", "#CBCCC6", "#191E2A", "#D9D7CE", "#272D38", "#D9D7CE", "#3D4455",
                                                  "#2E3A4D", "#FFCC66", "#F3F4F5", "#232834", "#5CCFE6", "#D9D7CE", "#707A8C"),
            new cComposeTheme("Night Owl", false, "#011627", "#D6DEEB", "#010E1A", "#D6DEEB", "#0B2942", "#D6DEEB", "#1D3B53",
                                                  "#0E2F4F", "#82AAFF", "#EAF0FF", "#0B2236", "#C792EA", "#D6DEEB", "#637777"),
            new cComposeTheme("Synthwave", false, "#241B2F", "#F4EEE4", "#1B1424", "#FFFFFF", "#34294F", "#FFFFFF", "#495495",
                                                  "#3B1E54", "#FF7EDB", "#FFFFFF", "#2A2139", "#36F9F6", "#F4EEE4", "#A59FB5"),
            new cComposeTheme("Ember", false,     "#1C1210", "#F1D9CF", "#140C0A", "#F7E4DC", "#2E1B16", "#F7E4DC", "#5A2E22",
                                                  "#4A1E12", "#FF6B35", "#FFF1EA", "#24160F", "#FFB347", "#F1D9CF", "#A8877A"),
            new cComposeTheme("Deep Sea", false,  "#0B1D26", "#CFE8EF", "#071419", "#DDF2F7", "#12303D", "#DDF2F7", "#1E4A5C",
                                                  "#0E3B4D", "#29B6D1", "#E8FAFF", "#0F2833", "#4DD0A1", "#CFE8EF", "#6E98A6"),
            new cComposeTheme("Plum", false,      "#22142B", "#E8D9F1", "#1A0F21", "#F1E6F8", "#33203F", "#F1E6F8", "#4E3260",
                                                  "#43225A", "#C77DFF", "#FBF3FF", "#2B1A36", "#FF9EBB", "#E8D9F1", "#9D86AD"),
            new cComposeTheme("Olive", false,     "#1F2318", "#E2E6D4", "#171A12", "#EDF0E2", "#2E3424", "#EDF0E2", "#4A5338",
                                                  "#3A4526", "#B5C760", "#F6FAE8", "#262B1D", "#E0B65C", "#E2E6D4", "#8D957A"),
            new cComposeTheme("Graphite", false,  "#2A2A2A", "#DADADA", "#202020", "#EEEEEE", "#383838", "#EEEEEE", "#505050",
                                                  "#3A3A3A", "#BDBDBD", "#FFFFFF", "#2F2F2F", "#7E7E7E", "#E6E6E6", "#8C8C8C"),
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

        // True when a theme of that name exists (Story 6846, the per-pane overrides).
        public static bool Exists(string astrName) {
            foreach (cComposeTheme t in maAll)
                if (String.Equals(t.Name, astrName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
