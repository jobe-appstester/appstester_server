using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharpAdbClient;
using SharpAdbClient.DeviceCommands;

namespace AppsTester.Builder.Android
{
    internal class AndroidApplicationTester
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AndroidApplicationTester> _logger;

        public AndroidApplicationTester(IConfiguration configuration, ILogger<AndroidApplicationTester> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<TestApplicationResponse> TestAsync(TestApplicationRequest request)
        {
            var tempDirectory = CreateBuildDirectory(request);
            _logger.LogInformation($"Generated temporary directory: {tempDirectory}");

            ExtractSubmitFiles(request, tempDirectory);
            ExtractTemplateFiles(request, tempDirectory);

            await ExecuteGradleTaskAsync(tempDirectory, "assembleDebug");
            await ExecuteGradleTaskAsync(tempDirectory, "assembleDebugAndroidTest");
            
            var adbClient = CreateAdbClient();
            
            var packageManager = new PackageManager(adbClient, adbClient.GetDevices().First());
            
            var apkFilePath = Path.Join(tempDirectory, "app", "build", "outputs", "apk", "debug", "app-debug.apk");
            packageManager.InstallPackage(apkFilePath, reinstall: true);
            _logger.LogInformation($"Reinstalled debug application in directory: {tempDirectory}");
            
            var apkFilePath2 = Path.Join(tempDirectory, "app", "build", "outputs", "apk", "androidTest", "debug", "app-debug-androidTest.apk");
            packageManager.InstallPackage(apkFilePath2, reinstall: true);
            _logger.LogInformation($"Reinstalled androidTest application in directory: {tempDirectory}");
            
            var consoleOutputReceiver = new ConsoleOutputReceiver();
            _logger.LogInformation($"Started testing of Android application for event {request.RequestId}");
            adbClient.ExecuteRemoteCommand("am instrument -r -w com.example.preprofexama.test/androidx.test.runner.AndroidJUnitRunner", adbClient.GetDevices().First(), consoleOutputReceiver);
            _logger.LogInformation($"Completed testing of Android application for event {request.RequestId}");
            var consoleOutput = consoleOutputReceiver.ToString();

            return ParseOutputTestResults(request, consoleOutput);
        }

        private TestApplicationResponse ParseOutputTestResults(TestApplicationRequest request, string consoleOutput)
        {
            var statusRegexp = new Regex("^INSTRUMENTATION_(STATUS|STATUS_CODE):\\s(.*?)(=(.*?))?((?=INSTRUMENTATION)|(?=onError)|$)", RegexOptions.Singleline);
            var resultRegexp = new Regex("^INSTRUMENTATION_(RESULT|CODE):\\s(.*?)(=(.*?))?((?=INSTRUMENTATION)|(?=onError)|$)", RegexOptions.Singleline);
            var onErrorRegexp = new Regex("^onError:\\scommandError=(.*?)\\smessage=(.*?)((?=INSTRUMENTATION)|(?=onError)|$)", RegexOptions.Singleline);
            
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
                        statusResults.Add(match.Groups[2].Value.Trim(), match.Groups[4].Value.Trim());
                    else
                    {
                        statusResults.Add("result_code", match.Groups[2].Value.Trim());
            
                        if (match.Groups[2].Value.Trim() != "1")
                        {
                            statuses.Add(statusResults.ToDictionary(p => p.Key, p => p.Value.Trim()));
                        }
            
                        statusResults.Clear();
                    }
            
                    consoleOutput = consoleOutput.Substring(match.Length).Trim();
                    continue;
                }
            
                match = resultRegexp.Match(consoleOutput);
                if (match.Success)
                {
                    if (match.Groups[1].Value.Trim() == "RESULT")
                        resultResults.Add(match.Groups[2].Value.Trim(), match.Groups[4].Value.Trim());
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
            
                if (string.IsNullOrWhiteSpace(consoleOutput))
                {
                    break;
                }
            
                _logger.LogCritical($"Unknown unparsed data for event {request.RequestId}: {consoleOutput}");
                break;
            }

            var totalResults = results.First();

            return new TestApplicationResponse
            {
                RequestId = request.RequestId,
                Grade = int.Parse(totalResults["grade"]),
                PassedTests = int.Parse(totalResults["passTests"]),
                TotalTests = int.Parse(totalResults["totalTests"]),
                ResultOutput = totalResults["stream"]
            };
        }

        private AdbClient CreateAdbClient()
        {
            _logger.LogInformation($"Connecting to ADB server at {_configuration["Adb:Host"]}:5037");
            var adbClient = new AdbClient(new DnsEndPoint(_configuration["Adb:Host"], 5037), point => new AdbSocket(point));
            _logger.LogInformation($"Successfully connected to ADB server at {_configuration["Adb:Host"]}:5037");
            return adbClient;
        }

        private async Task ExecuteGradleTaskAsync(string tempDirectory, string taskName)
        {
            _logger.LogInformation($"Started gradle task \"{taskName}\" in directory: {tempDirectory}");
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Join(tempDirectory, OperatingSystem.IsWindows() ? "gradlew.bat" : "gradle"),
                    Arguments = taskName,
                    WorkingDirectory = tempDirectory
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            
            _logger.LogInformation($"Completed gradle task \"{taskName}\" in directory: {tempDirectory}");
        }

        private void ExtractTemplateFiles(TestApplicationRequest request, string tempDirectory)
        {
            using var archive = new ZipArchive(request.TemplateZipArchiveFileStream, ZipArchiveMode.Read);
            archive.ExtractToDirectory(tempDirectory, true);
            _logger.LogInformation($"Extracted template files into the directory: {tempDirectory}");
        }

        private void ExtractSubmitFiles(TestApplicationRequest request, string tempDirectory)
        {
            using var archive = new ZipArchive(request.SubmitZipArchiveFileStream, ZipArchiveMode.Read);
            archive.ExtractToDirectory(tempDirectory, true);
            _logger.LogInformation($"Extracted submit files into the directory: {tempDirectory}");
        }

        private static string CreateBuildDirectory(TestApplicationRequest testApplicationRequest)
        {
            var tempDirectory = Path.Join(Path.GetTempPath(), testApplicationRequest.RequestId.ToString());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }
    }

    internal class TestApplicationRequest
    {
        public Guid RequestId { get; set; }
        public FileStream TemplateZipArchiveFileStream { get; set; }
        public FileStream SubmitZipArchiveFileStream { get; set; }
    }

    internal class TestApplicationResponse
    {
        public Guid RequestId { get; set; }
        public int PassedTests { get; set; }
        public int TotalTests { get; set; }
        public int Grade { get; set; }
        public string ResultOutput { get; set; }
    }
}