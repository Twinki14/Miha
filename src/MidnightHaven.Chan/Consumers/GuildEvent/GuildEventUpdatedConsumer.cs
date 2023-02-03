﻿using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using MidnightHaven.Chan.Helpers;
using MidnightHaven.Chan.Services.Logic.Interfaces;
using SlimMessageBus;

namespace MidnightHaven.Chan.Consumers.GuildEvent;

public class GuildEventUpdatedConsumer : IConsumer<IGuildScheduledEvent>
{
    private readonly DiscordSocketClient _client;
    private readonly IGuildSettingsService _guildSettingsService;
    private readonly ILogger<GuildEventUpdatedConsumer> _logger;

    public GuildEventUpdatedConsumer(
        DiscordSocketClient client,
        IGuildSettingsService guildSettingsService,
        ILogger<GuildEventUpdatedConsumer> logger)
    {
        _client = client;
        _guildSettingsService = guildSettingsService;
        _logger = logger;
    }

    public async Task OnHandle(IGuildScheduledEvent guildEvent)
    {
        var loggingChannel = await _guildSettingsService.GetLoggingChannelAsync(guildEvent.Guild.Id);
        if (loggingChannel.IsFailed)
        {
            _logger.LogInformation("Failed getting logging channel for guild {GuildId} {EventId}", guildEvent.Guild.Id, guildEvent.Id);
            return;
        }

        var coverImageUrl = guildEvent.CoverImageId != null ? guildEvent.GetCoverImageUrl().Replace($"/{guildEvent.Guild.Id}", "") : null;
        var location = guildEvent.Location ?? "Unknown";
        string? voiceChannel = null;

        if (location is "Unknown" && guildEvent.ChannelId.HasValue)
        {
            voiceChannel = (guildEvent as SocketGuildEvent)?.Channel.Name;
            location = "Discord";
        }

        var fields = new List<EmbedFieldBuilder>
        {
            new EmbedFieldBuilder()
                .WithName("Start time")
                .WithValue(
                    guildEvent.StartTime.ToDiscordTimestamp(TimestampTagStyles.LongDateTime) + " - " +
                    guildEvent.StartTime.ToDiscordTimestamp(TimestampTagStyles.Relative))
                .WithIsInline(false)
        };

        if (guildEvent.EndTime is not null)
        {
            fields.Add(new EmbedFieldBuilder()
                .WithName("End time")
                .WithValue(
                    guildEvent.EndTime.ToDiscordTimestamp(TimestampTagStyles.LongDateTime) + " - " +
                    guildEvent.EndTime.ToDiscordTimestamp(TimestampTagStyles.Relative))
                .WithIsInline(false));
        }

        if (voiceChannel is not null)
        {
            fields.Add(new EmbedFieldBuilder()
                .WithName("Voice channel")
                .WithValue(voiceChannel)
                .WithIsInline(false));
        }

        var embed = EmbedHelper.ScheduledEvent(
            eventVerb: "Event updated",
            eventName: guildEvent.Name,
            eventLocation: location,
            eventDescription: null,
            eventImageUrl: coverImageUrl,
            embedColor: Color.LightOrange,
            authorAvatarUrl: guildEvent.Creator is null ? _client.CurrentUser.GetAvatarUrl() : guildEvent.Creator.GetAvatarUrl(),
            authorUsername: guildEvent.Creator?.Username,
            fields: fields);

        await loggingChannel.Value.SendMessageAsync(embed: embed.Build());
    }
}
