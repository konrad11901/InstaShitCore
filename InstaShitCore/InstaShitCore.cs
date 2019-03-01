// InstaShitCore - Core of InstaShit, a bot for Insta.Ling which solves daily sessions
// Created by Konrad Krawiec
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

namespace InstaShitCore
{
    public class InstaShitCore
    {
        // CONST FIELDS
        // Insta.Ling version which has been tested with this InstaShit version
        public const string DefaultInstaLingVersion = "fwif0zy4ty6nte8";

        // PRIVATE READONLY FIELDS
        private readonly HttpClient client;
        private readonly HttpClient synonymsApiClient;
        private readonly Random rndGenerator = new Random();
        private readonly Settings settings;
        private readonly Dictionary<string, int> wordsHistory;
        private readonly Dictionary<string, string> wordsDictionary;
        private readonly Dictionary<string, int> wordsCount;
        private readonly List<List<int>> mistakesCount;

        // PRIVATE FIELDS
        private string childId;

        // PUBLIC AND PROTECTED MEMBERS

        /// <summary>
        /// Gets the latest Insta.Ling version (read from the Insta.Ling website source).
        /// </summary>
        public string LatestInstaLingVersion { get; private set; }

        /// <summary>
        /// Creates an instance of the InstaShitCore class.
        /// </summary>
        /// <param name="settings">InstaShit settings to use in this instance.</param>
        /// <param name="wordsDictionary">Words dictionary to use in this instance.</param>
        /// <param name="wordsHistory">Words history to use in this instance.</param>
        public InstaShitCore(Settings settings, Dictionary<string, string> wordsDictionary,
            Dictionary<string, int> wordsHistory)
        {
            client = new HttpClient()
            {
                BaseAddress = new Uri("https://instaling.pl")
            };
            client.DefaultRequestHeaders.Add("User-Agent", UserAgents.IE[rndGenerator.Next(0, UserAgents.IE.Count)]);
            synonymsApiClient = new HttpClient()
            {
                BaseAddress = new Uri("https://api.datamuse.com")
            };
            this.settings = settings;
            this.wordsDictionary = wordsDictionary;
            this.wordsHistory = wordsHistory;
            mistakesCount = new List<List<int>>();
            wordsCount = new Dictionary<string, int>();
            for (var i = 0; i < settings.IntelligentMistakesData.Count; i++)
            {
                mistakesCount.Add(new List<int>());
                for (var j = 0; j < settings.IntelligentMistakesData[i].Count; j++)
                    mistakesCount[i].Add(0);
            }
        }

        /// <summary>
        /// Creates an instance of the InstaShitCore class.
        /// </summary>
        /// <param name="settings">InstaShit settings to use in this instance.</param>
        public InstaShitCore(Settings settings) : this(settings, new Dictionary<string, string>(),
            new Dictionary<string, int>())
        {

        }

        /// <summary>
        /// Creates an instance of the InstaShitCore class.
        /// </summary>
        /// <param name="data">InstaShitData object which contains settings, words dictionary and words history.</param>
        public InstaShitCore(InstaShitData data) : this(data.Settings, data.WordsDictionary, data.WordsHistory)
        {

        }
        
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
        /// Gets the number of milliseconds since 1970/01/01 (equivalent of JavaScript GetTime() function).
        /// </summary>
        /// <returns>The number of milliseconds since 1970/01/01.</returns>
        public static int GetJsTime()
        {
            var dateTime = new DateTime(1970, 1, 1);
            var timeSpan = DateTime.Now.ToUniversalTime() - dateTime;
            return (int)timeSpan.TotalMilliseconds;
        }

        /// <summary>
        /// Gets the InstaShit's settings from local storage.
        /// </summary>
        /// <param name="baseLocation">Location of the InstaShit files.</param>
        /// <returns>The object of Settings class with loaded values or an empty one,
        /// if there's nothing in the local storage.</returns>
        public static Settings GetSettings(string baseLocation)
        {
            if (File.Exists(Path.Combine(baseLocation, "settings.json")))
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(
                    Path.Combine(baseLocation, "settings.json")));
            return null;
        }

        /// <summary>
        /// Gets the words history from local storage.
        /// </summary>
        /// <param name="baseLocation">Location of the InstaShit files.</param>
        /// <returns>The Dictionary with loaded values or an empty one,
        /// if there's nothing in the local storage.</returns>
        public static Dictionary<string, int> GetWordsHistory(string baseLocation)
        {
            if (File.Exists(Path.Combine(baseLocation, "wordsHistory.json")))
                return JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(
                    Path.Combine(baseLocation, "wordsHistory.json")));
            else
                return new Dictionary<string, int>();
        }

        /// <summary>
        /// Gets the words dictionary from local storage.
        /// </summary>
        /// <param name="baseLocation">Location of the InstaShit files.</param>
        /// <returns>The Dictionary with loaded values or an empty one,
        /// if there's nothing in the local storage.</returns>
        public static Dictionary<string, string> GetWordsDictionary(string baseLocation)
        {
            if (File.Exists(Path.Combine(baseLocation, "wordsDictionary.json")))
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(
                    Path.Combine(baseLocation, "wordsDictionary.json")));
            else
                return new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets the InstaShit data from local storage.
        /// </summary>
        /// <param name="baseLocation">Location of the InstaShit files.</param>
        /// <returns>The object of the InstaShitData class with loaded values or an empty one,
        /// if there's nothing in the local storage.</returns>
        public static InstaShitData GetInstaShitData(string baseLocation)
        {
            return new InstaShitData
            {
                Settings = GetSettings(baseLocation),
                WordsHistory = GetWordsHistory(baseLocation),
                WordsDictionary = GetWordsDictionary(baseLocation)
            };
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
            Debug($"Result from /learning/main.php: {resultString}");
            if (!resultString.Contains("<title>insta.ling</title>"))
                return false;
            childId = resultString.Substring(resultString.IndexOf("child_id=", StringComparison.Ordinal) + 9, 6);
            Debug($"childID = {childId}");
            await UpdateInstaLingVersion();
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
        /// Updates the Insta.Ling version to the latest one (read from Insta.Ling website source).
        /// </summary>
        public async Task UpdateInstaLingVersion()
        {
            if (childId == null)
                throw new InvalidOperationException("User is not logged in");
            try
            {
                var resultString = await GetGetResultAsync("ling2/html_app/app.php?child_id=" + childId);
                int startIndex = resultString.IndexOf("updateParams(id, answer", StringComparison.Ordinal) + 32;
                int length = resultString.IndexOf(");", startIndex, StringComparison.Ordinal) - startIndex - 1;
                string version = resultString.Substring(startIndex, length);
                Debug("Insta.Ling version = " + version);
                if (version != DefaultInstaLingVersion)
                    Debug("WARNING: Insta.Ling has been updated since this InstaShit release!");
                LatestInstaLingVersion = version;
            }
            catch
            {
                Debug($"WARNING: An error occured while trying to update Insta.Ling version, using default version " +
                    $"({DefaultInstaLingVersion})");
                LatestInstaLingVersion = DefaultInstaLingVersion;
            }
        }

        /// <summary>
        /// Gets the time to wait before continuing.
        /// </summary>
        /// <returns>The number of milliseconds to wait.</returns>
        public int SleepTime => rndGenerator.Next(settings.MinimumSleepTime, settings.MaximumSleepTime + 1);

        /// <summary>
        /// Gets the information about the answer to the question.
        /// </summary>
        /// <returns>Data about the answer or null, if all the questions have been answered.</returns>
        public async Task<Answer> GetAnswerAsync()
        {
            if (childId == null)
                throw new InvalidOperationException("User is not logged in");
            Dictionary<string, object> wordData;
            while(true)
            {
                wordData = await GenerateNextWordAsync();
                if (wordData.ContainsKey("summary"))
                    return null;
                if (settings.AnswerMarketingQuestions || wordData["type"].ToString() != "marketing")
                    break;
                Debug("Skipping marketing question");
            }
            var word = wordData["word"].ToString();
            var wordId = wordData["id"].ToString();
            var answer = new Answer
            {
                WordId = wordId,
                Word = word
            };
            if (!wordsCount.ContainsKey(wordId))
                wordsCount.Add(wordId, 0);
            if (!wordsDictionary.ContainsKey(word))
                wordsDictionary.Add(word, wordData["translations"].ToString());
            if (!wordsHistory.ContainsKey(wordId))
                wordsHistory.Add(wordId, 0);
            var correctAnswer = AnswerCorrectly(wordId);
            if (!correctAnswer)
            {
                mistakesCount[wordsHistory[wordId]][wordsCount[wordId]]++;
                wordsCount[wordId]++;
                answer.AnswerWord = await GetWrongWordAsync(word);
            }
            else
            {
                if (wordsCount[wordId] == 0)
                    wordsHistory[wordId] = -1;
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
            if (childId == null)
                throw new InvalidOperationException("User is not logged in");
            if (LatestInstaLingVersion == null)
                await UpdateInstaLingVersion();
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("child_id", childId),
                new KeyValuePair<string, string>("word_id", answer.WordId),
                new KeyValuePair<string, string>("version", LatestInstaLingVersion),
                new KeyValuePair<string, string>("answer", answer.AnswerWord)
            });
            var resultString = await GetPostResultAsync("/ling2/server/actions/save_answer.php", content);
            Debug(resultString);
            var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(resultString);
            return (jsonResponse["grade"].ToString() == "1" && answer.Word == answer.AnswerWord)
                   || ((jsonResponse["grade"].ToString() == "0" || jsonResponse["grade"].ToString() == "2") 
                   && answer.Word != answer.AnswerWord);
        }

        /// <summary>
        /// Gets results of today's training.
        /// </summary>
        /// <returns>Results of today's training.</returns>
        public async Task<ChildResults> GetResultsAsync()
        {
            if (childId == null)
                throw new InvalidOperationException("User is not logged in");
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
        /// <param name="baseLocation">Location in which session data should be saved.</param>
        public void SaveSessionData(string baseLocation)
        {
            foreach (var key in wordsHistory.Keys.ToList())
                if (wordsHistory[key] != -1 && wordsCount.ContainsKey(key))
                    wordsHistory[key]++;
            File.WriteAllText(Path.Combine(baseLocation, "wordsHistory.json"), 
                JsonConvert.SerializeObject(wordsHistory, Formatting.Indented));
            File.WriteAllText(Path.Combine(baseLocation, "wordsDictionary.json"), 
                JsonConvert.SerializeObject(wordsDictionary, Formatting.Indented));
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
            switch (mistakeType)
            {
                case 0:
                case 1 when !settings.AllowTypo:
                    return "";
                case 1:
                {
                    for (var i = 0; i < word.Length - 1; i++)
                    {
                        if (word[i] == word[i + 1])
                            return word.Remove(i, 1);
                    }
                    return "";
                }
                default:
                {
                    if (!settings.AllowSynonym) return "";
                    var result = await synonymsApiClient.GetAsync("/words?ml=" + word);
                    var synonyms = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(
                        await result.Content.ReadAsStringAsync());
                    if (synonyms.Count == 0)
                        return "";
                    if (synonyms.Count == 1)
                        return synonyms[0]["word"].ToString();
                    int maxRnd = synonyms.Count == 2 ? 2 : 3;
                    return synonyms[rndGenerator.Next(0, maxRnd)]["word"].ToString();
                }
            }
        }

        /// <summary>
        /// Sends the POST request to the specified URL and returns the result of this request as a string value.
        /// </summary>
        /// <param name="requestUri">The request URL.</param>
        /// <param name="content">The content of this POST Request.</param>
        /// <returns>Result of POST request.</returns>
        private async Task<string> GetPostResultAsync(string requestUri, HttpContent content)
        {
            var result = await client.PostAsync(requestUri, content);
            return await result.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Sends the GET request to the specified URL and returns the result of this request as a string value.
        /// </summary>
        /// <param name="requestUri">The request URL.</param>
        /// <returns>Result of GET request.</returns>
        private async Task<string> GetGetResultAsync(string requestUri)
        {
            var result = await client.GetAsync(requestUri);
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
            if (wordsHistory[wordId] == -1 || wordsCount[wordId] == -1 ||
                wordsHistory[wordId] >= settings.IntelligentMistakesData.Count ||
                wordsCount[wordId] >= settings.IntelligentMistakesData[wordsHistory[wordId]].Count ||
                (settings.IntelligentMistakesData[wordsHistory[wordId]][wordsCount[wordId]].MaxNumberOfMistakes !=
                 -1 && mistakesCount[wordsHistory[wordId]][wordsCount[wordId]] >=
                 settings.IntelligentMistakesData[wordsHistory[wordId]][wordsCount[wordId]].MaxNumberOfMistakes))
                return true;
            var rndPercentage = rndGenerator.Next(1, 101);
            return rndPercentage > settings.IntelligentMistakesData[wordsHistory[wordId]][wordsCount[wordId]].RiskPercentage;
        }
    }
}
