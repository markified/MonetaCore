using System.Text.Json;
using MonetaCore.Models;

namespace MonetaCore.Services;

public interface ISystemConfigurationService
{
    Task<SystemConfigurationSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SystemConfigurationSettings settings, CancellationToken cancellationToken = default);
}

public class SystemConfigurationService : ISystemConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SystemConfigurationService(IWebHostEnvironment environment)
    {
        string appDataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataDirectory);
        _settingsPath = Path.Combine(appDataDirectory, "system-configuration.json");
    }

    public async Task<SystemConfigurationSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_settingsPath))
            {
                var defaults = new SystemConfigurationSettings();
                await WriteUnsafeAsync(defaults, cancellationToken);
                return defaults;
            }

            await using var stream = File.Open(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var settings = await JsonSerializer.DeserializeAsync<SystemConfigurationSettings>(stream, JsonOptions, cancellationToken);
            return settings ?? new SystemConfigurationSettings();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(SystemConfigurationSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await WriteUnsafeAsync(settings, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteUnsafeAsync(SystemConfigurationSettings settings, CancellationToken cancellationToken)
    {
        await using var stream = File.Open(_settingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
    }
}