namespace UnityGitTool.Presentation
{
    /// <summary>Cómo se verá un nombre (repo o rama) en el servicio remoto, ya corregido.</summary>
    public sealed class NamePreview
    {
        public string Heading { get; }
        public string DisplayName { get; }
        public string Url { get; }
        public string Note { get; }
        public bool IsValid { get; }

        private NamePreview(bool isValid, string heading, string displayName, string url, string note)
        {
            IsValid = isValid;
            Heading = heading;
            DisplayName = displayName;
            Url = url;
            Note = note;
        }

        public static NamePreview Valid(string heading, string displayName, string url, string note = null) =>
            new NamePreview(true, heading, displayName, url, note);

        public static NamePreview Invalid(string note) =>
            new NamePreview(false, null, null, null, note);
    }
}
