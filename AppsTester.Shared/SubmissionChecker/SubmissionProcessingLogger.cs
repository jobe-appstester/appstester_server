using System;
using Microsoft.Extensions.Logging;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionProcessingLogger : ILogger
    {
    }

    internal class SubmissionProcessingLogger<TSubmissionChecker> : ISubmissionProcessingLogger
        where TSubmissionChecker : ISubmissionChecker
    {
        private readonly ILogger<TSubmissionChecker> _logger;
        private readonly ISubmissionProcessingContextAccessor _processingContextAccessor;

        // ReSharper disable once ContextualLoggerProblem
        public SubmissionProcessingLogger(
            ILogger<TSubmissionChecker> logger,
            ISubmissionProcessingContextAccessor processingContextAccessor)
        {
            _logger = logger;
            _processingContextAccessor = processingContextAccessor;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            var submissionId = _processingContextAccessor.ProcessingContext.Event.SubmissionId;
            _logger
                .Log(
                    logLevel,
                    eventId,
                    state,
                    exception,
                    formatter: (s, e) => $"[{submissionId}] {formatter(s, e)}"
                );
        }

        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

        public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);
    }
}