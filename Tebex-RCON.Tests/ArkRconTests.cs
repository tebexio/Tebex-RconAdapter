using System.Diagnostics;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Tebex.Tests;

/// <summary>
/// Launches <see cref="ArkRestartHarness"/> in a separate process and asserts it exits cleanly. A crash of the adapter
/// (unhandled exception on a timer thread) kills the child process and fails the test with its output attached.
/// </summary>
public class ArkRconTests
{
    private readonly ITestOutputHelper _output;

    public ArkRconTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SurvivesArkServerRestart()
    {
        AssertHarnessPasses("restart");
    }

    [Fact]
    public void DeliversPackagesPromptlyWhilePollingPlayerList()
    {
        AssertHarnessPasses("delivery");
    }

    private void AssertHarnessPasses(string scenario)
    {
        var harness = Assembly.GetExecutingAssembly().Location;
        var psi = new ProcessStartInfo("dotnet", $"\"{harness}\" {scenario}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(harness)!,
        };

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit(TimeSpan.FromSeconds(120));
        if (!exited)
        {
            process.Kill(true);
        }
        process.WaitForExit();

        var output = stdout.Result + stderr.Result;
        _output.WriteLine(output);

        Assert.True(exited, "harness timed out\n" + output);
        Assert.DoesNotContain("Unhandled exception", output);
        Assert.Contains("HARNESS PASSED", output);
        Assert.Equal(0, process.ExitCode);
    }
}
