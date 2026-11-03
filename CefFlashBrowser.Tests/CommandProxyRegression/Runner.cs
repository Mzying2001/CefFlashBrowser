using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

internal static class CommandProxyRegression
{
    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int system(string command);

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int _wsystem(string command);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    private delegate bool ConsoleHandler(uint control);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleHandler handler, bool add);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint control, uint group);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XCount, YCount, Fill, Flags;
        public short Show, ReservedSize;
        public IntPtr ReservedData, Input, Output, Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo
    {
        public IntPtr Process, Thread;
        public uint ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string directory,
        ref StartupInfo startup, out ProcessInfo process);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll")]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint code);

    [DllImport("kernel32.dll")]
    private static extern bool TerminateProcess(IntPtr process, uint code);

    private sealed class Result
    {
        public int ExitCode;
        public byte[] Output, Error;
    }

    private static readonly string Fixture = typeof(CommandProxyRegression).Assembly.Location;
    private static readonly string Cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");

    private static ProcessStartInfo StartInfo(string executable, string arguments, string directory, string comspec)
    {
        var info = new ProcessStartInfo(executable, arguments)
        {
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = directory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.EnvironmentVariables["ComSpec"] = comspec;
        info.EnvironmentVariables["COMMAND_PROXY_TEST"] = "inherited value";
        return info;
    }

    private static Result Run(string executable, string arguments, string directory, string comspec, string input = "")
    {
        using (var process = Process.Start(StartInfo(executable, arguments, directory, comspec)))
        using (var output = new MemoryStream())
        using (var error = new MemoryStream())
        {
            Task outputTask = process.StandardOutput.BaseStream.CopyToAsync(output);
            Task errorTask = process.StandardError.BaseStream.CopyToAsync(error);
            process.StandardInput.Write(input);
            process.StandardInput.Close();
            if (!process.WaitForExit(10000))
            {
                process.Kill();
                throw new Exception("Process timed out: " + executable);
            }
            if (!Task.WaitAll(new[] { outputTask, errorTask }, 10000))
                throw new Exception("Inherited output handles did not close.");
            return new Result { ExitCode = process.ExitCode, Output = output.ToArray(), Error = error.ToArray() };
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Compare(string proxy, string directory, string arguments, string input = "")
    {
        Result expected = Run(Cmd, arguments, directory, Cmd, input);
        Result actual = Run(proxy, arguments, directory, proxy, input);
        Require(expected.ExitCode == actual.ExitCode, "Exit code differs: " + arguments);
        Require(expected.Output.SequenceEqual(actual.Output), "stdout differs: " + arguments);
        if (!expected.Error.SequenceEqual(actual.Error))
        {
            File.WriteAllBytes(Path.Combine(directory, "expected-stderr.bin"), expected.Error);
            File.WriteAllBytes(Path.Combine(directory, "actual-stderr.bin"), actual.Error);
        }
        Require(expected.Error.SequenceEqual(actual.Error), "stderr differs: " + arguments);
    }

    private static void TestForwarding(string proxy, string directory)
    {
        foreach (string arguments in new[] { "/c echo NOT SANDBOXED", "/C \"echo NOT SANDBOXED\"", " \t/c\t echo NOT SANDBOXED \t" })
        {
            Result result = Run(proxy, arguments, directory, proxy);
            Require(result.ExitCode == 0 && result.Output.Length == 0 && result.Error.Length == 0,
                "Flash diagnostic was not filtered: " + arguments);
        }

        foreach (string arguments in new[]
        {
            "/c echo hello", "/c echo not sandboxed", "/c echo NOT SANDBOXED extra",
            "/c echo prefix NOT SANDBOXED", "/c echo NOT SANDBOXED & exit /b 7",
            "/c \"echo NOT SANDBOXED & exit /b 7\"", "/d /c echo NOT SANDBOXED",
            "/c echo NOT SANDBOXED | findstr SANDBOXED", "/c echo a^&b",
            "/c echo \"quoted text\"", "/c echo error 1>&2", "/c definitely_missing_command_proxy_test_command",
            "/u /c echo \u4e2d\u6587", "/c echo %COMMAND_PROXY_TEST%", "/c cd", "/c echo %ComSpec%",
            "/v:on /c \"set value=expanded&echo !value!\"", "/s /c \"echo spaces preserved\"",
            "/c for /l %i in (1,1,6000) do @echo 01234567890123456789",
            "/k exit /b 11"
        })
            Compare(proxy, directory, arguments);

        foreach (string code in new[] { "0", "7", "259", "3010", "-1" })
        {
            Result result = Run(proxy, "/c exit /b " + code, directory, proxy);
            Require(result.ExitCode == int.Parse(code), "Exit code was truncated: " + code);
        }
        Compare(proxy, directory, "/c set /p value=&call echo %%value%%", "input from parent\r\n");
        Compare(proxy, directory, "", "echo interactive input\r\nexit /b 9\r\n");

        string redirected = Path.Combine(directory, "redirected.txt");
        Run(proxy, "/c echo NOT SANDBOXED > \"" + redirected + "\"", directory, proxy);
        Require(File.ReadAllText(redirected).Trim() == "NOT SANDBOXED", "Redirection was incorrectly filtered.");
        string batch = Path.Combine(directory, "batch with spaces \u4e2d\u6587.cmd");
        File.WriteAllText(batch, "@echo off\r\necho %~1\r\nexit /b 23\r\n", Encoding.ASCII);
        Compare(proxy, directory, "/c \"\"" + batch + "\" \"argument with spaces\"\"");
        Compare(proxy, directory, "/c \"\"" + Fixture + "\" streams\"");

        // A real CRT caller exercises the unquoted COMSPEC path and CRT handle inheritance.
        foreach (string mode in new[] { "system", "wsystem" })
        {
            if (mode == "system" && Encoding.Default.GetString(Encoding.Default.GetBytes(proxy)) != proxy)
            {
                Console.WriteLine("ANSI system() cannot represent this installation path; _wsystem() still covers it.");
                continue;
            }
            Result filtered = Run(Fixture, mode + " \"echo NOT SANDBOXED\"", directory, proxy);
            Require(filtered.ExitCode == 0 && filtered.Output.Length == 0 && filtered.Error.Length == 0,
                "CRT diagnostic was not filtered: " + mode);
            Result expected = Run(Fixture, mode + " \"echo forwarded & exit /b 17\"", directory, Cmd);
            Result actual = Run(Fixture, mode + " \"echo forwarded & exit /b 17\"", directory, proxy);
            Require(actual.ExitCode == 17 && expected.Output.SequenceEqual(actual.Output) &&
                expected.Error.SequenceEqual(actual.Error), "CRT forwarding differs: " + mode);
        }
        Console.WriteLine("Command forwarding, raw arguments, streams, environment and exit codes passed.");
    }

    private static int Interrupt(string executable, string directory, uint control)
    {
        string ready = Path.Combine(directory, "ready.txt");
        File.Delete(ready);
        var info = new ProcessStartInfo(executable, "/c \"\"" + Fixture + "\" wait \"" + ready + "\"\"")
        {
            UseShellExecute = false,
            WorkingDirectory = directory
        };
        using (var process = Process.Start(info))
        {
            var timer = Stopwatch.StartNew();
            while (!File.Exists(ready) && !process.HasExited && timer.ElapsedMilliseconds < 10000)
                Thread.Sleep(10);
            Require(File.Exists(ready), "Console command did not become ready.");
            Require(GenerateConsoleCtrlEvent(control, 0), "GenerateConsoleCtrlEvent failed.");
            if (!process.WaitForExit(10000))
            {
                process.Kill();
                throw new Exception("Console control event did not finish the command.");
            }
            return process.ExitCode;
        }
    }

    private static void TestConsole(string proxy, string directory)
    {
        Require(GetConsoleWindow() != IntPtr.Zero, "Console fixture has no isolated console.");
        string observed = Path.Combine(directory, "console.txt");
        var info = new ProcessStartInfo(proxy, "/c \"\"" + Fixture + "\" inspect \"" + observed + "\"\"")
        {
            UseShellExecute = false,
            WorkingDirectory = directory
        };
        using (var process = Process.Start(info))
        {
            Require(process.WaitForExit(10000) && process.ExitCode == 0, "Console inspection failed.");
        }
        Require(File.ReadAllText(observed) == GetConsoleWindow().ToInt64().ToString(), "Proxy did not reuse its parent's console.");
        ConsoleHandler handler = delegate(uint control) { return control == 0 || control == 1; };
        Require(SetConsoleCtrlHandler(handler, true), "Cannot install fixture control handler.");
        try
        {
            foreach (uint control in new uint[] { 0, 1 })
            {
                int expected = Interrupt(Cmd, directory, control);
                int actual = Interrupt(proxy, directory, control);
                Require(expected == actual, "Proxy did not preserve the console control exit code: " + control);
            }
        }
        finally
        {
            SetConsoleCtrlHandler(handler, false);
            GC.KeepAlive(handler);
        }
    }

    private static void RunConsoleFixture(string proxy, string directory)
    {
        var startup = new StartupInfo { Size = Marshal.SizeOf(typeof(StartupInfo)), Flags = 1, Show = 0 };
        ProcessInfo native;
        var command = new StringBuilder("\"" + Fixture + "\" console \"" + proxy + "\" \"" + directory + "\"");
        Require(CreateProcess(Fixture, command, IntPtr.Zero, IntPtr.Zero, false, 0x10, IntPtr.Zero,
            directory, ref startup, out native), "Cannot create isolated hidden console fixture.");
        CloseHandle(native.Thread);
        try
        {
            if (WaitForSingleObject(native.Process, 30000) != 0)
            {
                TerminateProcess(native.Process, 1);
                throw new Exception("Console fixture timed out.");
            }
            string failure = Path.Combine(directory, "console-failure.txt");
            uint exitCode;
            Require(GetExitCodeProcess(native.Process, out exitCode) && exitCode == 0,
                File.Exists(failure) ? File.ReadAllText(failure) : "Console fixture failed.");
        }
        finally
        {
            CloseHandle(native.Process);
        }
        Console.WriteLine("Parent console reuse, Ctrl+C and Ctrl+Break exit codes passed.");
    }

    public static int Main(string[] args)
    {
        try
        {
            switch (args[0])
            {
                case "system": return system(args[1]);
                case "wsystem": return _wsystem(args[1]);
                case "inspect": File.WriteAllText(args[1], GetConsoleWindow().ToInt64().ToString()); return 0;
                case "wait": File.WriteAllText(args[1], "ready"); Thread.Sleep(30000); return 0;
                case "streams":
                    byte[] bytes = Enumerable.Range(0, 131072).Select(i => (byte)i).ToArray();
                    Console.OpenStandardOutput().Write(bytes, 0, bytes.Length);
                    Console.OpenStandardError().Write(bytes, 0, bytes.Length);
                    return 37;
                case "console": TestConsole(args[1], args[2]); return 0;
                case "test": TestForwarding(args[1], args[2]); RunConsoleFixture(args[1], args[2]); return 0;
                default: throw new Exception("Unknown fixture mode.");
            }
        }
        catch (Exception error)
        {
            if (args.Length > 2 && args[0] == "console")
                File.WriteAllText(Path.Combine(args[2], "console-failure.txt"), error.ToString());
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
