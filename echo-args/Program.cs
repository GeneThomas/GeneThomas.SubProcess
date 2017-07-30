using System;
using System.IO;
using System.Text;
using System.Threading;

// used to test class SubProcess
// writes args to stdout separated by newline
// see --help section for details

namespace echo_args
{
    class Program
    {
        static int Main(string[] args)
        {
            // do no trust a command line args parser
            
            try {
                TextWriter writer = Console.Out;
                string option = "";
                foreach (string arg in args)
                {
                    if (option == "" && arg.Length > 0 && arg[0] == '-')
                    {
                        if (arg == "--error")
                        {
                            // --error ERROR-TEXT
                            option = arg;
                            writer = Console.Error;
                            continue;
                        }
                        else if (arg == "--code-units")
                        {
                            option = arg;
                            continue;
                        }
                        else if (arg == "--exit")
                        {
                            // --exit EXIT-CODE
                            option = arg;
                            continue;
                        }
                        else if (arg == "--wait")
                        {
                            // --wait seconds
                            option = arg;
                            continue;
                        }
                        else if (arg == "--env")
                        {
                            // --env VARIABLE
                            option = arg;
                            continue;
                        }
                        else if (arg == "--pwd")
                        {
                            // wait forever, never exiting
                            Console.WriteLine(Directory.GetCurrentDirectory());
                            continue;
                        }
                        else  if (arg == "--utf8")
                        {
                            Console.OutputEncoding = new UTF8Encoding(false);
                            // changes the code page
                            // which stays changed after exit
                            writer = Console.Out;
                            // Console.Out changed
                            Console.InputEncoding = new UTF8Encoding(false);
                            continue;
                        }
                        else if (arg == "--output-encoding")
                        {
                            // wait forever, never exiting
                            Console.WriteLine(Console.OutputEncoding.WebName);
                            continue;
                        }
                        else if (arg == "--input")
                        {
                            string line = Console.In.ReadLine();
                            if (line == null)
                                throw new Exception("Tried to read input for --input but there is no input line");
                            Console.WriteLine(line.ToUpper());
                            continue;
                        }
                        else if (arg == "--input-code-units")
                        {
                            string line = Console.In.ReadLine();
                            if (line == null)
                                throw new Exception("Tried to read input for --input-code-units but there is no input line");
                            Console.WriteLine(ToCodeUnits(line));
                            continue;
                        }
                        else if (arg == "--close")
                        {
                            // close stdout and stderr
                            Console.Out.Close();
                            Console.Error.Close();
                            continue;
                        }
                        else if (arg == "--help")
                        {
                            Console.WriteLine("echo-args [--options] [TEXT...]");
                            Console.WriteLine("");
                            Console.WriteLine("  TEXT                 print the text to stdout");
                            Console.WriteLine("  --error ERROR-TEXT   write ERROR-TEXT to stderror");
                            Console.WriteLine("  --exit EXIT-CODE     exit with the given exit code");
                            Console.WriteLine("  --wait SECONDS       sleep for the given number of seconds (floating point)");
                            Console.WriteLine("  --pwd                print the current directory");
                            Console.WriteLine("  --env ENV-VAR        print the value of the given environment variable");
                            Console.WriteLine("  --output-encoding    print the Conole.OutputEncoding");
                            Console.WriteLine("  --close              close stdout and stderror");
                            Console.WriteLine("  --input              read a line of input and print the line in upper case");
                            Console.WriteLine("  --input-code-units   read a line of input and print the UTF-16 code units");
                            Console.WriteLine("  --code-units TEXT    print the hex values for each UTF-16 code unit");
                            Console.WriteLine("  --utf8               set the Conole.OutputEncoding to UTF-8");
                            Console.WriteLine("");
                            Console.WriteLine("Any output is followed by a newline");
                            return 0;
                        }
                        else
                        {
                            throw new Exception("Invalid option " + arg);
                        }
                    }
                    else if (option == "--exit")
                    {
                        int rc = int.Parse(arg);
                        return rc;
                    }
                    else if (option == "--wait")
                    {
                        int ms = (int)(double.Parse(arg) * 1000);
                        Thread.Sleep(ms);
                        option = "";
                        continue;
                    }
                    else if (option == "--env")
                    {
                        string value = Environment.GetEnvironmentVariable(arg);
                        if (value == null)
                            throw new Exception("No such environment variable '" + arg + "'");
                        Console.WriteLine(value);
                        option = "";
                        continue;
                    }
                    else if (option == "--code-units")
                    {
                        Console.WriteLine(ToCodeUnits(arg));
                        option = "";
                        continue;
                    }
                    writer.WriteLine(arg);
                    option = "";
                    writer = Console.Out;
                }
                return 0;

            } catch (Exception e)
            {
                Console.Error.WriteLine("echo-args: Failed: " + e.Message);
                return 1;
            }
        }

        /// <summary>
        /// Returns string of UTF-16 code units in hex separated by spaces
        /// </summary>
        /// <param name="str">String</param>
        /// <returns></returns>
        private static string ToCodeUnits(string str)
        {
            string r = "";
            string sep = "";
            foreach (char c in str)
            {
                r += String.Format("{0}{1:X}", sep, (int)c);
                sep = " ";
            }
            return r;
        }
    }
}
