// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace NuGet.CommandLine
{
    public class Program
    {
        private Logging.ILogger _log;

        public int Main(string[] args)
        {
#if DEBUG
            if (args.Contains("--debug"))
            {
                args = args.Skip(1).ToArray();
                System.Diagnostics.Debugger.Launch();
            }
#endif

            var app = new CommandLineApplication();
            app.Name = "nuget3";
            app.FullName = ".NET Package Manager";
            app.HelpOption("-h|--help");
            app.VersionOption("--version", GetType().GetTypeInfo().Assembly.GetName().Version.ToString());

            var verbosity = app.Option("-v|--verbosity <verbosity>", "The verbosity of logging to use. Allowed values: Debug, Verbose, Information, Warning, Error", CommandOptionType.SingleValue);

            // Set up logging
            _log = new CommandOutputLogger(verbosity);

            app.Command("restore", restore =>
            {
                var command = new RestoreCommandLineCommand(restore, _log);
                restore.OnExecute(() => command.Execute());
            });

            app.Command("config", config =>
            {
                var command = new ConfigCommandLineCommand(config, _log);
                config.OnExecute(() => command.Execute());
            });

            app.Command("diag", diag =>
                {
                    diag.Description = "Diagnostic commands for debugging package dependency graphs";
                    diag.Command("lockfile", lockfile =>
                        {
                            lockfile.Description = "Dumps data from the project lock file";

                            var project = lockfile.Option("--project <project>", "Path containing the project lockfile, or the patht to the lockfile itself", CommandOptionType.SingleValue);
                            var target = lockfile.Option("--target <target>", "View information about a specific project target", CommandOptionType.SingleValue);
                            var library = lockfile.Argument("<library>", "Optionally, get detailed information about a specific library");

                            lockfile.OnExecute(() =>
                                {
                                    var diagnostics = new DiagnosticCommands(_log);
                                    var projectFile = project.HasValue() ? project.Value() : Path.GetFullPath(".");
                                    return diagnostics.Lockfile(projectFile, target.Value(), library.Value);
                                });
                        });
                    diag.OnExecute(() =>
                        {
                            diag.ShowHelp();
                            return 0;
                        });
                });

            app.OnExecute(() =>
                {
                    app.ShowHelp();
                    return 0;
                });

            return app.Execute(args);
        }
    }
}
