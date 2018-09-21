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
    class GradeRecorder
    {
        // Canvas acccess token
        private static string TOKEN = "";

        // CS3500 Spring 2017
        private static string courseID = "428181";

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
            SortedSet<string> allGroups = new SortedSet<string>();
            using (HttpClient client = CreateClient())
            {
                string url = String.Format("/api/v1/courses/{0}/groups?per_page=200", courseID);
                while (url != null)
                {
                    HttpResponseMessage response = client.GetAsync(url).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        dynamic resp = JArray.Parse(response.Content.ReadAsStringAsync().Result);
                        foreach (dynamic group in resp)
                        {
                            allGroups.Add(group.name.ToString());
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
            foreach (string group in allGroups)
            {
                Console.WriteLine(group);
            }
        }
    }
}


