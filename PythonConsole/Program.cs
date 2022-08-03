// See https://aka.ms/new-console-template for more information

using PythonRunner;
using System.Diagnostics;

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

if(ph.RunMode == RunMode.WaitForExit)
{
    foreach (var s in ph.OutputLog)
    {
        Console.WriteLine(s);
    }
}


Console.WriteLine("Done");
Console.ReadKey();