using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;

namespace KattisGrading
{
    public class Grade
    {
        public DateTime SubmitTime { get; }

        public string Language { get; }

        public string Runtime { get; }

        public string Comment { get; }

        public int ExtraDaysUsed { get; }

        public int TestsPassed { get; }

        public int PublicTestsPassed { get; private set; }

        public int PrivateTestsPassed { get; private set; }

        public int Score { get; }

        private string Plural(string v, int n)
        {
            return (n == 1) ? v : (v + "s");
        }

        public Grade(dynamic submission, Problem problem)
        {
            SubmitTime = submission.time;
            Language = submission.language;
            Runtime = submission.runtime;
            TestsPassed = GetTestsPassed(submission);
            ExtraDaysUsed = GetDaysLate(problem.DueDate, SubmitTime);
            Score = GetScore(problem);
            Comment = GetComment(problem);
        }

        public Grade(Problem problem)
        {
            SubmitTime = Convert.ToDateTime("01-01-2000");
            Language = "Unknown";
            Runtime = "Unknown";
            TestsPassed = 0;
            ExtraDaysUsed = 0;
            Score = 0;
            PublicTestsPassed = 0;
            PrivateTestsPassed = 0;
            Comment = "No program submitted before the cutoff date";
        }

        private static int GetDaysLate(DateTime dueTime, DateTime submittedTime)
        {
            if (submittedTime > dueTime)
            {
                TimeSpan delta = submittedTime - dueTime;
                if (delta.Days == 0 && delta.Hours == 0)
                {
                    return 0;
                }
                else
                {
                    return delta.Days + 1;
                }
            }
            else
            {
                return 0;
            }
        }

        private int GetTestsPassed(dynamic submission)
        {
            int testsPassed = 0;
            foreach (dynamic testcase in submission.testcases)
            {
                if (testcase.judgement == "Accepted")
                {
                    testsPassed++;
                }
            }
            return testsPassed;
        }

        private int GetScore(Problem problem)
        {
            int totalTests = problem.PublicTests + problem.PrivateTests;
            PrivateTestsPassed = (TestsPassed - problem.PublicTests > 0) ? TestsPassed - problem.PublicTests : 0;
            PublicTestsPassed = TestsPassed - PrivateTestsPassed;

            // All tests passed
            if (TestsPassed == totalTests)
            {
                return problem.MaxGrade;
            }

            // All public tests passed; some private tests passed
            else if (TestsPassed >= problem.PublicTests)
            {
                if (problem.PrivateTests == 1)
                {
                    return 0;
                }
                double partialPct = PrivateTestsPassed / (problem.PrivateTests - 1.0);
                return (int)Math.Round(problem.MaxGrade * problem.PublicPct + problem.MaxGrade * problem.PrivatePct * partialPct, MidpointRounding.AwayFromZero);
            }

            // Some public tests passed; no private tests passed
            else
            {
                double partialPct = PublicTestsPassed / problem.PublicTests;
                return (int)Math.Round(problem.MaxGrade * problem.PublicPct * partialPct, MidpointRounding.AwayFromZero);
            }
        }

        private string GetComment(Problem problem)
        {
            string extra = "";
            if (ExtraDaysUsed > 0)
            {
                extra = "(submitted " + ExtraDaysUsed + " " + Plural("day", ExtraDaysUsed) + " late) ";
            }
            return Language + " program with run time of " + Runtime + " seconds " + extra +
                "passed " + Math.Min(TestsPassed, problem.PublicTests) + " out of " + problem.PublicTests + " public tests and " +
                (TestsPassed - problem.PublicTests) + " out of " + problem.PrivateTests + " private tests on Kattis " + SubmitTime;
        }
    }

    public class Gradebook
    {
        private Dictionary<string, dynamic> history;

        public Gradebook(dynamic exported)
        {
            history = new Dictionary<string, dynamic>();
            foreach (dynamic student in exported.students)
            {
                string email = student.email.ToString();
                int index = email.IndexOf("@utah.edu");
                if (index < 0) index = email.IndexOf("@umail.utah.edu");
                if (index < 0) continue;
                string unid = email.Substring(0, index).ToLower();
                history[unid] = student;
            }
        }

        /// <summary>
        /// Get the maximum number of test cases passed by each student by the due date plus extra days.
        /// <summary>
        public Grade GetGrade(string unid, string problemName, Problem problem)
        {
            dynamic student;
            if (!history.TryGetValue(unid, out student))
            {
                return new Grade(problem);
            }

            Grade grade = new Grade(problem);
            foreach (dynamic submission in student.submissions)
            {
                if (submission.problem == problemName)
                {
                    DateTime submissionTime = Convert.ToDateTime((string)submission.time);
                    if (submissionTime <= problem.LockDate && submissionTime >= problem.StartDate)
                    {
                        Grade g = new Grade(submission, problem);
                        if (g.TestsPassed > grade.TestsPassed)
                        {
                            grade = g;
                        }
                        else if (g.TestsPassed == grade.TestsPassed && submissionTime < grade.SubmitTime)
                        {
                            grade = g;
                        }
                    }
                }
            }
            return grade;
        }
    }
}

