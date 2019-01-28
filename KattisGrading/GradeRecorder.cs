using Authentication;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
        // Set of Kattis problems that will be assigned
        private static Dictionary<string, Problem> problems = new Dictionary<string, Problem>();

        // Compare grades during run?
        private static bool compareGrades = false;

        // Record grades during run?
        private static bool recordGrades = false;

        // Create the problems dictionary
        static GradeRecorder()
        {
            problems.Add("utah.anagram",
                new Problem()
                {
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("ceiling",
                new Problem()
                {
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("utah.galaxyquest",
                new Problem()
                {
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("utah.autosink",
                new Problem()
                {
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("utah.rumormill",
                new Problem()
                {
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("getshorty",
                new Problem()
                {
                    PublicTests = 1,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = {  }
                });

            problems.Add("utah.numbertheory",
                new Problem()
                {
                    PublicTests = 1,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("bank",
                new Problem()
                {
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("utah.rainbow",
                new Problem()
                {
                    PublicTests = 2,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("narrowartgallery",
                new Problem()
                {
                    PublicTests = 3,
                    PublicPct = .10,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("spiderman",
                new Problem()
                {
                    PublicTests = 1,
                    PublicPct = 0,
                    PrivatePct = .50,
                    SkipUsers = { }
                });

            problems.Add("utah.tsp",
                new Problem()
                {
                    PublicTests = 2,
                    PublicPct = .166667,
                    PrivatePct = .75,
                    SkipUsers = { }
                });
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
        private static string ToUnid(String s)
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
            if ((aspects & AUTH) > 0) client.DefaultRequestHeaders.Add("Authorization", "Bearer " + Auth.TOKEN);
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


        private static String GetKattisProblemName(string html)
        {
            Regex r = new Regex(@"""https://utah.kattis.com/problems/([^""]+)\""");
            Match m = r.Match(html);
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
            // Get the course name and the Kattis problem
            String courseName;
            using (HttpClient client = CreateClient(REDIRECT))
            {
                string url = String.Format("https://utah.instructure.com/api/v1/courses/{0}", Auth.COURSE_ID);
                HttpResponseMessage response = client.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    dynamic resp = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                    courseName = resp.name;
                    Console.WriteLine("Grading Kattis problem for " + courseName);
                }
                else
                {
                    Console.WriteLine("Error: " + response.StatusCode);
                    Console.WriteLine(response.ReasonPhrase.ToString());
                    Console.WriteLine("Unable to read course");
                    return;
                }
            }

            // Get the assignment ID
            string assignID;
            {
                Console.Write("Enter assignment name: ");
                string assignName = Console.ReadLine();
                List<string> assignmentIDs = CanvasHttp.GetAssignmentIDs(assignName);
                if (assignmentIDs.Count == 0)
                {
                    Console.WriteLine("There is no assigment by that name");
                    return;
                }
                else if (assignmentIDs.Count > 1)
                {
                    Console.WriteLine("There are " + assignmentIDs.Count + " assignments by that name");
                    return;
                }
                else
                {
                    assignID = assignmentIDs[0];
                }
            }

            // Get the Kattis problem name
            string problemName;
            using (HttpClient client = CreateClient(REDIRECT))
            {
                string url = String.Format("https://utah.instructure.com/api/v1/courses/{0}/assignments/{1}", Auth.COURSE_ID, assignID);
                HttpResponseMessage response = client.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    dynamic resp = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                    problemName = GetKattisProblemName(resp.description.ToString());
                    if (problemName == null)
                    {
                        Console.WriteLine("No Kattis problem referenced from the problem");
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Grading Kattis problem " + problemName);
                        problem = problems[problemName];
                        problem.StartDate = Convert.ToDateTime(Auth.START_TIME).ToLocalTime();
                    }
                }
                else
                {
                    Console.WriteLine("Error: " + response.StatusCode);
                    Console.WriteLine(response.ReasonPhrase.ToString());
                    Console.WriteLine("Unable to read assignment");
                    return;
                }
            }

            // Due date overrides
            Dictionary<int, Tuple<DateTime, DateTime>> overrides = new Dictionary<int, Tuple<DateTime, DateTime>>();

            // Get the due dates and assignment name
            using (HttpClient client = CreateClient(AUTH))
            {
                String url = String.Format("https://utah.instructure.com/api/v1/courses/{0}/assignments/{1}?include[]=overrides", Auth.COURSE_ID, assignID);
                HttpResponseMessage response = client.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    dynamic resp = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                    problem.DueDate = Convert.ToDateTime(resp.due_at.ToString()).ToLocalTime();
                    problem.LockDate = Convert.ToDateTime(resp.lock_at.ToString()).ToLocalTime();
                    problem.MaxGrade = Convert.ToInt32(resp.points_possible.ToString());
                    Console.WriteLine("Assignment: " + resp.name);
                    Console.WriteLine("Start Date: " + problem.StartDate);
                    Console.WriteLine("Default Due Date: " + problem.DueDate);
                    Console.WriteLine("Default Lock Date: " + problem.LockDate);
                    Console.WriteLine("Max Grade: " + problem.MaxGrade);

                    foreach (dynamic oride in resp.overrides)
                    {
                        foreach (dynamic id in oride.student_ids)
                        {
                            overrides[(int)id] = new Tuple<DateTime, DateTime>(Convert.ToDateTime(oride.due_at).ToLocalTime(), Convert.ToDateTime(oride.lock_at).ToLocalTime());
                        }
                    }
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
                    { "user", Auth.KATTIS_LOGIN },
                    { "password", Auth.KATTIS_PASSWORD },
                    { "submit", "Submit" },
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
                String url = String.Format("https://utah.kattis.com/courses/{0}/{1}/export?type=results&submissions=all&include_testcases=true", Auth.KATTIS_COURSE, Auth.KATTIS_SEMESTER);
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

            // Get the number of test cases
            problem.PrivateTests = -1;
            List<Grade> grades = new List<Grade>();

            foreach (dynamic student in exported.students)
            {
                foreach (dynamic submission in student.submissions)
                {
                    if (submission.problem == problemName && submission.testcase_count != null)
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
            Dictionary<string, int> allIDs = new Dictionary<string, int>();
            using (HttpClient client = CreateClient(REDIRECT | AUTH))
            {
                string url = String.Format("https://utah.instructure.com/api/v1/courses/{0}/search_users?enrollment_type[]=student&per_page=200", Auth.COURSE_ID);
                while (url != null)
                {
                    HttpResponseMessage response = client.GetAsync(url).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic resp = JArray.Parse(response.Content.ReadAsStringAsync().Result);
                        foreach (dynamic user in resp)
                        {
                            allStudents[user.sis_user_id.ToString()] = user.name.ToString();
                            allIDs[user.sis_user_id.ToString()] = (int)user.id;
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
                // Get due and lock dates for this student.  They can differ because of extensions.
                DateTime dueDate;
                DateTime lockDate;
                getDueAndLockDate(allIDs[unid], overrides, out dueDate, out lockDate);

                Grade g = gradebook.GetGrade(unid, problemName, problem, dueDate, lockDate);

                string compareResult = "";

                if (compareGrades)
                {
                    using (HttpClient client = CreateClient(REDIRECT | AUTH))
                    {
                        String url = String.Format("https://utah.instructure.com/api/v1/courses/{0}/assignments/{1}/submissions/sis_user_id:{2}", Auth.COURSE_ID, assignID, unid);

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
                if (g.DueDate != problem.DueDate || g.LockDate != problem.LockDate)
                {
                    Console.WriteLine("   " + "Due " + g.DueDate + "  " + "Locked " + g.LockDate);
                    Console.WriteLine("   " + "Due " + problem.DueDate + "  " + "Locked " + problem.LockDate);
                }

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

                        string url = String.Format("https://utah.instructure.com/api/v1/courses/{0}/assignments/{1}/submissions/sis_user_id:{2}", Auth.COURSE_ID, assignID, unid);
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

        /// <summary>
        /// Gets the due and lock dates for the student for the assignment.
        /// </summary>
        private static void getDueAndLockDate(int id, Dictionary<int, Tuple<DateTime,DateTime>> extensions, out DateTime dueDate, out DateTime lockDate)
        {
            dueDate = problem.DueDate;
            lockDate = problem.LockDate;
            if (extensions.TryGetValue(id, out Tuple<DateTime,DateTime> extension))
            {
                dueDate = extension.Item1;
                lockDate = extension.Item2;
            }
        }
    }
}
