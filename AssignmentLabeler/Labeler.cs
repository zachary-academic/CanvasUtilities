using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace AssignmentLabeler
{
    /// <summary>
    /// To use:
    /// (1) Set TOKEN if necessary
    /// (2) Set courseID if necessary
    /// (3) Set assignID
    /// </summary>
    class GradeRecorder
    {
        // Canvas acccess token
        private static string TOKEN = "2~XJE0CRdKd4XUePwp2XLT1uC6yLspVfkLy2F32LrUBJ7ggUmoi3oqobON75QkYNJO";

        // Spring 2018
        private static string courseID = "475628";
        private static string assignID = "4525677";

        /// <summary>
        /// Creates an HttpClient for communicating with GitHub.  The GitHub API requires specific information
        /// to appear in each request header.
        /// </summary>
        public static HttpClient CreateClient()
        {
            // Create a client whose base address is the Canvas server and that carries an authorization token
            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri("https://utah.instructure.com")
            };
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + TOKEN);
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
            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/api/v1/courses/{0}", courseID);
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

            // Get the assignment name
            String assignName;
            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/api/v1/courses/{0}/assignments/{1} ", courseID, assignID);
                HttpResponseMessage response = client.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    dynamic resp = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                    assignName = resp.name;
                }
                else
                {
                    Console.WriteLine("Error: " + response.StatusCode);
                    Console.WriteLine(response.ReasonPhrase.ToString());
                    Console.WriteLine("Unable to read assignment");
                    return;
                }
            }

            Console.WriteLine("You are about to label the submissions for " + courseName + " " + assignName);
            Console.WriteLine("Do you want to continue? [y/n]");
            String yesno = Console.ReadLine();
            if (yesno != "y") return;

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
                            allStudents[user.id.ToString()] = user.name.ToString();
                            //allStudents[user.sis_user_id.ToString()] = user.name.ToString();
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

            foreach (string id in allStudents.Keys)
            {
                using (HttpClient client = CreateClient())
                {
                    string data = "&comment[text_comment]=" + Uri.EscapeDataString("If you have questions, contact TANYA (if your last name comes before LIN alphabetically) or JOE (otherwise)");
                    string url = String.Format("/api/v1/courses/{0}/assignments/{1}/submissions/{2}", courseID, assignID, id);
                    //string url = String.Format("/api/v1/courses/{0}/assignments/{1}/submissions/sis_user_id:{2}", courseID, assignID, id);

                    StringContent content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");
                    HttpResponseMessage response = client.PutAsync(url, content).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Success: " + allStudents[id] + " " + id);
                    }
                    else
                    {
                        Console.WriteLine("Error: " + response.StatusCode);
                        Console.WriteLine(response.ReasonPhrase.ToString());
                        Console.WriteLine(allStudents[id] + " " + id);
                        Console.WriteLine();
                    }
                }
            }
        }
    }
}


