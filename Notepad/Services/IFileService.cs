using Windows.Storage;
using Windows.Storage.Streams;

namespace Notepad.Services;

/// <summary>
/// Service interface for file operations.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Reads the content of a file as an IBuffer.
    /// </summary>
    /// <param name="file">The file to read.</param>
    /// <returns>The file content as an IBuffer.</returns>
    Task<IBuffer> ReadFileAsync(StorageFile file);

    /// <summary>
    /// Reads the content of a file by path as an IBuffer.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>The file content as an IBuffer.</returns>
    Task<IBuffer> ReadFileAsync(string filePath);

    /// <summary>
    /// Writes an IBuffer to a file.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="content">The IBuffer to write.</param>
    Task WriteFileAsync(string filePath, IBuffer content);
}
