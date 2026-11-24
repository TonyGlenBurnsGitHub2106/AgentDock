namespace AgentDockStandalone {

    // One entry the compose box's Up arrow steps through (Story 6828): a prompt you sent, or one
    // of the agent's answers.
    internal sealed class cHistoryEntry {
        public string Text;
        public readonly bool IsAnswer;
        public cHistoryEntry(string astrText, bool ablnIsAnswer) { Text = astrText; IsAnswer = ablnIsAnswer; }
    }

    // What Up steps through, chosen in the compose box and remembered (ComposeUpMode).
    internal enum eUpMode { Prompts = 0, Conversation = 1, Answers = 2 }
}
