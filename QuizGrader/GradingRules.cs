using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;

namespace QuizGrader
{
    public class GradingRules
    {
        /// <summary>
        /// Mapping from question IDs to sets of Answers
        /// </summary>
        private Dictionary<int, SortedSet<Answer>> answerMap;

        /// <summary>
        /// The number of attempts that were used to compile this data
        /// </summary>
        public int Count { private set; get; }

        public GradingRules(List<Question> questions, AllAttempts allAttempts)
        {
            answerMap = new Dictionary<int, SortedSet<Answer>>();
            foreach (Question question in questions)
            {
                answerMap[question.Id] = new SortedSet<Answer>();
            }

            foreach (Attempt attempt in allAttempts.GetAttempts())
            {
                Count++;
                foreach (Question question in questions)
                {
                    Answer answer = attempt.GetAnswer(question.Id);
                    if (answer.Text.Length > 0)
                    {
                        answerMap[question.Id].Add(answer);
                    }
                }
            }
        }

        public GradingRules(CsvReader input)
        {
            input.Configuration.IgnoreBlankLines = false;
            answerMap = new Dictionary<int, SortedSet<Answer>>();
            input.Read();
            Count = input.GetField<int>(0);
            while (input.Read())
            {
                Console.WriteLine(input.GetField(1));
                int questionID = Convert.ToInt32(input.GetField(1));
                answerMap[questionID] = new SortedSet<Answer>();
                while (input.Read())
                {
                    string answerText = input.GetField(1);
                    if (answerText == null || answerText.Length == 0) break;
                    double score = input.GetField<double>(2);
                    string comment = input.GetField(3);
                    comment = (comment == null) ? "" : comment;
                    answerMap[questionID].Add(new Answer(answerText, score, comment));
                }
            }
        }

        public void Write(CsvWriter output, List<Question> questions)
        {
            output.WriteField(Count);
            output.NextRecord();

            foreach (Question question in questions)
            {
                int key = question.Id;
                output.WriteField(question.Name);
                output.WriteField(key);
                output.WriteField("Out of " + question.Points);
                output.NextRecord();
                foreach (Answer answer in answerMap[key])
                {
                    output.WriteField("");
                    output.WriteField(answer.Text);
                    output.WriteField(answer.Points);
                    if (answer.Comment.Length > 0)
                    {
                        output.WriteField(answer.Comment);
                    }
                    output.NextRecord();
                }
                output.WriteField("");
                output.WriteField("");
                output.WriteField("");
                output.NextRecord();
            }
        }

        public Answer GetAnswer(int questionID, string answerText)
        {
            answerText = answerText.Trim();
            if (answerText.Length == 0)
            {
                return new Answer();
            }
            foreach (Answer a in answerMap[questionID])
            {
                if (a.Text == answerText)
                {
                    return a;
                }
            }
            throw new Exception("Answer not found \"" + answerText + "\"");
        }
    }
}
