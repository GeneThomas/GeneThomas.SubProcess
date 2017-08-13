
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System;

using System.Diagnostics;

namespace Gt.SubProcess
{
    // exit code on Windows is DWORD/UINT (32 bit unsigned)
    // exit code on Unix is unsigned 8 bits (of an int)
    //    see WEXITSTATUS(status) in man 2 exit
    //    sh -c exit 256; echo $? # gives 0
    //    EXIT_FAILURE on Linux is defined as 1

    // command line arguments are passed as UTF-16 ok

    // a normal .NET app will see code page 850
    // and write ? rather than unicode BMP or UTF-16
    // it can change the Console.OutputEncoding 
    // but this changes the code page which persists
    // after the application has exited

    // if applcations do not make theselves utf-8
    // we can have to guess the encoding

    /// <summary>
    /// run a sub process
    /// </summary>
    public class SubProcess: IDisposable
    {   
        // - stdin and stdout can be binary
        // - UTF-8 is the default charset
        // - support Async methods

        public SubProcess(params string[] args):
            this(args as IEnumerable<string>)
        {
        }

        public SubProcess(IEnumerable<string> args)
        {
            Args.AddRange(args);
        }

        private Stream _in = Pipe;
        /// <summary>
        /// Set how the subprocess's input is handled
        /// 
        /// In = new FileStream("input.txt", FileMode.Open);
        ///      a stream
        /// In = SubProcess.Through
        /// In = SubProcess.Swallow
        ///      no input
        /// In = SubProcess.Pipe
        ///      the default
        ///      allows Write()/WriteLine() of string or byte[]s
        /// </summary>
        public Stream In
        {
            get
            {
                return _in;
            }
            set
            {
                if (value is _SentinalStream)
                {
                    if (value == Capture)
                        throw new ArgumentException("SubProcess.In can not be Capture");
                    if (value == ToOut)
                        throw new ArgumentException("SubProcess.In can not be ToOut");
                }
                _in = value;
            }
        }

        /// <summary>
        /// Set the encoding of input
        /// Defaults to UTF-8 without BOM
        /// Set if null in Start()
        /// </summary>
        public Encoding InEncoding = null;

        private Stream _out = Capture;
        /// <summary>
        /// Set how the subprocess's output is handled:
        /// 
        /// Out = new FileStream("output.txt", FileMode.OpenOrCreate);
        ///       a stream
        /// Out = SubProcess.Through
        /// Out = SubProcess.Capture
        ///       the default
        ///       to string
        ///       string output = subprocess.OutputString
        /// Out = SubProcess.Swallow
        ///       throw away
        /// Out = SubProcess.Pipe
        ///       read incremenally
        ///       Stream stream = subprocess.OutputStream
        ///       TextReader reader = subprocess.OutputReader
        /// </summary>
        public Stream Out
        {
            get
            {
                return _out;
            }
            set
            {
                if (value is _SentinalStream)
                {
                    if (value == ToOut)
                        throw new ArgumentException("SubProcess.Out can not be ToOut");
                }
                _out = value;
            }
        }

        /// <summary>
        /// Set the encoding of stdout
        /// Defaults to UTF-8
        /// </summary>
        /// Set if null in Start()
        public Encoding OutEncoding = null;

        private Stream _error = Capture;
        /// <summary>
        /// Set how the subprocess's error output is handled
        /// Error = new FileStream("error.txt", FileMode.OpenOrCreate);
        ///       a stream
        /// Error = SubProcess.Through
        /// Error = SubProcess.Capture
        ///       the default
        ///       to string
        ///       string errorMessage = subprocess.ErrorString
        /// Error = SubProcess.Swallow
        ///       throw away
        /// Error = SubProcess.ToOut
        ///     redirect Error to Out
        /// Error = SubProcess.Pipe
        ///       read incremenally
        ///       Stream stream = subprocess.ErrorStream
        ///       TextReader reader = subprocess.ErrorReader
        /// </summary>
        public Stream Error
        {
            get
            {
                return _error;
            }
            set
            {
                _error = value;
            }
        }

        /// <summary>
        /// Set the encoding of stderror
        /// Defaults to UTF-8
        /// Set if null in Start()
        /// </summary>
        public Encoding ErrorEncoding = null;
                
        /// <summary>
        /// Run via the operating shell
        /// Use this with care
        /// In, Out and Error can not be redirected if using this
        /// So set all to Through (not the defaults)
        /// Nor can Environment variables be set.
        /// </summary>
        public bool UseShell = false;

        /// <summary>
        /// The directory to start in
        /// Do not use with UseShell
        /// </summary>
        public string Directory = "";

        private IDictionary<string, string> _environment = null;
        /// <summary>
        /// Environment variables to set for the sub process
        /// There is no way to remove variables (due to a 
        /// deficiency in System.Diagnostics.Process)
        /// e.g.
        /// new SubProcess("my-app") {
        ///     Environment = {
        ///         ["NAME"] = "VALUE"
        ///      }   
        /// };
        /// </summary>
        public IDictionary<string, string> Environment
        {
            get
            {
                if (_environment == null)
                    _environment = new Dictionary<string, string>();
                return _environment;
            }
        }

        /// <summary>
        /// Timeout after given number of seconds
        /// </summary                    
        public double Timeout = 0.0;
        // 0.0 or less means no timeout

        public bool HasTimeout
        {
            get
            {
                return Timeout > 0.0;
            }
        }

        public bool ShowWindow = false;

        // ================================================================================

        /// <summary>
        /// The arguments including the program name
        /// </summary>
        private Arguments Args = new Arguments();

        /// <summary>
        /// null if Out not Captured
        /// </summary>
        private MemoryStream OutCapture;

        /// <summary>
        /// null if Error not Captured 
        /// </summary>
        private MemoryStream ErrorCapture;

        /// <summary>
        /// created on demand
        /// uses InEncoging encoding
        /// </summary>
        private TextWriter InWriter = null;

        /// <summary>
        /// read stdout and stderr and
        /// write stdin
        /// initialised in Start()
        /// </summary>
        private List<Task> Tasks = new List<Task>();
            

        private Process Process;
        // null until Start()ed, see HasStarted

        /// <summary>
        /// not to be used
        /// facilitates constants for initialisation of Out, Error and In
        /// </summary>
        public class _SentinalStream : Stream
        {            
            private _SentinalStream()
            {
                // can not be constructed
            }

            public static readonly _SentinalStream Through = new _SentinalStream();
            public static readonly _SentinalStream Swallow = new _SentinalStream();
            public static readonly _SentinalStream Capture = new _SentinalStream();
            public static readonly _SentinalStream Pipe = new _SentinalStream();
            public static readonly _SentinalStream ToOut = new _SentinalStream();

            public override bool CanRead
            {
                get
                {
                    throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
                }
            }
            public override bool CanSeek
            {
                get
                {
                    throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
                }
            }
            public override bool CanWrite
            {
                get
                {
                    throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
                }
            }
            public override long Length
            {
                get
                {
                    throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
                }
            }
            public override long Position
            {
                get
                {
                    throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
                }
                set
                {
                    throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
                }
            }
            public override void Flush()
            {
                throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
            }
        }
        public static readonly _SentinalStream Swallow = _SentinalStream.Swallow;
        public static readonly _SentinalStream Through = _SentinalStream.Through;
        public static readonly _SentinalStream Capture = _SentinalStream.Capture;
        public static readonly _SentinalStream Pipe = _SentinalStream.Pipe;
        public static readonly _SentinalStream ToOut = _SentinalStream.ToOut;

	///<summary>
	///	
	///
        public bool HasExited
        {
            get
            {
                if (!HasStarted)
                    return false;
                return Process.HasExited;
            }
        }

	///<summary>
	///Returns true of the sb-process is sill runnning.
	///</summary>
        public bool IsAlive
        {
            get
            {
                if (!HasStarted)
                    return false;
                return !Process.HasExited;
            }
        }

	///<summary>
	///The number that the sub-process exited with.Traditionally 0 means a good exit, and non-ero means a problem occured.
	///</summar>
        public int ExitCode
        {
            get
            {
                if (!HasStarted)
                    throw new InvalidOperationException("Attempt to get SubProcess.ExitCode before process run");
                return Process.ExitCode;
            }
        }

        public string ExecutableName
        {
            get
            {
                if (Args.Count == 0)
                    return "<no executable name>";
                return Args[0];
            }
        }

	/// <summary>
        /// Returns all stdout that has been captured
        /// </summary>
	/// Out must be to SubProcess.Capture before Start()ing.
        /// Encoding set in OutEncoding, defaults to UTF-8
        public string OutputString
        {
            get
            {
                if (!HasStarted)
                    throw new InvalidOperationException("Attempt to read output (SubProcess.OutputString) of process but process has not started");
                if (OutCapture == null)
                    throw new InvalidOperationException("Attempt to read output (SubProcess.OutputString) but std output not captured (Out=SubProcess.Capture)");
                OutCapture.Seek(0, SeekOrigin.Begin);
                return new StreamReader(OutCapture, OutEncoding).ReadToEnd();
            }
        }
        
        /// <summary>
        /// Get the Stream for stdout
        /// </summary>
	/// Out must be to SubProcess.Pipe before Start()ing.
        /// Encoding set in OutEncoding, defaults to UTF-8
        public Stream OutputStream
        {
            get
            {
                if (!HasStarted)
                    throw new InvalidOperationException("Attempt to get output stream (SubProcess.OutputStream) of process but process has not started");
                if (Out != Pipe)
                    throw new InvalidOperationException("Attempt to get output stream (SubProcess.OutputStream) but std output not piped (Out=SubProcess.Pipe)");
                return Process.StandardOutput.BaseStream;
            }
        }
        
        /// <summary>
        /// Get the TextReader for stdout
        /// </summary>
	/// Out must be to SubProcess.Pipe before Start()ing.
        /// Encoding set in OutEncoding, defaults to UTF-8
        public TextReader OutputReader
        {
            get
            {
                if (!HasStarted)
                    throw new InvalidOperationException("Attempt to get output stream (SubProcess.OutputReader) of process but process has not started");
                if (Out != Pipe)
                    throw new InvalidOperationException("Attempt to get output stream (SubProcess.OutputReader) but std output not piped (Out=SubProcess.Pipe)");
                return Process.StandardOutput;
            }
        }

        public string ErrorString
        {
            get
            {
                if (!HasStarted)
                    throw new InvalidOperationException("Attempt to read error output (SubProcess.ErrorString) of process but process has not started");
                if (ErrorCapture == null)
                    throw new InvalidOperationException("Attempt to read error output (SubProcess.ErrorString) but std error not captured");
                ErrorCapture.Seek(0, SeekOrigin.Begin);
                return new StreamReader(ErrorCapture, ErrorEncoding).ReadToEnd();
            }
        }

        public Stream ErrorStream
        {
            get
            {
                if (!HasStarted)
                    throw new InvalidOperationException("Attempt to get error stream (SubProcess.ErrorStream) of process but process has not started");
                if (Error != Pipe)
                    throw new InvalidOperationException("Attempt to get error stream (SubProcess.ErrorStream) but std error not piped (Error=SubProcess.Pipe)");
                return Process.StandardError.BaseStream;
            }
        }

        /// <summary>
        /// Get the TextReader for stderror
        /// </summary>
        /// Encoding set in ErrorEncoding, defaults to UTF-8
        public TextReader ErrorReader {
            get {
                if (!HasStarted)
                    throw new InvalidOperationException("Attempt to get error stream (SubProcess.GetErrorReader()) of process but process has not started");
                if (Error != Pipe)
                    throw new InvalidOperationException("Attempt to get error stream (SubProcess.GetErrorReader()) but std error not piped (Errpr=SubProcess.Pipe)");
                return Process.StandardError;
            }
        }

	///<summary>
	/// Write the given bytes, from `offset` and of length `count` to the processes' standard input. 
	/// The SubProcess must have been started with `In = SubProcess.Pipe`,
	///<summary>
        public void Write(byte[] buffer, int offset, int count)
        {
            if (!HasStarted)
                throw new InvalidOperationException("Attempt to write to process (SubProcess.Write()) but process not started");
            if (In != Pipe)
                throw new InvalidOperationException("Attempt to write to process (SubProcess.Write()) but input not piped");
            Process.StandardInput.BaseStream.Write(buffer, offset, count);
            Process.StandardInput.BaseStream.Flush();
        }
        
	///<summary>
	/// Write the given string to the sub-processes' standard input. The SubProcess must have been started with `In = SubProcess.Pipe`,
	///the default.
	///<summary>
        public void Write(string str)
        {
            if (!HasStarted)
                throw new InvalidOperationException("Attempt to write to process (SubProcess.Write()) but process not started");
            if (In != Pipe)
                throw new InvalidOperationException("Attempt to write to process (SubProcess.Write()) but input not piped");
            if (InWriter == null)
                InWriter = new StreamWriter(Process.StandardInput.BaseStream, InEncoding) { AutoFlush = true };
            InWriter.Write(str);
        }
        
	///<summary>
	///Write the given string to the sub-processes' standard input and append a newline. 
	///The SubProcess must have been started with `In = SubProcess.Pipe`, the default.
	///<summary>
        public void WriteLine(string str)
        {
            Write(str + "\n");
        }

        /// <summary>
        /// Exceptions thrown by SubProcess class
        /// </summary>
        public class SubProcessException : Exception
        {
            public SubProcessException(string msg) :
                base(msg)
            {
            }
        }

        /// <summary>
        /// Exception for a process that exited
        /// with a non zero exit code
        /// </summary>
        public class Failed : SubProcessException
        {
            public int ExitCode;
            public readonly string ErrorOutput;
            // output from stderror, "" if not captured

            public Failed(SubProcess subprocess) :
                base(subprocess.ExecutableName + " exited with " + subprocess.ExitCode + (subprocess.ErrorCapture != null ? (": " + subprocess.ErrorString) : ""))
            {
                ExitCode = subprocess.ExitCode;
                if (subprocess.ErrorCapture != null)
                    ErrorOutput = subprocess.ErrorString;
                else
                    ErrorOutput = "";
            }
        }

        /// <summary>
        /// exception for a process that exited
        /// subcessfully but did not output the
        /// expected output
        /// </summary>
        public class UnexpectedOutput : SubProcessException
        {
            public readonly string Expected;
            public readonly string Output;
                // output from stdout

            public UnexpectedOutput(SubProcess subprocess, string expected, string output) :
                base(subprocess.ExecutableName + " outputted '" + output + "' not the expected '" + expected + "'")
            {
                Expected = expected;
                Output = output;
            }
        }

        /// <summary>
        /// Exception for a process that 
        /// did not exit in the allowed time
        /// </summary>
        public class TimeoutException : SubProcessException
        {
            public readonly double Timeout;

            public TimeoutException(SubProcess subprocess) :
                base(subprocess.ExecutableName + " did not finish within " + subprocess.Timeout + " seconds")
            {
                Timeout = subprocess.Timeout;
            }
        }

	///<summary>
	/// Returns the argument list formatted using quotes as required.
	/// </summary>
        public override string ToString()
        {
            return Args.ToString();
        }

        public class Arguments : List<string>
        {
            public override string ToString()
            {
                if (Count == 0)
                    return "<no executable name>";
                return JoinToString(this);
            }

            public static string JoinToString(List<string> arguments)
            {
                StringBuilder buffer = new StringBuilder();

                foreach (string arg in arguments)
                {
                    if (buffer.Length != 0)
                        buffer.Append(' ');

                    bool quote = false;
                    if (arg.Length == 0)
                    {
                        quote = true;
                    }
                    else
                    {
                        foreach (char chr in arg)
                        {
                            // no whitespace chars are surrogates
                            if (char.IsWhiteSpace(chr) || chr == '"' || chr == '\\')
                            {
                                quote = true;
                                break;
                            }
                        }
                    }
                    if (quote)
                    {
                        // must be compatible with Win32 CommandLineToArgvW() 
                        //   https://msdn.microsoft.com/en-us/library/windows/desktop/bb776391(v=vs.85).aspx
                        // 1. 2n backslashes followed by a quotation mark produce n backslashes followed by a quotation mark.
                        // 2. (2n) +1 backslashes followed by a quotation mark again produce n backslashes followed by a quotation mark.
                        // 3. n backslashes not followed by a quotation mark simply produce n backslashes.

                        buffer.Append('"');
                        int numBackslashes = 0;
                        foreach (char chr in arg)
                        {
                            if (chr == '\"')
                            {
                                // (2n) +1 backslashes followed by a quotation mark again produce n backslashes followed by a quotation mark.
                                for (int i = 0; i < numBackslashes; ++i)
                                    buffer.Append('\\');
                                buffer.Append('\\');
                                numBackslashes = 0;
                            }
                            else if (chr == '\\')
                            {
                                ++numBackslashes;
                            }
                            else
                            {
                                numBackslashes = 0;
                            }
                            buffer.Append(chr);
                        }
                        buffer.Append('"');
                    } else
                    {
                        buffer.Append(arg);
                    }
                }
                return buffer.ToString();
            }

            public string ExecutableName { 
                get {
                    if (Count == 0)
                        return "<no executable name>";
                    return this[0];
                } 
            }

            public string ArgumentsString { 
                get {
                    return JoinToString(GetRange(1, Count - 1));
                } 
            }
        }

        public void Add(string arg)
        {
            Args.Add(arg);
        }

        public void Add(string option, string value)
        {
            Args.Add(option);
            Args.Add(value);
        }

        public bool HasStarted
        {
            get
            {
                return Process != null;
            }
        }

        /// <summary>
        /// Stream that writes it contents to another stream
        /// </summary>
        public class CopyStream : Stream
        {
            Stream Target;

            public CopyStream(Stream target)
            {
                Target = target;
            }
            
            public override bool CanRead
            {
                get
                {
                    return false;
                }
            }
            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }
            public override bool CanWrite
            {
                get
                {
                    return true;
                }
            }
            public override long Length
            {
                get
                {
                    throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
                }
            }
            public override long Position
            {
                get
                {
                    throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
                }
                set
                {
                    throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
                }
            }
            public override void Flush()
            {
                
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException("SubPorcess.SentinalStreams are not to be used");
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                Target.Write(buffer, offset, count);
            }
        }

        static private Encoding _utf8NoBomEncoding;
        /// <summary>
        // without BOM unlike Encoding.UTF8
        /// </summary>
        static private Encoding Utf8NoBomEncoding
        {
            get
            {
                if (_utf8NoBomEncoding == null)
                    _utf8NoBomEncoding = new UTF8Encoding(false);
                return _utf8NoBomEncoding;
            }
        }

        public void Start()
        {
            if (InEncoding == null)
                InEncoding = Utf8NoBomEncoding;
            if (OutEncoding == null)
                OutEncoding = Utf8NoBomEncoding;
            if (ErrorEncoding == null)
                ErrorEncoding = Utf8NoBomEncoding;

            Process = new Process();
            Process.StartInfo.FileName = Args.ExecutableName;
            Process.StartInfo.Arguments = Args.ArgumentsString;
            Process.StartInfo.UseShellExecute = UseShell;
            Process.StartInfo.CreateNoWindow = !ShowWindow;
            if (_environment != null) {
                foreach (var envVar in _environment)
                {
                    Process.StartInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
                }
            }
            
            if  (Directory != "")
                Process.StartInfo.WorkingDirectory = Directory;
            /// Process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            //    does not stop windows being shown for command line apps
            
            Stream inSource = null;
            if (In is _SentinalStream)
            {
                if (In == Through)
                {

                }
                else if (In == Capture)
                {
                    throw new LogicError("SubProcess.In can not be Capture");
                }
                else if (In == Swallow)
                {
                    inSource = Stream.Null;
                    Process.StartInfo.RedirectStandardInput = true;
                }
                else if (In == ToOut)
                {
                    throw new LogicError("SubProcess.In can not be ToOut");
                }
                else if (In == Pipe)
                {
                    // we write directly to Process.StandardInput
                    Process.StartInfo.RedirectStandardInput = true;
                }
                else
                {
                    throw new LogicError("SubProcess.In: Invalid _SentinalStream");
                }
            }
            else {
                inSource = In;
                Process.StartInfo.RedirectStandardInput = true;
            }

            Stream outTarget = null;
            if (Out is _SentinalStream)
            {
                if (Out == Through)
                {

                }
                else if (Out == Capture)
                {
                    OutCapture = new MemoryStream();
                    outTarget = OutCapture;
                    Process.StartInfo.RedirectStandardOutput = true;
                }
                else if (Out == Swallow)
                {
                    outTarget = Stream.Null;
                    Process.StartInfo.RedirectStandardOutput = true;
                }
                else if (Out == ToOut)
                {
                    throw new LogicError("SubProcess.Out can not be ToOut");
                }
                else if (Out == Pipe)
                {
                    Process.StartInfo.RedirectStandardOutput = true;
                }
                else
                {
                    throw new LogicError("SubProcess.Out: Invalid _SentinalStream");
                }
            } else {
                outTarget = Out;
                Process.StartInfo.RedirectStandardOutput = true;
            }
            if (Process.StartInfo.RedirectStandardOutput)
                Process.StartInfo.StandardOutputEncoding = OutEncoding;

            Stream errorTarget = null;
            if (Error is _SentinalStream)
            {
                if (Error == Through)
                {

                }
                else if (Error == Capture)
                {
                    ErrorCapture = new MemoryStream();
                    errorTarget = ErrorCapture;
                    Process.StartInfo.RedirectStandardError = true;
                    // no neeed to set StandardErrorEncoding as we are dealing with Streams
                }
                else if (Error == Swallow)
                {
                    errorTarget = Stream.Null;
                    Process.StartInfo.RedirectStandardError = true;
                    // no neeed to set StandardErrorEncoding as we are dealing with Streams
                }
                else if (Error == ToOut)
                {
                    errorTarget = outTarget;
                    Process.StartInfo.RedirectStandardError = true;
                    // do not set StandardErrorEncoding as this overwrites
                    // the stderr part in Process and breaks the ToOut
                    // behaviour
                }
                else if (Error == Pipe)
                {
                    Process.StartInfo.RedirectStandardError = true;
                    Process.StartInfo.StandardErrorEncoding = ErrorEncoding;
                }
                else
                {
                    throw new LogicError("SubProcess.Error: Invalid _SentinalStream");
                }
            } else {
                errorTarget = Error;
                Process.StartInfo.RedirectStandardError = true;
            }

            Process.Start();

            if (outTarget != null)
            {
                Task readOut = Process.StandardOutput.BaseStream.CopyToAsync(outTarget);
                Tasks.Add(readOut);
            }
            if (errorTarget != null)
            {
                Task readError;
                if (errorTarget == outTarget)
                {
                    // can not ject CopyToAsync(errorTarget/outTarget) as sometimes bytes
                    // go missing when Error = ToOut
                    readError = Process.StandardError.BaseStream.CopyToAsync(new CopyStream(outTarget));
                }
                else
                {
                    readError = Process.StandardError.BaseStream.CopyToAsync(errorTarget);
                }
                Tasks.Add(readError);
            }
            if (Process.StartInfo.RedirectStandardInput)
                Process.StandardInput.AutoFlush = true;
            if (inSource != null)
            {
                Task writeIn = inSource.CopyToAsync(Process.StandardInput.BaseStream);
                writeIn.ContinueWith((task) => Process.StandardInput.BaseStream.Flush());
                Tasks.Add(writeIn);
            }
        }

        public int Wait()
        {
            if (!HasStarted)
                Start();

            if (HasTimeout)
            {
                DateTime start = DateTime.Now;
                if (!Task.WaitAll(Tasks.ToArray(), (int)(Timeout * 1000)))
                    throw new TimeoutException(this);
                int leftMs = (int)((Timeout - (DateTime.Now - start).TotalSeconds) * 1000);
                if (!Process.WaitForExit(leftMs > 0 ? leftMs : 0))
                    throw new TimeoutException(this);
                return Process.ExitCode;
            }
            else
            {
                Task.WaitAll(Tasks.ToArray());
                Process.WaitForExit();
                return Process.ExitCode;
            }            
        }
        
         async public Task<int> WaitAsync()
         {
            if (!HasStarted)
                Start();

            if (HasTimeout)
            {
                DateTime start = DateTime.Now;
                Task tasks = Task.WhenAll(Tasks);
                if (await Task.WhenAny(tasks, Task.Delay((int)(Timeout * 1000))) != tasks)
                {
                    throw new TimeoutException(this);
                }
                int leftMs = (int)((DateTime.Now - start).TotalMilliseconds);
                if (!Process.WaitForExit(leftMs))
                    throw new TimeoutException(this);
                return Process.ExitCode;
            }
            else
            {
                await Task.WhenAll(Tasks);
                Process.WaitForExit();
                return Process.ExitCode;
            }
        }

        /// <summary>
        /// Kill the process.
        /// Throw is not started or exited already
        /// Asynchronous, does not wait for the process to exit
        /// Wait() to wait for exit
        /// </summary>
        public void Kill()
        {
            if (!HasStarted)
                throw new InvalidOperationException("Attempt to SubProcess.Kill() before process has started");
            Process.Kill();
        }

        /// <summary>
        /// Wait for the process to exit
        /// Throw if exits with a non zero exit code.
        /// </summary>
        public void Check()
        {
            int rc = Wait();
            if (rc != 0)
                throw new Failed(this);
        }

        /// <summary>
        /// WaitAsync for the process to exit
        /// Throw if exits with a non zero exit code.
        /// </summary>
        async public Task CheckAsync()
        {
            int rc = await WaitAsync();
            if (rc != 0)
                throw new Failed(this);
        }
        
        /// <summary>
        /// Run the program returning the exit code.
        /// </summary>
        /// <param name="args">Program name and arguments to the program</param>
        /// <returns>The programs exit code</returns>
        static public int Call(IEnumerable<string> args)
        {
            // return exit code
            return new SubProcess(args)
            {
                Out = SubProcess.Through,
                Error = SubProcess.Through
            }.Wait();
        }

        /// <summary>
        /// Run the program returning the exit code.
        /// </summary>
        /// <param name="args">Program name and arguments to the program</param>
        /// <returns>The program's exit code</returns>
        static public int Call(params string[] args)
        {
            // return exit code
            return Call(new List<string>(args));
        }

        /// <summary>
        /// Run the program checking that is exits sucessfully
        /// </summary>
        /// If the program exits with a non zero exit code an exception containing 
        /// the standard error output is thrown.
        /// <param name="args">Program name and arguments to the program</param>
        /// <returns></returns>
        static public void CheckCall(IEnumerable<string> args)
        {
            // throw on error
            new SubProcess(args).Check();            
        }

        /// <summary>
        /// Run the program throw if exits with a non zero exit code.
        /// </summary>
        /// If the program exits with a non zero exit code an exception containing 
        /// the standard error output is thrown.
        /// <param name="args">Program name and arguments to the program</param>
        /// <returns></returns>
        static public void CheckCall(params string[] args)
        {
            // throw on error
            CheckCall(new List<string>(args));
        }

        public class _CheckOutput {

            private IEnumerable<string> args;

            public _CheckOutput(IEnumerable<string> args)
            {
                this.args = args;
            }

            public void With(string expected)
            {
                SubProcess sp = new SubProcess(args);
                sp.Check();
                string output = sp.OutputString;
                if (output != expected)
                    throw new UnexpectedOutput(sp, expected, output);
            }
        }

        /// <summary>
        /// Run the program checking that is exits sucessfully and outputs as expected
        /// </summary>
        /// If the program exits with a non zero exit code an exception containing 
        /// the standard error output is thrown.
        /// If the output is not as expected, specified by .With(string expectedOutput)
        /// throw.
        /// e.g. Subprocess.CheckOutput("get-state", "--current-user").With("awake")
        /// <param name="args">Program name and arguments to the program</param>
        /// <returns></returns>
        static public _CheckOutput CheckOutput(IEnumerable<string> args)
        {
            return new _CheckOutput(args);
        }

        /// <summary>
        /// Run the program checking that is exits sucessfully and outputs as expected
        /// </summary>
        /// If the program exits with a non zero exit code an exception containing 
        /// the standard error output is thrown.
        /// If the output is not as expected, specified by .With(string expectedOutput)
        /// throw.
        /// e.g. Subprocess.CheckOutput("get-state", "--current-user").With("awake")
        /// <param name="args">Program name and arguments to the program</param>
        /// <returns></returns>
        static public _CheckOutput CheckOutput(params string[] args)
        {
            return CheckOutput(new List<string>(args));
        }

        /// <summary>
        /// Implements IDisposable
        /// So SubProcess can be used within using()
        /// If the block exits and the subprocess is still
        /// alive Kill() will be called.
        /// </summary>
        public void Dispose()
        {
            if (!HasStarted)
                return;
            if (HasExited)
                return;
            try
            {
                Kill();
            } catch (InvalidOperationException)
            {
                // exited after we checked HasExited()
            }
        }
    }
}
