using System.CommandLine;
using NicheImageRipper.Core.DataStructures;
using NicheImageRipper.Core.Enums;
using NicheImageRipper.Core.Utility;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.Configuration;
using NicheImageRipper.Sdk.Features;
using NicheImageRipper.Sdk.Utility;
using Serilog.Events;

namespace NicheImageRipper.Tui;

/// <summary>
/// TUI/CLI front end. With no args, runs as an interactive REPL. With args, parses and executes a
/// single command then exits — unless <c>--interactive</c>/<c>-i</c> is also given, in which case it
/// runs that command first and then drops into the REPL. Both modes share one System.CommandLine
/// command tree, defined once in <see cref="BuildRootCommand"/>.
/// </summary>
public class NicheImageRipperCli : NicheImageRipper.Core.NicheImageRipper
{
    private const string InteractiveFlagLong = "--interactive";
    private const string InteractiveFlagShort = "-i";

    private CancellationToken _cancellationToken;

    public async Task Run(string[] args, CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        WarnMissingFeatures();

        var root = BuildRootCommand();
        var knownCommands = root.Subcommands
            .SelectMany(c => new[] { c.Name }.Concat(c.Aliases))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (args.Length == 0)
        {
            await RunRepl(root, knownCommands);
            return;
        }

        var dropToRepl = args.Any(a => a is InteractiveFlagLong or InteractiveFlagShort);
        var commandArgs = args.Where(a => a is not (InteractiveFlagLong or InteractiveFlagShort)).ToArray();

        if (commandArgs.Length > 0)
        {
            await ExecuteLine(root, knownCommands, commandArgs);
        }

        if (dropToRepl)
        {
            await RunRepl(root, knownCommands);
        }
    }

    private void WarnMissingFeatures()
    {
        if (!AvailableFeatures.HasFeature(FeatureKeys.Ffmpeg))
        {
            Logger.Warning("ffmpeg not found. Some functionality may be limited.");
        }

        if (!AvailableFeatures.HasFeature(FeatureKeys.YtDlp))
        {
            Logger.Warning("yt-dlp not found. Some functionality may be limited.");
        }

        if (!AvailableFeatures.HasFeature(FeatureKeys.MegaCmd))
        {
            Logger.Warning("MEGAcmd not found. Some functionality may be limited.");
        }

        if (!AvailableFeatures.HasFeature(FeatureKeys.FlareSolverr))
        {
            Logger.Warning("FlareSolverr not found. Some functionality may be limited.");
        }
    }

    private async Task RunRepl(RootCommand root, IReadOnlySet<string> knownCommands)
    {
        while (true)
        {
            try
            {
                LogMessageToFile($"{Title}> ", newLine: false);
                var userInput = Console.ReadLine();
                if (string.IsNullOrEmpty(userInput))
                {
                    continue;
                }

                var tokens = userInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                {
                    continue;
                }

                if (tokens[0] is "q" or "quit")
                {
                    LogMessageToFile("Exiting...");
                    await SaveData(_cancellationToken);
                    return;
                }

                await ExecuteLine(root, knownCommands, tokens);
            }
            catch (Exception e)
            {
                Logger.Error(e, "An unhanded exception occurred");
            }
        }
    }

    /// <summary>
    /// Runs one tokenized command line, whether it came from process args or a REPL line. If the
    /// first token isn't a recognized command, the whole line is treated as URL(s) to queue — same
    /// default behavior the original hand-rolled switch had.
    /// </summary>
    private async Task ExecuteLine(RootCommand root, IReadOnlySet<string> knownCommands, string[] tokens)
    {
        if (!knownCommands.Contains(tokens[0]))
        {
            var rejected = QueueUrls(string.Join(" ", tokens));
            HandleRejectedUrls(rejected);
            return;
        }

        await root.Parse(tokens).InvokeAsync(cancellationToken: _cancellationToken);
    }

    private RootCommand BuildRootCommand()
    {
        var root = new RootCommand("NicheImageRipper");

        root.Subcommands.Add(BuildBooruCommand());
        root.Subcommands.Add(BuildClearCommand());
        root.Subcommands.Add(BuildConfigCommand());
        root.Subcommands.Add(BuildDebugCommand());
        root.Subcommands.Add(BuildDelayCommand());
        root.Subcommands.Add(BuildHelpCommand());
        root.Subcommands.Add(BuildHistoryCommand());
        root.Subcommands.Add(BuildLoadCommand());
        root.Subcommands.Add(BuildListCommand());
        root.Subcommands.Add(BuildLookupCommand());
        root.Subcommands.Add(BuildMergeCommand());
        root.Subcommands.Add(BuildPeekCommand());
        root.Subcommands.Add(BuildQueueCommand());
        root.Subcommands.Add(BuildRipCommand());
        root.Subcommands.Add(BuildRegenCommand());
        root.Subcommands.Add(BuildRetriesCommand());
        root.Subcommands.Add(BuildSaveCommand());
        root.Subcommands.Add(BuildSkipCommand());
        root.Subcommands.Add(BuildTailCommand());
        #if DEBUG
        root.Subcommands.Add(BuildTestCommand());
        #endif
        root.Subcommands.Add(BuildVersionCommand());

        return root;
    }

    // ---- Command definitions ----

    private Command BuildBooruCommand()
    {
        var tagsArg = new Argument<string>("tags");
        var cmd = new Command("booru", "Queue a booru.com search by tags") { tagsArg };
        cmd.SetAction(parseResult =>
        {
            var tags = parseResult.GetValue(tagsArg);
            var url = "https://booru.com/post?tags=" + tags;
            var rejected = QueueUrls(url);
            HandleRejectedUrls(rejected);
        });
        return cmd;
    }

    private Command BuildClearCommand()
    {
        var cmd = new Command("clear", "Clear the cache or the URL queue");
        cmd.Aliases.Add("c");

        var cacheCmd = new Command("cache", "Clear the partial-save cache");
        cacheCmd.SetAction(_ =>
        {
            ClearCache();
            LogMessageToFile("Cache cleared");
        });

        var queueCmd = new Command("queue", "Clear the URL queue");
        queueCmd.SetAction(_ =>
        {
            UrlQueue.Clear();
            LogMessageToFile("Queue cleared");
        });

        cmd.Subcommands.Add(cacheCmd);
        cmd.Subcommands.Add(queueCmd);
        return cmd;
    }

    private Command BuildConfigCommand()
    {
        var cmd = new Command("config", "Get or set a config value");

        var pathArg = new Argument<string?>("path") { Arity = ArgumentArity.ZeroOrOne };
        var savePathCmd = new Command("savepath", "Get or set the save path") { pathArg };
        savePathCmd.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathArg);
            if (path is null)
            {
                LogMessageToFile($"Save path: {SavePath}");
                return;
            }

            if (FileUtility.IsValidAndEnsureDirectory(path))
            {
                SavePath = path;
                LogMessageToFile($"Save path set to {SavePath}");
            }
            else
            {
                LogMessageToFile("Invalid path", LogEventLevel.Warning);
            }
        });

        var actionArg = new Argument<string?>("action") { Arity = ArgumentArity.ZeroOrOne };
        var postDownloadCmd = new Command("postdownloadaction", "Get or toggle a post-download action") { actionArg };
        postDownloadCmd.SetAction(parseResult =>
        {
            var actionName = parseResult.GetValue(actionArg);
            if (actionName is null)
            {
                LogMessageToFile($"Post download action: {PostDownloadAction}");
                return;
            }

            if (!Enum.TryParse(actionName, true, out PostDownloadAction action))
            {
                var validActions = string.Join(", ", Enum.GetNames<PostDownloadAction>());
                LogMessageToFile($"Invalid action. Valid actions: {{{validActions}}}", LogEventLevel.Warning);
                return;
            }

            if (action == PostDownloadAction.None)
            {
                PostDownloadAction = action;
            }
            else if (PostDownloadAction.HasFlag(action))
            {
                PostDownloadAction &= ~action;
            }
            else
            {
                PostDownloadAction |= action;
            }

            LogMessageToFile($"Post download action set to {PostDownloadAction}");
        });

        cmd.Subcommands.Add(savePathCmd);
        cmd.Subcommands.Add(postDownloadCmd);
        return cmd;
    }

    private Command BuildDebugCommand()
    {
        var cmd = new Command("debug", "Enable debug mode");
        cmd.SetAction(_ =>
        {
            Logger.Warning("Debug mode is not yet implemented.");
            Debugging = true;
        });
        return cmd;
    }

    private Command BuildDelayCommand()
    {
        var msArg = new Argument<int?>("milliseconds") { Arity = ArgumentArity.ZeroOrOne };
        var cmd = new Command("delay", "Get or set the retry delay in milliseconds") { msArg };
        cmd.SetAction(parseResult =>
        {
            var ms = parseResult.GetValue(msArg);
            if (ms is null)
            {
                LogMessageToFile($"Retry delay: {RetryDelay} ms");
                return;
            }

            RetryDelay = ms.Value;
            LogMessageToFile($"Retry delay set to {ms} ms");
        });
        return cmd;
    }

    private Command BuildHelpCommand()
    {
        var cmd = new Command("help", "Show available commands");
        cmd.SetAction(_ => LogMessageToFile("""
            Commands:
            - q(uit): Exit the REPL, saving all data and disposing of resources.
            - r(ip) [urls...]: Queue URL(s) (if given) and start the ripping process.
            - queue: Display all URLs currently in the queue.
            - clear|c cache: Clear the cache.
            - clear|c queue: Clear the URL queue.
            - retries [n]: Get or set the maximum number of retries (default: current value).
            - delay [ms]: Get or set the delay between retries in milliseconds (default: current value).
            - skip [index]: Skip a URL at a specific index in the queue (default: first URL).
            - debug: Enable debug mode for the HTML parser.
            - save: Save the current state and data.
            - history: Display the history of processed URLs or actions.
            - load|l [filename]: Load URLs from a specified file (default: 'UnfinishedRips.json').
            - peek|head: Display the first URL in the queue without removing it.
            - tail: Display the last URL in the queue without removing it.
            - regen: Regenerate the HTML parser driver.
            - [URL(s)]: Queue a URL or list of URLs for processing. Handles failures with options for re-queuing.

            CLI-only: --interactive / -i after any command drops into this REPL once it finishes.
            """));
        return cmd;
    }

    private Command BuildHistoryCommand()
    {
        var cmd = new Command("history", "Display recent history entries");
        cmd.SetAction(_ => PrintHistory());
        return cmd;
    }

    private Command BuildLoadCommand()
    {
        var fileArg = new Argument<string?>("filename") { Arity = ArgumentArity.ZeroOrOne };
        var cmd = new Command("load", "Load URLs from a file") { fileArg };
        cmd.Aliases.Add("l");
        cmd.SetAction(parseResult =>
        {
            var filename = parseResult.GetValue(fileArg);
            var urls = JsonUtility.Deserialize<List<string>>(filename ?? "UnfinishedRips.json")!;
            LoadUrls(urls);
            LogMessageToFile("URLs loaded");
        });
        return cmd;
    }

    private Command BuildListCommand()
    {
        var cmd = new Command("list", "List all queued URLs with their index");
        cmd.SetAction(_ =>
        {
            var message = "";
            foreach (var (i, queuedUrl) in UrlQueue.Enumerate())
            {
                message += $"{i}: {queuedUrl}\n";
            }

            LogMessageToFile(message);
        });
        return cmd;
    }

    private Command BuildLookupCommand()
    {
        var textArg = new Argument<string[]>("text") { Arity = ArgumentArity.OneOrMore };
        var cmd = new Command("lookup", "Look up a history entry by (partial) folder name") { textArg };
        cmd.SetAction(parseResult =>
        {
            var parts = parseResult.GetValue(textArg) ?? [];
            var remainder = parts.Join(" ");
            var folderName = ExtractionUtility.ExtractFolderNameFromString(remainder);
            if (folderName is null)
            {
                LogMessageToFile("Invalid folder name", LogEventLevel.Warning);
                return;
            }

            var historyEntry = HistoryDb.GetHistoryEntryByDirectoryName(folderName);
            if (historyEntry is null)
            {
                LogMessageToFile("No history entry found", LogEventLevel.Warning);
                return;
            }

            LogMessageToFile($"[{historyEntry.Date:s}] Url: {historyEntry.Url}");
        });
        return cmd;
    }

    private Command BuildMergeCommand()
    {
        var fileArg = new Argument<string>("filename");
        var cmd = new Command("merge", "Merge another history database into this one") { fileArg };
        cmd.SetAction(parseResult =>
        {
            var filename = parseResult.GetValue(fileArg)!;
            HistoryDb.MergeHistory(filename);
            LogMessageToFile("History merged");
        });
        return cmd;
    }

    private Command BuildPeekCommand()
    {
        var cmd = new Command("peek", "Show the first queued URL without removing it");
        cmd.Aliases.Add("head");
        cmd.SetAction(_ => LogMessageToFile(UrlQueue.Count == 0 ? "Queue is empty" : UrlQueue[0]));
        return cmd;
    }

    private Command BuildQueueCommand()
    {
        var cmd = new Command("queue", "Display all queued URLs");
        cmd.SetAction(_ => LogMessageToFile(string.Join("\n", UrlQueue)));
        return cmd;
    }

    private Command BuildRipCommand()
    {
        var urlsArg = new Argument<string[]>("urls") { Arity = ArgumentArity.ZeroOrMore };
        var cmd = new Command("rip", "Queue URL(s) if given, then start ripping") { urlsArg };
        cmd.Aliases.Add("r");
        cmd.SetAction(async (parseResult, ct) =>
        {
            var urls = parseResult.GetValue(urlsArg) ?? [];
            if (urls.Length > 0)
            {
                var rejected = QueueUrls(string.Join(" ", urls));
                HandleRejectedUrls(rejected);
            }

            await Rip(ct);
        });
        return cmd;
    }

    private Command BuildRegenCommand()
    {
        var cmd = new Command("regen", "Regenerate the HTML parser driver");
        cmd.SetAction(_ => Logger.Warning("Regenerating the HTML parser driver is not yet implemented."));
        return cmd;
    }

    private Command BuildRetriesCommand()
    {
        var countArg = new Argument<int?>("count") { Arity = ArgumentArity.ZeroOrOne };
        var cmd = new Command("retries", "Get or set the maximum number of retries") { countArg };
        cmd.SetAction(parseResult =>
        {
            var count = parseResult.GetValue(countArg);
            if (count is null)
            {
                LogMessageToFile($"Max retries: {MaxRetries - 1}");
                return;
            }

            MaxRetries = count.Value + 1;
            LogMessageToFile($"Max retries set to {MaxRetries}");
        });
        return cmd;
    }

    private Command BuildSaveCommand()
    {
        var cmd = new Command("save", "Save the current state and data");
        cmd.SetAction(async (_, ct) =>
        {
            await SaveData(ct);
            LogMessageToFile("Data saved");
        });
        return cmd;
    }

    private Command BuildSkipCommand()
    {
        var indexArg = new Argument<int?>("index") { Arity = ArgumentArity.ZeroOrOne };
        var cmd = new Command("skip", "Skip (remove) a URL at a given index in the queue") { indexArg };
        cmd.SetAction(parseResult =>
        {
            if (UrlQueue.Count == 0)
            {
                LogMessageToFile("Queue is empty");
                return;
            }

            var i = parseResult.GetValue(indexArg) ?? 0;
            var normalizedIndex = Math.Abs(i);
            if (normalizedIndex >= UrlQueue.Count)
            {
                LogMessageToFile("Index out of range");
                return;
            }

            string url;
            if (i < 0)
            {
                url = UrlQueue[^normalizedIndex];
                UrlQueue.RemoveAt(UrlQueue.Count - normalizedIndex);
            }
            else
            {
                url = UrlQueue[normalizedIndex];
                UrlQueue.RemoveAt(i);
            }

            LogMessageToFile($"Skipping {url}");
        });
        return cmd;
    }

    private Command BuildTailCommand()
    {
        var cmd = new Command("tail", "Show the last queued URL without removing it");
        cmd.SetAction(_ => LogMessageToFile(UrlQueue.Count == 0 ? "Queue is empty" : UrlQueue[^1]));
        return cmd;
    }

    #if DEBUG
    private Command BuildTestCommand()
    {
        var cmd = new Command("test", "Normalize all URLs currently stored in the history DB");
        cmd.SetAction(_ => NormalizeUrlsInDb());
        return cmd;
    }
    #endif

    private Command BuildVersionCommand()
    {
        var cmd = new Command("version", "Show the application version");
        cmd.Aliases.Add("v");
        cmd.SetAction(_ => LogMessageToFile($"{Title} v{Version}"));
        return cmd;
    }

    // ---- Shared helpers (unchanged from original) ----

    private void PrintHistory()
    {
        Console.WriteLine("+----------------------------------------+");
        Console.WriteLine("| Directory Name | URL | Date | Num URLs |");
        Console.WriteLine("+----------------------------------------+");
        var history = HistoryDb.GetHistory(1, 100);
        foreach (var entry in history)
        {
            Console.WriteLine($"| {entry.DirectoryName} | {entry.Url} | {entry.Date} | {entry.NumUrls} |");
            Console.WriteLine("+----------------------------------------+");
        }
    }

    private void HandleRejectedUrls(RejectedUrlsInfo failedUrls)
    {
        var urlsToRequeue = new List<RejectedUrlInfo>(failedUrls.Count);
        foreach (var failedUrl in failedUrls.Urls)
        {
            switch (failedUrl.Reason)
            {
                case QueueFailureReason.None:
                    break;
                case QueueFailureReason.AlreadyQueued:
                    LogMessageToFile($"URL already queued: {failedUrl.Url}");
                    break;
                case QueueFailureReason.NotSupported:
                    LogMessageToFile($"URL not supported: {failedUrl.Url}");
                    break;
                case QueueFailureReason.PreviouslyProcessed:
                    LogMessageToFile($"Re-rip url (y/n)? {failedUrl.Url}", newLine: false);
                    var response = Console.ReadLine();
                    if (response == "y")
                    {
                        urlsToRequeue.Add(failedUrl);
                    }

                    break;
                default:
                    throw new InvalidOperationException("Invalid QueueFailureReason: " + failedUrl.Reason);
            }
        }

        RequeueUrls(failedUrls.WithRejectedUrls(urlsToRequeue));
    }
}