// InstaShitCore - Core of InstaShit, a bot for Insta.Ling which solves daily sessions
// Created by Konrad Krawiec
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Net;

namespace InstaShitCore
{
    public class InstaShitCore
    {
        // PRIVATE FIELDS
        private readonly CookieContainer cookieContainer;
        private readonly HttpClientHandler handler;
        private readonly HttpClient client;
        private readonly HttpClient synonymsApiClient;
        private readonly Random rndGenerator = new Random();
        private readonly Settings settings;
        private string childId;
        private readonly Dictionary<string, int> sessionCount;
        private readonly Dictionary<string, string> words;
        private readonly Dictionary<string, int> wordsCount;
        private readonly List<List<int>> mistakesCount;
        private readonly string baseLocation;
        private string sessionId;

        // PUBLIC AND PROTECTED MEMBERS

        /// <summary>
        /// Creates an instance of the InstaShitCore class.
        /// </summary>
        /// <param name="baseLocation">Directory where the user files are located.</param>
        /// <param name="ignoreSettings">Specifies if settings file should be ignored</param>
        protected InstaShitCore(string baseLocation, bool ignoreSettings = false)
        {
            cookieContainer = new CookieContainer();
            handler = new HttpClientHandler()
            {
                CookieContainer = cookieContainer
            };
            client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://instaling.pl")
            };
            synonymsApiClient = new HttpClient()
            {
                BaseAddress = new Uri("https://api.datamuse.com")
            };
            this.baseLocation = baseLocation;
            settings = GetSettings(ignoreSettings);
            if (File.Exists(GetFileLocation("wordsHistory.json")))
                sessionCount = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(GetFileLocation("wordsHistory.json")));
            else
                sessionCount = new Dictionary<string, int>();
            if (File.Exists(GetFileLocation("wordsDictionary.json")))
                words = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(GetFileLocation("wordsDictionary.json")));
            else
                words = new Dictionary<string, string>();
            wordsCount = new Dictionary<string, int>();
            mistakesCount = new List<List<int>>();
            for (var i = 0; i < settings.IntelligentMistakesData.Count; i++)
            {
                mistakesCount.Add(new List<int>());
                for (var j = 0; j < settings.IntelligentMistakesData[i].Count; j++)
                    mistakesCount[i].Add(0);

            }
        }

        /// <summary>
        /// Gets the location of specified file.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns>The location of specified file.</returns>
        protected string GetFileLocation(string fileName) => Path.Combine(baseLocation, fileName);
        
        /// <summary>
        /// Writes the specified string value to the trace listeners if debug mode is turned on.
        /// </summary>
        /// <param name="text">The value to write.</param>
        protected virtual void Debug(string text)
        {
            if(DebugMode)
                System.Diagnostics.Debug.WriteLine(text);
        }

        /// <summary>
        /// Gets the value that specifies if debug mode is turned on.
        /// </summary>
        protected bool DebugMode => settings.Debug;

        /// <summary>
        /// Gets the number of miliseconds since 1970/01/01 (equivalent of JavaScript GetTime() function).
        /// </summary>
        /// <returns>The number of miliseconds since 1970/01/01.</returns>
        public static int GetJsTime()
        {
            var dateTime = new DateTime(1970, 1, 1);
            var timeSpan = DateTime.Now.ToUniversalTime() - dateTime;
            return (int)timeSpan.TotalMilliseconds;
        }

        /// <summary>
        /// Gets the InstaShit's settings from settings file.
        /// </summary>
        /// <returns>The object of Settings class with loaded values.</returns>
        protected virtual Settings GetSettings(bool ignoreSettings)
        {
            if (!ignoreSettings && File.Exists(GetFileLocation("settings.json")))
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(GetFileLocation("settings.json")));
            return null;
        }

        /// <summary>
        /// Attempts to login to Insta.Ling.
        /// </summary>
        /// <returns>True if the attempt to login was successful; otherwise, false.</returns>
        public async Task<bool> TryLoginAsync()
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "login"),
                new KeyValuePair<string, string>("from", ""),
                new KeyValuePair<string, string>("log_email", settings.Login),
                new KeyValuePair<string, string>("log_password", settings.Password)
            });
            var resultString = await GetPostResultAsync("/teacher.php?page=teacherActions", content);
            Debug("Successfully posted to /teacher.php?page=teacherActions");
            Debug($"Result from /learning/student.php: {resultString}");
            if (!resultString.Contains("<title>insta.ling</title>"))
                return false;
            childId = resultString.Substring(resultString.IndexOf("child_id=", StringComparison.Ordinal) + 9, 6);
            Debug($"childID = {childId}");
            sessionId = cookieContainer.GetCookies(new Uri("https://instaling.pl"))["PHPSESSID"].Value;
            return true;
        }

        /// <summary>
        /// Checks if the currrent session is new.
        /// </summary>
        /// <returns>True if the current session is new; otherwise, false.</returns>
        public async Task<bool> IsNewSessionAsync()
        {
            if (childId == null)
                throw new InvalidOperationException("User is not logged in");
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("child_id", childId),
                new KeyValuePair<string, string>("repeat", ""),
                new KeyValuePair<string, string>("start", ""),
                new KeyValuePair<string, string>("end", "")
            });
            var resultString = await GetPostResultAsync("/ling2/server/actions/init_session.php", content);
            var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(resultString);
            Debug("JSONResponse from POST /ling2/server/actions/init_session.php: " + resultString);
            return (bool)jsonResponse["is_new"];
        }

        /// <summary>
        /// Gets the time to wait before continuing.
        /// </summary>
        /// <returns>The number of miliseconds to wait.</returns>
        public int SleepTime => rndGenerator.Next(settings.MinimumSleepTime, settings.MaximumSleepTime + 1);

        /// <summary>
        /// Gets the information about the answer to the question.
        /// </summary>
        /// <returns>Data about the answer.</returns>
        public async Task<Answer> GetAnswerAsync()
        {
            var wordData = await GenerateNextWordAsync();
            if (wordData.ContainsKey("summary"))
                return null;
            var word = wordData["word"].ToString();
            var wordId = wordData["id"].ToString();
            var answer = new Answer
            {
                WordId = wordId,
                Word = word
            };
            if (!wordsCount.ContainsKey(wordId))
                wordsCount.Add(wordId, 0);
            if (!words.ContainsKey(word))
                words.Add(word, wordData["translations"].ToString());
            if (!sessionCount.ContainsKey(wordId))
                sessionCount.Add(wordId, 0);
            var correctAnswer = AnswerCorrectly(wordId);
            if (!correctAnswer)
            {
                mistakesCount[sessionCount[wordId]][wordsCount[wordId]]++;
                wordsCount[wordId]++;
                answer.AnswerWord = await GetWrongWordAsync(word);
            }
            else
            {
                if (wordsCount[wordId] == 0)
                    sessionCount[wordId] = -1;
                wordsCount[wordId] = -1;
                answer.AnswerWord = word;
            }
            return answer;
        }

        /// <summary>
        /// Attempts to answer the question.
        /// </summary>
        /// <param name="answer">Information about the answer.</param>
        /// <returns>True if the attempt to answer the question was successful; otherwise, false.</returns>
        public async Task<bool> TryAnswerQuestionAsync(Answer answer)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("child_id", childId),
                new KeyValuePair<string, string>("word_id", answer.WordId),
                new KeyValuePair<string, string>("version", "cyh5fh7unfrnu8e"),
                new KeyValuePair<string, string>("answer", answer.AnswerWord)
            });
            var resultString = await GetPostResultAsync("/ling2/server/actions/save_answer.php", content);
            Debug(resultString);
            var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(resultString);
            return (jsonResponse["grade"].ToString() == "1" && answer.Word == answer.AnswerWord)
                   || ((jsonResponse["grade"].ToString() == "0" || jsonResponse["grade"].ToString() == "2") && answer.Word != answer.AnswerWord);
        }

        /// <summary>
        /// Gets results of today's training.
        /// </summary>
        /// <returns>Results of today's training.</returns>
        public async Task<ChildResults> GetResultsAsync()
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("child_id", childId),
                new KeyValuePair<string, string>("date", GetJsTime().ToString())
            });
            var resultString = await GetPostResultAsync("/ling2/server/actions/grade_report.php", content);
            Debug(resultString);
            try
            {
                var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(resultString);
                var childResults = new ChildResults();
                if (jsonResponse.ContainsKey("prev_mark"))
                    childResults.PreviousMark = jsonResponse["prev_mark"].ToString();
                childResults.DaysOfWork = jsonResponse["work_week_days"].ToString();
                childResults.ExtraParentWords = jsonResponse["parent_words_extra"].ToString();
                childResults.TeacherWords = jsonResponse["teacher_words"].ToString();
                childResults.ParentWords = jsonResponse["parent_words"].ToString();
                childResults.CurrrentMark = jsonResponse["current_mark"].ToString();
                childResults.WeekRemainingDays = jsonResponse["week_remaining_days"].ToString();
                return childResults;
            }
            catch
            {
                Debug("Error occured while trying to parse results");
                return new ChildResults();
            }
        }

        /// <summary>
        /// Saves the current session's data.
        /// </summary>
        public void SaveSessionData()
        {
            foreach (var key in sessionCount.Keys.ToList())
                if (sessionCount[key] != -1)
                    sessionCount[key]++;
            File.WriteAllText(GetFileLocation("wordsHistory.json"), JsonConvert.SerializeObject(sessionCount, Formatting.Indented));
            File.WriteAllText(GetFileLocation("wordsDictionary.json"), JsonConvert.SerializeObject(words, Formatting.Indented));
        }

        // PRIVATE MEMBERS

        /// <summary>
        /// Creates a new, not correct word based on the specified string value.
        /// </summary>
        /// <param name="word">The word to process.</param>
        /// <returns>A word with a mistake.</returns>
        private async Task<string> GetWrongWordAsync(string word)
        {
            // Three possible mistakes:
            // 0 - no answer
            // 1 - answer with a typo (TODO)
            // 2 - synonym
            var mistakeType = rndGenerator.Next(0, 3);
            if (mistakeType == 0)
                return "";
            else if (mistakeType == 1)
            {
                if (!settings.AllowTypo) return "";
                for (var i = 0; i < word.Length - 1; i++)
                {
                    if (word[i] == word[i + 1])
                        return word.Remove(i, 1);
                }
                //This doesn't seem to work well, so it's disabled for now
                /*
                string stringToReplace = "";
                string newString;
                if (word.Contains("c"))
                {
                    stringToReplace = "c";
                    newString = "k";
                }
                else if (word.Contains("t"))
                {
                    stringToReplace = "t";
                    newString = "d";
                }
                else if (word.Contains("d"))
                {
                    stringToReplace = "d";
                    newString = "t";
                }
                else
                    return "";
                var regex = new Regex(Regex.Escape(stringToReplace));
                return regex.Replace(word, newString, 1);
                */
                return "";
            }
            else
            {
                if (!settings.AllowSynonym) return "";
                var result = await synonymsApiClient.GetAsync("/words?ml=" + word);
                var synonyms = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(await result.Content.ReadAsStringAsync());
                if (synonyms.Count == 0)
                    return "";
                if (synonyms.Count == 1)
                    return synonyms[0]["word"].ToString();
                int maxRnd;
                maxRnd = synonyms.Count == 2 ? 2 : 3;
                return synonyms[rndGenerator.Next(0, maxRnd)]["word"].ToString();
            }
        }

        /// <summary>
        /// Sends the POST request to the specified URL and returns the result of this request as a string value.
        /// </summary>
        /// <param name="requestUri">The request URL></param>
        /// <param name="content">The content of this POST Request</param>
        /// <returns>Result of POST request.</returns>
        private async Task<string> GetPostResultAsync(string requestUri, HttpContent content)
        {
            var result = await client.PostAsync(requestUri, content);
            return await result.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Generates the next word.
        /// </summary>
        /// <returns>A Dictionary which contains information about generated word.</returns>
        private async Task<Dictionary<string, object>> GenerateNextWordAsync()
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("child_id", childId),
                new KeyValuePair<string, string>("date", GetJsTime().ToString())
            });
            var resultString = await GetPostResultAsync("/ling2/server/actions/generate_next_word.php", content);
            Debug("Result from generate_next_word.php: " + resultString);
            var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(resultString);
            return jsonResponse;
        }

        /// <summary>
        /// Checks if the answer to the question about the specified word should be correct or not.
        /// </summary>
        /// <param name="wordId">ID of the word to check.</param>
        /// <returns>True if the answer should be correct; otherwise, false.</returns>
        private bool AnswerCorrectly(string wordId)
        {
            if (sessionCount[wordId] == -1 || wordsCount[wordId] == -1 ||
                sessionCount[wordId] >= settings.IntelligentMistakesData.Count ||
                wordsCount[wordId] >= settings.IntelligentMistakesData[sessionCount[wordId]].Count ||
                (settings.IntelligentMistakesData[sessionCount[wordId]][wordsCount[wordId]].MaxNumberOfMistakes !=
                 -1 && mistakesCount[sessionCount[wordId]][wordsCount[wordId]] >=
                 settings.IntelligentMistakesData[sessionCount[wordId]][wordsCount[wordId]].MaxNumberOfMistakes))
                return true;
            var rndPercentage = rndGenerator.Next(1, 101);
            return rndPercentage > settings.IntelligentMistakesData[sessionCount[wordId]][wordsCount[wordId]].RiskPercentage;
        }
    }
}
