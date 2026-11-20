using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    /// <summary>
    /// Mensaje de commit: título (primera línea, la que se ve en el historial) y una
    /// descripción opcional que git guarda como cuerpo del mensaje.
    /// </summary>
    public sealed class CommitMessage
    {
        public string Title { get; }
        public string Description { get; }
        public bool HasDescription => Description.Length > 0;

        public CommitMessage(string title, string description = null)
        {
            // El título es una sola línea: los saltos de línea se convierten en espacios.
            Title = (title ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            Description = (description ?? string.Empty).Replace("\r\n", "\n").Trim();
        }

        public OperationResult Validate() =>
            Title.Length == 0 ? OperationResult.Fail("El título del commit no puede estar vacío.") : OperationResult.Ok();
    }
}
