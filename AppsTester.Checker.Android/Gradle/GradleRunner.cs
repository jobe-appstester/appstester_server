using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Shared.SubmissionChecker;
using Medallion.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mono.Unix;

namespace AppsTester.Checker.Android.Gradle
{
    internal interface IGradleRunner
    {
        bool IsGradlewInstalledInDirectory(string tempDirectory);
        
        Task<GradleTaskExecutionResult> ExecuteTaskAsync(
            string tempDirectory, string taskName, CancellationToken cancellationToken);
    }
    
    internal class GradleRunner : IGradleRunner
    {
        private readonly ISubmissionProcessingLogger _logger;
        private readonly IConfiguration _configuration;
        private readonly ISubmissionProcessingContextAccessor _submissionProcessingContextAccessor;

        public GradleRunner(
            IConfiguration configuration,
            ISubmissionProcessingLogger logger,
            ISubmissionProcessingContextAccessor submissionProcessingContextAccessor)
        {
            _configuration = configuration;
            _logger = logger;
            _submissionProcessingContextAccessor = submissionProcessingContextAccessor;
        }

        public bool IsGradlewInstalledInDirectory(string tempDirectory)
        {
            return File.Exists(Path.Join(tempDirectory, "gradlew"));
        }

        public async Task<GradleTaskExecutionResult> ExecuteTaskAsync(
            string tempDirectory, string taskName, CancellationToken cancellationToken)
        {
            var submissionsMutex = _submissionProcessingContextAccessor.ProcessingContext.SubmissionsMutex;

            await submissionsMutex.WaitAsync(cancellationToken);

            try
            {
                EnsureGradlewExecutionRights(tempDirectory, taskName);

                _logger.LogInformation(
                    "Started gradle task \"{taskName}\" in directory: {tempDirectory}", taskName, tempDirectory);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Join(tempDirectory, "gradlew"),
                        Arguments = taskName,
                        WorkingDirectory = tempDirectory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        Environment =
                        {
                            ["ANDROID_ROOT_SDK"] = _configuration["ANDROID_SDK_ROOT"]
                        }
                    }
                };

                process.Start();

                var readOutputTask = process.StandardOutput.ReadToEndAsync();
                var readErrorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(readErrorTask, readOutputTask, process.WaitForExitAsync(cancellationToken));

                _logger.LogInformation(
                    "Completed gradle task \"{taskName}\" in directory: {tempDirectory}", taskName, tempDirectory);

                return new GradleTaskExecutionResult
                (
                    ExitCode: process.ExitCode,
                    StandardError: readErrorTask.Result,
                    StandardOutput: readOutputTask.Result
                );
            }
            finally
            {
                submissionsMutex.Release();
            }
        }

        private void EnsureGradlewExecutionRights(string tempDirectory, string taskName)
        {
            try
            {
                // ReSharper disable once ObjectCreationAsStatement
                new UnixFileInfo(Path.Join(tempDirectory, "gradlew"))
                {
                    FileAccessPermissions = FileAccessPermissions.UserExecute
                };
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error happened during execution of Gradle task {taskName}", taskName);
            }
        }
    }
}