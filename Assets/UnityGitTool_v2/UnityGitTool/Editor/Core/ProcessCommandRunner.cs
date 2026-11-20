using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnityGitTool.Core
{
    /// <summary>
    /// Ejecuta un programa externo en un hilo de fondo para no congelar el editor.
    /// Única responsabilidad: lanzar el proceso y devolver su salida.
    /// </summary>
    public sealed class ProcessCommandRunner : ICommandRunner
    {
        private readonly string _executable;
        private readonly IReadOnlyDictionary<string, string> _environment;

        public ProcessCommandRunner(string executable, IReadOnlyDictionary<string, string> environment = null)
        {
            _executable = string.IsNullOrWhiteSpace(executable) ? throw new ArgumentException(nameof(executable)) : executable;
            _environment = environment ?? new Dictionary<string, string>();
        }

        public Task<CommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            return Task.Run(() => Run(workingDirectory, arguments, cancellationToken), cancellationToken);
        }

        private CommandResult Run(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _executable,
                Arguments = ArgumentEscaper.Join(arguments),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            foreach (var variable in _environment)
                startInfo.EnvironmentVariables[variable.Key] = variable.Value;

            var output = new StringBuilder();
            var error = new StringBuilder();

            try
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                    process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    while (!process.WaitForExit(100))
                    {
                        if (!cancellationToken.IsCancellationRequested) continue;
                        TryKill(process);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    process.WaitForExit(); // Vacía los buffers de lectura asíncrona.
                    // TrimEnd y no Trim: en "git status" un espacio inicial tiene significado.
                    return new CommandResult(process.ExitCode, output.ToString().TrimEnd(), error.ToString().Trim());
                }
            }
            catch (Win32Exception exception)
            {
                return new CommandResult(-1, string.Empty, $"No se pudo ejecutar '{_executable}': {exception.Message}");
            }
        }

        private static void TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { /* El proceso ya terminó. */ }
        }
    }
}
