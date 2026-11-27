using System.Collections.Generic;
using System.Linq;
using QuickGit.Core;

namespace QuickGit.Git
{
    /// <summary>
    /// Commit message: title (first line, the one shown in the history) and an
    /// optional description that git stores as the message body.
    /// </summary>
    public sealed class CommitMessage
    {
        public string Title { get; }
        public string Description { get; }
        public bool HasDescription => Description.Length > 0;

        /// <summary>"Key: value" lines at the end of the message (git "trailers" convention).</summary>
        public IReadOnlyList<string> Trailers { get; }

        public CommitMessage(string title, string description = null) : this(title, description, new string[0]) { }

        private CommitMessage(string title, string description, IReadOnlyList<string> trailers)
        {
            Trailers = trailers;
            // The title is a single line: line breaks become spaces.
            Title = (title ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            Description = (description ?? string.Empty).Replace("\r\n", "\n").Trim();
        }

        public CommitMessage WithTrailer(string key, string value) =>
            new CommitMessage(Title, Description, Trailers.Concat(new[] { $"{key}: {value}" }).ToList());

        public OperationResult Validate() =>
            Title.Length == 0 ? OperationResult.Fail("The commit title cannot be empty.") : OperationResult.Ok();
    }
}
