using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SpotCrate.Helpers;
using static Fluents.Fluent;

namespace SpotCrate.Services;

public class YtDlpService(ILogger<YtDlpService> logger, GlobalConfiguration configuration)
{
    private const string BeforeRunPolicy = "before-run";
    private const string NeverPolicy = "never";
    private const string PipExecutable = "/env/bin/pip";
    private const string YtDlpExecutable = "/env/bin/yt-dlp";
    private static readonly Regex FixedVersionRegex = new("^[0-9A-Za-z][0-9A-Za-z._-]*$", RegexOptions.Compiled);

    public async Task EnsureReadyForRun(CancellationToken cancellationToken)
    {
        var policy = ParsePolicy(configuration.YT_DLP_UPDATE_POLICY);

        logger.LogInformation("yt-dlp update policy for this run: {policy}", policy.RawValue);

        var initialVersion = await TryGetInstalledVersion(cancellationToken);
        if (initialVersion is null)
        {
            logger.LogWarning("Could not determine the current yt-dlp version before applying the update policy.");
        }
        else
        {
            logger.LogInformation("Current yt-dlp version before applying the update policy: {version}", initialVersion);
        }

        switch (policy.Kind)
        {
            case YtDlpUpdatePolicyKind.Never:
                logger.LogInformation("Skipping yt-dlp update because the policy is set to \"{policy}\".", NeverPolicy);
                break;

            case YtDlpUpdatePolicyKind.BeforeRun:
                await TryInstallLatest(cancellationToken);
                break;

            case YtDlpUpdatePolicyKind.FixedVersion:
                if (string.Equals(initialVersion, policy.FixedVersion, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("yt-dlp {version} is already installed. Skipping update for this run.", policy.FixedVersion);
                    break;
                }

                await TryInstallFixedVersion(policy.FixedVersion!, cancellationToken);
                break;

            default:
                logger.LogWarning(
                    "Ignoring invalid YT_DLP_UPDATE_POLICY value \"{policy}\". Supported values are \"{beforeRun}\", \"{never}\", or a fixed yt-dlp version.",
                    policy.RawValue, BeforeRunPolicy, NeverPolicy);
                break;
        }

        var finalVersion = await TryGetInstalledVersion(cancellationToken);
        if (finalVersion is null)
        {
            logger.LogWarning("Could not determine the final yt-dlp version for this run.");
            return;
        }

        logger.LogInformation("yt-dlp version selected for this run: {version}", finalVersion);
    }

    private async Task<string?> TryGetInstalledVersion(CancellationToken cancellationToken)
    {
        try
        {
            var result = await RunProcess(YtDlpExecutable, "--version", TimeSpan.FromSeconds(15), cancellationToken);
            if (result.TimedOut)
            {
                logger.LogWarning("Timed out while checking the installed yt-dlp version.");
                return null;
            }

            if (result.ExitCode != 0)
            {
                LogCommandOutput(result, "yt-dlp --version", failed: true);
                logger.LogWarning("Could not determine the installed yt-dlp version because the version command exited with code {exitCode}.", result.ExitCode);
                return null;
            }

            LogCommandOutput(result, "yt-dlp --version", failed: false);

            return SplitLines(result.StandardOutput).FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not determine the installed yt-dlp version.");
            return null;
        }
    }

    private async Task TryInstallLatest(CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating yt-dlp to the latest available version before starting downloads.");

        try
        {
            var result = await RunProcess(PipExecutable, "install --no-cache-dir --disable-pip-version-check -U yt-dlp", TimeSpan.FromMinutes(2), cancellationToken);
            if (result.TimedOut)
            {
                logger.LogWarning("Timed out while updating yt-dlp. Continuing with the currently installed version.");
                return;
            }

            if (result.ExitCode != 0)
            {
                LogCommandOutput(result, "pip install -U yt-dlp", failed: true);
                logger.LogWarning("Failed to update yt-dlp to the latest version. Continuing with the currently installed version.");
                return;
            }

            LogCommandOutput(result, "pip install -U yt-dlp", failed: false);
            logger.LogInformation("yt-dlp update completed successfully.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update yt-dlp to the latest version. Continuing with the currently installed version.");
        }
    }

    private async Task TryInstallFixedVersion(string version, CancellationToken cancellationToken)
    {
        logger.LogInformation("Installing yt-dlp version {version} before starting downloads.", version);

        try
        {
            var result = await RunProcess(PipExecutable, $"install --no-cache-dir --disable-pip-version-check yt-dlp=={version}", TimeSpan.FromMinutes(2), cancellationToken);
            if (result.TimedOut)
            {
                logger.LogWarning("Timed out while installing yt-dlp version {version}. Continuing with the currently installed version.", version);
                return;
            }

            if (result.ExitCode != 0)
            {
                LogCommandOutput(result, $"pip install yt-dlp=={version}", failed: true);
                logger.LogWarning("Failed to install yt-dlp version {version}. Continuing with the currently installed version.", version);
                return;
            }

            LogCommandOutput(result, $"pip install yt-dlp=={version}", failed: false);
            logger.LogInformation("yt-dlp version {version} installed successfully.", version);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to install yt-dlp version {version}. Continuing with the currently installed version.", version);
        }
    }

    private static YtDlpUpdatePolicy ParsePolicy(string? rawPolicy)
    {
        var value = string.IsNullOrWhiteSpace(rawPolicy)
            ? BeforeRunPolicy
            : rawPolicy.Trim();

        if (value.Equals(BeforeRunPolicy, StringComparison.OrdinalIgnoreCase))
        {
            return new(YtDlpUpdatePolicyKind.BeforeRun, BeforeRunPolicy);
        }

        if (value.Equals(NeverPolicy, StringComparison.OrdinalIgnoreCase))
        {
            return new(YtDlpUpdatePolicyKind.Never, NeverPolicy);
        }

        if (FixedVersionRegex.IsMatch(value))
        {
            return new(YtDlpUpdatePolicyKind.FixedVersion, value, value);
        }

        return new(YtDlpUpdatePolicyKind.Invalid, value);
    }

    private async Task<ProcessExecutionResult> RunProcess(string fileName, string arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            WorkingDirectory = "/app",
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"Failed to start process \"{fileName}\".");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Try(() =>
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }).Ignore().Execute();

            await process.WaitForExitAsync(CancellationToken.None);

            return new(-1, await standardOutputTask, await standardErrorTask, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Try(() =>
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }).Ignore().Execute();

            throw;
        }

        return new(process.ExitCode, await standardOutputTask, await standardErrorTask, false);
    }

    private void LogCommandOutput(ProcessExecutionResult result, string commandDescription, bool failed)
    {
        var outputLines = SplitLines(result.StandardOutput).ToList();
        var errorLines = SplitLines(result.StandardError).ToList();

        foreach (var line in outputLines)
        {
            logger.LogDebug("{command}: {output}", commandDescription, line);
        }

        foreach (var line in errorLines)
        {
            if (failed)
            {
                logger.LogWarning("{command}: {error}", commandDescription, line);
            }
            else
            {
                logger.LogDebug("{command}: {error}", commandDescription, line);
            }
        }
    }

    private static IEnumerable<string> SplitLines(string value)
    {
        return value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private enum YtDlpUpdatePolicyKind
    {
        BeforeRun,
        Never,
        FixedVersion,
        Invalid
    }

    private sealed record YtDlpUpdatePolicy(YtDlpUpdatePolicyKind Kind, string RawValue, string? FixedVersion = null);

    private sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);
}
