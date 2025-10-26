using CheckForUpdates.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PythonRunner
{
    public class PythonHelper
    {
        private string? _pythonPath;
        private bool _hasConsole = false;

        public RunMode RunMode { get; set; } = RunMode.WaitForExit;

        public DataReceivedEventHandler OnScriptPrint { get; set; }
        public DataReceivedEventHandler OnScriptError { get; set; }
        public EventHandler OnExit { get; set; }

        public List<string> OutputLog { get; private set; }
        public List<string> ErrorLog { get; private set; }

        public PythonHelper()
        {
            CheckAppVersion(GetAppVersion());

            _pythonPath = FindPython();

            OutputLog = new List<string>();
            ErrorLog = new List<string>();

            try
            {
                _ = Console.GetCursorPosition();
                _hasConsole = true;
            }
            catch
            {
                _hasConsole = false;
            }
        }

        private string GetAppVersion()
        {
            try
            {
                string assemblyString = "ScoutPostProcessing";
                Assembly assembly = Assembly.Load(assemblyString);
                Version version = assembly.GetName().Version;
                return version.Major + "." + version.Minor + "." + version.Build;
            }
            catch (Exception)
            {
                return "";
            }
        }
        private void CheckAppVersion(string current_version)
        {
            ReadRelease info = new ReadRelease(current_version, "", "");

            if (info.HasNewRelease())
                throw new Exception("deprecated_version");

            List<CheckForUpdates.Model.ReleaseInfo> hists = info.history_releases(current_version);
            if (hists.Count > 0)
            {
                CheckForUpdates.Model.ReleaseInfo hist = hists[0];
                if (!hist.version.Equals(current_version))
                    throw new Exception("deprecated_version");
            }
            else
                throw new Exception("deprecated_version");
        }

        #region RegistrySearch

        private static string? FindPython()
        {
            string pythonPath = SearchInRegistry(Registry.CurrentUser);

            if (pythonPath == null || pythonPath == "")
                pythonPath = SearchInRegistry(Registry.LocalMachine);

            if (pythonPath == null || pythonPath == "")
                pythonPath = SearchInRegistry(Registry.Users);

            //if (pythonPath == null || pythonPath == "")
            //    pythonPath = SearchInRegistry(Registry.LocalMachine);

            if (pythonPath == null || pythonPath == "")
            {
                Console.WriteLine($"ERROR: Python installation is not found.");
                throw new Exception("Python installation not found!");
            }

            return pythonPath;
        }

        private static string SearchInRegistry(RegistryKey startKey)
        {
            string key32 = @"SOFTWARE\Python";
            string key64 = @"SOFTWARE\Wow6432Node\Python";

            RegistryKey? pythonKey = null;
            string pythonExePath = null;
            foreach (var key in new List<string>() { key32, key64 })
            {
                RegistryKey? pythonCore = startKey.OpenSubKey(key);

                if (pythonCore == null)
                    continue;

                pythonKey = SearchPythonPathRecursive(pythonCore, out pythonExePath);

                if (pythonKey != null)
                    break;
            }

            if (pythonKey == null)
                return "";
            
            return pythonExePath;

        }

        private static RegistryKey SearchPythonPathRecursive(RegistryKey Key, out string pythonInstallPath)
        {
            var subKeysNames = Key.GetSubKeyNames();

            foreach (var subKeyName in subKeysNames)
            {
                var newKey = Key.OpenSubKey(subKeyName);

                var installPathKey = newKey.OpenSubKey("InstallPath");
                if (installPathKey != null)
                {
                    var value = installPathKey.GetValue("ExecutablePath");
                    
                    if (value != null)
                    {
                        string pythonExePath = value.ToString();

                        if (pythonExePath.EndsWith("python.exe"))
                        {
                            pythonInstallPath = pythonExePath;
                            return newKey;
                        }
                    }

                    try
                    {
                        value = installPathKey.GetValue(null);

                        var folder = new DirectoryInfo(value.ToString());
                        var pythonFile = folder.GetFiles().ToList().Find(a => a.FullName.EndsWith("python.exe"));
                        if (pythonFile != null && pythonFile.Length > 0)
                        {
                            pythonInstallPath = pythonFile.FullName;
                            return newKey;
                        }
                    }
                    catch { }
                }

                RegistryKey nextKey = SearchPythonPathRecursive(newKey, out pythonInstallPath);
                if (nextKey != null)
                    return nextKey;
            }

            pythonInstallPath = null;

            return null;
        }

        #endregion


        public bool RunConsoleCommand(string command)
        {
            ErrorLog = new();
            OutputLog = new();

            switch (RunMode)
            {
                case RunMode.WaitForExit:
                    return RunWaitForExit(command);
                case RunMode.ProgressEvents:
                    return RunProgressEvents(command);
                default:
                    throw new Exception($"Unknown PythonHelper RunMode: {RunMode}.");
            }
        }

        public bool RunConsoleCommand(string script, params string[] arguments)
        {
            // See https://aka.ms/new-console-template for more information

            StringBuilder sb_command = new();
            sb_command.Append(script);
            sb_command.Append(" ");

            if (arguments != null && arguments.Length > 0)
            {
                foreach (var item in arguments)
                {
                    sb_command.Append("\"");
                    sb_command.Append(item);
                    sb_command.Append("\"");
                    sb_command.Append(" ");
                }
            }

            Console.WriteLine("INFO: Calling Python...");
            return RunConsoleCommand(sb_command.ToString());
        }

        private bool RunProgressEvents(string command)
        {
            Process proc = new Process();
            proc.StartInfo.FileName = _pythonPath;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.Arguments = command.Trim();
            proc.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;

            proc.OutputDataReceived += UpdateOutputLog;
            proc.ErrorDataReceived += UpdateErrorLog;

            proc.OutputDataReceived += OnScriptPrint;
            proc.ErrorDataReceived += OnScriptError;
            proc.Exited += OnExit;

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            if (ErrorLog != null && ErrorLog.Count != 0)
            {
                Console.WriteLine("WARNING: " + string.Join("; ", ErrorLog));
                return false;
            }
            else
                return true;
        }

        public bool SetupEnviromentVariable(EnvironmentVariableTarget scope)
        {
            try
            {
                var pythonFolder = Path.GetDirectoryName(_pythonPath);
                var requiredVars = new List<string>() { pythonFolder };

                if (pythonFolder.ToLower().Contains("anaconda"))
                {
                    requiredVars.Add($@"{pythonFolder}\Scripts");
                    requiredVars.Add($@"{pythonFolder}\Library\bin");
                    requiredVars.Add($@"{pythonFolder}\Library\usr\bin");
                    requiredVars.Add($@"{pythonFolder}\Library\mingw-w64\bin");
                }
                else
                {
                    requiredVars.Add($@"{pythonFolder}\Scripts");
                }

                string? oldVars = Environment.GetEnvironmentVariable("Path", scope);

                if (oldVars == null)
                {
                    foreach (var v in requiredVars)
                    {
                        if (_hasConsole)
                            Console.WriteLine($"Adding {v} to Path...");
                    }
                    Environment.SetEnvironmentVariable("Path", string.Join(";", requiredVars), scope);
                }
                else
                {
                    var currentVars = oldVars
                        .Split(';').ToList()
                        .Where(a => String.IsNullOrWhiteSpace(a) == false)
                        .ToList();
                    foreach (var v in requiredVars)
                    {
                        if (currentVars.Exists(a => a == v) == false)
                        {
                            currentVars.Add(v);
                            Console.WriteLine($"Adding {v} to Path...");
                        }
                        else
                        {
                            Console.WriteLine($"Tried adding {v} to Path but it was already there...");
                        }
                    }

                    Environment.SetEnvironmentVariable("Path", string.Join(";", currentVars), scope);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool RunWaitForExit(string command)
        {
            Process proc = new Process();
            proc.StartInfo.FileName = _pythonPath;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.Arguments = command.Trim();
            proc.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;

            proc.Start();
            proc.WaitForExit();

            try
            {
                while (proc.StandardOutput.EndOfStream == false)
                {
                    string? newOutput = proc.StandardOutput.ReadLine();
                    if (!String.IsNullOrWhiteSpace(newOutput))
                    {
                        OutputLog.Add(newOutput);
                    }
                }
                while (proc.StandardError.EndOfStream == false)
                {
                    string? newError = proc.StandardError.ReadLine();
                    if (!String.IsNullOrWhiteSpace(newError))
                    {
                        ErrorLog.Add(newError);
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }


            if (ErrorLog != null && ErrorLog.Count != 0)
                return false;
            else
                return true;
        }

        public bool TryUpdatePip()
        {
            bool success = RunConsoleCommand($"-m pip install --upgrade pip");
            if (!success)
                success = RunConsoleCommand($"-m ensurepip --upgrade");
            return success;
        }

        public bool TryInstallPackages(string requirementsPath)
        {
            bool success = RunConsoleCommand($"-m pip install -r \"{requirementsPath}\"");
            return success;
        }

        private void UpdateErrorLog(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                ErrorLog.Add(e.Data);
            }
        }

        private void UpdateOutputLog(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                OutputLog.Add(e.Data);
            }
        }

        public static bool TestPython(out string message)
        {
            string? path = FindPython();

            if (path != null)
            {
                message = $"Python found at {path}.";
                return true;
            }
            else
            {
                message = $"Could not locate Python.";
                return false;
            }
        }
    }
}