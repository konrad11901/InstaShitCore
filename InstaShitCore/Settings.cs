// InstaShitCore - Core of InstaShit, a bot for Insta.Ling which solves daily sessions
// Created by Konrad Krawiec
using System.Collections.Generic;

namespace InstaShitCore
{
    public class Settings
    {
        public string Login { get; set; }
        public string Password { get; set; }
        public int MinimumSleepTime { get; set; }
        public int MaximumSleepTime { get; set; }
        public List<List<IntelligentMistakesDataEntry>> IntelligentMistakesData { get; set; }
        public bool AllowTypo { get; set; } = true;
        public bool AllowSynonym { get; set; } = true;
        public bool AnswerMarketingQuestions { get; set; } = false;
        public bool Debug { get; set; } = false;
    }
}
