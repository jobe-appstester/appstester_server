using System.Collections.Generic;
using EasyNetQ;

namespace AppsTester.Shared.SubmissionChecker.Events
{
    [Queue(queueName: "Submissions.ChecksRequestEvents")]
    public class SubmissionCheckRequestEvent : SubmissionCheckEvent
    {
        public Dictionary<string, object> PlainParameters { get; set; }

        public Dictionary<string, string> Files { get; set; }
    }
}