using System;
using System.Collections.Generic;

namespace AppsTester.Shared.Events
{
    public class SubmissionCheckRequestEvent : SubmissionCheckEvent
    {
        public Dictionary<string, object> PlainParameters { get; set; }

        public Dictionary<string, string> Files { get; set; }

        public SubmissionCheckRequestEvent(Guid submissionId) : base(submissionId)
        {
        }
    }
}