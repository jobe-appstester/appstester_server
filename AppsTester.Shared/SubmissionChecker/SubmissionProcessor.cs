using AppsTester.Shared.SubmissionChecker.Events;

namespace AppsTester.Shared.SubmissionChecker
{
    internal interface ISubmissionProcessor
    {
        void SetProcessingContext(SubmissionProcessingContext processingContext);
    }

    public abstract class SubmissionProcessor : ISubmissionProcessor
    {
        protected SubmissionProcessingContext SubmissionProcessingContext;

        protected SubmissionCheckRequestEvent SubmissionCheckRequestEvent => SubmissionProcessingContext.Event;

        public void SetProcessingContext(SubmissionProcessingContext processingContext)
        {
            SubmissionProcessingContext = processingContext;
        }
    }
}