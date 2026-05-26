using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Infrastructure.Storages.Local;

public class LocalFileStorageManager : IFileStorageManager
{
    private readonly string _basePath;

    public LocalFileStorageManager(LocalOptions option)
    {
        _basePath = ResolveBasePath(option?.Path);
        Directory.CreateDirectory(_basePath);
    }

    public async Task CreateAsync(IFileEntry fileEntry, Stream stream, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_basePath, fileEntry.FileLocation);
        var folder = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        using var fileStream = File.Create(filePath);
        await stream.CopyToAsync(fileStream, cancellationToken);
    }

    public async Task DeleteAsync(IFileEntry fileEntry, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            var path = Path.Combine(_basePath, fileEntry.FileLocation);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }, cancellationToken);
    }

    public Task<byte[]> ReadAsync(IFileEntry fileEntry, CancellationToken cancellationToken = default)
    {
        return File.ReadAllBytesAsync(Path.Combine(_basePath, fileEntry.FileLocation), cancellationToken);
    }

    public Task ArchiveAsync(IFileEntry fileEntry, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task UnArchiveAsync(IFileEntry fileEntry, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private static string ResolveBasePath(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && IsUsableRoot(configuredPath))
        {
            return configuredPath;
        }

        var contentRootPath = Environment.GetEnvironmentVariable("CONTENT_ROOT_PATH");
        if (!string.IsNullOrWhiteSpace(contentRootPath))
        {
            return Path.Combine(contentRootPath, ".tmp", "files");
        }

        return Path.Combine(AppContext.BaseDirectory, ".tmp", "files");
    }

    private static bool IsUsableRoot(string path)
    {
        try
        {
            if (!Path.IsPathRooted(path))
            {
                return true;
            }

            var root = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(root))
            {
                return false;
            }

            return DriveInfo.GetDrives().Any(d => string.Equals(d.Name, root, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
