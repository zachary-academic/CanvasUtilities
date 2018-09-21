using CsvHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuizGrader
{
    public class AllAttempts
    {
        public AllAttempts (CsvReader parser, List<Question> questions)
        {
            if (!parser.Read())
            {
                throw new Exception("No headers in CVS file");
            }

            Dictionary<int, int> columns = new Dictionary<int, int>();
            int count = 0;
            int col = 2;
            int attemptColumn = -1;
            while (count < questions.Count)
            {
                string field = parser.GetField(col);
                if (field == "attempt")
                {
                    attemptColumn = col;
                }

                int index = field.IndexOf(":");
                if (index >= 0)
                {
                    int questionID;
                    if (Int32.TryParse(field.Substring(0, index), out questionID))
                    {
                        if (questions.Find(q => q.Id == questionID) != null)
                        {
                            columns[col] = questionID;
                            count++;
                        }
                    }
                }
                col++;
            }

            attempts = new Dictionary<string, Attempt>();
            while (parser.Read())
            {
                Attempt attempt = new Attempt(parser, columns, attemptColumn);
                attempts[attempt.UserID + "_" + attempt.Number] = attempt;
            }
        }

        public int Count { get => attempts.Count; }

        public IEnumerable<Attempt> GetAttempts ()
        {
            foreach (Attempt a in new SortedSet<Attempt>(attempts.Values))
            {
                yield return a;
            }
        }

        private Dictionary<string, Attempt> attempts;

        public Attempt GetAttempt(string userId, int attempt)
        {
            return attempts[userId + "_" + attempt];
        }
    }
}
