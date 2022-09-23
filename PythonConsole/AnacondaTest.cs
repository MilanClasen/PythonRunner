using PythonRunner;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PythonConsole
{
    internal class AnacondaTest
    {
        public static void RunTest()
        {
            string pathScript = @"Scripts\testNumpy.py";

            var ph = new PythonHelper()
            {
                RunMode = RunMode.WaitForExit
            };

            ph.RunConsoleCommand(pathScript, null);
            


        }
    }
}
