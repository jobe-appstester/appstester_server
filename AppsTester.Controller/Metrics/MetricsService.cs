using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace AppsTester.Controller.Metrics
{
    public interface IMetricsService
    {
        void CaptureSubmissionCheckResult(int questionNumber, string authorId, int handleTimeMs, string resultStatus, string testingDevice, string testingServer);
    }
    public class MetricsService : IMetricsService
    {
        private readonly Meter meter;
        private readonly Counter<int> submissionResultCounter;
        public MetricsService()
        {
            meter = new Meter("AppsTester.Controller", "1.0.0");
            submissionResultCounter = meter.CreateCounter<int>("submission-result");
        }
        public void CaptureSubmissionCheckResult(int questionNumber, string authorId, int handleTimeMs, string resultStatus, string testingDevice, string testingServer)
        {
            submissionResultCounter.Add(1,
                new KeyValuePair<string, object>("question", "Red"));
        }
    }
}
