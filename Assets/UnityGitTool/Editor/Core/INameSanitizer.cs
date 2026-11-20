using System.Globalization;
using System.Text;

namespace UnityGitTool.Core
{
    /// <summary>
    /// Corrige automáticamente nombres que no cumplen unas reglas (ramas, repositorios...).
    /// Cada tipo de nombre tiene su propia implementación (SRP) y el presentador sólo
    /// conoce esta interfaz (DIP), así que se pueden añadir reglas nuevas sin tocarlo (OCP).
    /// </summary>
    public interface INameSanitizer
    {
        /// <summary>
        /// Corrección "en vivo" mientras se escribe: cambia carácter por carácter sin variar
        /// la longitud del texto, para no mover el cursor (p. ej. "mi rama" → "mi-rama").
        /// </summary>
        string SanitizeWhileTyping(string raw);

        /// <summary>Corrección completa y definitiva, aplicada justo antes de usar el nombre.</summary>
        string Sanitize(string raw);
    }

    public static class TextNormalizer
    {
        /// <summary>"Diseño Básico" → "Diseno Basico". Mantiene la longitud de los caracteres compuestos.</summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var builder = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
                var baseChar = decomposed[0];
                builder.Append(CharUnicodeInfo.GetUnicodeCategory(baseChar) == UnicodeCategory.NonSpacingMark ? c : baseChar);
            }

            return builder.ToString();
        }
    }
}
