namespace QuickGit.Git
{
    /// <summary>Standard .gitignore for Unity projects.</summary>
    public sealed class UnityGitIgnoreTemplate : IGitIgnoreTemplate
    {
        public string Content => @"# --- Unity ---
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/
/[Mm]emoryCaptures/
/[Rr]ecordings/

# .meta files are only ignored when their asset is ignored too
!/[Aa]ssets/**/*.meta

/[Aa]ssets/Plugins/Editor/JetBrains*
/[Aa]ssets/[Aa]ddressable[Aa]ssets[Dd]ata/*/*.bin*
/[Aa]ssets/[Ss]treamingAssets/aa.meta
/[Aa]ssets/[Ss]treamingAssets/aa/*

# --- IDEs ---
.vs/
.vscode/
.idea/
.gradle/
.consulo/
ExportedObj/
*.csproj
*.unityproj
*.sln
*.suo
*.tmp
*.user
*.userprefs
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
*.opendb
*.VC.db
*.pidb.meta
*.pdb.meta
*.mdb.meta

# --- Builds and others ---
sysinfo.txt
crashlytics-build.properties
*.apk
*.aab
*.unitypackage
*.app
.DS_Store
Thumbs.db
";
    }
}
