using CsvHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuizGrader
{
    public class Attempt: IComparable<Attempt>
    {
        public Attempt (CsvReader parser, Dictionary<int, int> columns, int attemptColumn)
        {
            UserName = parser.GetField(0);
            UserID = Convert.ToInt32(parser.GetField(1));
            Number = Convert.ToInt32(parser.GetField(attemptColumn));

            answers = new Dictionary<int, Answer>();
            foreach (int col in columns.Keys)
            {
                answers[columns[col]] = new Answer(parser.GetField(col), Convert.ToDouble(parser.GetField(col+1)), ""); 
            }
        }

        public Answer GetAnswer (int questionID)
        {
            return answers[questionID];
        }

        public int UserID { private set; get; }

        public string UserName { private set; get; }

        public int Number { private set; get; }

        private Dictionary<int, Answer> answers;

        public override string ToString ()
        {
            return "[" + Number + " " + UserName + "]";
        }

        public int CompareTo(Attempt a)
        {
            if (this.UserName == a.UserName)
            {
                return this.Number.CompareTo(a.Number);
            }
            else
            {
                return this.UserName.CompareTo(a.UserName);
            }
        }
    }
}
