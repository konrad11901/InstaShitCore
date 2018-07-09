// InstaShitCore - Core of InstaShit, a bot for Insta.Ling which solves daily sessions
// Created by Konrad Krawiec
using System.Collections.Generic;

namespace InstaShitCore
{
    public class InstaShitData
    {
        public Settings Settings { get; set; }
        public Dictionary<string, string> WordsDictionary { get; set; }
        public Dictionary<string, int> WordsHistory { get; set; }
    }
}
