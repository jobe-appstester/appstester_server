using System;
using Microsoft.Extensions.Logging;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionProcessingLogger : ILogger
    {
    }

    internal class SubmissionProcessingLogger<TSubmissionChecker> : SubmissionProcessor, ISubmissionProcessingLogger
        where TSubmissionChecker : ISubmissionChecker
    {
        private readonly ILogger<TSubmissionChecker> _logger;

        // ReSharper disable once ContextualLoggerProblem
        public SubmissionProcessingLogger(ILogger<TSubmissionChecker> logger)
        {
            _logger = logger;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            var submissionId = SubmissionCheckRequestEvent.SubmissionId;
            _logger
                .Log(
                    logLevel,
                    eventId,
                    state,
                    exception,
                    formatter: (s, e) => $"[Submission|{submissionId}]{formatter(s, e)}"
                );
        }

        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

        public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);
    }
}