using System;
using System.Collections.Generic;
using System.Linq;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    /// <summary>Parsing and formatting helpers for git output (shared by the services).</summary>
    internal static class GitOutput
    {
        public static IEnumerable<string> Lines(string text) =>
            (text ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);

        public static OperationResult ToResult(CommandResult result, string successMessage, string failurePrefix)
        {
            if (result.Success) return OperationResult.Ok(successMessage);
            var detail = result.BestMessage;
            return OperationResult.Fail(string.IsNullOrWhiteSpace(detail) ? failurePrefix : $"{failurePrefix}:\n{detail}");
        }
    }
}
