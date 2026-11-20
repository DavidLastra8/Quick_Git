namespace UnityGitTool.Core
{
    /// <summary>Salida cruda de un proceso externo.</summary>
    public readonly struct CommandResult
    {
        public int ExitCode { get; }
        public string Output { get; }
        public string Error { get; }
        public bool Success => ExitCode == 0;

        public CommandResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output ?? string.Empty;
            Error = error ?? string.Empty;
        }

        /// <summary>Texto más útil para mostrar: git escribe muchos mensajes informativos en stderr.</summary>
        public string BestMessage => string.IsNullOrWhiteSpace(Error) ? Output : Error;
    }
}
