namespace QuickGit.Core
{
    /// <summary>Raw output of an external process.</summary>
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

        /// <summary>Most useful text to show: git writes many informational messages to stderr.</summary>
        public string BestMessage => string.IsNullOrWhiteSpace(Error) ? Output : Error;
    }
}
