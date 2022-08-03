using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PythonRunner
{
    /// <summary>
    /// Python execution mode. If WaitForExit will wait until Python process finishes. If ProgressEvents will call event delegates whenever a print statement is issued by the Python script. 
    /// </summary>
    public enum RunMode
    {
        WaitForExit,
        ProgressEvents
    }
}
