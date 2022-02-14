using System.Threading;
using AppsTester.Shared.SubmissionChecker.Events;

namespace AppsTester.Shared.SubmissionChecker
{
    public record SubmissionProcessingContext
    (
        SubmissionCheckRequestEvent Event,
        SemaphoreSlim SubmissionsMutex,
        CancellationToken CancellationToken
    );
}