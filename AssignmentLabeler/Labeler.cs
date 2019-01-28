using Authentication;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace CanvasUtilities
{
    /// <summary>
    /// Adds a note to each student's grade for a specified assignment.  The main purpose of doing this is to
    /// let the student know who is responsible for the assignment so they know who to contact with questions.
    /// </summary>
    public class AssignmentLabeler
    {
        // This is the note that we will be added to each student's grade.  Customize it as necessary.
        private static string message = "Contact me via Piazza if you have any questions.  Please note that " +
            "some answers that were marked wrong by the auto-grader may have been given points during a manual " +
            "regrade.  If so, any points awarded will be displayed even though the answer is marked wrong.  " +
            "Please look carefully before contacting me.";

        /// <summary>
        /// Adds a note to each student's grade
        /// </summary>
        public static void Main(string[] args)
        {
            // Get the course name
            String courseName = "";
            using (HttpClient client = CanvasHttp.MakeClient())
            {
                string url = String.Format("/api/v1/courses/{0}", Auth.COURSE_ID);
                HttpResponseMessage response = client.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    dynamic resp = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                    courseName = resp.name;
                    Console.WriteLine("Labeler for " + courseName);
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

            // Confirm with the user
            Console.WriteLine("You are about to label the submissions for " + courseName + " " + assignName + " with this message: ");
            Console.WriteLine();
            Console.WriteLine(message);
            Console.WriteLine();
            Console.WriteLine("Do you want to continue? [y/n]");
            String yesno = Console.ReadLine();
            if (yesno != "y") return;

            // Get the students
            Dictionary<string, string> allStudents = new Dictionary<string, string>();
            using (HttpClient client = CanvasHttp.MakeClient())
            {
                string url = String.Format("/api/v1/courses/{0}/search_users?enrollment_type[]=student&per_page=200", Auth.COURSE_ID);
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
                        url = CanvasHttp.GetNextURL(response.Headers);
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

            // Do the labeling
            foreach (string id in allStudents.Keys)
            {
                using (HttpClient client = CanvasHttp.MakeClient())
                {
                    string data = "&comment[text_comment]=" + Uri.EscapeDataString(message);
                    string url = String.Format("/api/v1/courses/{0}/assignments/{1}/submissions/{2}", Auth.COURSE_ID, assignID, id);
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


