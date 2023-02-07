using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AppsTester.Checker.Android.Results;
using AppsTester.Shared.SubmissionChecker;
using AppsTester.Shared.SubmissionChecker.Events;
using Microsoft.Extensions.Logging;

namespace AppsTester.Checker.Android.Instrumentations
{
    internal interface IInstrumentationsOutputParser
    {
        SubmissionCheckResultEvent Parse(string consoleOutput);
    }
    
    internal class InstrumentationsOutputParser : IInstrumentationsOutputParser
    {
        private readonly ISubmissionProcessingLogger _logger;

        public InstrumentationsOutputParser(ISubmissionProcessingLogger logger)
        {
            _logger = logger;
        }

        public SubmissionCheckResultEvent Parse(string consoleOutput)
        {
            var statusRegexp =
                new Regex("^INSTRUMENTATION_(STATUS|STATUS_CODE):\\s(.*?)(=(.*?))?((?=\\sINSTRUMENTATION)|(?=\\sonError)|$)",
                    RegexOptions.Singleline);
            var resultRegexp =
                new Regex("^INSTRUMENTATION_(RESULT|CODE):\\s(.*?)(=(.*?))?((?=\\sINSTRUMENTATION)|(?=\\sonError)|$)",
                    RegexOptions.Singleline);
            var onErrorRegexp =
                new Regex("^onError:\\scommandError=(.*?)\\smessage=(.*?)((?=\\sINSTRUMENTATION)|(?=\\sonError)|$)",
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
                        statusResults.Add("result_code", match.Groups[2].Value.Split('\n').First()
                            .Trim());

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

                _logger.LogCritical("Unknown unparsed data: {consoleOutput}", consoleOutput);
                break;
            }

            if (!results.Any() || errors.Any())
            {
                return new SubmissionCheckResultEvent()
                    .WithResult(
                        new CompilationErrorResult
                        (
                            CompilationError: consoleOutput + Environment.NewLine +
                                         string.Join(Environment.NewLine, errors.Select(e => e["message"]))
                        ));
            }

            var totalResults = results.First();

            return new SubmissionCheckResultEvent()
                .WithResult(
                    new CheckResult
                    (
                        Grade: int.Parse(totalResults.GetValueOrDefault("grade", "0")),
                        TotalGrade: int.Parse(totalResults.GetValueOrDefault("maxGrade", "0")),
                        ResultCode: CheckResultCode.Success,
                        TestResults: statuses
                            .Where(s => s["id"] == "AndroidJUnitRunner")
                            .Select(s => new CheckTestResult
                            (
                                Class: s["class"],
                                Test: s["test"],
                                ResultCode: (CheckTestResultCode)int.Parse(s["result_code"]),
                                Stream: s["stream"]
                            ))
                            .ToList()
                    ));
        }
    }
}