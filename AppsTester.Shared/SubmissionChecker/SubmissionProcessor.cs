using AppsTester.Shared.Events;

namespace AppsTester.Shared.SubmissionChecker
{
    internal interface ISubmissionProcessor
    {
        void SetProcessingSubmission(SubmissionCheckRequestEvent submissionCheckRequestEvent);
    }

    public abstract class SubmissionProcessor : ISubmissionProcessor
    {
        protected SubmissionCheckRequestEvent SubmissionCheckRequestEvent;

        public void SetProcessingSubmission(SubmissionCheckRequestEvent submissionCheckRequestEvent)
        {
            SubmissionCheckRequestEvent = submissionCheckRequestEvent;
        }
    }
}