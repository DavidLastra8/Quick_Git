namespace UnityGitTool.Core
{
    /// <summary>
    /// Resultado inmutable de una operación. Evita usar excepciones para el flujo normal
    /// (un pull rechazado no es "excepcional", es un resultado esperado).
    /// </summary>
    public class OperationResult
    {
        public bool Success { get; }
        public string Message { get; }

        protected OperationResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public static OperationResult Ok(string message = null) => new OperationResult(true, message);
        public static OperationResult Fail(string message) => new OperationResult(false, message);
    }

    public sealed class OperationResult<T> : OperationResult
    {
        public T Value { get; }

        private OperationResult(bool success, T value, string message) : base(success, message)
        {
            Value = value;
        }

        public static OperationResult<T> Ok(T value, string message = null) =>
            new OperationResult<T>(true, value, message);

        public new static OperationResult<T> Fail(string message) =>
            new OperationResult<T>(false, default, message);
    }
}
