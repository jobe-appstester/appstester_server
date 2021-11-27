using System.Collections.Generic;
using Newtonsoft.Json;

namespace AppsTester.Controller.Submissions
{
    public sealed class Submission
    {
        [JsonProperty("attempt_id")]
        public int AttemptId { get; set; }

        [JsonProperty("checker_system_name")]
        public string CheckerSystemName { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, object> Parameters { get; set; }

        [JsonProperty("files")]
        public Dictionary<string, string> Files { get; set; }
    }
}