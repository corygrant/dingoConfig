using dingoConfig.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace dingoConfig.Core.Services;

public class FileStorageService : IFileStorageService
{
    private readonly ILogger<FileStorageService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileStorageService(ILogger<FileStorageService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async Task<T?> ReadJsonAsync<T>(string filePath) where T : class
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("ReadJsonAsync called with null or empty file path");
            return null;
        }

        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("File not found: {FilePath}", filePath);
                return null;
            }

            _logger.LogDebug("Reading JSON file: {FilePath}", filePath);
            
            var json = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("File is empty: {FilePath}", filePath);
                return null;
            }

            var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            _logger.LogDebug("Successfully deserialized JSON from: {FilePath}", filePath);
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize JSON from file: {FilePath}", filePath);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied reading file: {FilePath}", filePath);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error reading file: {FilePath}", filePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading file: {FilePath}", filePath);
            return null;
        }
    }

    public async Task<bool> WriteJsonAsync<T>(T data, string filePath) where T : class
    {
        if (data == null)
        {
            _logger.LogWarning("WriteJsonAsync called with null data");
            return false;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("WriteJsonAsync called with null or empty file path");
            return false;
        }

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogDebug("Created directory: {Directory}", directory);
            }

            // Use atomic write operation with temporary file
            var tempFilePath = filePath + ".tmp";
            
            _logger.LogDebug("Writing JSON to temporary file: {TempFilePath}", tempFilePath);
            
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(tempFilePath, json);
            
            // Atomic move operation
            File.Move(tempFilePath, filePath, overwrite: true);
            
            _logger.LogDebug("Successfully wrote JSON file: {FilePath}", filePath);
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize data to JSON for file: {FilePath}", filePath);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied writing file: {FilePath}", filePath);
            return false;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Directory not found for file: {FilePath}", filePath);
            return false;
        }
        catch (PathTooLongException ex)
        {
            _logger.LogError(ex, "File path too long: {FilePath}", filePath);
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error writing file: {FilePath}", filePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error writing file: {FilePath}", filePath);
            return false;
        }
        finally
        {
            // Clean up temporary file if it exists
            var tempFilePath = filePath + ".tmp";
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temporary file: {TempFilePath}", tempFilePath);
                }
            }
        }
    }

    public async Task<bool> DeleteFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("DeleteFileAsync called with null or empty file path");
            return false;
        }

        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("File not found for deletion: {FilePath}", filePath);
                return false;
            }

            await Task.Run(() => File.Delete(filePath));
            _logger.LogDebug("Successfully deleted file: {FilePath}", filePath);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied deleting file: {FilePath}", filePath);
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error deleting file: {FilePath}", filePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting file: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            return await Task.FromResult(File.Exists(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if file exists: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<List<string>> GetFilesAsync(string directoryPath, string searchPattern = "*")
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            _logger.LogWarning("GetFilesAsync called with null or empty directory path");
            return new List<string>();
        }

        try
        {
            if (!Directory.Exists(directoryPath))
            {
                _logger.LogDebug("Directory not found: {DirectoryPath}", directoryPath);
                return new List<string>();
            }

            var files = await Task.Run(() => 
                Directory.GetFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly).ToList());
            
            _logger.LogDebug("Found {FileCount} files in directory: {DirectoryPath}", files.Count, directoryPath);
            return files;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied accessing directory: {DirectoryPath}", directoryPath);
            return new List<string>();
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Directory not found: {DirectoryPath}", directoryPath);
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting files from directory: {DirectoryPath}", directoryPath);
            return new List<string>();
        }
    }

    public async Task<bool> CreateDirectoryAsync(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            _logger.LogWarning("CreateDirectoryAsync called with null or empty directory path");
            return false;
        }

        try
        {
            if (Directory.Exists(directoryPath))
            {
                _logger.LogDebug("Directory already exists: {DirectoryPath}", directoryPath);
                return true;
            }

            await Task.Run(() => Directory.CreateDirectory(directoryPath));
            _logger.LogDebug("Successfully created directory: {DirectoryPath}", directoryPath);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied creating directory: {DirectoryPath}", directoryPath);
            return false;
        }
        catch (PathTooLongException ex)
        {
            _logger.LogError(ex, "Directory path too long: {DirectoryPath}", directoryPath);
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error creating directory: {DirectoryPath}", directoryPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating directory: {DirectoryPath}", directoryPath);
            return false;
        }
    }

    public async Task<bool> DirectoryExistsAsync(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        try
        {
            return await Task.FromResult(Directory.Exists(directoryPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if directory exists: {DirectoryPath}", directoryPath);
            return false;
        }
    }

    public async Task<string> ReadTextAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            _logger.LogDebug("Reading text file: {FilePath}", filePath);
            var content = await File.ReadAllTextAsync(filePath);
            _logger.LogDebug("Successfully read text file: {FilePath}", filePath);
            return content;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied reading file: {FilePath}", filePath);
            throw;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error reading file: {FilePath}", filePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading file: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> WriteTextAsync(string content, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("WriteTextAsync called with null or empty file path");
            return false;
        }

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogDebug("Created directory: {Directory}", directory);
            }

            _logger.LogDebug("Writing text file: {FilePath}", filePath);
            await File.WriteAllTextAsync(filePath, content ?? string.Empty);
            _logger.LogDebug("Successfully wrote text file: {FilePath}", filePath);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied writing file: {FilePath}", filePath);
            return false;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Directory not found for file: {FilePath}", filePath);
            return false;
        }
        catch (PathTooLongException ex)
        {
            _logger.LogError(ex, "File path too long: {FilePath}", filePath);
            return false;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error writing file: {FilePath}", filePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error writing file: {FilePath}", filePath);
            return false;
        }
    }
}