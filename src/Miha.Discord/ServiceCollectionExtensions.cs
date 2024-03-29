using System.Reflection;
using Discord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Miha.Discord.Consumers;
using Miha.Discord.Consumers.GuildEvent;
using Miha.Discord.Services;
using Miha.Discord.Services.Hosted;
using Miha.Discord.Services.Interfaces;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using GuildEventMonitorService = Miha.Discord.Services.Hosted.GuildEventMonitorService;

namespace Miha.Discord;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscordOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DiscordOptions>().Bind(configuration.GetSection(DiscordOptions.Section));

        return services;
    }

    public static IServiceCollection AddDiscordServices(this IServiceCollection services)
    {
        services.AddSingleton<IGuildScheduledEventService, GuildScheduledEventService>();
        
        return services;
    }
    
    public static IServiceCollection AddDiscordHostedServices(this IServiceCollection services)
    {
        services.AddHostedService<InteractionHandler>();
        services.AddHostedService<GuildEventMonitorService>();
        services.AddHostedService<GuildEventScheduleService>();
        services.AddHostedService<SlimMessageBusService>();

        return services;
    }

    public static IServiceCollection AddDiscordMessageBus(this IServiceCollection services)
    {
        services.AddSlimMessageBus(builder =>
        {
            builder
                .Produce<IGuildScheduledEvent>(x => x.DefaultTopic("guildevent"))
                .Consume<IGuildScheduledEvent>(x => x
                    .Topic(Topics.GuildEvent.Cancelled).WithConsumer<GuildEventCancelledConsumer>())
                .Consume<IGuildScheduledEvent>(x => x
                    .Topic(Topics.GuildEvent.Created).WithConsumer<GuildEventCreatedConsumer>())
                .Consume<IGuildScheduledEvent>(x => x
                    .Topic(Topics.GuildEvent.Started).WithConsumer<GuildEventStartedConsumer>())
                .Consume<IGuildScheduledEvent>(x => x
                    .Topic(Topics.GuildEvent.Updated).WithConsumer<GuildEventUpdatedConsumer>())
                .WithProviderMemory();

            // Auto discover consumers and register inside DI container);

            builder.AddServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        return services;
    }
}
