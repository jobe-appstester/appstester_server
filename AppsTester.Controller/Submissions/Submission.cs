using System.Collections.Generic;

namespace AppsTester.Controller.Submissions
{
    public sealed class Submission
    {
        public int AttemptId { get; set; }
        public string CheckerSystemName { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public Dictionary<string, string> Files { get; set; }
    }
}