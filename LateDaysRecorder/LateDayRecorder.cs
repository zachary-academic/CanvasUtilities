using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
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
        public int MaxGrade { get; set; }
        public double PublicPct { get; set; }
        public double PrivatePct { get; set; }
        public ISet<string> SkipUsers { get; set; } = new HashSet<string>();
    }

    class GradeRecorder
    {
        // Canvas acccess token
        private static string TOKEN = "";

        // CS 4150 Spring 2017
        //private static string courseID = "428184";

        // CS 4150 Fall 2016
        //private static string courseID = "401191";

        // CS 3500 Spring 2017
        private static string courseID = "428181";

        private static string lateDaysAssignID = "3784272";

        /// <summary>
        /// Creates an HttpClient for communicating with GitHub.  The GitHub API requires specific information
        /// to appear in each request header.
        /// </summary>
        public static HttpClient CreateClient()
        {
            // Create a client whose base address is the GitHub server
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("https://utah.instructure.com");

            // Tell the server that the client will accept this particular type of response data
            //client.DefaultRequestHeaders.Accept.Clear();
            //client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            // This is an authorization token that you can create by logging in to your GitHub account.
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + TOKEN);

            // There is more client configuration to do, depending on the request.
            return client;
        }

        /// <summary>
        /// Obtains the logs
        /// </summary>
        public static void Main(string[] args)
        {
            // Get all the students
            Dictionary<string, string> allStudents = new Dictionary<string, string>();
            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/api/v1/courses/{0}/search_users?enrollment_type[]=student&per_page=200", courseID);

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
            }


            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/api/v1/courses/{0}/assignments/{1}/overrides", courseID, "3760703");
                HttpResponseMessage response = client.GetAsync(url).Result;
                Console.WriteLine(JArray.Parse(response.Content.ReadAsStringAsync().Result));
            }

            // Get all the assignments that are past their due date
            Dictionary <string, dynamic> assignments = new Dictionary<string, dynamic>();
            DateTime now = DateTime.Now;
            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/api/v1/courses/{0}/assignments?per_page=200", courseID);

                while (url != null)
                {
                    HttpResponseMessage response = client.GetAsync(url).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic resp = JArray.Parse(response.Content.ReadAsStringAsync().Result);
                        foreach (dynamic assignment in resp)
                        {
                            Console.WriteLine(assignment.name + " " + assignment.due_at);
                            if ((bool)(assignment.published) && assignment.due_at != null)
                            {
                                DateTime due = ((DateTime)assignment.due_at).ToLocalTime();
                                if (now > due)
                                {
                                    assignments.Add((string)assignment.id, assignment);
                                }
                            }
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
            }

            // For each student, obtain late days.  Note that because of extensions, every student can have a different due date.
            foreach (string unid in allStudents.Keys)
            {
                Console.WriteLine(allStudents[unid]);
                string explain = "";
                int lateDays = 0;

                foreach (string assignID in assignments.Keys)
                {
                    bool ignore = ((DateTime)assignments[assignID].due_at).Minute == 58;
                    if (ignore) continue;

                    bool noHandin = assignments[assignID].submission_types.First == "none";
                    DateTime? submissionTime = GetSubmissionTime(assignID, unid, noHandin);
                    DateTime subTime; 
                    if (submissionTime.HasValue)
                    {
                        subTime = submissionTime.Value;
                    }
                    else
                    {
                        continue;
                    }
                    DateTime dueTime = ((DateTime)assignments[assignID].due_at).ToLocalTime();
                    if (subTime > dueTime)
                    {
                        int daysLate = (((int)Math.Truncate((subTime - dueTime).TotalHours)) + 23) / 24;
                        lateDays += daysLate;
                        explain += assignments[assignID].name + " was due " + dueTime + " and was turned in " +
                            subTime + ", which was " + daysLate + " " + ((daysLate == 1) ? "day" : "days") + " late.\n";

                    }
                }

                Console.WriteLine(explain);
                RecordLateDays(lateDays, explain, unid);
            }
        }

        private static void RecordLateDays(int lateDays, string explain, string unid)
        {
            using (HttpClient client = CreateClient())
            {
                Console.WriteLine(unid);
                string data = "&comment[text_comment]=" + Uri.EscapeDataString(explain) + "&submission[posted_grade]=" + lateDays;
                string url = String.Format("/api/v1/courses/{0}/assignments/{1}/submissions/sis_user_id:{2}", courseID, lateDaysAssignID, unid);
                StringContent content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");
                HttpResponseMessage response = client.PutAsync(url, content).Result;

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Success: "  + unid);
                }
                else
                {
                    Console.WriteLine("Error: " + response.StatusCode);
                    Console.WriteLine(response.ReasonPhrase.ToString());
                    Console.WriteLine(unid);
                    Console.WriteLine();
                }
            }
        }

        private static DateTime? GetSubmissionTime (string assignID, string unid, bool noHandin)
        {
            using (HttpClient client = CreateClient())
            {
                if (noHandin)
                {
                    string url = String.Format("/api/v1/courses/{0}/assignments/{1}/submissions/sis_user_id:{2}?include[]=submission_comments", courseID, assignID, unid);
                    HttpResponseMessage response = client.GetAsync(url).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        dynamic resp = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                        foreach (dynamic comment in resp.submission_comments)
                        {
                            if ((string)(comment.author_name) == "JOE ZACHARY")
                            {
                                Regex dt = new Regex(@"\d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d");
                                Match m = dt.Match((string)comment.comment);
                                if (m.Success)
                                {
                                    DateTime subTime;
                                    if (DateTime.TryParse(m.Groups[0].Value, out subTime))
                                    {
                                        return subTime;
                                    }                                  
                                }
                            }
                        }
                        return null;
                    }
                    else
                    {
                        Console.WriteLine("Error: " + response.StatusCode);
                        Console.WriteLine(response.ReasonPhrase.ToString());
                        Console.WriteLine("Unable to read users");
                        throw new Exception("Error reading submission");
                    }
                }
                else
                {
                    string url = String.Format("/api/v1/courses/{0}/assignments/{1}/submissions/sis_user_id:{2}", courseID, assignID, unid);
                    HttpResponseMessage response = client.GetAsync(url).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic resp = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                        //Console.WriteLine(resp);
                        if (resp.score == 0) return null;
                        //if (IsEmptySubmission()) return GetEmptySubmissionTime();
                        if (resp.submitted_at == null) return null;
                        DateTime subTime = ((DateTime)resp.submitted_at).ToLocalTime();
                        return subTime;
                    }
                    else
                    {
                        Console.WriteLine("Error: " + response.StatusCode);
                        Console.WriteLine(response.ReasonPhrase.ToString());
                        Console.WriteLine("Unable to read users");
                        throw new Exception("Error reading submission");
                    }
                }
            }
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
    }
}


