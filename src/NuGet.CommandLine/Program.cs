// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Framework.Runtime.Common.CommandLine;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Logging;
using NuGet.ProjectModel;

namespace NuGet.CommandLine
{
    public class Program
    {
        private ILogger _log;

        public int Main(string[] args)
        {
#if DEBUG
            if (args.Contains("--debug"))
            {
                args = args.Skip(1).ToArray();
                System.Diagnostics.Debugger.Launch();
            }
#endif

            // Set up logging
            _log = new CommandOutputLogger();

            var app = new CommandLineApplication();
            app.Name = "nuget3";
            app.FullName = ".NET Package Manager";
            app.HelpOption("-h|--help");
            app.VersionOption("--version", GetType().GetTypeInfo().Assembly.GetName().Version.ToString());

            app.Command("analyze", analyze =>
                {
                    analyze.Description = "Analyzes the project to check that all packages are compatible";

                    var sources = analyze.Option("-s|--source <source>", "Specifies a NuGet package source to use during the restore", CommandOptionType.MultipleValue);
                    var packagesDirectory = analyze.Option("--packages <packagesDirectory>", "Directory to install packages in", CommandOptionType.SingleValue);
                    var parallel = analyze.Option("-p|--parallel <noneOrNumberOfParallelTasks>", $"The number of concurrent tasks to use when restoring. Defaults to {RestoreRequest.DefaultDegreeOfConcurrency}; pass 'none' to run without concurrency.", CommandOptionType.SingleValue);
                    var projectFile = analyze.Argument("[project file]", "The path to the project to restore for, either a project.json or the directory containing it. Defaults to the current directory");
                    var frameworkName = analyze.Argument("[frameworkName]", "The name of the framework context in which to perform the analysis");
                    var runtimeId = analyze.Argument("[runtimeId]", "The name of the runtime context in which to perform the analysis");

                    analyze.OnExecute(async () =>
                    {
                        // Load the project
                        var externalProjects = new List<string>();
                        PackageSpec project = LoadProject(projectFile, externalProjects);

                        // Resolve the root directory
                        var rootDirectory = PackageSpecResolver.ResolveRootDirectory(project.BaseDirectory);
                        _log.LogVerbose($"Found project root directory: {rootDirectory}");

                        var packagesDir = GetPackagesDirectory(packagesDirectory);

                        var packageSources = GetSources(sources, project.BaseDirectory);

                        var request = new AnalyzeRequest(
                            project,
                            packageSources,
                            packagesDir,
                            NuGetFramework.Parse(frameworkName.Value),
                            runtimeId.Value);

                        AddProjects(externalProjects, request.ExternalProjects);

                        request.MaxDegreeOfConcurrency = GetParallelDegree(parallel);
                        if (request.MaxDegreeOfConcurrency <= 1)
                        {
                            _log.LogInformation("Running non-parallel analysis");
                        }
                        else
                        {
                            _log.LogInformation($"Running analysis with {request.MaxDegreeOfConcurrency} concurrent jobs");
                        }
                        var command = new AnalyzeCommand(_log);
                        var sw = Stopwatch.StartNew();
                        var result = await command.ExecuteAsync(request);
                        sw.Stop();

                        _log.LogInformation($"Analysis completed in {sw.ElapsedMilliseconds:0.00}ms!");

                        return 0;
                    });
                });

            app.Command("restore", restore =>
                {
                    restore.Description = "Restores packages for a project and writes a lock file";

                    var sources = restore.Option("-s|--source <source>", "Specifies a NuGet package source to use during the restore", CommandOptionType.MultipleValue);
                    var packagesDirectory = restore.Option("--packages <packagesDirectory>", "Directory to install packages in", CommandOptionType.SingleValue);
                    var parallel = restore.Option("-p|--parallel <noneOrNumberOfParallelTasks>", $"The number of concurrent tasks to use when restoring. Defaults to {RestoreRequest.DefaultDegreeOfConcurrency}; pass 'none' to run without concurrency.", CommandOptionType.SingleValue);
                    var projectFile = restore.Argument("[project file]", "The path to the project to restore for, either a project.json or the directory containing it. Defaults to the current directory");

                    restore.OnExecute(async () =>
                    {
                        // Load the project
                        var externalProjects = new List<string>();
                        PackageSpec project = LoadProject(projectFile, externalProjects);

                        // Resolve the root directory
                        var rootDirectory = PackageSpecResolver.ResolveRootDirectory(project.BaseDirectory);
                        _log.LogVerbose($"Found project root directory: {rootDirectory}");

                        var packagesDir = GetPackagesDirectory(packagesDirectory);

                        var packageSources = GetSources(sources, project.BaseDirectory);

                        var request = new RestoreRequest(
                            project,
                            packageSources,
                            packagesDir);

                        AddProjects(externalProjects, request.ExternalProjects);

                        // Run the restore
                        request.MaxDegreeOfConcurrency = GetParallelDegree(parallel);
                        if (request.MaxDegreeOfConcurrency <= 1)
                        {
                            _log.LogInformation("Running non-parallel restore");
                        }
                        else
                        {
                            _log.LogInformation($"Running restore with {request.MaxDegreeOfConcurrency} concurrent jobs");
                        }
                        var command = new RestoreCommand(_log);
                        var sw = Stopwatch.StartNew();
                        var result = await command.ExecuteAsync(request);
                        sw.Stop();

                        _log.LogInformation($"Restore completed in {sw.ElapsedMilliseconds:0.00}ms!");

                        return 0;
                    });
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

        private static int GetParallelDegree(CommandOption parallel)
        {
            if (parallel.HasValue())
            {
                int parallelDegree;
                if (string.Equals(parallel.Value(), "none", StringComparison.OrdinalIgnoreCase))
                {
                    return 1;
                }
                else if (int.TryParse(parallel.Value(), out parallelDegree))
                {
                    return parallelDegree;
                }
            }

            return RestoreRequest.DefaultDegreeOfConcurrency;
        }

        private static void AddProjects(List<string> externalProjects, IList<ExternalProjectReference> projectsList)
        {
            if (externalProjects != null)
            {
                foreach (var externalReference in externalProjects)
                {
                    projectsList.Add(
                        new ExternalProjectReference(
                            externalReference,
                            Path.Combine(Path.GetDirectoryName(externalReference), PackageSpec.PackageSpecFileName),
                            projectReferences: Enumerable.Empty<string>()));
                }
            }
        }

        private string GetPackagesDirectory(CommandOption packagesDirectory)
        {
            // Resolve the packages directory
            var packagesDir = packagesDirectory.HasValue() ?
                packagesDirectory.Value() :
                Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".nuget", "packages");
            _log.LogVerbose($"Using packages directory: {packagesDir}");
            return packagesDir;
        }

        private static IEnumerable<PackageSource> GetSources(CommandOption sources, string baseDirectory)
        {
            var packageSources = sources.Values.Select(s => new PackageSource(s));
            if (!packageSources.Any())
            {
                var settings = Settings.LoadDefaultSettings(baseDirectory,
                    configFileName: null,
                    machineWideSettings: null);
                var packageSourceProvider = new PackageSourceProvider(settings);
                packageSources = packageSourceProvider.LoadPackageSources();
            }

            return packageSources;
        }

        private PackageSpec LoadProject(CommandArgument projectFile, List<string> externalProjects)
        {
            PackageSpec project;
            var projectPath = Path.GetFullPath(projectFile.Value ?? ".");
            if (string.Equals(PackageSpec.PackageSpecFileName, Path.GetFileName(projectPath), StringComparison.OrdinalIgnoreCase))
            {
                _log.LogVerbose($"Reading project file {projectFile.Value}");
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(projectFile.Value), Path.GetFileName(projectPath), projectFile.Value);
            }
            else if (MsBuildUtility.IsMsBuildBasedProject(projectPath))
            {
#if DNXCORE50
                                throw new NotSupportedException();
#else
                externalProjects.AddRange(MsBuildUtility.GetProjectReferences(projectPath));

                var packageSpecFile = Path.Combine(projectPath, PackageSpec.PackageSpecFileName);
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(packageSpecFile), Path.GetFileName(projectPath), projectFile.Value);
                _log.LogVerbose($"Reading project file {projectFile.Value}");
#endif
            }
            else
            {
                var file = Path.Combine(projectPath, PackageSpec.PackageSpecFileName);

                _log.LogVerbose($"Reading project file {file}");
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(file), Path.GetFileName(projectPath), file);
            }
            _log.LogVerbose($"Loaded project {project.Name} from {project.FilePath}");
            return project;
        }
    }
}
