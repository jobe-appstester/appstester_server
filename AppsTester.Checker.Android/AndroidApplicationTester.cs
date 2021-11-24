using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Checker.Android.Adb;
using AppsTester.Checker.Android.RabbitMQ;
using AppsTester.Shared;
using EasyNetQ;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mono.Unix;
using SharpAdbClient;
using SharpAdbClient.DeviceCommands;

namespace AppsTester.Checker.Android
{
    internal class GradleTaskResult
    {
        public string Output { get; set; }
        public string Error { get; set; }
        public int Code { get; set; }
    }
    
    internal class AndroidApplicationTester
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAdbClientProvider _adbClientProvider;
        private readonly IRabbitBusProvider _rabbitBusProvider;
        private readonly ILogger<AndroidApplicationTester> _logger;

        public AndroidApplicationTester(
            IConfiguration configuration,
            ILogger<AndroidApplicationTester> logger,
            IHttpClientFactory httpClientFactory,
            IAdbClientProvider adbClientProvider,
            IRabbitBusProvider rabbitBusProvider)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _adbClientProvider = adbClientProvider;
            _rabbitBusProvider = rabbitBusProvider;
        }

        public List<DeviceData> GetOnlineDevices()
        {
            var adbClient = _adbClientProvider.GetAdbClient();
            return adbClient.GetDevices().Where(d => d.State == DeviceState.Online).ToList();
        }

        public async Task<SubmissionCheckResult> CheckSubmissionAsync(
            SubmissionCheckRequest submissionCheckRequest,
            DeviceData deviceData,
            CancellationToken cancellationToken)
        {
            var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            var submissionCheckStatusEvent = new SubmissionCheckStatusEvent
            {
                SubmissionId = submissionCheckRequest.Id,
                OccurenceDateTime = DateTime.UtcNow
            };
            submissionCheckStatusEvent.SetStatus(new AndroidCheckStatus { Status = "checking_started" });
            await rabbitConnection.PubSub.PublishAsync(submissionCheckStatusEvent);
            
            var tempDirectory = CreateBuildDirectory(submissionCheckRequest);
            _logger.LogInformation($"Generated temporary directory: {tempDirectory}");
            
            submissionCheckStatusEvent = new SubmissionCheckStatusEvent
            {
                SubmissionId = submissionCheckRequest.Id,
                OccurenceDateTime = DateTime.UtcNow
            };
            submissionCheckStatusEvent.SetStatus(new AndroidCheckStatus { Status = "unzip_files" });
            await rabbitConnection.PubSub.PublishAsync(submissionCheckStatusEvent);

            try
            {
                await ExtractSubmitFilesAsync(submissionCheckRequest, tempDirectory);
            }
            catch (ZipException e)
            {
                Console.WriteLine(e);
                return new SubmissionCheckResult
                {
                    Id = submissionCheckRequest.Id,
                    Grade = 0,
                    GradleError = "Cannot extract the ZIP file",
                    ResultCode = SubmissionCheckResultCode.CompilationError,
                    TestResults = new List<SubmissionCheckTestResult>(),
                    TotalGrade = 0
                };
            }
            
            await ExtractTemplateFilesAsync(submissionCheckRequest, tempDirectory);
            
            submissionCheckStatusEvent = new SubmissionCheckStatusEvent {
                SubmissionId = submissionCheckRequest.Id,
                OccurenceDateTime = DateTime.UtcNow
            };
            submissionCheckStatusEvent.SetStatus(new AndroidCheckStatus { Status = "gradle_build" });
            await rabbitConnection.PubSub.PublishAsync(submissionCheckStatusEvent);

            var assembleDebugTaskResult = await ExecuteGradleTaskAsync(tempDirectory, "assembleDebug");
            if (assembleDebugTaskResult.Code != 0)
            {
                return new SubmissionCheckResult
                {
                    Id = submissionCheckRequest.Id,
                    Grade = 0,
                    TotalGrade = 0,
                    TestResults = new List<SubmissionCheckTestResult>(),
                    GradleError = (assembleDebugTaskResult.Output + Environment.NewLine + Environment.NewLine + assembleDebugTaskResult.Error).Trim(),
                    ResultCode = SubmissionCheckResultCode.CompilationError
                };
            }
            
            var assembleDebugAndroidTaskResult = await ExecuteGradleTaskAsync(tempDirectory, "assembleDebugAndroidTest");
            if (assembleDebugAndroidTaskResult.Code != 0)
            {
                return new SubmissionCheckResult
                {
                    Id = submissionCheckRequest.Id,
                    Grade = 0,
                    TotalGrade = 0,
                    TestResults = new List<SubmissionCheckTestResult>(),
                    GradleError = (assembleDebugAndroidTaskResult.Output + Environment.NewLine + Environment.NewLine + assembleDebugAndroidTaskResult.Error).Trim(),
                    ResultCode = SubmissionCheckResultCode.CompilationError
                };
            }

            var adbClient = _adbClientProvider.GetAdbClient();
            
            submissionCheckStatusEvent = new SubmissionCheckStatusEvent {
                SubmissionId = submissionCheckRequest.Id,
                OccurenceDateTime = DateTime.UtcNow
            };
            submissionCheckStatusEvent.SetStatus(new AndroidCheckStatus { Status = "install_application" });
            await rabbitConnection.PubSub.PublishAsync(submissionCheckStatusEvent);

            await Task.Run(() =>
            {
                var packageManager = new PackageManager(adbClient, deviceData);
                
                foreach (var package in packageManager.Packages.Where(p => p.Key.Contains("profexam")))
                    packageManager.UninstallPackage(package.Key);

                var apkFilePath = Path.Join(tempDirectory, "app", "build", "outputs", "apk", "debug", "app-debug.apk");
                packageManager.InstallPackage(apkFilePath, true);
                _logger.LogInformation($"Reinstalled debug application in directory: {tempDirectory}");

                var apkFilePath2 = Path.Join(tempDirectory, "app", "build", "outputs", "apk", "androidTest", "debug",
                    "app-debug-androidTest.apk");
                packageManager.InstallPackage(apkFilePath2, true);
                _logger.LogInformation($"Reinstalled androidTest application in directory: {tempDirectory}");
            }, cancellationToken);
            
            submissionCheckStatusEvent = new SubmissionCheckStatusEvent {
                SubmissionId = submissionCheckRequest.Id,
                OccurenceDateTime = DateTime.UtcNow
            };
            submissionCheckStatusEvent.SetStatus(new AndroidCheckStatus { Status = "test" });
            await rabbitConnection.PubSub.PublishAsync(submissionCheckStatusEvent);

            var consoleOutputReceiver = new ConsoleOutputReceiver();
            _logger.LogInformation($"Started testing of Android application for event {submissionCheckRequest.Id}");
            await adbClient.ExecuteRemoteCommandAsync(
                $"am instrument -r -w {submissionCheckRequest.Parameters["android_package_name"]}", deviceData,
                consoleOutputReceiver, Encoding.UTF8, cancellationToken);
            _logger.LogInformation($"Completed testing of Android application for event {submissionCheckRequest.Id}");
            var consoleOutput = consoleOutputReceiver.ToString();

            Directory.Delete(tempDirectory, true);

            return ParseOutputTestResults(submissionCheckRequest, consoleOutput);
        }

        private SubmissionCheckResult ParseOutputTestResults(SubmissionCheckRequest submissionCheckRequest,
            string consoleOutput)
        {
            var statusRegexp =
                new Regex("^INSTRUMENTATION_(STATUS|STATUS_CODE):\\s(.*?)(=(.*?))?((?=INSTRUMENTATION)|(?=onError)|$)",
                    RegexOptions.Singleline);
            var resultRegexp =
                new Regex("^INSTRUMENTATION_(RESULT|CODE):\\s(.*?)(=(.*?))?((?=INSTRUMENTATION)|(?=onError)|$)",
                    RegexOptions.Singleline);
            var onErrorRegexp =
                new Regex("^onError:\\scommandError=(.*?)\\smessage=(.*?)((?=INSTRUMENTATION)|(?=onError)|$)",
                    RegexOptions.Singleline);

            var statusResults = new Dictionary<string, string>();
            var resultResults = new Dictionary<string, string>();

            var statuses = new List<Dictionary<string, string>>();
            var results = new List<Dictionary<string, string>>();
            var errors = new List<Dictionary<string, string>>();

            while (true)
            {
                var match = statusRegexp.Match(consoleOutput);
                if (match.Success)
                {
                    if (match.Groups[1].Value.Trim() == "STATUS")
                    {
                        statusResults.Add(match.Groups[2].Value.Trim(), match.Groups[4].Value.Trim());
                    }
                    else
                    {
                        statusResults.Add("result_code", match.Groups[2].Value.Trim());

                        if (match.Groups[2].Value.Trim() != "1")
                            statuses.Add(statusResults.ToDictionary(p => p.Key, p => p.Value.Trim()));

                        statusResults.Clear();
                    }

                    consoleOutput = consoleOutput.Substring(match.Length).Trim();
                    continue;
                }

                match = resultRegexp.Match(consoleOutput);
                if (match.Success)
                {
                    if (match.Groups[1].Value.Trim() == "RESULT")
                    {
                        resultResults.Add(match.Groups[2].Value.Trim(), match.Groups[4].Value.Trim());
                    }
                    else
                    {
                        resultResults.Add("result_code", match.Groups[2].Value.Trim());

                        results.Add(resultResults.ToDictionary(p => p.Key, p => p.Value.Trim()));
                        resultResults.Clear();
                    }

                    consoleOutput = consoleOutput.Substring(match.Length).Trim();
                    continue;
                }

                match = onErrorRegexp.Match(consoleOutput);
                if (match.Success)
                {
                    errors.Add(new Dictionary<string, string>
                    {
                        ["commandError"] = match.Groups[1].Value.Trim(),
                        ["message"] = match.Groups[2].Value.Trim()
                    });
                    consoleOutput = consoleOutput.Substring(match.Length).Trim();
                    continue;
                }

                if (string.IsNullOrWhiteSpace(consoleOutput)) break;

                _logger.LogCritical($"Unknown unparsed data for event {submissionCheckRequest.Id}: {consoleOutput}");
                break;
            }

            if (!results.Any() || errors.Any())
            {
                return new SubmissionCheckResult
                {
                    Id = submissionCheckRequest.Id,
                    Grade = 0,
                    TotalGrade = 0,
                    TestResults = new List<SubmissionCheckTestResult>(),
                    GradleError = consoleOutput + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => e["message"])),
                    ResultCode = SubmissionCheckResultCode.CompilationError,
                };
            }
            
            var totalResults = results.First();

            return new SubmissionCheckResult
            {
                Id = submissionCheckRequest.Id,
                Grade = int.Parse(totalResults.GetValueOrDefault("grade", "0")),
                TotalGrade = int.Parse(totalResults.GetValueOrDefault("maxGrade", "0")),
                ResultCode = SubmissionCheckResultCode.Success,
                TestResults = statuses
                    .Where(s => s["id"] == "AndroidJUnitRunner")
                    .Select(s => new SubmissionCheckTestResult
                    {
                        Class = s["class"],
                        Test = s["test"],
                        ResultCode = (SubmissionCheckTestResultCode)int.Parse(s["result_code"]),
                        Stream = s["stream"]
                    })
                    .ToList()
            };
        }

        private async Task<GradleTaskResult> ExecuteGradleTaskAsync(string tempDirectory, string taskName)
        {
            _logger.LogInformation($"Started gradle task \"{taskName}\" in directory: {tempDirectory}");

            try
            {
                var unixFileInfo = new UnixFileInfo(Path.Join(tempDirectory, "gradlew"))
                {
                    FileAccessPermissions = FileAccessPermissions.AllPermissions
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new GradleTaskResult
                {
                    Code = -1,
                    Output = "",
                    Error = "Invalid ZIP file structure",
                };
            }

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

            await Task.WhenAll(readErrorTask, readOutputTask, process.WaitForExitAsync());

            _logger.LogInformation($"Completed gradle task \"{taskName}\" in directory: {tempDirectory}");
            
            return new GradleTaskResult
            {
                Code = process.ExitCode,
                Error = readErrorTask.Result,
                Output = readOutputTask.Result
            };
        }

        private async Task ExtractTemplateFilesAsync(SubmissionCheckRequest submissionCheckRequest,
            string tempDirectory)
        {
            var fileStream = await DownloadFileAsync(submissionCheckRequest.Files["template"]);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            await Task.Run(() => archive.ExtractToDirectory(tempDirectory, true));
            _logger.LogInformation($"Extracted template files into the directory: {tempDirectory}");
        }

        private async Task ExtractSubmitFilesAsync(SubmissionCheckRequest submissionCheckRequest, string tempDirectory)
        {
            var downloadFileStream = await DownloadFileAsync(submissionCheckRequest.Files["submission"]);

            var downloadedFile = new MemoryStream();
            await downloadFileStream.CopyToAsync(downloadedFile);

            await Task.Run(() =>
            {
                var fastZip = new FastZip();
                fastZip.ExtractZip(downloadedFile, tempDirectory, FastZip.Overwrite.Always, null, null, null, false, true);
            });
            _logger.LogInformation($"Extracted submit files into the directory: {tempDirectory}");
        }

        private async Task<Stream> DownloadFileAsync(string fileHash)
        {
            using var httpClient = _httpClientFactory.CreateClient();
            var fileStream = await httpClient.GetStreamAsync(
                $"{_configuration["Controller:Url"]}/api/v1/files/{fileHash}");
            return fileStream;
        }

        private static string CreateBuildDirectory(SubmissionCheckRequest submissionCheckRequest)
        {
            var tempDirectory = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }
    }
}