using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Miha.Discord.Extensions;
using Miha.Logic.Services.Interfaces;
using SlimMessageBus;

namespace Miha.Discord.Consumers.GuildEvent;

public class GuildEventCancelledConsumer(
    DiscordSocketClient client,
    IGuildService guildService,
    ILogger<GuildEventCancelledConsumer> logger) : IConsumer<IGuildScheduledEvent>
{
    public async Task OnHandle(IGuildScheduledEvent guildEvent, CancellationToken cancellationToken)
    {
        var loggingChannel = await guildService.GetLoggingChannelAsync(guildEvent.Guild.Id);
        if (loggingChannel.IsFailed)
        {
            if (loggingChannel.Reasons.Any(m => m.Message == "Logging channel not set"))
            {
                logger.LogDebug("Guild logging channel not set {GuildId} {EventId}", guildEvent.Guild.Id, guildEvent.Id);
                return;
            }

            logger.LogInformation("Failed getting logging channel for guild {GuildId} {EventId}", guildEvent.Guild.Id, guildEvent.Id);
            return;
        }

        var location = guildEvent.Location ?? "Unknown";

        if (location is "Unknown" && guildEvent.ChannelId.HasValue)
        {
            location = "Discord";
        }

        var embed = new EmbedBuilder().AsScheduledEvent(
            eventVerb: "Event cancelled",
            eventName: guildEvent.Name,
            eventLocation: location,
            eventDescription: null,
            color: Color.Red,
            authorAvatarUrl: guildEvent.Creator is null ? client.CurrentUser.GetAvatarUrl() : guildEvent.Creator.GetAvatarUrl(),
            authorUsername: guildEvent.Creator?.Username);

        await loggingChannel.Value.SendMessageAsync(embed: embed.Build());
    }
}
