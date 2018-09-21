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

namespace KattisGrading
{
    public class Problem
    {
        public string CourseID { get; set; }
        public string AssignID { get; set; }
        public int PrivateTests { get; set; }
        public int PublicTests { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime LockDate { get; set; }
        public DateTime StartDate { get; set; }
        public int MaxGrade { get; set; }
        public double PrivatePct { get; set; }
        public double PublicPct { get; set; }
        public ISet<string> SkipUsers { get; set; } = new HashSet<string>();
    }

    /// <summary>
    /// To use:
    /// (1) Set TOKEN if necessary
    /// (2) Set courseID if necessary
    /// (3) Set assignName using keys from constructor
    /// (4) Set appropriate AssignID in static constructor
    /// (5) Set compareGrades and recordGrades as appropriate
    /// </summary>

    class GradeRecorder
    {
        // Time before which submissions are ignored
        private static string startTime = "01/01/2018";

        // Canvas acccess token
        private static string TOKEN = "";

        // Kattis login name
        private static string KATTIS_LOGIN = "";

        // Kattis password
        private static string KATTIS_PASSWORD = "";

        // Name of course on Kattis
        private static string KATTIS_COURSE = "CS4150";

        // Name of semester on Kattis
        private static string KATTIS_SEMESTER = "S18";

        // Set of Kattis problems that will be assigned
        private static Dictionary<string, Problem> problems = new Dictionary<string, Problem>();

        // Canvas course ID
        private static string courseID = "475628";

        // Name of assignment from problems dictionary
        private static string assignName = "utah.anagram";

        // Compare grades during run?
        private static bool compareGrades = true;

        // Record grades during run?
        private static bool recordGrades = false;

        // Create the problems dictionary
        static GradeRecorder()
        {
            problems.Add("utah.anagram",
                new Problem()
                {
                    AssignID = "4525831",
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("ceiling",
                new Problem()
                {
                    AssignID = "3660340",
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("utah.galaxyquest",
                new Problem()
                {
                    AssignID = "3660341",
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("utah.autosink",
                new Problem()
                {
                    AssignID = "3660342",
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("utah.rumormill",
                new Problem()
                {
                    AssignID = "3660343",
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("getshorty",
                new Problem()
                {
                    AssignID = "3660345",
                    PublicTests = 1,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { "u0831180" }
                });

            problems.Add("utah.numbertheory",
                new Problem()
                {
                    AssignID = "3660346",
                    PublicTests = 1,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("bank",
                new Problem()
                {
                    AssignID = "3660347",
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("utah.rainbow",
                new Problem()
                {
                    AssignID = "3660348",
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("narrowartgallery",
                new Problem()
                {
                    AssignID = "3660336",
                    PublicTests = 3,
                    PublicPct = .10,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("spiderman",
                new Problem()
                {
                    AssignID = "3844932",
                    PublicTests = 1,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("utah.tsp",
                new Problem()
                {
                    AssignID = "3660338",
                    PublicTests = 2,
                    PublicPct = .166667,
                    PrivatePct = .75,
                    SkipUsers = { }
                });


            // Obtain problem information
            problem = problems[assignName];
            problem.StartDate = Convert.ToDateTime(startTime);
        }

        // The current problem
        private static Problem problem;

        // Maintains cookie state across HTTP calls
        private static CookieContainer cookies = new CookieContainer();

        // Used to make no special HTTP request
        private const int NONE = 0x0;

        // Used to request that an authentication token be included with HTTP requests
        private const int AUTH = 0x1;

        // Used to request that HTTP redirect response be followed
        private const int REDIRECT = 0x01;

        /// <summary>
        /// Pads out s to be a uNID
        /// </summary>
        private static string ToUnid (String s)
        {
            while (s.Length < 7)
            {
                s = "0" + s;
            }
            return "u" + s;
        }

        /// <summary>
        /// Creates an HttpClient for communicating with a REST server.
        /// </summary>
        public static HttpClient CreateClient(int aspects)
        {
            // Create a client, customizing redirect and authorization behavior
            HttpClient client = new HttpClient(new WebRequestHandler() { AllowAutoRedirect = (aspects & REDIRECT) > 0, CookieContainer = cookies });
            if ((aspects & AUTH) > 0) client.DefaultRequestHeaders.Add("Authorization", "Bearer " + TOKEN);
            return client;
        }

        private static string GetNextURL(HttpResponseHeaders headers)
        {
            string allHeaders = headers.ToString();
            Regex r = new Regex("<([^<>]*)>; rel=\"next\"");
            Match m = r.Match(allHeaders);
            if (m.Success)
            {
                return m.Groups[1].ToString();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Obtains the logs
        /// </summary>
        public static void Main(string[] args)
        {
            // Get the course name
            String courseName = "";
            using (HttpClient client = CreateClient(REDIRECT))
            {
                string url = String.Format("https://utah.instructure.com/api/v1/courses/{0}", courseID);
                HttpResponseMessage response = client.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    dynamic resp = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                    courseName = resp.name;
                }
                else
                {
                    Console.WriteLine("Error: " + response.StatusCode);
                    Console.WriteLine(response.ReasonPhrase.ToString());
                    Console.WriteLine("Unable to read course");
                    return;
                }
            }

            // Get the due dates and assignment name
            using (HttpClient client = CreateClient(AUTH))
            {
                String url = String.Format("https://utah.instructure.com/api/v1/courses/{0}/assignments/{1}", courseID, problem.AssignID);
                HttpResponseMessage response = client.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    dynamic resp = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                    problem.DueDate = Convert.ToDateTime(resp.due_at.ToString()).ToLocalTime();
                    problem.LockDate = Convert.ToDateTime(resp.lock_at.ToString()).ToLocalTime();
                    problem.MaxGrade = Convert.ToInt32(resp.points_possible.ToString());
                    Console.WriteLine("Assignment: " + resp.name);
                    Console.WriteLine("Start Date: " + problem.StartDate);
                    Console.WriteLine("Due Date: " + problem.DueDate);
                    Console.WriteLine("Lock Date: " + problem.LockDate);
                    Console.WriteLine("Max Grade: " + problem.MaxGrade);
                }
                else
                {
                    Console.WriteLine("Error obtaining due date: " + response.StatusCode);
                    return;
                }
            }

            // Get the token
            string csrfToken;
            using (HttpClient client = CreateClient(REDIRECT))
            {
                String url = "https://utah.kattis.com/login";
                HttpResponseMessage response = client.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    string body = response.Content.ReadAsStringAsync().Result;
                    string pattern = "name=\"csrf_token\" value=\"";
                    int index1 = body.LastIndexOf(pattern);
                    index1 = index1 + pattern.Length;
                    int index2 = body.IndexOf("\"", index1);
                    csrfToken = body.Substring(index1, index2 - index1);
                }
                else
                {
                    Console.WriteLine("Error obtaining Kattis token: " + response.StatusCode);
                    return;
                }
            }

            // Get the cookie
            using (HttpClient client = CreateClient(NONE))
            {
                String url = "https://utah.kattis.com/login";
                var values = new Dictionary<string, string>
                {
                    { "user", KATTIS_LOGIN },
                    { "password", KATTIS_PASSWORD },
                    {"submit", "Submit" },
                    {"csrf_token", csrfToken }
                };

                var content = new FormUrlEncodedContent(values);
                HttpResponseMessage response = client.PostAsync(url, content).Result;

                if (response.StatusCode != HttpStatusCode.Redirect)
                {
                    Console.WriteLine("Error logging in to Kattis: " + response.StatusCode);
                    return;
                }
            }

            // Get the data
            dynamic exported;
            using (HttpClient client = CreateClient(REDIRECT))
            {
                String url = String.Format("https://utah.kattis.com/courses/{0}/{1}/export?type=results&submissions=all&include_testcases=true", KATTIS_COURSE, KATTIS_SEMESTER);
                //client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; …) Gecko/20100101 Firefox/57.0");
                //client.DefaultRequestHeaders.Referrer = new Uri("https://utah.kattis.com/courses/CS4150/S18/export");
                //client.DefaultRequestHeaders.Host = "utah.kattis.com";
                Console.WriteLine(client.DefaultRequestHeaders.Accept);

                HttpResponseMessage response = client.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    String s = response.Content.ReadAsStringAsync().Result;
                    exported = JsonConvert.DeserializeObject(response.Content.ReadAsStringAsync().Result);
                }
                else
                {
                    Console.WriteLine("Error obtaining Kattis data: " + response.StatusCode);
                    return;
                }
            }

            // Get the name of the Kattis problem
            bool foundName = false;
            foreach (dynamic problem in exported.problemgroups[0].problems)
            {
                if (problem.problem_name == assignName)
                {
                    Console.WriteLine("Problem: " + problem.title);
                    foundName = true;
                    break;
                }
            }
            if (!foundName)
            {
                Console.WriteLine("Problem finding Kattis problem: " + assignName);
                return;
            }

            // Get the number of test cases
            problem.PrivateTests = -1;
            List<Grade> grades = new List<Grade>();

            foreach (dynamic student in exported.students)
            {
                foreach (dynamic submission in student.submissions)
                {
                    if (submission.problem == assignName && submission.testcase_count != null)
                    {
                        int tests = Convert.ToInt32(submission.testcase_count.ToString());
                        Console.WriteLine("Total tests: " + tests);
                        problem.PrivateTests = tests - problem.PublicTests;
                        break;
                    }
                }
                if (problem.PrivateTests >= 0) break;
            }

            if (problem.PrivateTests < 0)
            {
                Console.WriteLine("Unable to get count of tests");
                return;
            }

            Dictionary<string, string> allStudents = new Dictionary<string, string>();
            using (HttpClient client = CreateClient(REDIRECT | AUTH))
            {
                string url = String.Format("https://utah.instructure.com/api/v1/courses/{0}/search_users?enrollment_type[]=student&per_page=200", courseID);
                while (url != null)
                {
                    HttpResponseMessage response = client.GetAsync(url).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic resp = JArray.Parse(response.Content.ReadAsStringAsync().Result);
                        foreach (dynamic user in resp)
                        {
                            allStudents[user.sis_user_id.ToString()] = user.name.ToString();
                        }
                        url = GetNextURL(response.Headers);
                    }
                    else
                    {
                        Console.WriteLine("Error: " + response.StatusCode);
                        Console.WriteLine(response.ReasonPhrase.ToString());
                        Console.WriteLine("Unable to read users");
                        return;
                    }
                }
                Console.WriteLine("Student count: " + allStudents.Count);
            }

            Console.WriteLine();
            Console.WriteLine((compareGrades) ? "Using only students whose grades have changed" : "Using all students");
            Console.WriteLine((recordGrades) ? "Recording grades" : "Not recording grades");
            Console.Write("Do you want to continue? [y/n] ");
            String yesno = Console.ReadLine();
            if (yesno != "y") return;
            Console.WriteLine();

            Gradebook gradebook = new Gradebook(exported);
            foreach (string unid in allStudents.Keys)
            {
                Grade g = gradebook.GetGrade(unid, assignName, problem);
                string compareResult = "";

                if (compareGrades)
                {
                    using (HttpClient client = CreateClient(REDIRECT | AUTH))
                    {
                        String url = String.Format("https://utah.instructure.com/api/v1/courses/{0}/assignments/{1}/submissions/sis_user_id:{2}", courseID, problem.AssignID, unid);
                        
                        HttpResponseMessage response = client.GetAsync(url).Result;

                        int oldGrade = -1;
                        if (response.IsSuccessStatusCode)
                        {
                            dynamic resp = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                            string og = resp.score.ToString();
                            if (og == null || og == "")
                            {
                                oldGrade = -1;
                            }
                            else
                            {
                                oldGrade = Convert.ToInt32(resp.score.ToString());
                            }
                        }

                        if (oldGrade == -1)
                        {
                            compareResult = "No old grade";
                        }
                        else if (g.Score != oldGrade)
                        {
                            compareResult = "Old grade " + oldGrade;
                        }
                        else
                        {
                            continue;
                        }
                    }
                }

                Console.WriteLine();
                Console.WriteLine(allStudents[unid] + " " + unid);

                if (problem.SkipUsers.Contains(unid))
                {
                    Console.WriteLine("   Skipping");
                    continue;
                }

                Console.Write("   Passed: " + g.PublicTestsPassed + "/" + problem.PublicTests + " " + g.PrivateTestsPassed + "/" + problem.PrivateTests);
                Console.Write("   Grade: " + g.Score);
                if (compareGrades)
                {
                    Console.Write("    (" + compareResult + ")");
                }
                if (g.ExtraDaysUsed > 0)
                {
                    Console.Write("   Extra: " + g.ExtraDaysUsed);
                }
                Console.WriteLine();
                //Console.WriteLine("    " + g.Comment);

                if (recordGrades)
                {
                    using (HttpClient client = CreateClient(REDIRECT | AUTH))
                    {
                        string data = "submission[posted_grade]=" + g.Score;
                        data += "&comment[text_comment]=" + Uri.EscapeDataString(g.Comment);

                        string url = String.Format("https://utah.instructure.com/api/v1/courses/{0}/assignments/{1}/submissions/sis_user_id:{2}", courseID, problem.AssignID, unid);
                        StringContent content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");
                        HttpResponseMessage response = client.PutAsync(url, content).Result;

                        if (response.IsSuccessStatusCode)
                        {
                            Console.WriteLine("Success: " + allStudents[unid] + " " + unid);
                        }
                        else
                        {
                            Console.WriteLine("Error: " + response.StatusCode);
                            Console.WriteLine(response.ReasonPhrase.ToString());
                            Console.WriteLine(allStudents[unid] + " " + unid);
                            Console.WriteLine();
                        }
                    }
                }
            }
        }
    }
}
