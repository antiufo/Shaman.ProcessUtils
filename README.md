# Shaman.ProcessUtils

Library for working with external processes.

```csharp
using Shaman.Runtime;

// Arguments are automatically quoted if necessary.
try
{
    ProcessUtils.Run("program", "arg1", "--option", "C:\Some directory\File.dat");
}
catch (ProcessException ex) when ex.ExitCode == 5
{
    // ex.ErrorText is the process' stderr
}

// RunPassThrough: prints stdout and stderr to the the current process stdout and stderr.
// RunFrom/RunPassThroughFrom: sets the current directory for the program.

```