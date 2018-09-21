using CsvHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace QuizGrader
{
    /// <summary>
    /// To use:
    /// (1) Set TOKEN
    /// (2) Set courseID
    /// (3) Set quizID
    /// </summary>
    class GradeRecorder
    {
        // Canvas acccess token
        private static string TOKEN = "";

        // Spring 2018
        private static string courseID = "475628";

        // Current quiz name
        private static string quizName;

        // Current quiz number (not assignment number)
        private static string quizID;

        /// <summary>
        /// Creates an HttpClient for communicating with Canvas.
        /// </summary>
        public static HttpClient CreateClient()
        {
            CookieContainer cookies = new CookieContainer();
            HttpClient client = new HttpClient(new WebRequestHandler() { CookieContainer = cookies })
            {
                BaseAddress = new Uri("https://utah.instructure.com")
            };
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + TOKEN);
            return client;
        }

        /// <summary>
        /// Returns the URL used to get next page of results or null
        /// </summary>        
        private static string GetNextURL(HttpResponseHeaders headers)
        {
            string allHeaders = headers.ToString();
            Regex r = new Regex("<([^<>]*)>; rel=\"next\"");
            Match m = r.Match(allHeaders);
            return (m.Success) ? m.Groups[1].ToString() : null;
        }

        /// <summary>
        /// Either writes out all answers provided by students for a quiz, or updates the scores
        /// and comments for all students.
        /// </summary>
        public static void Main(string[] args)
        {
            Console.WriteLine("Quiz Grader for " + GetCourseName());

            // Get unique Quiz ID
            Console.Write("Enter name of quiz: ");
            quizName = Console.ReadLine();
            List<string> quizIDs = GetQuizIDs(quizName);
            if (quizIDs.Count == 0)
            {
                Console.WriteLine("There is no quiz by that name");
                return;
            }
            else if (quizIDs.Count > 1)
            {
                Console.WriteLine("There are " + quizIDs.Count + " quizzes by that name");
                return;
            }
            else
            {
                quizID = quizIDs[0];
            }

            // Get choice and confirm
            Console.Write("Do you want to [g]et quiz answers or [s]et quiz scores? [g/s] ");
            string answer = Console.ReadLine().ToLower().Trim();
            if (answer == "g" || answer == "get")
            {
                GetAnswers();
            }
            else if (answer == "s" || answer == "set")
            {
                SetScores();
            }
            else
            {
                Console.WriteLine("Unknown option: " + answer);
            }

            Console.Write("Done");
            Console.ReadLine();
        }

        private static string GetQuizFilename()
        {
            return "Quizzes/quiz_" + quizName + "_" + quizID + ".csv";
        }

        public static void SetScores()
        {
            // Get information about all of the short answer questions that appear on the quiz
            List<Question> questions = GetQuestions();
            Console.WriteLine(questions.Count + " short answer questions found");

            // Get information about all submissions to the quiz
            List<Submission> submissions = GetSubmissions();
            Console.WriteLine(submissions.Count + " submissions found");

            // Get information about attempts
            AllAttempts attempts = GetAllAttempts(questions);
            Console.WriteLine(attempts.Count + " attempts found");

            // Read the grading rules for this quiz
            GradingRules rules;
            using (var input = new CsvReader(new StreamReader(@"..\..\" + GetQuizFilename())))
            {
                rules = new GradingRules(input);
            }
            Console.WriteLine(GetQuizFilename() + " is based on " + rules.Count + " attempts");

            // Warn the user
            int delta = attempts.Count - rules.Count;
            if (delta != 0)
            {
                Console.WriteLine("There have " + delta + " attempts since " + GetQuizFilename() + " was created");
                Console.WriteLine("You should recreate that file");
            }

            // Make sure we proceed
            Console.Write("Update grades in Canvas? [y/n] ");
            if (Console.ReadLine().Trim().ToLower() != "y")
            {
                return;
            }

            // Change the grade of attempt if necessary
            foreach (Attempt attempt in attempts.GetAttempts())
            {
                Submission submission = GetSubmission(submissions, attempt.UserID, attempt.Number);

                dynamic correction = GetCorrection(attempt, questions, rules, out string name);
                if (correction == null)
                {
                    Console.WriteLine("unchanged (v" + attempt.Number + ") " + name);
                }
                else if (submission == null)
                {
                    Console.WriteLine("*MISSING* (v" + attempt.Number + ") " + name);
                }
                else
                {
                    using (HttpClient client = CreateClient())
                    {
                        String url = String.Format("/api/v1/courses/{0}/quizzes/{1}/submissions/{2} ", courseID, quizID, submission.Id);
                        var content = new StringContent(correction.ToString(), Encoding.UTF8, "application/json");
                        //HttpResponseMessage response = client.PutAsync(url, content).Result;
                        HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);

                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine("updated   (v" + attempt.Number + ") " + name);
                        }
                        else
                        {
                            Console.WriteLine("*FAILED*  (v" + attempt.Number + ") " + name + " " + response.StatusCode);
                            return;
                        }
                    }
                }
            }
        }

        private static dynamic GetCorrection(Submission s, AllAttempts attempts, IList<Question> questions, GradingRules rules,
            out string name)
        {
            Attempt attempt = attempts.GetAttempt(s.UserId, s.Attempt);
            name = attempt.UserName;

            JObject questionsObj = null;
            foreach (Question q in questions)
            {
                Answer realAnswer = attempt.GetAnswer(q.Id);
                Answer ruleAnswer = rules.GetAnswer(q.Id, realAnswer.Text);

                if (realAnswer.Points != ruleAnswer.Points || ruleAnswer.Comment.Length > 0)
                {
                    if (questionsObj == null)
                    {
                        questionsObj = new JObject();
                    }

                    JObject change = new JObject();
                    if (realAnswer.Points != ruleAnswer.Points)
                    {
                        change["score"] = ruleAnswer.Points;
                    }
                    if (ruleAnswer.Comment.Length > 0)
                    {
                        change["comment"] = ruleAnswer.Comment;
                    }
                    questionsObj[q.Id.ToString()] = change;
                }
            }

            if (questionsObj != null)
            {
                JObject correction = new JObject();
                JArray items = new JArray();
                correction["quiz_submissions"] = items;
                JObject attemptObj = new JObject();
                items.Add(attemptObj);
                attemptObj["attempt"] = s.Attempt;
                attemptObj["questions"] = questionsObj;
                return correction;
            }
            else
            {
                return null;
            }
        }

        private static string GetCourseName()
        {
            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/api/v1/courses/{0}", courseID);
                HttpResponseMessage response = client.GetAsync(url).Result;
                if (response.IsSuccessStatusCode)
                {
                    dynamic course = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                    return course.name;
                }
                else
                {
                    Console.WriteLine("Error: " + response.StatusCode);
                    Console.WriteLine(response.ReasonPhrase.ToString());
                    Console.WriteLine("Unable to read course information");
                    throw new Exception();
                }
            }
        }

        private static Submission GetSubmission (List<Submission> submissions, int userID, int attempt)
        {
            foreach (Submission s in submissions)
            {
                if (attempt == -1 && s.UserId == userID.ToString())
                {
                    return s;
                }
                if (attempt >= 0 && s.UserId == userID.ToString() && s.Attempt == attempt) return s;
            }
            return null;
        }

        private static dynamic GetCorrection(Attempt attempt, IList<Question> questions, GradingRules rules, out string name)
        {
            name = attempt.UserName;

            JObject questionsObj = null;
            foreach (Question q in questions)
            {
                Answer realAnswer = attempt.GetAnswer(q.Id);
                Answer ruleAnswer = rules.GetAnswer(q.Id, realAnswer.Text);

                if (realAnswer.Points != ruleAnswer.Points || ruleAnswer.Comment.Length > 0)
                {
                    if (questionsObj == null)
                    {
                        questionsObj = new JObject();
                    }

                    JObject change = new JObject();
                    if (realAnswer.Points != ruleAnswer.Points)
                    {
                        change["score"] = ruleAnswer.Points;
                    }
                    if (ruleAnswer.Comment.Length > 0)
                    {
                        change["comment"] = ruleAnswer.Comment;
                    }
                    questionsObj[q.Id.ToString()] = change;
                }
            }

            if (questionsObj != null)
            {
                JObject correction = new JObject();
                JArray items = new JArray();
                correction["quiz_submissions"] = items;
                JObject attemptObj = new JObject();
                items.Add(attemptObj);
                attemptObj["attempt"] = attempt.Number;
                attemptObj["questions"] = questionsObj;
                return correction;
            }
            else
            {
                return null;
            }
        }

        public static void GetAnswers()
        {
            // Get the short-answer questions that appear on the quiz
            List<Question> questions = GetQuestions();
            Console.WriteLine(questions.Count + " short answer questions found");

            // Get information about all submissions to the quiz
            AllAttempts allAttempts = GetAllAttempts(questions);
            Console.WriteLine(allAttempts.Count + " attempts found");

            // Organize into grading rules
            GradingRules rules = new GradingRules(questions, allAttempts);

            // Write the information to a CSV
            Console.Write("Write results to " + GetQuizFilename() + "? [y/n] ");
            if (Console.ReadLine().Trim().ToLower() == "y")
            {
                WriteAnalysis(questions, rules);
            }
        }

        private static void WriteAnalysis(List<Question> questions, GradingRules rules)
        {
            questions.Sort((q1, q2) => q1.Position.CompareTo(q2.Position));
            using (var output = new CsvWriter(new StreamWriter(@"..\..\" + GetQuizFilename())))
            {
                rules.Write(output, questions);
            }
        }

        private static AllAttempts GetAllAttempts(List<Question> questions)
        {
            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/api/v1/courses/{0}/quizzes/{1}/reports", courseID, quizID);
                var inputs = new Dictionary<string, string>();
                inputs["quiz_report[report_type]"] = "student_analysis";
                inputs["quiz_report[includes_all_versions]"] = "true";

                while (true)
                {
                    var content = new FormUrlEncodedContent(inputs);
                    HttpResponseMessage response = client.PostAsync(url, content).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        dynamic qs = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                        if (qs.file != null)
                        {
                            using (StreamReader reader = new StreamReader(WebRequest.Create(qs.file.url.ToString()).GetResponse().GetResponseStream()))
                            {
                                using (CsvReader parser = new CsvReader(reader))
                                {
                                    AllAttempts attempts = new AllAttempts(parser, questions);
                                    return attempts;
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Waiting for quiz report to be generated by Canvas...");
                            Thread.Sleep(1000);
                        }
                    }
                    else if (response.StatusCode == HttpStatusCode.Conflict)
                    {
                        Console.WriteLine("Waiting for quiz report to be generated by Canvas...");
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Console.WriteLine("Error: " + response.StatusCode);
                        Console.WriteLine(response.ReasonPhrase.ToString());
                        Console.WriteLine("Unable to obtain quiz analysis");
                        throw new Exception();
                    }
                }
            }
        }

        private static List<string> GetQuizIDs(string quizName)
        {
            List<string> quizIDs = new List<string>();
            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/api/v1/courses/{0}/quizzes?search_term={1}&per_page=100", courseID, quizName);
                while (url != null)
                {
                    HttpResponseMessage response = client.GetAsync(url).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        dynamic quizzes = JArray.Parse(response.Content.ReadAsStringAsync().Result);
                        foreach (dynamic quiz in quizzes)
                        {
                            if (quiz.title == quizName)
                            {
                                quizIDs.Add(quiz.id.ToString());
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error: " + response.StatusCode);
                        Console.WriteLine(response.ReasonPhrase.ToString());
                        Console.WriteLine("Unable to read quiz information");
                        throw new Exception();
                    }
                    url = GetNextURL(response.Headers);
                }
                return quizIDs;
            }
        }

        /// <summary>
        /// Returns information about all short-answer questions that appear on the quiz
        /// </summary>
        /// <returns></returns>
        private static List<Question> GetQuestions()
        {
            List<Question> questions = new List<Question>();
            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/api/v1/courses/{0}/quizzes/{1}/questions?per_page=100", courseID, quizID);
                while (url != null)
                {
                    HttpResponseMessage response = client.GetAsync(url).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        dynamic qs = JArray.Parse(response.Content.ReadAsStringAsync().Result);
                        foreach (dynamic question in qs)
                        {
                            if (question.question_type == "short_answer_question")
                            {
                                questions.Add(new Question(question));
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error: " + response.StatusCode);
                        Console.WriteLine(response.ReasonPhrase.ToString());
                        Console.WriteLine("Unable to read question information");
                        throw new Exception();
                    }
                    url = GetNextURL(response.Headers);
                }
                return questions;
            }
        }

        /// <summary>
        /// Get information about all submissions made to the quiz
        /// </summary>
        private static List<Submission> GetSubmissions()
        {
            List<Submission> submissions = new List<Submission>();
            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/api/v1/courses/{0}/quizzes/{1}/submissions?per_page=100", courseID, quizID);
                while (url != null)
                {
                    HttpResponseMessage response = client.GetAsync(url).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic data = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                        data = data.quiz_submissions;
                        foreach (dynamic submission in data)
                        {
                            submissions.Add(new Submission(submission));
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error: " + response.StatusCode);
                        Console.WriteLine(response.ReasonPhrase.ToString());
                        Console.WriteLine("Unable to read course");
                        throw new Exception();
                    }
                    url = GetNextURL(response.Headers);
                }

                return submissions;
            }
        }

        /// <summary>
        /// Accumulate answers given to the questions in the submission
        /// </summary>
        /// <param name="questions"></param>
        /// <param name="answers"></param>
        private static void AddAnswers(Submission submission, IList<Question> questions, Dictionary<int, SortedSet<string>> answers)
        {
            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/courses/{0}/quizzes/{1}/history?headless=1&user_id={2}&version={3}", courseID, quizID, submission.UserId, submission.Attempt);
                HttpResponseMessage response = client.GetAsync(url).Result;
                if (response.IsSuccessStatusCode)
                {
                    string page = response.Content.ReadAsStringAsync().Result;
                    foreach (Question q in questions)
                    {
                        int start = page.IndexOf("<input type=\"text\" name=\"question_" + q.Id + "\"");
                        start = page.IndexOf("value=\"", start) + 7;
                        int stop = page.IndexOf("\"", start);

                        String value = page.Substring(start, stop - start);
                        SortedSet<string> set;
                        if (answers.TryGetValue(q.Position, out set))
                        {
                            set.Add(value.Trim());
                        }
                        else
                        {
                            set = new SortedSet<string>();
                            answers[q.Position] = set;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Error: " + (int)response.StatusCode);
                    Console.WriteLine(response.ReasonPhrase.ToString());
                    Console.WriteLine("Unable to read course");
                    return;
                }
            }
        }

        private static List<KeyValuePair<string, string>> GetInputs(Submission submission, IList<Question> questions)
        {
            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/courses/{0}/quizzes/{1}/history?headless=1&hide_student_name=0&score_updated=1&user_id={2}&version={3}", courseID, quizID, submission.UserId, submission.Attempt);
                HttpResponseMessage response = client.GetAsync(url).Result;
                if (response.IsSuccessStatusCode)
                {
                    string page = response.Content.ReadAsStringAsync().Result;
                    //Console.WriteLine(page);

                    List<KeyValuePair<string, string>> inputs = new List<KeyValuePair<string, string>>();
                    int inputIndex = page.IndexOf("<input ");
                    while (inputIndex >= 0)
                    {
                        int nameIndex = page.IndexOf("name=\"", inputIndex) + 6;
                        int valueIndex = page.IndexOf("value=\"", inputIndex) + 7;
                        int nameQuote = page.IndexOf("\"", nameIndex);
                        int valueQuote = page.IndexOf("\"", valueIndex);

                        string name = page.Substring(nameIndex, nameQuote - nameIndex);
                        string value = page.Substring(valueIndex, valueQuote - valueIndex);

                        if (name != "answer_text")
                        {
                            inputs.Add(new KeyValuePair<string, string>(name, value));
                        }
                        inputIndex = page.IndexOf("<input ", inputIndex + 1);
                    }

                    int textIndex = page.IndexOf("<textarea ");
                    while (textIndex >= 0)
                    {
                        int nameIndex = page.IndexOf("name=\"", textIndex) + 6;
                        int valueIndex = page.IndexOf(">", textIndex) + 1;
                        int nameQuote = page.IndexOf("\"", nameIndex);
                        int valueEnd = page.IndexOf("</textarea>", valueIndex);

                        string name = page.Substring(nameIndex, nameQuote - nameIndex);
                        string value = page.Substring(valueIndex, valueEnd - valueIndex);

                        inputs.Add(new KeyValuePair<string, string>(name, value));
                        textIndex = page.IndexOf("<textarea ", textIndex + 1);
                    }
                    return inputs;
                }
                else
                {
                    Console.WriteLine("Error: " + (int)response.StatusCode);
                    Console.WriteLine(response.ReasonPhrase.ToString());
                    Console.WriteLine("Unable to read page");
                    throw new Exception();
                }
            }
        }
    }
}
