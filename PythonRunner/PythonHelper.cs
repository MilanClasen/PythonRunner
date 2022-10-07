using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PythonRunner
{
    public class PythonHelper
    {
        private string _pythonPath;
        private bool _hasConsole = false;

        public RunMode RunMode { get; set; } = RunMode.WaitForExit;
        
        public DataReceivedEventHandler OnScriptPrint { get; set; }
        public DataReceivedEventHandler OnScriptError { get; set; }
        public EventHandler OnExit { get; set; }

        public List<string> OutputLog { get; private set; }
        public List<string> ErrorLog { get; private set; }

        public PythonHelper()
        {
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

        #region RegistrySearch

        private static string? FindPython()
        {
            string pythonPath = SearchInRegistry(Registry.CurrentUser);

            if (pythonPath == null || pythonPath == "")
                pythonPath = SearchInRegistry(Registry.LocalMachine);

            if (pythonPath == null || pythonPath == "")
                throw new Exception("Python installation not found!");

            return pythonPath;
        }

        private static string SearchInRegistry(RegistryKey startKey)
        {
            string key32 = @"SOFTWARE\Python";
            string key64 = @"SOFTWARE\Wow6432Node\Python";

            RegistryKey? pythonKey = null;
            foreach (var key in new List<string>() { key32, key64 })
            {
                RegistryKey? pythonCore = startKey.OpenSubKey(key);

                if (pythonCore == null)
                    continue;

                pythonKey = SearchPythonPathRecursive(pythonCore);

                if (pythonKey != null)
                    break;
            }



            if (pythonKey == null)
                return "";
            string pythonExePath = pythonKey.OpenSubKey("InstallPath").GetValue("ExecutablePath").ToString();

            return pythonExePath;

        }

        private static RegistryKey SearchPythonPathRecursive(RegistryKey Key)
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
                            return newKey;
                        }
                    }
                }

                RegistryKey nextKey = SearchPythonPathRecursive(newKey);
                if (nextKey != null)
                    return nextKey;
            }

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

            string command = script + " ";
            if (arguments != null && arguments.Length > 0)
            {
                command += string.Join(" ", arguments);
            }

            return RunConsoleCommand(command);
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
                return false;
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

                if(oldVars == null)
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
                        if(currentVars.Exists(a => a == v) == false)
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

            proc.Start();
            proc.WaitForExit();

            try
            {
                while(proc.StandardOutput.EndOfStream == false)
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

        public bool TryInstallPackages(string requirementsPath)
        {
            bool success = RunConsoleCommand($"-m pip install -r \"{requirementsPath}\"");
            return success ;
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

            if(path != null)
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