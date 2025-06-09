namespace dingoConfig.Core.Interfaces;

public interface IFileStorageService
{
    Task<T?> ReadJsonAsync<T>(string filePath) where T : class;
    Task<bool> WriteJsonAsync<T>(T data, string filePath) where T : class;
    Task<bool> DeleteFileAsync(string filePath);
    Task<bool> FileExistsAsync(string filePath);
    Task<List<string>> GetFilesAsync(string directoryPath, string searchPattern = "*");
    Task<bool> CreateDirectoryAsync(string directoryPath);
    Task<bool> DirectoryExistsAsync(string directoryPath);
    Task<string> ReadTextAsync(string filePath);
    Task<bool> WriteTextAsync(string content, string filePath);
}