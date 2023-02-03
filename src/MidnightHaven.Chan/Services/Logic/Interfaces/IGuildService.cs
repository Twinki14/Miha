﻿using Discord;
using FluentResults;
using MidnightHaven.Redis.Documents;

namespace MidnightHaven.Chan.Services.Logic.Interfaces;

public interface IGuildService
{
    Task<Result<GuildDocument?>> GetAsync(ulong? guildId);
    Task<Result<GuildDocument?>> UpsertAsync(GuildDocument document);
    Task<Result<GuildDocument?>> UpsertAsync(ulong? guildId, Action<GuildDocument> optionsFunc);
    Task<Result> DeleteAsync(ulong? guildId);

    Task<Result<ITextChannel>> GetLoggingChannelAsync(ulong? guildId);
    Task<Result<ITextChannel>> GetAnnouncementChannelAsync(ulong? guildId);
    Task<Result<IRole>> GetAnnouncementRoleAsync(ulong? guildId);
}
