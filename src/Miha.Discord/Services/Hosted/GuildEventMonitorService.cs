using Cronos;
using Discord;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Util;
using Discord.WebSocket;
using Humanizer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Miha.Discord.Extensions;
using Miha.Logic.Services.Interfaces;
using Miha.Shared.ZonedClocks.Interfaces;
using Newtonsoft.Json;

namespace Miha.Discord.Services.Hosted;

public partial class GuildEventMonitorService(
    DiscordSocketClient client,
    IGuildService guildService,
    IEasternStandardZonedClock easternStandardZonedClock,
    IOptions<DiscordOptions> discordOptions,
    ILogger<GuildEventMonitorService> logger) : DiscordClientService(client, logger)
{
    private readonly DiscordOptions _discordOptions = discordOptions.Value;

    private const string Schedule = "0,5,10,15,20,25,30,35,40,45,50,55 ? * * *"; // https://crontab.cronhub.io/

    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 32 });
    private readonly MemoryCacheEntryOptions _memoryCacheEntryOptions = new MemoryCacheEntryOptions()
        .SetSize(1)
        .SetAbsoluteExpiration(TimeSpan.FromMinutes(25))
        .SetSlidingExpiration(TimeSpan.FromMinutes(15));

    private readonly CronExpression _cron = CronExpression.Parse(Schedule, CronFormat.Standard);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Waiting for client to be ready...");

        await Client.WaitForReadyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckScheduledEventsAsync();

                var utcNow = easternStandardZonedClock.GetCurrentInstant().ToDateTimeUtc();
                var nextUtc = _cron.GetNextOccurrence(DateTimeOffset.UtcNow, easternStandardZonedClock.GetTimeZoneInfo());

                if (nextUtc is null)
                {
                    logger.LogWarning("Next utc occurence is null");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                var next = nextUtc.Value - utcNow;

                logger.LogDebug("Waiting {Time} until next operation", next.Humanize(3));

                await Task.Delay(nextUtc.Value - utcNow, stoppingToken);
            }
            catch (Exception e)
            {
                LogExceptionInBackgroundServiceLoop(e);

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task CheckScheduledEventsAsync()
    {
        SocketGuild guild;

        try
        {
            guild = Client.GetGuild(_discordOptions.Guild!.Value);
            if (guild is null)
            {
                logger.LogCritical("Guild is null {GuildId}", _discordOptions.Guild.Value);
                return;
            }
        }
        catch (Exception e)
        {
            LogError(e);
            return;
        }

        foreach (var guildEvent in guild.Events)
        {
            try
            {
                var announcementChannel = await guildService.GetAnnouncementChannelAsync(guild.Id);
                if (announcementChannel.IsFailed)
                {
                    if (announcementChannel.Reasons.Any(m => m.Message == "Announcement channel not set"))
                    {
                        logger.LogDebug("Guild announcement channel not set {GuildId} {EventId}", guildEvent.Guild.Id, guildEvent.Id);
                        return;
                    }

                    logger.LogInformation("Failed getting announcement channel for guild {GuildId} {EventId}", guild.Id, guildEvent.Id);
                    continue;
                }

                var startsIn = guildEvent.StartTime - DateTime.UtcNow;

                // "Round" our minutes up
                if (startsIn.Seconds <= 60)
                {
                    startsIn = new TimeSpan(startsIn.Days, startsIn.Hours, startsIn.Minutes + 1, 0);
                }

                logger.LogDebug("GuildEvent {EventId} starts in (rounded) {StartsInTotalMinutes}", guildEvent.Id, startsIn.TotalMinutes);

                if (startsIn.TotalMinutes is < 5 or > 20 || _memoryCache.TryGetValue(guildEvent.Id, out bool notified) && notified)
                {
                    logger.LogDebug("GuildEvent {EventId} starts too soon or is already notified", guildEvent.Id);
                    continue;
                }

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("GuildEvent {EventId} {GuildEventJson}", guildEvent.Id, JsonConvert.SerializeObject(new
                    {
                        guildEvent.StartTime,
                        guildEvent.Id,
                        guildEvent.EndTime,
                        creator = guildEvent.Creator is null ? "null" : "[ ]",
                        location = guildEvent.Location is null ? "null" : "[ ]",
                        channel = guildEvent.Channel is null ? "null" : "[ ]",
                        guildEvent.Status
                    }, new JsonSerializerSettings
                    {
                        MaxDepth = 1,
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }));
                }

                if (guildEvent.Status is GuildScheduledEventStatus.Active)
                {
                    _memoryCache.Set(guildEvent.Id, true, _memoryCacheEntryOptions);
                    continue;
                }

                var location = guildEvent.Location ?? "Unknown";
                string? voiceChannel = null;

                if (location is "Unknown" && guildEvent.Channel is not null)
                {
                    voiceChannel = guildEvent.Channel.Name;
                    location = "Discord";
                }

                var fields = new List<EmbedFieldBuilder>();
                if (voiceChannel is not null)
                {
                    fields.Add(new EmbedFieldBuilder()
                        .WithName("Voice channel")
                        .WithValue(voiceChannel)
                        .WithIsInline(false));
                }
                else if (!string.IsNullOrEmpty(guildEvent.Creator?.Username))
                {
                    fields.Add(new EmbedFieldBuilder()
                        .WithName("Hosted by")
                        .WithValue(guildEvent.Creator.Username)
                        .WithIsInline(false));
                }

                var embed = new EmbedBuilder().AsScheduledEvent(
                    eventVerb: "Event is starting soon!",
                    eventName: guildEvent.Name + " - " + guildEvent.StartTime.ToDiscordTimestamp(TimestampTagStyles.Relative),
                    eventLocation: location,
                    eventDescription: string.Empty,
                    color: Color.DarkBlue,
                    authorAvatarUrl: guildEvent.Creator is null ? Client.CurrentUser.GetAvatarUrl() : guildEvent.Creator.GetAvatarUrl(),
                    authorUsername: guildEvent.Creator?.Username,
                    fields: fields);

                await announcementChannel.Value.SendMessageAsync(embed: embed.Build());

                _memoryCache.Set(guildEvent.Id, true, _memoryCacheEntryOptions);
            }
            catch (Exception e)
            {
                LogError(e, guildEvent.Id);
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Exception occurred in GuildEventMonitorService")]
    public partial void LogError(Exception e);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Exception occurred in GuildEventMonitorService during guildEvent {guildEventId}")]
    public partial void LogError(Exception e, ulong guildEventId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Exception occurred in background service loop")]
    public partial void LogExceptionInBackgroundServiceLoop(Exception e);
}
