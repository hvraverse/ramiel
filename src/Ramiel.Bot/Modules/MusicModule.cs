﻿using Discord;
using Discord.Interactions;
using Ramiel.Bot.Attributes;
using Ramiel.Bot.Models;
using Ramiel.Bot.Services;
using System.Text;
using Victoria.Player;

namespace Ramiel.Bot.Modules
{
    [Group("music", "Music commands")]
    [UserVoiceChannelRequired]
    public class MusicModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly MusicService _musicService;

        public MusicModule(MusicService musicService)
        {
            _musicService = musicService;
        }

        [SlashCommand("play", "Play audio from a youtube video or playlist")]
        public async Task PlayAsync([Summary(description: "YouTube video or playlist URL")] string url)
        {
            if (!_musicService.IsPlaying(Context.Guild))
            {
                var voiceState = Context.User as IVoiceState;
                if (voiceState?.VoiceChannel == null)
                {
                    await RespondAsync("You must be connected to a voice channel!");
                    return;
                }

                await _musicService.JoinAsync(voiceState, Context.Channel as ITextChannel);
            }

            var statusMessage = await _musicService.PlayAsync(Context.Guild, Context.Channel as ITextChannel, url);

            await RespondAsync(statusMessage);
        }

        [SlashCommand("stop", "Stop playback")]
        public async Task StopAsync()
        {
            if (!_musicService.IsPlaying(Context.Guild))
            {
                await RespondAsync("Try playing something first!");
                return;
            }

            await _musicService.StopAsync(Context.Guild);

            await RespondAsync("Paused playback");
        }

        [SlashCommand("resume", "Resume playback")]
        public async Task ResumeAsync()
        {
            if (!_musicService.IsPlaying(Context.Guild))
            {
                await RespondAsync("Try playing something first!");
                return;
            }

            await _musicService.ResumeAsync(Context.Guild);

            await RespondAsync("Resumed playback");
        }

        [SlashCommand("queue", "Get queue")]
        public async Task QueueAsync()
        {
            if (!_musicService.IsPlaying(Context.Guild))
            {
                await RespondAsync("Try playing something first!");
                return;
            }

            var queue = await _musicService.QueueAsync(Context.Guild);
            var nowPlaying = await _musicService.NowPlayingAsync(Context.Guild);

            if (!queue.Any() && nowPlaying == null)
            {
                await RespondAsync("Queue empty! Add some music!");
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine("__Now Playing:__");
            sb.AppendLine(GetQueueTrackString(nowPlaying));

            if (queue.Count() > 0)
            {
                sb.AppendLine();
                sb.AppendLine("__Up Next:__");

                var cap = 10;
                if (queue.Count() < 10)
                {
                    cap = queue.Count();
                }

                for (var i = 0; i < cap; i++)
                {
                    sb.AppendLine($"`{i + 1}.` {GetQueueTrackString(queue.ElementAt(i))}");
                }
            }

            var loopDescription = "";
            var loopType = _musicService.GetLoopType(Context.Guild);
            if (loopType != LoopTypeEnum.Off)
            {
                loopDescription = $" | 🔁[{loopType}] Enabled";
            }

            var playerDuration = _musicService.GetPlayerDuration(Context.Guild);
            sb.AppendLine();
            sb.AppendLine($"**{queue.Count()} tracks in queue | {GetDurationString(playerDuration)} total length{loopDescription}**");

            var embed = new EmbedBuilder()
                .WithTitle($"Queue for {Context.Guild.Name}")
                .WithColor(0x0345fc)
                .WithDescription(sb.ToString());

            await RespondAsync(embed: embed.Build());
        }

        [SlashCommand("shuffle", "Shuffle queue")]
        public async Task ShuffleAsync()
        {
            if (!_musicService.IsPlaying(Context.Guild))
            {
                await RespondAsync("Try playing something first!");
                return;
            }

            await _musicService.ShuffleAsync(Context.Guild);

            await RespondAsync("Tracks shuffled!");
        }

        [SlashCommand("skip", "Skip playing item")]
        public async Task SkipAsync()
        {
            if (!_musicService.IsPlaying(Context.Guild))
            {
                await RespondAsync("Try playing something first!");
                return;
            }

            await _musicService.SkipAsync(Context.Guild);

            await RespondAsync("Skipped track!");
        }

        [SlashCommand("clear", "Clear queue")]
        public async Task ClearAsync()
        {
            if (!_musicService.IsPlaying(Context.Guild))
            {
                await RespondAsync("Try playing something first!");
                return;
            }

            await _musicService.ClearAsync(Context.Guild);

            await RespondAsync("Cleared queue");
        }

        [SlashCommand("nowplaying", "See information on playing item")]
        public async Task NowPlayingAsync()
        {
            if (!_musicService.IsPlaying(Context.Guild))
            {
                await RespondAsync("Try playing something first!");
                return;
            }

            var track = await _musicService.NowPlayingAsync(Context.Guild);
            if (track == null)
            {
                await RespondAsync("Try playing something first!");
                return;
            }

            var sb = new StringBuilder();

            sb.Append($"{GetDurationString(track.Position)} `");

            var segLength = track.Duration / 30;
            var seekPosition = (int)Math.Round(track.Position.TotalSeconds / segLength.TotalSeconds);

            for (var i = 0; i <= 30; i++)
            {
                sb.Append(i == seekPosition ? "🔘" : "▬");
            }

            sb.Append($"` {GetDurationString(track.Duration)}");

            var embed = new EmbedBuilder()
                .WithAuthor("Now Playing 🎵")
                .WithTitle(track.Title)
                .WithUrl(track.Url)
                .WithThumbnailUrl($"https://i.ytimg.com/vi/{track.Id}/hqdefault.jpg")
                .WithColor(0x0345fc)
                .WithDescription(sb.ToString());

            await RespondAsync(embed: embed.Build());
        }

        [SlashCommand("disconnect", "Disconnect from the voice channel")]
        public async Task DisconnectAsync()
        {
            if (!_musicService.IsPlaying(Context.Guild))
            {
                await RespondAsync("Try playing something first!");
                return;
            }

            await RespondAsync("See ya!");

            await _musicService.DisconnectAsync(Context.Guild);
        }

        [SlashCommand("removeduplicates", "Remove duplicate items from queue")]
        public async Task RemoveDuplicatesAsync()
        {
            if (!_musicService.IsPlaying(Context.Guild))
            {
                await RespondAsync("Try playing something first!");
                return;
            }

            await _musicService.RemoveDuplicatesAsync(Context.Guild);

            await RespondAsync("Duplicate tracks removed");
        }

        [SlashCommand("loop", "Toggle track looping")]
        public async Task LoopAsync(LoopTypeEnum loopType)
        {
            if (!_musicService.IsPlaying(Context.Guild))
            {
                await RespondAsync("Try playing something first!");
                return;
            }

            await _musicService.LoopAsync(Context.Guild, loopType);

            await RespondAsync($"Looping set to {loopType}");
        }

        private static string GetDurationString(TimeSpan duration)
        {
            var d = TimeSpan.FromSeconds(duration.TotalSeconds);

            var sb = new StringBuilder();
            if (d.Hours > 0)
            {
                sb.Append($"{d.Hours}:{d.Minutes:D2}");
            }
            else
            {
                sb.Append($"{d.Minutes}");
            }
            sb.Append($":{d.Seconds:D2}");

            return sb.ToString();
        }

        private static string GetQueueTrackString(LavaTrack track)
        {
            return $"[{track.Title}]({track.Url}) | `{GetDurationString(track.Duration)}`";
        }
    }
}
