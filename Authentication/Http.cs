using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Authentication
{
    public static class CanvasHttp
    {
        /// <summary>
        /// Creates an HttpClient for communicating with GitHub.  The GitHub API requires specific information
        /// to appear in each request header.
        /// </summary>
        // Create a client whose base address is the Canvas server and that carries an authorization token
        public static HttpClient MakeClient()
        {
            CookieContainer cookies = new CookieContainer();
            HttpClient client = new HttpClient(new WebRequestHandler() { CookieContainer = cookies })
            {
                BaseAddress = new Uri("https://utah.instructure.com")
            };
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + Auth.TOKEN);
            return client;
        }

        public static string GetNextURL(HttpResponseHeaders headers)
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

        public static List<string> GetAssignmentIDs(string assignmentName)
        {
            List<string> assignmentIDs = new List<string>();
            using (HttpClient client = CanvasHttp.MakeClient())
            {
                string url = String.Format("/api/v1/courses/{0}/assignments", Auth.COURSE_ID);
                while (url != null)
                {
                    HttpResponseMessage response = client.GetAsync(url).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        dynamic assignments = JArray.Parse(response.Content.ReadAsStringAsync().Result);
                        foreach (dynamic assignment in assignments)
                        {
                            if (assignment.name == assignmentName)
                            {
                                assignmentIDs.Add(assignment.id.ToString());
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error: " + response.StatusCode);
                        Console.WriteLine(response.ReasonPhrase.ToString());
                        Console.WriteLine("Unable to read assignment information");
                        throw new Exception();
                    }
                    url = GetNextURL(response.Headers);
                }
                return assignmentIDs;
            }
        }
    }
}
