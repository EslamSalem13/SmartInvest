using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace SmartInvest.Launcher;

internal static class Program
{
    private const int BackendHttpsPort = 7250;
    private const int BackendHttpPort = 5187;
    private const int FrontendPort = 4200;
    private const string FrontendUrl = "http://localhost:4200";

    private static readonly object ConsoleLock = new();
    private static readonly object ProcessLock = new();
    private static readonly List<Process> StartedProcesses = [];
    private static CancellationTokenSource? _shutdown;
    private static bool _isStopping;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "SmartInvest Launcher";

        try
        {
            var projectRoot = FindProjectRoot();
            var prerequisites = ValidatePrerequisites(projectRoot);

            WriteBanner(projectRoot);

            if (args.Contains("--check", StringComparer.OrdinalIgnoreCase))
            {
                await PrintEnvironmentCheckAsync(prerequisites);
                return 0;
            }

            _shutdown = new CancellationTokenSource();
            RegisterShutdownHandlers();

            EnsurePortIsFree(BackendHttpsPort, "Backend HTTPS");
            EnsurePortIsFree(BackendHttpPort, "Backend HTTP");
            EnsurePortIsFree(FrontendPort, "Frontend");

            var backend = StartBackend(prerequisites);
            var frontend = StartFrontend(prerequisites);

            Log("LAUNCHER", "Waiting for the Backend and Frontend to become ready...");
            await Task.WhenAll(
                WaitForPortAsync("Backend", BackendHttpsPort, backend, _shutdown.Token),
                WaitForPortAsync("Frontend", FrontendPort, frontend, _shutdown.Token));

            Log("LAUNCHER", $"SmartInvest is ready at {FrontendUrl}");
            Log("LAUNCHER", "Press Ctrl+C or close this window to stop both services.");

            if (!args.Contains("--no-browser", StringComparer.OrdinalIgnoreCase))
            {
                OpenBrowser(FrontendUrl);
            }

            await WaitForShutdownOrUnexpectedExitAsync(_shutdown.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception exception)
        {
            Log("ERROR", exception.Message, ConsoleColor.Red);
            Log("LAUNCHER", "Press any key to close.");

            if (!Console.IsInputRedirected)
            {
                Console.ReadKey(intercept: true);
            }

            return 1;
        }
        finally
        {
            StopStartedProcesses();
            _shutdown?.Dispose();
        }
    }

    private static LauncherPrerequisites ValidatePrerequisites(string projectRoot)
    {
        var apiProject = Path.Combine(
            projectRoot,
            "Backend",
            "src",
            "SmartInvest.API",
            "SmartInvest.API.csproj");
        var frontendDirectory = Path.Combine(projectRoot, "Frontend");
        var packageJson = Path.Combine(frontendDirectory, "package.json");
        var angularCli = Path.Combine(frontendDirectory, "node_modules", ".bin", "ng.cmd");

        if (!File.Exists(apiProject) || !File.Exists(packageJson))
        {
            throw new InvalidOperationException(
                "The SmartInvest Backend or Frontend could not be found. " +
                "Place SmartInvest.Launcher.exe in the repository root.");
        }

        var dotnetPath = FindExecutableOnPath("dotnet.exe")
                         ?? throw new InvalidOperationException(
                             ".NET SDK was not found. Install the .NET 10 SDK and try again.");
        var npmPath = FindExecutableOnPath("npm.cmd")
                      ?? throw new InvalidOperationException(
                          "npm was not found. Install Node.js and try again.");

        if (!File.Exists(angularCli))
        {
            throw new InvalidOperationException(
                "Frontend dependencies are missing. Run 'npm install' inside the Frontend folder first.");
        }

        return new LauncherPrerequisites(
            projectRoot,
            apiProject,
            Path.GetDirectoryName(apiProject)!,
            frontendDirectory,
            dotnetPath,
            npmPath);
    }

    private static string FindProjectRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("SMARTINVEST_ROOT");
        var startingDirectories = new[]
        {
            configuredRoot,
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var start in startingDirectories.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start!));

            while (directory is not null)
            {
                var apiProject = Path.Combine(
                    directory.FullName,
                    "Backend",
                    "src",
                    "SmartInvest.API",
                    "SmartInvest.API.csproj");
                var packageJson = Path.Combine(directory.FullName, "Frontend", "package.json");

                if (File.Exists(apiProject) && File.Exists(packageJson))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException(
            "Could not locate the SmartInvest repository. " +
            "Place the launcher in the repository root or set SMARTINVEST_ROOT.");
    }

    private static Process StartBackend(LauncherPrerequisites prerequisites)
    {
        var startInfo = CreateBaseProcessStartInfo(
            prerequisites.DotnetPath,
            prerequisites.ApiDirectory);
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(prerequisites.ApiProject);
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] =
            $"https://localhost:{BackendHttpsPort};http://localhost:{BackendHttpPort}";

        return StartManagedProcess("BACKEND", startInfo);
    }

    private static Process StartFrontend(LauncherPrerequisites prerequisites)
    {
        var commandProcessor = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        var startInfo = CreateBaseProcessStartInfo(
            commandProcessor,
            prerequisites.FrontendDirectory);
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/s");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add(
            $"npm.cmd start -- --host localhost --port {FrontendPort}");

        return StartManagedProcess("FRONTEND", startInfo);
    }

    private static ProcessStartInfo CreateBaseProcessStartInfo(
        string fileName,
        string workingDirectory)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }

    private static Process StartManagedProcess(string label, ProcessStartInfo startInfo)
    {
        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                Log(label, eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                Log(label, eventArgs.Data, ConsoleColor.DarkYellow);
            }
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Failed to start {label}.");
        }

        lock (ProcessLock)
        {
            StartedProcesses.Add(process);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        Log("LAUNCHER", $"Started {label} (PID {process.Id}).");
        return process;
    }

    private static async Task WaitForPortAsync(
        string serviceName,
        int port,
        Process process,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"{serviceName} stopped before it became ready (exit code {process.ExitCode}).");
            }

            if (await IsPortOpenAsync(port, cancellationToken))
            {
                Log("LAUNCHER", $"{serviceName} is ready on port {port}.");
                return;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException(
            $"{serviceName} did not become ready on port {port} within 90 seconds.");
    }

    private static async Task<bool> IsPortOpenAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(400));
            await client.ConnectAsync("localhost", port, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static void EnsurePortIsFree(int port, string serviceName)
    {
        if (IsPortOpenAsync(port).GetAwaiter().GetResult())
        {
            throw new InvalidOperationException(
                $"{serviceName} port {port} is already in use. " +
                "Stop the existing process, then run the launcher again.");
        }
    }

    private static async Task WaitForShutdownOrUnexpectedExitAsync(
        CancellationToken cancellationToken)
    {
        Process[] processes;

        lock (ProcessLock)
        {
            processes = [.. StartedProcesses];
        }

        var shutdownTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        var exitTasks = processes.Select(process => process.WaitForExitAsync()).ToArray();
        var completedTask = await Task.WhenAny(exitTasks.Append(shutdownTask));

        if (completedTask != shutdownTask && !cancellationToken.IsCancellationRequested)
        {
            var exitedProcess = processes.First(process => process.HasExited);
            throw new InvalidOperationException(
                $"A SmartInvest service stopped unexpectedly (PID {exitedProcess.Id}, " +
                $"exit code {exitedProcess.ExitCode}).");
        }

        await shutdownTask;
    }

    private static void RegisterShutdownHandlers()
    {
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Log("LAUNCHER", "Stopping SmartInvest...");
            _shutdown?.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) => StopStartedProcesses();
    }

    private static void StopStartedProcesses()
    {
        lock (ProcessLock)
        {
            if (_isStopping)
            {
                return;
            }

            _isStopping = true;

            foreach (var process in StartedProcesses)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5_000);
                    }
                }
                catch
                {
                    // Best effort during console/window shutdown.
                }
                finally
                {
                    process.Dispose();
                }
            }

            StartedProcesses.Clear();
        }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            Log("LAUNCHER", $"Could not open the browser automatically: {exception.Message}");
        }
    }

    private static async Task PrintEnvironmentCheckAsync(
        LauncherPrerequisites prerequisites)
    {
        Log("CHECK", $".NET: {prerequisites.DotnetPath}");
        Log("CHECK", $"npm: {prerequisites.NpmPath}");
        Log("CHECK", "Frontend dependencies: found");
        Log("CHECK", $"Backend HTTPS port {BackendHttpsPort}: " +
                     (await IsPortOpenAsync(BackendHttpsPort) ? "in use" : "available"));
        Log("CHECK", $"Backend HTTP port {BackendHttpPort}: " +
                     (await IsPortOpenAsync(BackendHttpPort) ? "in use" : "available"));
        Log("CHECK", $"Frontend port {FrontendPort}: " +
                     (await IsPortOpenAsync(FrontendPort) ? "in use" : "available"));
        Log("CHECK", "Launcher prerequisites are valid.", ConsoleColor.Green);
    }

    private static string? FindExecutableOnPath(string executableName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var pathEntry in pathValue.Split(Path.PathSeparator))
        {
            var directory = pathEntry.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var candidate = Path.Combine(directory, executableName);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void WriteBanner(string projectRoot)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("========================================");
        Console.WriteLine("        SmartInvest Launcher");
        Console.WriteLine("========================================");
        Console.ResetColor();
        Log("LAUNCHER", $"Project: {projectRoot}");
    }

    private static void Log(
        string label,
        string message,
        ConsoleColor? color = null)
    {
        lock (ConsoleLock)
        {
            var previousColor = Console.ForegroundColor;

            if (color.HasValue)
            {
                Console.ForegroundColor = color.Value;
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{label}] {message}");
            Console.ForegroundColor = previousColor;
        }
    }

    private sealed record LauncherPrerequisites(
        string ProjectRoot,
        string ApiProject,
        string ApiDirectory,
        string FrontendDirectory,
        string DotnetPath,
        string NpmPath);
}
