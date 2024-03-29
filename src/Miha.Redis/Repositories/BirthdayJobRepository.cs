using Miha.Redis.Documents;
using Miha.Redis.Repositories.Interfaces;
using Redis.OM.Contracts;

namespace Miha.Redis.Repositories;

public class BirthdayJobRepository(IRedisConnectionProvider provider) : DocumentRepository<BirthdayJobDocument>(provider), IBirthdayJobRepository;
