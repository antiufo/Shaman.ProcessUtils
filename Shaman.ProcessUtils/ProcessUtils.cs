using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Shaman.Runtime
{
    public static class ProcessUtils
    {
        public static ProcessStartInfo CreateProcessStartInfo(string workingDirectory, string command, params object[] args)
        {
            var psi = new ProcessStartInfo();
            //if (!File.Exists(command) && !command.Contains("/") && !command.Contains("\\"))
            //{
            //    var path = Environment.GetEnvironmentVariable("PATH");
            //    var pathext = Environment.GetEnvironmentVariable("PATHEXT");
            //    if (path != null && pathext != null)
            //    {
            //        var paths = 
            //    }

            //}


            var resolved = PathLookup(command, null);
            if (resolved == null) throw new FileNotFoundException("Cannot find application: " + command, command);
            if (resolved.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || resolved.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                args = new[] { "/c", command }.Concat(args).ToArray();
                command = "cmd.exe";
            }



            psi.FileName = command;
            psi.WorkingDirectory = workingDirectory;
            psi.Arguments = GetArguments(args);
            psi.UseShellExecute = false;
            return psi;
        }

        private static bool ContainsSpecialCharacters(string str)
        {
            return str.Any(y => !(char.IsLetterOrDigit(y) || y == '\\' || y == '|' || y == '/' || y == '.' || y == ':' || y == '-'));
        }

        public static string PathLookup(string file, string additionalSearchFolder)
        {
            if (file.Contains("/") || file.Contains("\\")) return file;
            if (file == "cmd.exe") return file;
            IEnumerable<string> paths = Environment.GetEnvironmentVariable("path").Split(';').Select(x => Environment.ExpandEnvironmentVariables(x)).ToList();
            if (additionalSearchFolder != null) paths = new[] { additionalSearchFolder }.Concat(paths);

            foreach (var p_ in paths)
            {
                var p = p_;
                if (!p.EndsWith("\\")) p += "\\";
                var pfile = p + file;
                if (File.Exists(pfile))
                {
                    var ext = Path.GetExtension(pfile).ToLower();
                    if (ext == ".exe" || ext == ".cmd" || ext == ".bat") return pfile;
                }
                if (File.Exists(pfile + ".exe")) return pfile + ".exe";
                if (File.Exists(pfile + ".cmd")) return pfile + ".cmd";
                if (File.Exists(pfile + ".bat")) return pfile + ".bat";

            }
            return null;
        }


        public static string GetArguments(params object[] arguments)
        {
            return string.Join(" ", arguments.Select(x =>
            {
                if (x is CommandLineNamedArgument) return x.ToString();
                if (x is RawCommandLineArgument) return x.ToString();
                var str = Convert.ToString(x, CultureInfo.InvariantCulture);
                if (ContainsSpecialCharacters(str) || string.IsNullOrEmpty(str)) return "\"" + str + "\"";
                return str;
            }).ToArray());
        }

        public static string GetCommandLine(string file, params object[] arguments)
        {
            return "\"" + file + "\" " + GetArguments(arguments);
        }

        public static StreamReader RunFromRaw(string workingDirectory, string command, params object[] args)
        {
            var psi = CreateProcessStartInfo(workingDirectory, command, args);
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            var stderr = new StringWriter();

            var p = System.Diagnostics.Process.Start(psi);
            
            p.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    stderr.Write(e.Data);
                    stderr.Write('\n');
                }
            };
            p.BeginErrorReadLine();

          //  return p.StandardOutput;
            var wrapped = new WrappedStreamReader(p.StandardOutput.BaseStream, p, stderr, psi);
            return new StreamReader(wrapped, p.StandardOutput.CurrentEncoding, false);

            
        }
        
        public static IEnumerable<string> RunFromRawEnumerable(string workingDirectory, string command, params object[] args)
        {
            var reader = RunFromRaw(workingDirectory, command, args);
            var used = new bool[1];
            return MakeEnumerable(reader, used);
        }
        
        private static IEnumerable<string> MakeEnumerable(TextReader reader, bool[] used)
        {
            if(used[0]) throw new InvalidOperationException("GetEnumerator() can only be called once on process outputs.");
            used[0] = true;
            using(reader)
            {
                while(true)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;
                    yield return line;
                }
            }
        }




        public static string RunFrom(string workingDirectory, string command, params object[] args)
        {
            var psi = CreateProcessStartInfo(workingDirectory, command, args);
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;

            var allOutput = new StringWriter();
            var stderr = new StringWriter();
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                p.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        allOutput.Write(e.Data);
                        allOutput.Write('\n');
                    }
                };
                p.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        allOutput.Write(e.Data);
                        allOutput.Write('\n');
                        stderr.Write(e.Data);
                        stderr.Write('\n');
                    }
                };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    var err = stderr.ToString();
                    if (string.IsNullOrWhiteSpace(err)) err = allOutput.ToString();
                    throw new ProcessException(p.ExitCode, err, string.Format("Execution of {0} with arguments {1} failed with exit code {2}: {3}", psi.FileName, psi.Arguments, p.ExitCode, err));
                }
                return allOutput.ToString();
            }
        }



        public static void RunPassThroughFrom(string workingDirectory, string command, params object[] args)
        {
            var psi = CreateProcessStartInfo(workingDirectory, command, args);

            using (var p = System.Diagnostics.Process.Start(psi))
            {
                p.WaitForExit();

                if (p.ExitCode != 0)
                    throw new ProcessException(p.ExitCode, null, string.Format("Execution of {0} with arguments {1} failed with exit code {2}.", psi.FileName, psi.Arguments, p.ExitCode));

            }
        }

        public static string Run(string command, params object[] args)
        {
            return RunFrom(Environment.GetEnvironmentVariable("SystemRoot") ?? "/", command, args);
        }

        public static void RunPassThrough(string command, params object[] args)
        {
            RunPassThroughFrom(Environment.GetEnvironmentVariable("SystemRoot") ?? "/", command, args);
        }

        public static CommandLineNamedArgument NamedArgument(string name, object value)
        {
            return new CommandLineNamedArgument(name, value);
        }


        public class RawCommandLineArgument
        {
            public string Value { get; private set; }
            public RawCommandLineArgument(string value)
            {
                this.Value = value;
            }

            public override string ToString()
            {
                return Value;
            }
        }

        public class CommandLineNamedArgument
        {

            public CommandLineNamedArgument(string name, object value)
            {
                this.Name = name;
                this.Value = value;
            }

            public string Name { get; private set; }
            public object Value { get; private set; }
            public override string ToString()
            {
                var v = Convert.ToString(Value, CultureInfo.InvariantCulture);
                return Name + (ContainsSpecialCharacters(v) ? "\"" + v + "\"" : v);
            }
        }

        private class WrappedStreamReader : Stream
        {
            private Stream baseStream;
            private Process p;
            private StringWriter stderr;
            private ProcessStartInfo psi;

            public WrappedStreamReader(Stream baseStream, Process p, StringWriter stderr, ProcessStartInfo psi)
            {
                this.baseStream = baseStream;
                this.p = p;
                this.stderr = stderr;
                this.psi = psi;
            }

            public override bool CanRead { get { return true; } }

            public override bool CanSeek { get { return false; } }

            public override bool CanWrite { get { return false; } }

            public override long Length { get { throw new NotSupportedException(); } }

            public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }

            public override void Flush()
            {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {

                var k = baseStream.Read(buffer, offset, count);
                if (k == 0)
                {
                    p.WaitForExit();
                    if (p.ExitCode != 0)
                    {
                        var err = stderr.ToString();
                        throw new ProcessException(p.ExitCode, err, string.Format("Execution of {0} with arguments {1} failed with exit code {2}: {3}", psi.FileName, psi.Arguments, p.ExitCode, err));
                    }
                }
                return k;

            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try
                    {
                        if (!p.HasExited) p.Kill();
                    }
                    catch
                    {
                    }
                    p.Dispose();
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }



    public class ProcessException : Exception
    {
        public int ExitCode { get; private set; }
        public string ErrorText { get; private set; }

        public ProcessException(int exitCode)
        {
            this.ExitCode = exitCode;
        }

        public ProcessException(int exitCode, string errorText, string message)
            : base(message)
        {
            this.ExitCode = exitCode;
            this.ErrorText = errorText;
        }
    }


}
