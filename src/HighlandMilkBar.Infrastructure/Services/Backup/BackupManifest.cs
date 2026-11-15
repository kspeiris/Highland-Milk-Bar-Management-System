using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Infrastructure.Services.Backup;

/// <summary>
/// Reads and writes the per-backup manifest (<c>&lt;file&gt;.db.manifest.json</c>) and
/// computes SHA-256 checksums. Manifests are written to a temporary file and atomically
/// renamed so a partially written manifest can never be observed.
/// </summary>
internal static class BackupManifest
{
    public const string ManifestSuffix = ".manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string GetManifestPath(string backupFilePath) => backupFilePath + ManifestSuffix;

    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) builder.Append(b.ToString("x2"));
        return builder.ToString();
    }

    public static void Write(string backupFilePath, BackupManifestDto manifest)
    {
        var manifestPath = GetManifestPath(backupFilePath);
        var tempPath = manifestPath + ".partial";
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(tempPath, json, Encoding.UTF8);
        if (File.Exists(manifestPath)) File.Delete(manifestPath);
        File.Move(tempPath, manifestPath);
    }

    /// <summary>Returns null when the manifest is missing or malformed; never throws.</summary>
    public static BackupManifestDto? Read(string backupFilePath)
    {
        var manifestPath = GetManifestPath(backupFilePath);
        if (!File.Exists(manifestPath)) return null;
        try
        {
            var json = File.ReadAllText(manifestPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<BackupManifestDto>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void Delete(string backupFilePath)
    {
        var manifestPath = GetManifestPath(backupFilePath);
        if (File.Exists(manifestPath)) File.Delete(manifestPath);
    }
}