using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuizGrader
{
    public class Answer : IComparable<Answer>
    {
        public Answer ()
        {
            Text = "";
            Points = 0;
            Comment = "";
        }

        public Answer (string text, double points, string comment)
        {
            Text = text.Trim();
            Points = points;
            Comment = comment;
        }

        public String Text { private set; get; }

        public double Points { private set; get; }

        public String Comment { private set; get; }

        public int CompareTo(Answer a)
        {
            return Text.CompareTo(a.Text);
        }
    }
}
