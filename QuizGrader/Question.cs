using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuizGrader
{
    public class Question
    {
        public Question(dynamic data)
        {
            Id = data.id;
            Points = data.points_possible;
            Position = data.position;
            Name = "Question " + Position;

            answers = new List<string>();
            dynamic janswers = data.answers;
            for (int i = 0; i < janswers.Count; i++)
            {
                answers.Add(janswers[i].text.ToString().Trim());
            }
        }

        public override String ToString()
        {
            return "[Name=" + Name + " Id=" + Id + ", Points=" + Points + ", Position=" + Position + "]";
        }

        public bool IsAnswer(string s)
        {
            return answers.Contains(s.Trim());
        }

        public int Id { get; private set; }
        public int Points { get; private set; }
        public int Position { get; private set; }
        public string Name { get; private set; }

        private List<string> answers;
        
    }
}
