using System;

namespace AppsTester.Checker.Android;

public class SubmissionsOptions
{
    public TimeSpan? TestTimeout { get; set; }
    public TimeSpan? InstallTimeout { get; set; }
}