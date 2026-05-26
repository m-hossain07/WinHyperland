using System.IO;
using System.Text.RegularExpressions;

var path = @"d:\Antigravity\WinHyperland\SettingsService.cs";
var content = File.ReadAllText(path);

content = Regex.Replace(content, @"public interface SettingsService.*?// --- Implementation ------------------------------------", "// --- Implementation ------------------------------------", RegexOptions.Singleline);
content = content.Replace("public sealed class SettingsService : SettingsService", "public sealed class SettingsService");

File.WriteAllText(path, content);
