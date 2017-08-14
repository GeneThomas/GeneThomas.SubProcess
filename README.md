# Gt.SubProcess
A library to run sub-processes on .net
By Gene Thomas

Gt.SubProcess is on nuget - `Install-Package Gt.SubProcess`.

The `Process` class has a unusable string `StartInfo.Arguments`, the rules aroung quoting `"`s are complex so one can not just concatenate arguments to be passed in.
`SubProcess` takes a list of strings and handles the quoting internally. By default the standed output and standard error are captured into strings, accessable as
`.OutputString` and `ErrorString` after the sub-process has started.

```csharp
    using System;
    using System.IO;
    using Gt.SubProcess;

    namespace ReadmeExample
    {
        class Program
        {
            static int Main(string[] args) { 
                try {
                    SubProcess.Call("ls", "-l");

                    SubProcess.CheckCall("psql", "my-database", "fred");
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
```

## Static methods

SubProcess has a number of static methods which do not require one to create a SubProcess object.

`SubProcess.Call("ls", "-l", "-r", "-t")` and `SubProcess.Call(IEnumerable<string>)` run the program and return it's `int` return code.

`SubProcess.CheckCall("ls", "-l", "-r", "-t")` and `SubProcess.CheckCall(IEnumerable<string>)` run the program and throw a `Failed` exception if the program exits with a non zero exit code.

`SubProcess.CheckOutput("ls", "-l", "-r", "-t").With(expectedString)` and `CheckOutput(IEnumerable<string>).With(expectedString)` run the program, throw 
`Failed` exception if it exits with a non-zero exide code and throw an `UnexpectedOutput` exception if the program's standed output is not as expected, 
as specified in the `.With(string)` part.

If you need more control over the sub-process create and intance of `SubProcess` and initialise as required.

## SubProcess objects

`SubProcess` objects are created with a list of strings inline, e.g. `var p = new SubProcess("ls", "-l")` or with an `IEnumerable<string>`, e.g. a `List<string>`.
There a number of methods and properties that can be used to launch and run the program as required.

## Startup methods and properties

### `Add(string arg)`

Add an item to the process arguments list.
e.g. `p.Add("--verbose");`

### `Add(string options, string value)`

Add an item to the process arguments list.
e.g. `p.Add("--name", "fred");`

## Methods and properties of a running `SubProcess`
 
### `bool HasStarted`

Returns true if the sub-process has started.

### `string ToString()`

Returns the argument list formatted using quotes as required.

### `Start()`

Set the sub-process running.

### `int Wait()`

Wait for the process to exit, `Start()`ing if required. Returns the processes' exit code.
  
### `Check()`

Wait for the process to exit, `Start()`ing if required. Throw a `Failed` if the sub-process exits with a non-zero exit code.

### `kill()`

Terminate the sub-process. This happens asynchronously, we do not wait for the sub-process to exit.

### `int ExitCode`

The number that the sub-process exited with. Traditionally 0 means a good exit, and non-zero means a problem occured.

### `bool HasExited`

Returns true of the sub-process has run and exited.

### `bool IsAlive`

Returns true of the sub-process is still runnning.

### `string ExecutableName`

Returns the name of the sub-process.

### `Write(string input)`

Write the given string to the sub-processes' standard input. The SubProcess must have been started with `In = SubProcess.Pipe`,
the default.

### `Write(byte[] buffer, int offset, int count)`

Write the given bytes, from `offset` and of length `count` to the processes' standard input. 
The SubProcess must have been started with `In = SubProcess.Pipe`,

#### `WriteLine(string input)`

Write the given string to the sub-processes' standard input and append a newline. 
The SubProcess must have been started with `In = SubProcess.Pipe`, the default.

### `string OutputString`

Return a `string` containing the sub-processes' captured standard output.
The encoding defaults to UTF-8 but can be set using the startup `OutputEncoding` propery.
The `Out` property must be set to `SubProcess.Capture` or `SubProcess.Pipe` at startup.

### `string ErrorString`

Return a `string` containing the sub-processes' captured standard error.
The encoding defaults to UTF-8 but can be set using the startup `OutputEncoding` propery.
The `Error` property must be set to `SubProcess.Capture` or `SubProcess.Pipe` at startup.

### `TextReader OutputReader`

Return a `TextReader` to read the sub-processes' standard output.
The encoding defaults to UTF-8 but can be set using the startup `OutputEncoding` propery.
The `Out` property must be set to `Pipe` for this to wok.

### `TextReader ErrorReader`

Return a `TextReader` to read the sub-processes' standard error.
The encoding defaults to UTF-8 but can be set using the startup `ErrorEncoding` propery.
The `Error` property must be set to `Capture` or `Pipe` at startup.
` or `Pipe` at startup.

### `Stream OutputStream`

Return a `Stream` to read the sub-processes' standard error.
`Out`must be set to SubProcess.Capture` for this to work.

### `Stream ErrorStream`

Return a `Stream` to read the sub-processes' standard error.
`Error`must be set to SubProcess.Pipe` for this to work.

### `bool HasTimeout`

Return true if the sub-process took too long.

### `async Task Check()`

Asynchronous version of Check(). Wait for the process to exit, `Start()`ing if required. 
Throw a `Failed` if the sub-process exits with a non-zero exit code.

## `IDisposable interface`

 `SubProcess` inherits from `IDisposable` so can be used with a `using` block to ensure that the sub-process is terminated.
 e.g.
   using (SubProcess p = new SubProcess("find", ".", "-name", "*.exe"))
   {
        // do something
   }

`Kill()` is used if we leave the `using` block without `p` exiting.