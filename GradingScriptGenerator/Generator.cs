using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GradingScriptGenerator
{
    class Generator
    {
        // Canvas acccess token
        private static string TOKEN = "";

        // CS 4150 Spring 2017
        //private static string courseID = "428184";

        // CS 4150 Fall 2016
        //private static string courseID = "401191";

        // CS 3500 Spring 2017
        private static string courseID = "428181";

        private static string quizID = "953709";

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
            Console.Out.NewLine = "\n";

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
                            allStudents[user.sis_user_id.ToString()] = user.sortable_name.ToString();
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

            // Create the script commands for each student
            Console.WriteLine("#!/bin/csh -f");
            Console.WriteLine("git config --global credential.helper 'cache --timeout 3600'");
            Console.WriteLine();

            foreach (string unid in allStudents.Keys)
            {
                Console.WriteLine("");
                Console.WriteLine("git clone http://github.com/UofU-CS3500-S17/" + unid);
                Console.WriteLine("cd " + unid);
                Console.WriteLine("git checkout PS2 2> CHECKOUT.txt");
                Console.WriteLine("cd ..");
                Console.WriteLine("mv " + unid + " " + allStudents[unid].Replace(",", "").Replace(' ', '_'));
                Console.WriteLine();
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
