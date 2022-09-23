using PythonRunner;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PythonConsole
{
    internal class BasicTests
    {
        public static void RunTests()
        {


            var ph = new PythonHelper();

            ph.RunMode = RunMode.WaitForExit;
            ph.OnScriptPrint = OnPrint;
            ph.OnScriptError = OnError;

            void OnError(object sender, DataReceivedEventArgs e)
            {
                Console.WriteLine(e.Data);
            }

            void OnPrint(object sender, DataReceivedEventArgs e)
            {
                Console.WriteLine(e.Data);
            }

            ph.RunConsoleCommand(@"Scripts/scikit_test.py");

            if (ph.RunMode == RunMode.WaitForExit)
            {
                foreach (var s in ph.OutputLog)
                {
                    Console.WriteLine(s);
                }
            }


            Console.WriteLine("Done");
            Console.ReadKey();
        }
    }
}
