# GeneThomas.SubProcess
A library to run sub-processes no .net

The `Process` class has a unusable string `StartInfo.Arguments`, the rules aroung quoting `"`s are complex so one can not just concatenate arguments to be passed in.
`SubProcess` takes a list of strings and handles the quoting internally. By default the standed outputs and standard error are captured into strings, accessable as
`.OutputString` and `ErrorString` after the sub-process has started.