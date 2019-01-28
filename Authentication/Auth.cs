using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Authentication
{
    public static class Auth
    {
        /// <summary>
        /// Initializer to read credentials
        /// </summary>
        static Auth ()
        {
            using (StreamReader file = new System.IO.StreamReader("Auth.config"))
            {
                file.ReadLine();
                TOKEN = file.ReadLine();
                file.ReadLine();
                KATTIS_LOGIN = file.ReadLine();
                file.ReadLine();
                KATTIS_PASSWORD = file.ReadLine();
            }
        }

        // Canvas acccess token
        public static readonly string TOKEN;

        // Kattis login name
        public static readonly string KATTIS_LOGIN;

        // Kattis password
        public static readonly string KATTIS_PASSWORD;

        // Spring 2019 Canvas Course ID
        public const string COURSE_ID = "543759";

        // Time before which submissions are ignored
        public const string START_TIME = "01/06/2019";

        // Name of course on Kattis
        public const string KATTIS_COURSE = "CS4150";

        // Name of semester on Kattis
        public const string KATTIS_SEMESTER = "S19";
    }
}
