using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Notepad.Services;

/// <summary>
/// Service for file operations.
/// </summary>
public sealed class FileService : IFileService
{
    /// <inheritdoc/>
    public async Task<IBuffer> ReadFileAsync(StorageFile file)
    {
        return await FileIO.ReadBufferAsync(file);
    }

    /// <inheritdoc/>
    public async Task<IBuffer> ReadFileAsync(string filePath)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var buffer = new Windows.Storage.Streams.Buffer((uint)fileStream.Length);
        await using var stream = buffer.AsStream();
        await fileStream.CopyToAsync(stream);
        return buffer;
    }

    /// <inheritdoc/>
    public async Task WriteFileAsync(string filePath, IBuffer content)
    {
        await using var stream = content.AsStream();
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);

        await stream.CopyToAsync(fileStream);
    }
}
