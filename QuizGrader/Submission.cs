using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuizGrader
{
    public class Submission
    {
        public Submission (dynamic data)
        {
            Id = data.id;
            Attempt = data.attempt;
            UserId = data.user_id;
        }

        public override string ToString ()
        {
            return "[Id=" + Id + ", UserId=" + UserId + ", Attempt=" + Attempt + "]";
        }

        public int Id { private set; get; }

        public int Attempt { private set; get; }

        public string UserId { private set; get; }
    }
}
