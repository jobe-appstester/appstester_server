using EasyNetQ;
using Newtonsoft.Json;

namespace AppsTester.Shared
{
    [Queue(queueName: "Submissions.CheckStatusEvents")]
    public class SubmissionCheckStatusEvent : SubmissionCheckEvent
    {
        public string SerializedStatus { get; set; }

        public void SetStatus(object value)
        {
            SerializedStatus = JsonConvert.SerializeObject(value);
        }

        public TStatus GetStatus<TStatus>()
        {
            return JsonConvert.DeserializeObject<TStatus>(SerializedStatus);
        }
    }
}