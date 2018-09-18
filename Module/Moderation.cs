﻿using Discord.Commands;
using Discord.WebSocket;
using Discord;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using MopsBot.Module.Preconditions;
using System.Text.RegularExpressions;
using MopsBot.Data.Tracker;
using static MopsBot.StaticBase;

namespace MopsBot.Module
{
    public class Moderation : ModuleBase
    {
        [Group("Role")]
        [RequireBotPermission(ChannelPermission.ManageRoles)]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        public class Role : ModuleBase
        {
            [Command("CreateInvite", RunMode = RunMode.Async)]
            [Summary("Creates a reaction-invite message for the specified Role.\nPeople will be able to invite themselves into the role.")]
            [RequireBotPermission(ChannelPermission.AddReactions)]
            [RequireBotPermission(ChannelPermission.ManageMessages)]
            [RequireBotPermission(ChannelPermission.ReadMessageHistory)]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            public async Task createInvite(SocketRole role, bool isGerman = false)
            {
                var highestRole = ((SocketGuildUser)await Context.Guild.GetCurrentUserAsync()).Roles.OrderByDescending(x => x.Position).First();

                if (role != null && role.Position < highestRole.Position)
                    if (isGerman)
                        await StaticBase.ReactRoleJoin.AddInviteGerman((ITextChannel)Context.Channel, role);
                    else
                        await StaticBase.ReactRoleJoin.AddInvite((ITextChannel)Context.Channel, role);
                else
                    await ReplyAsync($"**Error**: Role `{role.Name}` could either not be found, or was beyond Mops' permissions.");
            }

            [Command("AddToUser")]
            [Summary("Adds the specified role, to the specified user, for the specified amount of time.")]
            [RequireUserPermission(GuildPermission.ManageRoles)]
            public async Task joinRole(SocketGuildUser person, int durationInMinutes, [Remainder]string role)
            {
                var highestRole = ((SocketGuildUser)await Context.Guild.GetCurrentUserAsync()).Roles.OrderByDescending(x => x.Position).First();
                var requestedRole = Context.Guild.Roles.FirstOrDefault(x => x.Name.ToLower().Equals(role.ToLower()));

                if (requestedRole == null || requestedRole.Position >= highestRole.Position)
                {
                    await ReplyAsync($"**Error**: Role `{role}` could either not be found, or was beyond Mops' permissions.");
                    return;
                }
                await StaticBase.MuteHandler.AddMute(person, Context.Guild.Id, durationInMinutes, role);
                await ReplyAsync($"``{role}`` Role added to ``{person.Username}`` for **{durationInMinutes}** minutes.");
            }
        }

        [Command("poll", RunMode = RunMode.Async), Summary("Creates a poll\nExample: !poll \"What should I play\" \"Dark Souls\" \"Osu!\" \"WoW\"")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        [RequireBotPermission(ChannelPermission.AddReactions)]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        [RequireBotPermission(ChannelPermission.ReadMessageHistory)]
        public async Task Poll(string title, params string[] options)
        {
            if (options.Length <= 10)
            {
                Data.Poll poll = new Data.Poll(title, options);
                await StaticBase.Poll.AddPoll((ITextChannel)Context.Channel, poll);
            }
            else
                await ReplyAsync("Can't have more than 10 options per poll.");
        }

        [Group("Giveaway")]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        [RequireBotPermission(ChannelPermission.AddReactions)]
        [RequireBotPermission(ChannelPermission.ManageMessages)]
        [RequireBotPermission(ChannelPermission.ReadMessageHistory)]
        public class Giveaway : ModuleBase
        {
            [Command("create", RunMode = RunMode.Async)]
            [Summary("Creates giveaway.")]
            public async Task create([Remainder]string game)
            {
                await ReactGiveaways.AddGiveaway(Context.Channel, game, Context.User);
            }
        }

        [Command("setPrefix")]
        [Summary("Changes the prefix of Mops in the current Guild")]
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        public async Task setPrefix([Remainder]string prefix)
        {
            if (prefix.StartsWith("?"))
            {
                await ReplyAsync($"`?` is required for Mops functionality. Cannot change prefix to `{prefix}`");
                return;
            }

            string oldPrefix;

            if (GuildPrefix.ContainsKey(Context.Guild.Id))
            {
                oldPrefix = GuildPrefix[Context.Guild.Id];
                GuildPrefix[Context.Guild.Id] = prefix;
            }

            else
            {
                oldPrefix = "!";
                GuildPrefix.Add(Context.Guild.Id, prefix);
            }

            savePrefix();

            await ReplyAsync($"Changed prefix from `{oldPrefix}` to `{prefix}`");
        }

        [Command("CreateCommand")]
        [Summary("Allows you to create a simple response command.\n" +
                 "Name of user: {User.Username}\n" +
                 "Mention of user: {User.Mention}")]
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        public async Task CreateCommand(string command, [Remainder] string responseText)
        {
            if (!StaticBase.CustomCommands.ContainsKey(Context.Guild.Id))
            {
                StaticBase.CustomCommands.Add(Context.Guild.Id, new Dictionary<string, string>());
            }

            if (!StaticBase.CustomCommands[Context.Guild.Id].ContainsKey(command))
            {
                StaticBase.CustomCommands[Context.Guild.Id].Add(command, responseText);
                await ReplyAsync($"Added new command **{command}**.");
            }

            else
            {
                StaticBase.CustomCommands[Context.Guild.Id][command] = responseText;
                await ReplyAsync($"Replaced command **{command}**.");
            }

            StaticBase.saveCommand();
        }

        [Command("RemoveCommand")]
        [Summary("Removes the specified custom command.")]
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        public async Task RemoveCommand(string command)
        {
            if (StaticBase.CustomCommands[Context.Guild.Id].ContainsKey(command))
            {
                if (StaticBase.CustomCommands[Context.Guild.Id].Count == 1)
                    StaticBase.CustomCommands.Remove(Context.Guild.Id);
                else
                    StaticBase.CustomCommands[Context.Guild.Id].Remove(command);

                StaticBase.saveCommand();
                await ReplyAsync($"Removed command **{command}**.");
            }
            else
            {
                await ReplyAsync($"Command **{command}** not found.");
            }
        }

        /*[Command("UseCustomCommand", RunMode = RunMode.Async)]
        [Hide()]
        public async Task UseCustomCommand(string command){
            var script = CSharpScript.Create($"return $\"{StaticBase.CustomCommands[Context.Guild.Id][command]}\";", globalsType: typeof(CustomContext));
            var result = await script.RunAsync(new CustomContext {User = Context.User});
            await ReplyAsync(result.ReturnValue.ToString());
        }*/

        [Command("UseCustomCommand", RunMode = RunMode.Async)]
        [Hide()]
        public async Task UseCustomCommand(string command)
        {
            var reply = StaticBase.CustomCommands[Context.Guild.Id][command];
            reply = reply.Replace("{User.Username}", $"{Context.User.Username}")
                         .Replace("{User.Mention}", $"{Context.User.Mention}");
            await ReplyAsync(reply);
        }

        [Command("kill")]
        // [Summary("Stops Mops to adapt to any new changes in code.")]
        [RequireBotManage()]
        [Hide]
        public Task kill()
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }

        [Command("openfiles", RunMode = RunMode.Async)]
        [RequireBotManage()]
        [Hide]
        public async Task openfiles()
        {
            await ReplyAsync(DateTime.Now + $" open files were {System.Diagnostics.Process.GetCurrentProcess().HandleCount}");
        }

        [Command("eval", RunMode = RunMode.Async)]
        [RequireBotManage()]
        [Hide]
        public async Task eval([Remainder]string expression)
        {
            try
            {
                var imports = Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default.WithReferences(typeof(MopsBot.Program).Assembly, typeof(Discord.Attachment).Assembly).WithImports("MopsBot", "Discord");
                var preCompilationTime = DateTime.Now.Ticks / 10000;
                var script = CSharpScript.Create(expression, globalsType: typeof(MopsBot.Module.Moderation)).WithOptions(imports);
                script.Compile();
                var preExecutionTime = DateTime.Now.Ticks / 10000;
                var result = await script.RunAsync(this);
                var postExecutionTime = DateTime.Now.Ticks / 10000;

                var embed = new EmbedBuilder();
                embed.Author = new EmbedAuthorBuilder().WithName(Context.User.Username).WithIconUrl(Context.User.GetAvatarUrl());
                embed.WithDescription($"```csharp\n{expression}```").WithTitle("Evaluation of code");
                embed.AddField("Compilation time", $"{preExecutionTime - preCompilationTime}ms", true);
                embed.AddField("Execution time", $"{postExecutionTime - preExecutionTime}ms", true);
                embed.AddField("Return value", result.ReturnValue?.ToString() ?? "`null or void`");

                await ReplyAsync("", embed: embed.Build());
            }
            catch (Exception e)
            {
                await ReplyAsync("**Error:** " + e.Message);
            }
        }

        [Command("help")]
        [Hide]
        public async Task help(string helpModule = null)
        {
            EmbedBuilder e = new EmbedBuilder();
            e.WithDescription("For more information regarding a **specific command**, please use **?<command>**\n" +
                              "To see the commands of a **submodule\\***, please use **help <submodule>**.")
             .WithColor(Discord.Color.Blue);

            if (helpModule == null)
            {
                foreach (var module in Program.Handler.commands.Modules.Where(x => !x.Preconditions.OfType<HideAttribute>().Any()))
                {
                    if (!module.IsSubmodule)
                    {
                        string moduleInformation = "";
                        moduleInformation += string.Join(", ", module.Commands.Where(x => !x.Preconditions.OfType<HideAttribute>().Any()).Select(x => $"[{x.Name}]({CommandHandler.GetCommandHelpImage(x.Name)})"));
                        moduleInformation += "\n";

                        moduleInformation += string.Join(", ", module.Submodules.Select(x => $"[{x.Name}\\*]({CommandHandler.GetCommandHelpImage(x.Name)})"));

                        e.AddField($"**{module.Name}**", moduleInformation);
                    }
                }

                if (StaticBase.CustomCommands.ContainsKey(Context.Guild.Id))
                {
                    e.AddField("**Custom Commands**", string.Join(", ", StaticBase.CustomCommands.Where(x => x.Key == Context.Guild.Id).First().Value.Select(x => $"`{x.Key}`")));
                }
            }
            else
            {
                var module = Program.Handler.commands.Modules.First(x => x.Name.ToLower().Equals(helpModule.ToLower()));
                
                string moduleInformation = "";
                moduleInformation += string.Join(", ", module.Commands.Where(x => !x.Preconditions.OfType<HideAttribute>().Any()).Select(x => $"[{x.Name}]({CommandHandler.GetCommandHelpImage($"{module.Name} {x.Name}")})"));
                moduleInformation += "\n";

                moduleInformation += string.Join(", ", module.Submodules.Select(x => $"{x.Name}\\*"));

                e.AddField($"**{module.Name}**", moduleInformation);
            }

            await ReplyAsync("", embed: e.Build());
        }
    }

    public class CustomContext
    {
        public IUser User;
    }
}
