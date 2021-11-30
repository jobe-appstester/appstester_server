using System.Threading;
using AppsTester.Shared.SubmissionChecker.Events;

namespace AppsTester.Shared.SubmissionChecker
{
    public record SubmissionProcessingContext
    (
        SubmissionCheckRequestEvent Event,
        CancellationToken CancellationToken
    );
}