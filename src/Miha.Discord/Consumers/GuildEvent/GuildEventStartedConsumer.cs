using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Miha.Discord.Extensions;
using Miha.Logic.Services.Interfaces;
using SlimMessageBus;

namespace Miha.Discord.Consumers.GuildEvent;

public class GuildEventStartedConsumer(
    DiscordSocketClient client,
    IGuildService guildService,
    ILogger<GuildEventStartedConsumer> logger) : IConsumer<IGuildScheduledEvent>
{
    public async Task OnHandle(IGuildScheduledEvent guildEvent, CancellationToken cancellationToken)
    {
        var announcementRole = await guildService.GetAnnouncementRoleAsync(guildEvent.Guild.Id);
        var announcementChannel = await guildService.GetAnnouncementChannelAsync(guildEvent.Guild.Id);
        if (announcementChannel.IsFailed)
        {
            if (announcementChannel.Reasons.Any(m => m.Message == "Announcement channel not set"))
            {
                logger.LogDebug("Guild announcement channel not set {GuildId} {EventId}", guildEvent.Guild.Id, guildEvent.Id);
                return;
            }

            logger.LogInformation("Failed getting announcement channel for guild {GuildId} {EventId}", guildEvent.Guild.Id, guildEvent.Id);
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

        var fields = new List<EmbedFieldBuilder>();

        if (guildEvent.EndTime is not null)
        {
            fields.Add(new EmbedFieldBuilder()
                .WithName("Ends")
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

        var embed = new EmbedBuilder().AsScheduledEvent(
            eventVerb: "Event starting!",
            eventName: guildEvent.Name,
            eventLocation: location,
            eventDescription: guildEvent.Description,
            eventImageUrl: coverImageUrl,
            color: Color.Green,
            authorAvatarUrl: guildEvent.Creator is null ? client.CurrentUser.GetAvatarUrl() : guildEvent.Creator.GetAvatarUrl(),
            authorUsername: guildEvent.Creator?.Username,
            fields: fields);

        await announcementChannel.Value.SendMessageAsync(announcementRole.ValueOrDefault?.Mention, embed: embed.Build());
    }
}
