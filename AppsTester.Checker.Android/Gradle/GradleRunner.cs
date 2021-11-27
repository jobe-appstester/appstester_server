using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ILogger<GradleRunner> _logger;
        private readonly IConfiguration _configuration;

        private readonly SemaphoreSlim _semaphore = new (initialCount: 1);

        public GradleRunner(ILogger<GradleRunner> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public bool IsGradlewInstalledInDirectory(string tempDirectory)
        {
            return File.Exists(Path.Join(tempDirectory, "gradlew"));
        }

        public async Task<GradleTaskExecutionResult> ExecuteTaskAsync(
            string tempDirectory, string taskName, CancellationToken cancellationToken)
        {
            EnsureGradlewExecutionRights(tempDirectory, taskName);

            try
            {
                _logger.LogInformation($"Started gradle task \"{taskName}\" in directory: {tempDirectory}");

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

                await _semaphore.WaitAsync(cancellationToken);

                process.Start();

                var readOutputTask = process.StandardOutput.ReadToEndAsync();
                var readErrorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(readErrorTask, readOutputTask, process.WaitForExitAsync(cancellationToken));

                _logger.LogInformation($"Completed gradle task \"{taskName}\" in directory: {tempDirectory}");

                return new GradleTaskExecutionResult
                (
                    ExitCode: process.ExitCode,
                    StandardError: readErrorTask.Result,
                    StandardOutput: readOutputTask.Result
                );
            }
            finally
            {
                _semaphore.Release();
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
                _logger.LogError(exception, $"Error happened during execution of Gradle task {taskName}");
            }
        }
    }
}