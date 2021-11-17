using System;
using System.Collections.Generic;

namespace AppsTester.Shared
{
    public class SubmissionCheckRequest
    {
        public Guid Id { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public Dictionary<string, string> Files { get; set; }
    }
}