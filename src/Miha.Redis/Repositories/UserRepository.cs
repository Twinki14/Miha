using Microsoft.Extensions.Logging;
using Miha.Redis.Documents;
using Miha.Redis.Repositories.Interfaces;
using Redis.OM;
using Redis.OM.Contracts;

namespace Miha.Redis.Repositories;

public partial class UserRepository(
    IRedisConnectionProvider provider,
    ILogger<UserRepository> logger)
    : DocumentRepository<UserDocument>(provider), IUserRepository
{
    private readonly IRedisConnectionProvider _provider = provider;

    public async Task<IEnumerable<UserDocument>> GetAllUsersWithBirthdayEnabledAsync()
    {
        var collection = _provider.RedisCollection<UserDocument>();

        return await collection.Where(user => user.EnableBirthday).ToListAsync();
    }

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Exception caught")]
    public partial void LogErrorException(Exception ex);
}
