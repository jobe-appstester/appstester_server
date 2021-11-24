using System;
using System.Collections.Generic;
using EasyNetQ;

namespace AppsTester.Shared
{
    [Queue(queueName: "Submissions.CheckRequests")]
    public class SubmissionCheckRequest
    {
        public Guid Id { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public Dictionary<string, string> Files { get; set; }
    }
}