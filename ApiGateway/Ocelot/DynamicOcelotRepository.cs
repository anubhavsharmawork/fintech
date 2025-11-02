using Microsoft.Extensions.Options;
using Ocelot.Configuration.File;
using Ocelot.Configuration.Repository;
using Ocelot.Responses;

namespace ApiGateway.OcelotConfig;

// Simple in-memory FileConfiguration repository so we can inject dynamic downstream port
public sealed class InMemoryFileConfigRepository : IFileConfigurationRepository
{
    private FileConfiguration _config;

    public InMemoryFileConfigRepository(FileConfiguration config)
    {
        _config = config ?? new FileConfiguration();
    }

    public Task<Response<FileConfiguration>> Get()
        => Task.FromResult<Response<FileConfiguration>>(new OkResponse<FileConfiguration>(_config));

    public Task<Response> Set(FileConfiguration fileConfiguration)
    {
        _config = fileConfiguration ?? new FileConfiguration();
        return Task.FromResult<Response>(new OkResponse());
    }
}
