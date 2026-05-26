using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;

var dir = @"d:\Antigravity\WinHyperland";
var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
    .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml") || f.EndsWith(".csproj"))
    .Where(f => !f.Contains(@"\bin\") && !f.Contains(@"\obj\") && !f.Contains(@"\Publish\"))
    .ToList();

var sb = new StringBuilder();

foreach (var f in files.OrderBy(x => x))
{
    var content = File.ReadAllText(f);
    
    // HyperlandController Duplicate Logic
    if (f.EndsWith("HyperlandController.cs"))
    {
        var dupLogic = @"if \(_settings\.ClickToOpenApp && _lastMediaInfo\?\.SourceAppId is not null\)
\s*\{
\s*try
\s*\{
\s*await Windows\.System\.Launcher\.LaunchUriAsync\(
\s*new Uri\(\$""shell:AppsFolder\\\\\{_lastMediaInfo\.SourceAppId\}""\)\);
\s*\}
\s*catch \{ \}
\s*\}";
        content = Regex.Replace(content, dupLogic, "await TryLaunchAppAsync();");
        
        var method = @"
        private async Task TryLaunchAppAsync()
        {
            if (_settings.ClickToOpenApp && _lastMediaInfo?.SourceAppId is not null)
            {
                try { await Windows.System.Launcher.LaunchUriAsync(new Uri($""shell:AppsFolder\\{_lastMediaInfo.SourceAppId}"")); } catch { }
            }
        }";
        content = content.Replace("private void SetupPointerEvents()", method + "\n\n        private void SetupPointerEvents()");
    }
    
    // Remove blank lines to save tokens
    content = Regex.Replace(content, @"^\s*$\n|\r", "", RegexOptions.Multiline);

    string ext = Path.GetExtension(f);
    string name = Path.GetFileName(f);
    if (ext == ".cs") sb.AppendLine($"// {name}");
    else if (ext == ".xaml" || ext == ".csproj") sb.AppendLine($"<!-- {name} -->");
    
    sb.AppendLine(content.Trim());
}

File.WriteAllText(Path.Combine(dir, "output.txt"), sb.ToString());
