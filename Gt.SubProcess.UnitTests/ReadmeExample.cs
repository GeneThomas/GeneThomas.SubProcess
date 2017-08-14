
using System;
using System.IO;
using Gt.SubProcess;

namespace ReadmeExample
{
    class Program
    {
        static int Main(string[] args) { 
            try {
                SubProcess.CheckCall("ls", "-l");
                    // throws if exits with non 0 exit code

                SubProcess p = new SubProcess("ssh", "me@mysite.com")
                {
                    Out = new FileStream("ssh-output.txt", FileMode.OpenOrCreate),
                    Error = SubProcess.Capture,
                    In = SubProcess.Pipe                    
                };
                p.Wait();

                Console.WriteLine(p.ErrorString);

                return 0; 
            } catch (Exception e) { 
                Console.Error.WriteLine("Fatal Error: " + e.Message); 
                return 1; 
            }
        } 
    }
}