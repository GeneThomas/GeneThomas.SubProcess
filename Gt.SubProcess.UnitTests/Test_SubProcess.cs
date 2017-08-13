using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

using System.Diagnostics;

namespace GeneThomas.SubProcess.UnitTests
{
    [TestFixture]
    public static class Test_SubProcess
    {
        const string echoArgs = "Gt.SubProcess/echo-args.exe";
        // echo-args [--error ERROR-TEXT] [--exit EXIT-CODE] [--wait SECONDS] [--pwd] [TEXT...]

        static string _(string str)
        {
            // translate LF to CRLF on Windows
            if (Environment.OSVersion.IsWindows())
                return str.Replace("\n", "\r\n");
            else
                return str;
        }

        [Test]
        public static void Test_SubProcess_calls()
        {
            // params versions
            SubProcess.Call(echoArgs, "1", "2", "3");
            SubProcess.CheckCall(echoArgs, "4", "5");
            SubProcess.CheckOutput(echoArgs, "A", "B", "C").With(_("A\nB\nC\n"));

            // IEnumerable<string> versions
            List<string> args = new List<string> { echoArgs, "10", "20", "30" };
            SubProcess.Call(args);
            args = new List<string> { echoArgs, "40", "50" };
            SubProcess.CheckCall(args);
            args = new List<string> { echoArgs, "ABBA", "BBQ", "CQ" };
            SubProcess.CheckOutput(args).With(_("ABBA\nBBQ\nCQ\n"));

            SubProcess s = new SubProcess(echoArgs, "an arg", "another arg", "--error", "some error");
            s.Wait();
            Assert.AreEqual(_("an arg\nanother arg\n"), s.OutputString);
            Assert.AreEqual(_("some error\n"), s.ErrorString);

            args = new List<string> { echoArgs, "an arg", "another arg", "--error", "some error" };
            s = new SubProcess(args);
            s.Check();
            Assert.AreEqual(_("an arg\nanother arg\n"), s.OutputString);
            Assert.AreEqual(_("some error\n"), s.ErrorString);

            s = new SubProcess(echoArgs, "hello");
            int rc = s.Wait();
            Assert.AreEqual(0, rc);

            s = new SubProcess(echoArgs, "hello", "--exit", "3");
            rc = s.Wait();
            Assert.AreEqual(3, rc);

            // captured stderr on failure
            args = new List<string> { echoArgs, "hello", "--error", "FAILED", "--exit", "1" };
            s = new SubProcess(args);
            SubProcess.Failed failed = Assert.Throws<SubProcess.Failed>(() =>
            {
                s.Check();
            });
            Assert.AreEqual(_("FAILED\n"), failed.ErrorOutput);
            Assert.AreEqual(1, failed.ExitCode);
            Assert.IsTrue(failed.Message.Contains("FAILED"));

            // did not capture stderr on failyre
            args = new List<string> { echoArgs, "hello", "--error", "FAILED", "--exit", "1" };
            s = new SubProcess(args)
            {
                Error = SubProcess.Through
            };
            failed = Assert.Throws<SubProcess.Failed>(() =>
            {
                s.Check();
            });
            Assert.AreEqual(1, failed.ExitCode);

            // Out tests

            s = new SubProcess(echoArgs, "helllo world")
            {
                Out = SubProcess.Swallow
            };
            s.Wait();

            s = new SubProcess(echoArgs, "helllo world")
            {
                Out = SubProcess.Through
            };
            s.Wait();

            string filename = Path.Combine(Path.GetTempPath(), "Test_SubProcess-" + Path.GetRandomFileName() + ".tmp");
            FileStream fs = new FileStream(filename, FileMode.CreateNew);
            s = new SubProcess(echoArgs, "hello world", "--error", "something has", "--error", "failed")
            {
                Out = fs
            };
            s.Wait();
            fs.Close();
            string fileContents = ReadFile(filename);
            Assert.AreEqual(_("hello world\n"), fileContents);
            File.Delete(filename);

            // Error tests

            s = new SubProcess(echoArgs, "hello world")
            {
                Error = SubProcess.Swallow
            };
            s.Wait();

            s = new SubProcess(echoArgs, "hello world")
            {
                Error = SubProcess.Through
            };
            s.Wait();

            s = new SubProcess(echoArgs, "hello world", "--error", "error message")
            {
                Error = SubProcess.ToOut
            };
            s.Wait();
            Assert.AreEqual(_("hello world\nerror message\n"), s.OutputString);

            s = new SubProcess(echoArgs, "hello world", "--error", "error message");
            s.Wait();
            Assert.AreEqual(_("hello world\n"), s.OutputString);
            Assert.AreEqual(_("error message\n"), s.ErrorString);

            filename = Path.Combine(Path.GetTempPath(), "Test_SubProcess-" + Path.GetRandomFileName() + ".tmp");
            fs = new FileStream(filename, FileMode.CreateNew);
            s = new SubProcess(echoArgs, "hello world", "--error", "something has", "--error", "failed")
            {
                Error = fs
            };
            s.Wait();
            fs.Close();
            fileContents = ReadFile(filename);
            Assert.AreEqual(_("something has\nfailed\n"), fileContents);
            File.Delete(filename);

            // redirect both to files
            filename = Path.Combine(Path.GetTempPath(), "Test_SubProcess-" + Path.GetRandomFileName() + ".tmp");
            string filenameError = Path.Combine(Path.GetTempPath(), "Test_SubProcess-error-" + Path.GetRandomFileName() + ".tmp");
            fs = new FileStream(filename, FileMode.CreateNew);
            FileStream fsError = new FileStream(filenameError, FileMode.CreateNew);
            s = new SubProcess(echoArgs, "hello world", "--error", "something has", "--error", "failed")
            {
                Out = fs,
                Error = fsError
            };
            s.Wait();
            fs.Close();
            fsError.Close();
            fileContents = ReadFile(filename);
            Assert.AreEqual(_("hello world\n"), fileContents);
            string fileContentsError = ReadFile(filenameError);
            Assert.AreEqual(_("something has\nfailed\n"), fileContentsError);
            File.Delete(filename);
            File.Delete(filenameError);
        }

        static string ReadFile(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                StreamReader sr = new StreamReader(fs);
                StringBuilder buffer = new StringBuilder();
                char[] block = new char[1024];
                for (;;)
                {
                    int chars = sr.ReadBlock(block, 0, block.Length);
                    buffer.Append(block, 0, chars);
                    if (chars < block.Length)
                        break;
                }
                return buffer.ToString();
            }
        }

        [Test]
        public static void Test_SubProcess_Commandline_Forming()
        {
            SubProcess.CheckOutput(echoArgs, "A", "B", "C").With(_("A\nB\nC\n"));
            SubProcess.CheckOutput(echoArgs, "A B", "C").With(_("A B\nC\n"));
            SubProcess.CheckOutput(echoArgs, "A\"B", "C").With(_("A\"B\nC\n"));

            SubProcess.CheckOutput(echoArgs, "A \\B", "C").With(_("A \\B\nC\n"));
            SubProcess.CheckOutput(echoArgs, "A \\\\B", "C").With(_("A \\\\B\nC\n"));
            SubProcess.CheckOutput(echoArgs, "A \\\\\"B", "C").With(_("A \\\\\"B\nC\n"));
            SubProcess.CheckOutput(echoArgs, "A \"B", "C").With(_("A \"B\nC\n"));

            SubProcess.CheckOutput(echoArgs, "A\\B", "C").With(_("A\\B\nC\n"));
            SubProcess.CheckOutput(echoArgs, "A\\\\B", "C").With(_("A\\\\B\nC\n"));
            SubProcess.CheckOutput(echoArgs, "A\\\\\"B", "C").With(_("A\\\\\"B\nC\n"));
            SubProcess.CheckOutput(echoArgs, "A\"B", "C").With(_("A\"B\nC\n"));

            SubProcess.CheckOutput(echoArgs, "").With(_("\n"));
            SubProcess.CheckOutput(echoArgs, "A\"B", "C").With(_("A\"B\nC\n"));
            SubProcess.CheckOutput(echoArgs, "A\\\"B", "C").With(_("A\\\"B\nC\n"));
            SubProcess.CheckOutput(echoArgs, "A\\\\\"B", "C").With(_("A\\\\\"B\nC\n"));
            SubProcess.CheckOutput(echoArgs, "A\\\\\\\"B", "C").With(_("A\\\\\\\"B\nC\n"));
        }

        [Test]
        public static void Test_SubProcess_IDisposable_alreadyExited()
        {
            SubProcess sp;
            using (sp = new SubProcess(echoArgs, "A", "B"))
            {
                sp.Start();
                while (sp.IsAlive)
                    Thread.Sleep(100);
            }
            Assert.IsTrue(sp.HasExited);
        }

        [Test]
        public static void Test_SubProcess_IDisposable_kills()
        {
            SubProcess sp;
            using (sp = new SubProcess(echoArgs, "--wait"))
            {
                sp.Start();
                Assert.IsTrue(sp.IsAlive);
            }
            sp.Wait();
            Assert.IsFalse(sp.IsAlive);
        }

        [Test]
        public static void Test_SubProcess_multiple_Waits()
        {
            SubProcess sp = new SubProcess(echoArgs, "A");
            sp.Wait();
            Assert.AreEqual(0, sp.Wait());
        }

        [Test]
        public static void Test_SubProcess_directory()
        {
            string cwd = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory("..");
            string parentDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(cwd);
            Assert.AreEqual(cwd, Directory.GetCurrentDirectory());

            SubProcess sp = new SubProcess(echoArgs, "--pwd")
            {
                Directory = parentDir
            };
            sp.Wait();
            Assert.AreEqual(sp.OutputString, _(parentDir + "\n"));
        }

        [Test]
        public static void Test_SubProcess_no_timeout()
        {
            SubProcess sp = new SubProcess(echoArgs, "a")
            {
                Timeout = 99999
            };
            sp.Wait();
        }

        [Test]
        public static void Test_SubProcess_timeout()
        {
            using (SubProcess sp = new SubProcess(echoArgs, "--wait", "100")
            {
                Timeout = 0.1
            })
            {
                Assert.Throws<SubProcess.TimeoutException>(() => sp.Wait());
            }
        }

        [Test]
        public static void Test_SubProcess_timeout_close()
        {
            // although echo-args closes stdout and stderr the read stdout and stderr
            // tasks do not complete before the timeout, as with non --close case
            using (SubProcess sp = new SubProcess(echoArgs, "--close", "--wait", "100")
            {
                Timeout = 0.1
            })
            {
                Assert.Throws<SubProcess.TimeoutException>(() => sp.Wait());
            }
        }

        [Test]
        public static void Test_SubProcess_no_timeout_async()
        {
            SubProcess sp = new SubProcess(echoArgs, "a")
            {
                Timeout = 99999
            };
            Task<int> task = sp.WaitAsync();
            task.Wait();
        }

        [Test]
        public static void Test_SubProcess_timeout_async()
        {
            using (SubProcess sp = new SubProcess(echoArgs, "--wait", "100")
            {
                Timeout = 0.1
            })
            {
                try
                {
                    sp.WaitAsync().Wait();
                    Assert.Fail("Did not throw on timeout");
                }
                catch (AggregateException ae)
                {
                    Assert.AreEqual(1, ae.InnerExceptions.Count);
                    Assert.IsTrue(ae.InnerExceptions[0] is SubProcess.TimeoutException);
                }
            }
        }

        [Test]
        public static void Test_SubProcess_async()
        {
            SubProcess sp = new SubProcess(echoArgs, "--exit", "2");
            Task<int> task = sp.WaitAsync();
            task.Wait();
            Assert.AreEqual(2, task.Result);
        }

        [Test]
        public static void Test_SubProcess_check_async()
        {
            SubProcess sp = new SubProcess(echoArgs, "--exit", "2");
            Task task = sp.CheckAsync();
            try
            {
                task.Wait();
                Assert.Fail("Did not throw on timeout");
            }
            catch (AggregateException ae)
            {
                Assert.AreEqual(1, ae.InnerExceptions.Count);
                Assert.IsTrue(ae.InnerExceptions[0] is SubProcess.Failed);
            }
        }

        [Test]
        public static void Test_SubProcess_async_output_redirect()
        {
            MemoryStream ms = new MemoryStream();
            SubProcess sp = new SubProcess(echoArgs, "Hello", "--exit", "2")
            {
                Out = ms
            };
            Task<int> task = sp.WaitAsync();
            task.Wait();
            Assert.AreEqual(2, task.Result);
            byte[] bytes = ms.GetBuffer();
            ms = new MemoryStream(bytes);
            StreamReader tr = new StreamReader(ms);
            Assert.AreEqual("Hello", tr.ReadLine());
        }

        [Test]
        public static void Test_SubProcess_invaid_input()
        {
            Assert.Throws<ArgumentException>(() => new SubProcess(echoArgs) { In = SubProcess.ToOut });
            Assert.Throws<ArgumentException>(() => new SubProcess(echoArgs) { In = SubProcess.Capture });
            new SubProcess(echoArgs) { In = SubProcess.Swallow }.Wait();
            new SubProcess(echoArgs) { In = SubProcess.Through }.Wait();
            new SubProcess(echoArgs) { In = SubProcess.Pipe }.Wait();
        }

        [Test]
        public static void Test_SubProcess_invaid_out()
        {
            Assert.Throws<ArgumentException>(() => new SubProcess(echoArgs) { Out = SubProcess.ToOut });
            new SubProcess(echoArgs) { Out = SubProcess.Pipe }.Wait();
            new SubProcess(echoArgs) { Out = SubProcess.Capture }.Wait();
            new SubProcess(echoArgs) { Out = SubProcess.Through }.Wait();
            new SubProcess(echoArgs) { Out = SubProcess.Swallow }.Wait();
        }

        [Test]
        public static void Test_SubProcess_invaid_error()
        {
            new SubProcess(echoArgs) { Error = SubProcess.ToOut }.Wait();
            new SubProcess(echoArgs) { Error = SubProcess.Pipe }.Wait();
            new SubProcess(echoArgs) { Error = SubProcess.Capture }.Wait();
            new SubProcess(echoArgs) { Error = SubProcess.Through }.Wait();
            new SubProcess(echoArgs) { Error = SubProcess.Swallow }.Wait();
        }

        [Test]
        public static void Test_SubProcess_no_tasks()
        {
            // internally no Tasks for redirecting stdout and stderr

            SubProcess sp = new SubProcess(echoArgs, "hello")
            {
                Out = SubProcess.Through,
                Error = SubProcess.Through
            };
            sp.Wait();

            // timeout version
            sp = new SubProcess(echoArgs, "hello")
            {
                Out = SubProcess.Through,
                Error = SubProcess.Through,
                Timeout = 99999
            };
            sp.Wait();

            // wait for a while in the task
            sp = new SubProcess(echoArgs, "hello", "--wait", "0.1")
            {
                Out = SubProcess.Through,
                Error = SubProcess.Through
            };
            sp.Wait();
        }

        class DeleteFile : IDisposable
        {
            // remember to delete the give file

            string Filename;
            public DeleteFile(string filename)
            {
                Filename = filename;
            }
            public void Dispose()
            {
                if (File.Exists(Filename))
                {
                    try
                    {
                        File.Delete(Filename);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
        }

        [Test]
        public static void Test_SubProcess_input_file()
        {
            string line = "a line of input";

            string filename = Path.Combine(Path.GetTempPath(), "Test_SubProcess-" + Path.GetRandomFileName() + ".tmp");
            Stream fs = new FileStream(filename, FileMode.CreateNew);
            StreamWriter sw = new StreamWriter(fs);
            for (int i = 0; i < 20; ++i)
                sw.WriteLine(line);
            sw.Close();
            fs.Close();
            fs = new FileStream(filename, FileMode.Open);
            byte[] buffer = new byte[17];
            fs.Read(buffer, 0, 17);
            SubProcess sp = new SubProcess(echoArgs, "--input")
            {
                In = fs
            };
            sp.Wait();
            Assert.AreEqual(_(line.ToUpper() + "\n"), sp.OutputString);
            fs.Close();
            File.Delete(filename);
        }

        [Test]
        public static void Test_SubProcess_input_memoryStream()
        {
            string line = "a line of input";
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms)
            {
                AutoFlush = true
            };

            sw.WriteLine(line);
            byte[] bytes = ms.GetBuffer();
            long len = ms.Length;
            ms = new MemoryStream(bytes, 0, (int)len);

            SubProcess sp = new SubProcess(echoArgs, "--input")
            {
                In = ms
            };
            sp.Wait();
            Assert.AreEqual(_(line.ToUpper() + "\n"), sp.OutputString);
        }

        [Test]
        public static void Test_SubProcess_input_pipe_write()
        {
            string line = "some input";

            SubProcess sp = new SubProcess(echoArgs, "--input")
            {
                In = SubProcess.Pipe
            };
            sp.Start();
            sp.Write(line + "\n");
            sp.Wait();
            Assert.AreEqual(_(line.ToUpper() + "\n"), sp.OutputString);
        }

        [Test]
        public static void Test_SubProcess_input_pipe_writeline()
        {
            string line = "some input";

            SubProcess sp = new SubProcess(echoArgs, "--input")
            {
                In = SubProcess.Pipe
            };
            sp.Start();
            sp.WriteLine(line);
            sp.Wait();
            Assert.AreEqual(_(line.ToUpper() + "\n"), sp.OutputString);
        }

        [Test]
        public static void Test_SubProcess_input_pipe_writeBinary()
        {
            string line = "some input";
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms);

            sw.WriteLine(line);
            sw.Flush();
            byte[] bytes = ms.GetBuffer();
            SubProcess sp = new SubProcess(echoArgs, "--input")
            {
                In = SubProcess.Pipe
            };
            sp.Start();
            sp.Write(bytes, 0, (int)ms.Length);
            sp.Wait();
            Assert.AreEqual(_(line.ToUpper() + "\n"), sp.OutputString);
        }

        [Test]
        public static void Test_process_input()
        {
            string dir = Directory.GetCurrentDirectory();
            Process p = new Process();
            p.StartInfo.FileName = echoArgs;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.Arguments = "--input --wait 0";
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.StandardInput.WriteLine("test line");
            p.WaitForExit();
            // works: TEST LINE
        }

        [Test]
        public static void Test_process_inputBinary()
        {
            string line = "some input";
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms);
            sw.WriteLine(line);
            sw.Flush();
            byte[] bytes = ms.GetBuffer();

            Process p = new Process();
            p.StartInfo.FileName = echoArgs;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.Arguments = "--input --wait 0";
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.StandardInput.BaseStream.Write(bytes, 0, (int)ms.Length);
            p.StandardInput.BaseStream.Flush();
            p.WaitForExit();
            // works: SOME INPUT
        }

        [Test]
        public static void Test_SubProcess_outputPipe_OutputReader()
        {
            using (SubProcess sp = new SubProcess(echoArgs, "--input", "--input", "--input")
            {
                Out = SubProcess.Pipe
            })
            {
                // OutputReader
                sp.Start();
                string line = "hello subprocess";
                sp.WriteLine(line);
                Assert.AreEqual(line.ToUpper(), sp.OutputReader.ReadLine());

                string line2 = "some more text";
                sp.WriteLine(line2);
                Assert.AreEqual(line2.ToUpper(), sp.OutputReader.ReadLine());
            }
        }

        [Test]
        public static void Test_SubProcess_outputPipe_OutputStream()
        {
            using (SubProcess sp = new SubProcess(echoArgs, "--input", "--input", "--input")
            {
                Out = SubProcess.Pipe
            })
            {
                sp.Start();

                // OutputStream
                string line = "talking to subprocess";
                sp.WriteLine(line);
                MemoryStream ms = new MemoryStream();
                StreamWriter sw = new StreamWriter(ms);
                sw.WriteLine(line.ToUpper());
                sw.Flush();
                byte[] bytesRaw = ms.GetBuffer();
                byte[] bytes = new byte[ms.Length];
                Array.Copy(bytesRaw, bytes, ms.Length);
                byte[] buffer = new byte[ms.Length];
                sp.OutputStream.Read(buffer, 0, (int)ms.Length);
                ms = new MemoryStream(buffer);
                string str = new StreamReader(ms).ReadToEnd();
                Assert.IsTrue(bytes.SequenceEqual(buffer));

                string line2 = "more interaction";
                sp.WriteLine(line2);
                ms = new MemoryStream();
                sw = new StreamWriter(ms);
                sw.WriteLine(line2.ToUpper());
                sw.Flush();
                bytesRaw = ms.GetBuffer();
                bytes = new byte[ms.Length];
                Array.Copy(bytesRaw, bytes, ms.Length);
                buffer = new byte[ms.Length];
                sp.OutputStream.Read(buffer, 0, (int)ms.Length);
                Assert.IsTrue(bytes.SequenceEqual(buffer));
            }
        }

        [Test]
        public static void Test_SubProcess_errorPipe_ErrorReader()
        {
            string line = "a line";
            string line2 = "another line";
            using (SubProcess sp = new SubProcess(echoArgs, "--error", line, "--error", line2)
            {
                Error = SubProcess.Pipe
            })
            {
                // OutputReader
                sp.Start();
                Assert.AreEqual(line, sp.ErrorReader.ReadLine());
                Assert.AreEqual(line2, sp.ErrorReader.ReadLine());
            }
        }

        [Test]
        public static void Test_SubProcess_errorPipe_ErrorStream()
        {
            string line = "some text";
            string line2 = "some more text";
            using (SubProcess sp = new SubProcess(echoArgs, "--error", line, "--error", line2)
            {
                Error = SubProcess.Pipe
            })
            {
                sp.Start();

                // OutputStream
                MemoryStream ms = new MemoryStream();
                StreamWriter sw = new StreamWriter(ms);
                sw.WriteLine(line);
                sw.Flush();
                byte[] bytesRaw = ms.GetBuffer();
                byte[] bytes = new byte[ms.Length];
                Array.Copy(bytesRaw, bytes, ms.Length);
                byte[] buffer = new byte[ms.Length];
                sp.ErrorStream.Read(buffer, 0, (int)ms.Length);
                Assert.IsTrue(bytes.SequenceEqual(buffer));

                ms = new MemoryStream();
                sw = new StreamWriter(ms);
                sw.WriteLine(line2);
                sw.Flush();
                bytesRaw = ms.GetBuffer();
                bytes = new byte[ms.Length];
                Array.Copy(bytesRaw, bytes, ms.Length);
                buffer = new byte[ms.Length];
                sp.ErrorStream.Read(buffer, 0, (int)ms.Length);
                Assert.IsTrue(bytes.SequenceEqual(buffer));
            }
        }

        [Test]
        public static void Test_SubProcess_utf8()
        {
            // output
            string ourEncWebName = Console.OutputEncoding.WebName;
            string waterTrebbleClef = "水𝄞";
            SubProcess sp = new SubProcess(echoArgs, "--utf8", waterTrebbleClef);
            sp.Wait();
            Assert.AreEqual(_(waterTrebbleClef + "\n"), sp.OutputString);
            Encoding ourEnc = Console.OutputEncoding;
            Assert.AreEqual(ourEnc.WebName, ourEncWebName);
            // has not changed to UTF-8
            Assert.AreNotEqual("utf-8", ourEnc.WebName);

            // error
            sp = new SubProcess(echoArgs, "--utf8", "--error", waterTrebbleClef);
            sp.Wait();
            Assert.AreEqual(_(waterTrebbleClef + "\n"), sp.ErrorString);

            // input
            //sp = new SubProcess(echoArgs, "--utf8", "--input-code-units", "--wait", "9999")
            sp = new SubProcess(echoArgs, "--utf8", "--input");
            sp.Start();
            sp.WriteLine(waterTrebbleClef);
            sp.Wait();
            Assert.AreEqual(_(waterTrebbleClef + "\n"), sp.OutputString);
        }

        [Test]
        public static void Test_SubProcess_environment()
        {
            string var = "MYVAR";
            Assert.AreEqual(null, Environment.GetEnvironmentVariable(var));
            string value = "VALUE";
            SubProcess sp = new SubProcess(echoArgs, "--env", var)
            {
                Environment =
                {
                    [var] = value
                }
            };
            sp.Check();
            Assert.AreEqual(_(value + "\n"), sp.OutputString);

            // unicode
            var = "MYVAR_水𝄞";
            Assert.AreEqual(null, Environment.GetEnvironmentVariable(var));
            value = "VALUE-水𝄞";
            sp = new SubProcess(echoArgs, "--utf8", "--env", var)
            {
                Environment =
                {
                    [var] = value
                }
            };
            sp.Check();
            Assert.AreEqual(_(value + "\n"), sp.OutputString);

            // existing variable
            var = "HOMEPATH";
            Assert.AreNotEqual(null, Environment.GetEnvironmentVariable(var));
            value = "VALUE-水𝄞";
            sp = new SubProcess(echoArgs, "--utf8", "--env", var)
            {
                Environment =
                {
                    [var] = value
                }
            };
            sp.Check();
            Assert.AreEqual(_(value + "\n"), sp.OutputString);
        }

        [Test]
        public static void Test_SubProcess_nonUtf8()
        {
            Process p = new Process
            {
                StartInfo =
                {
                    FileName = echoArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                }
            };
            p.Start();
            Encoding enc = p.StandardOutput.CurrentEncoding;
            // Windows-1252
            // but SubProcess is actually IBM-850 (capitalises ä to å (in windows-1252) which 
            // is õ to Õ in IBM-850)

            enc = Encoding.GetEncoding(850);

            string line = "a line ä";
            string line2 = "a line Ë";
            SubProcess sp = new SubProcess(echoArgs, "--input", "--error", line2)
            {
                OutEncoding = enc,
                InEncoding = enc,
                ErrorEncoding = enc
            };
            sp.Start();
            sp.WriteLine(line);
            sp.Wait();
            Assert.AreEqual(_(line.ToUpper() + "\n"), sp.OutputString);
            Assert.AreEqual(_(line2 + "\n"), sp.ErrorString);

        }
    }
}
