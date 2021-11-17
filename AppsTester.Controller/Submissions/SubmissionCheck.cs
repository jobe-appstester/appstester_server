using System;
using System.ComponentModel.DataAnnotations.Schema;
using AppsTester.Shared;
using Newtonsoft.Json;

namespace AppsTester.Controller.Submissions
{
    public class SubmissionCheck
    {
        public Guid Id { get; set; }
        
        public int MoodleId { get; set; }

        [NotMapped]
        public SubmissionCheckRequest SubmissionCheckRequest
        {
            get => JsonConvert.DeserializeObject<SubmissionCheckRequest>(SerializedSubmissionCheckRequest);
            set => SerializedSubmissionCheckRequest = JsonConvert.SerializeObject(value);
        }
        
        public string SerializedSubmissionCheckRequest { get; set; }

        [NotMapped]
        public SubmissionCheckResult SubmissionCheckResult
        {
            get => JsonConvert.DeserializeObject<SubmissionCheckResult>(SerializedSubmissionCheckResult);
            set => SerializedSubmissionCheckResult = JsonConvert.SerializeObject(value);
        }
        
        public string SerializedSubmissionCheckResult { get; set; }
        public string SubmissionCheckStatus { get; set; }
        
        public DateTime SendingDateTimeUtc { get; set; } = DateTime.UtcNow;
    }
}