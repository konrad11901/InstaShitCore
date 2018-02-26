// InstaShitCore - Core of InstaShit, a bot for Insta.Ling which solves daily sessions
// Created by Konrad Krawiec

namespace InstaShitCore
{
    public class ChildResults
    {
        public string PreviousMark { get; set; } = "NONE";
        public string DaysOfWork { get; set; }
        public string ExtraParentWords { get; set; }
        public string TeacherWords { get; set; }
        public string ParentWords { get; set; }
        public string CurrrentMark { get; set; }
        public string WeekRemainingDays { get; set; }
    }
}
