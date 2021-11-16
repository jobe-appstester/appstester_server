using System;

namespace AppsTester.Controller.Submissions
{
    internal class Submission
    {
        public Guid Id { get; set; }

        public byte[] TemplateFile { get; set; }
        public byte[] SubmitFile { get; set; }

        public int PassedTests { get; set; }
        public int TotalTests { get; set; }
        public int Grade { get; set; }
        public string ResultOutput { get; set; }

        public DateTime SendingDateTimeUtc { get; set; }
    }
}