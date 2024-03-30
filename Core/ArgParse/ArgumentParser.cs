using Core.ExtensionMethods;

namespace Core.ArgParse;

public static class ArgumentParser
{
    // public static T Parse<T>(string[] args)
    // {
    //     
    // }
    
    public static Arguments Parse(string[] args)
    {
        var arguments = new Arguments();
        var skip = 0;
        foreach (var (i, arg) in args.Enumerate())
        {
            if (skip > 0)
            {
                skip--;
                continue;
            }

            switch (arg)
            {
                case "-d":
                case "--debug":
                    arguments.Debug = true;
                    break;
                case "-p":
                case "--print":
                    arguments.PrintSite = true;
                    break;
                case "-n":
                case "--num-threads":
                    arguments.NumThreads = int.Parse(args[i + 1]);
                    skip = 1;
                    break;
                case "test":
                    arguments.RunMode = RunMode.Test;
                    break;
                case "gui":
                    arguments.RunMode = RunMode.Gui;
                    break;
                case "cli":
                    arguments.RunMode = RunMode.Cli;
                    break;
                default:
                    arguments.Url = arg;
                    break;
            }
        }

        if (arguments is { RunMode: RunMode.Test, Url: null })
        {
            throw new ArgumentException("Url is required for test mode");
        }

        return arguments;
    }
}