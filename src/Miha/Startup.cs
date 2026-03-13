using Discord;
using Discord.Addons.Hosting;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Miha.Logic;
using Miha.Redis;
using Miha.Discord;
using Miha.Health;
using Miha.Shared;
using TinyHealthCheck;

namespace Miha;

public static class Startup
{
    public static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.Configure<HostOptions>(hostOptions =>
        {
            hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
        });

        services.AddClocks();
        services.AddHealthServices(context.Configuration);

        services.AddRedis(context.Configuration);

        services
            .AddDiscordOptions(context.Configuration)
            .AddDiscordServices()
            .AddDiscordHostedServices()
            .AddDiscordMessageBus();

        services
            .AddDiscordHost((config, provider) =>
            {
                var discordOptions = provider.GetRequiredService<IOptions<DiscordOptions>>().Value;

                if (string.IsNullOrEmpty(discordOptions.Token))
                {
                    throw new ArgumentNullException(nameof(discordOptions.Token),
                        "Discord token cannot be empty or null");
                }

                if (discordOptions.Guild is null)
                {
                    throw new ArgumentNullException(nameof(discordOptions.Guild),
                        "Need a target guild id, Miha does not support multi-guilds yet");
                }

                config.SocketConfig = new DiscordSocketConfig
                {
                    LogLevel = LogSeverity.Verbose,
                    LogGatewayIntentWarnings = true,
                    AlwaysDownloadUsers = true,
                    GatewayIntents = GatewayIntents.GuildScheduledEvents
                                     | GatewayIntents.DirectMessageTyping
                                     | GatewayIntents.DirectMessageReactions
                                     | GatewayIntents.DirectMessages
                                     | GatewayIntents.GuildMessageTyping
                                     | GatewayIntents.GuildMessageReactions
                                     | GatewayIntents.GuildMessages
                                     | GatewayIntents.GuildVoiceStates
                                     | GatewayIntents.Guilds
                                     | GatewayIntents.GuildMembers
                };

                config.Token = discordOptions.Token;
            });

        services
            .AddInteractionService((config, _) =>
            {
                config.LogLevel = LogSeverity.Verbose;
                config.UseCompiledLambda = true;
            });

        services
            .AddLogicServices()
            .AddBackgroundServices();
    }

    private static IServiceCollection AddHealthServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HealthOptions>().Bind(configuration.GetSection(HealthOptions.Section));

        var options = configuration.GetSection(HealthOptions.Section).Get<HealthOptions>();

        options ??= new HealthOptions();

        services.AddCustomTinyHealthCheck<MihaHealthCheck>(config =>
        {
            config.Port = options.Port;
            config.Hostname = "*";
            config.UrlPath = options.Endpoint;
            return config;
        });

        return services;
    }

    private static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        // TODO - Disabled until Birthdays are actually implemented
        //services.AddHostedService<BirthdayScannerService>();

        return services;
    }
}
