﻿using Discord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ramiel.Bot;
using Ramiel.Bot.Services;
using Ramiel.Discord;
using Victoria;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, builder) =>
    {
        builder.Sources.Clear();

        builder.AddEnvironmentVariables();
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.Configure<BotConfiguration>(hostContext.Configuration);

        services.AddDiscordServices(config =>
        {
            config.DiscordSocket.GatewayIntents = GatewayIntents.GuildVoiceStates | GatewayIntents.GuildMembers | GatewayIntents.Guilds;
        });

        var botConfiguration = hostContext.Configuration.Get<BotConfiguration>();

        services.AddLavaNode(config =>
        {
            config.SelfDeaf = true;
            config.Hostname = botConfiguration.LavalinkHostname;
            config.Port = botConfiguration.LavalinkPort;
            config.Authorization = botConfiguration.LavalinkPassword;
            config.SocketConfiguration.BufferSize = 1024;
        });

        services.AddSingleton<MusicService>();

        services.AddHostedService<DiscordHostedService>();
    })
    .Build();

await host.RunAsync();