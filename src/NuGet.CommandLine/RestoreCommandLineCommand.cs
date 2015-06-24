// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Framework.Runtime.Common.CommandLine;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine
{
    internal class RestoreCommandLineCommand
    {
        private bool _restoringForSolution;
        private string _solutionFileFullPath;
        private string _packagesConfigFileFullPath;

        public RestoreCommandLineCommand(CommandLineApplication restore, Logging.ILogger logger)
        {
            restore.Description = "Restores packages for a project and writes a lock file";
            Logger = logger;
            Sources = restore.Option(
                "-source <source>",
                "Specifies a NuGet package source to use during the restore",
                CommandOptionType.MultipleValue);
            PackagesDirectory = restore.Option(
                "--packages <packagesDirectory>",
                "Directory to install packages in",
                CommandOptionType.SingleValue);
            Parallel = restore.Option(
                "-p|--parallel <noneOrNumberOfParallelTasks>",
                $"The number of concurrent tasks to use when restoring. Defaults to {RestoreRequest.DefaultDegreeOfConcurrency}; pass 'none' to run without concurrency.",
                CommandOptionType.SingleValue);
            FallBack = restore.Option(
                "-f|--fallbacksource <FEED>",
                "A list of packages sources to use as a fallback",
                CommandOptionType.MultipleValue);
            ConfigFile = restore.Option(
                "-config <configFile>",
                "Optional config file to use when restoring.",
                CommandOptionType.SingleValue);
            SolutionDirectory = restore.Option(
                "-solutiondirectory <solutionDirectory>",
                "The solution directory.",
                CommandOptionType.SingleValue);
            ProjectFile = restore.Argument(
                "[project file]",
                "The path to the project to restore for, either a project.json or the directory containing it. Defaults to the current directory");
        }

        public Logging.ILogger Logger { get; }

        public CommandOption Sources { get; }

        public CommandOption PackagesDirectory { get; }

        public CommandOption Parallel { get; }

        public CommandOption FallBack { get; }

        public CommandArgument ProjectFile { get; }

        public CommandOption ConfigFile { get; }

        public CommandOption SolutionDirectory { get; }

        public Task<int> Execute()
        {
            // Figure out the project directory
            var projectFilePath = Path.GetFullPath(ProjectFile.Value ?? ".");
            var projectFileName = Path.GetFileName(projectFilePath);
            if (string.IsNullOrEmpty(ProjectFile.Value) ||
                string.IsNullOrEmpty(Path.GetExtension(ProjectFile.Value)) ||
                string.Equals(ProjectManagement.Constants.PackageReferenceFile, projectFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(".sln", Path.GetExtension(projectFileName), StringComparison.OrdinalIgnoreCase))
            {
                return PerformNuGetV2Restore();
            }
            else
            {
                return PerformNuGetV3Restore(projectFilePath);
            }
        }

        private async Task<int> PerformNuGetV3Restore(string projectPath)
        {
            var projectFileName = Path.GetFileName(projectPath);
            PackageSpec project;
            IEnumerable<string> externalProjects = null;
            if (string.Equals(PackageSpec.PackageSpecFileName, projectFileName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogVerbose($"Reading project file {ProjectFile.Value}");
                projectPath = Path.GetDirectoryName(projectPath);
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(ProjectFile.Value), Path.GetFileName(projectPath), ProjectFile.Value);
            }
            else if (MsBuildUtility.IsMsBuildBasedProject(projectPath))
            {
                externalProjects = MsBuildUtility.GetProjectReferences(projectPath);

                var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
                var packageSpecFile = Path.Combine(projectDirectory, PackageSpec.PackageSpecFileName);
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(packageSpecFile), projectPath, ProjectFile.Value);
                Logger.LogVerbose($"Reading project file {ProjectFile.Value}");
            }
            else
            {
                var file = Path.Combine(projectPath, PackageSpec.PackageSpecFileName);

                Logger.LogVerbose($"Reading project file {file}");
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(file), Path.GetFileName(projectPath), file);
            }
            Logger.LogVerbose($"Loaded project {project.Name} from {project.FilePath}");

            // Resolve the root directory
            var rootDirectory = PackageSpecResolver.ResolveRootDirectory(projectPath);
            Logger.LogVerbose($"Found project root directory: {rootDirectory}");

            // Resolve the packages directory
            var packagesDir = PackagesDirectory.HasValue() ?
                PackagesDirectory.Value() :
                Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".nuget", "packages");
            Logger.LogVerbose($"Using packages directory: {packagesDir}");

            var packageSources = Sources.Values
               .SelectMany(value => value.Split(';').Select(s => new Configuration.PackageSource(value)));

            var settings = ReadSettings(Path.GetDirectoryName(projectPath));

            if (!packageSources.Any())
            {
                var packageSourceProvider = new Configuration.PackageSourceProvider(settings);
                packageSources = packageSourceProvider.LoadPackageSources();
            }

            int maxDegreesOfConcurrency = RestoreRequest.DefaultDegreeOfConcurrency;


            var request = new RestoreRequest(
                project,
                packageSources);

            if (PackagesDirectory.HasValue())
            {
                request.PackagesDirectory = PackagesDirectory.Value();
            }
            else
            {
                request.PackagesDirectory = SettingsUtility.GetGlobalPackagesFolder(settings);
            }

            // Resolve the packages directory
            Logger.LogVerbose($"Using packages directory: {request.PackagesDirectory}");

            if (externalProjects != null)
            {
                foreach (var externalReference in externalProjects)
                {
                    request.ExternalProjects.Add(
                        new ExternalProjectReference(
                            externalReference,
                            Path.Combine(Path.GetDirectoryName(externalReference), PackageSpec.PackageSpecFileName),
                            projectReferences: Enumerable.Empty<string>()));
                }
            }

            if (Parallel.HasValue())
            {
                int parallelDegree;
                if (string.Equals(Parallel.Value(), "none", StringComparison.OrdinalIgnoreCase))
                {
                    request.MaxDegreeOfConcurrency = 1;
                }
                else if (int.TryParse(Parallel.Value(), out parallelDegree))
                {
                    request.MaxDegreeOfConcurrency = parallelDegree;
                }
            }
            if (request.MaxDegreeOfConcurrency <= 1)
            {
                Logger.LogInformation("Running non-parallel restore");
            }
            else
            {
                Logger.LogInformation($"Running restore with {request.MaxDegreeOfConcurrency} concurrent jobs");
            }

            // Run the restore
            var command = new RestoreCommand(Logger, request);
            var sw = Stopwatch.StartNew();
            var result = await command.ExecuteAsync();
            sw.Stop();

            if (result.Success)
            {
                Logger.LogInformation($"Restore completed in {sw.ElapsedMilliseconds:0.00}ms!");
                return 0;
            }
            else
            {
                Logger.LogError($"Restore failed in {sw.ElapsedMilliseconds:0.00}ms!");
                return 1;
            }
        }

        private Configuration.ISettings ReadSettings(string workingDirectory)
        {
            Configuration.ISettings settings;
            if (ConfigFile.HasValue())
            {
                settings = Configuration.Settings.LoadDefaultSettings(Path.GetFullPath(ConfigFile.Value()),
                    configFileName: null,
                    machineWideSettings: null);
            }
            else
            {
                settings = Configuration.Settings.LoadDefaultSettings(workingDirectory,
                    configFileName: null,
                    machineWideSettings: null);
            }

            return settings;
        }

        private async Task<int> PerformNuGetV2Restore()
        {
            DetermineRestoreMode();
            var settings = ReadSettings(Path.GetDirectoryName(_solutionFileFullPath ?? _packagesConfigFileFullPath));
            var packagesFolderPath = GetPackagesFolderPath(settings);

            var packageSourceProvider = new Configuration.PackageSourceProvider(settings);
            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider,
                Enumerable.Concat(
                    Protocol.Core.v2.FactoryExtensionsV2.GetCoreV2(Repository.Provider),
                    Protocol.Core.v3.FactoryExtensionsV2.GetCoreV3(Repository.Provider)));
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, settings, packagesFolderPath);

            IEnumerable<Packaging.PackageReference> installedPackageReferences;
            if (_restoringForSolution)
            {
                installedPackageReferences = GetInstalledPackageReferencesFromSolutionFile(_solutionFileFullPath);
            }
            else
            {
                // By default the PackageReferenceFile does not throw if the file does not exist at the specified path.
                // So we'll need to verify that the file exists.
                if (!File.Exists(_packagesConfigFileFullPath))
                {
                    string message = String.Format(CultureInfo.CurrentCulture, "RestoreCommandFileNotFound", _packagesConfigFileFullPath);
                    throw new InvalidOperationException(message);
                }

                installedPackageReferences = GetInstalledPackageReferences(_packagesConfigFileFullPath);
            }

            var packageRestoreData = installedPackageReferences.Select(reference =>
                new PackageRestoreData(
                    reference,
                    new[] { _solutionFileFullPath ?? _packagesConfigFileFullPath },
                    isMissing: true));
            var packageRestoreContext = new PackageRestoreContext(nuGetPackageManager, packageRestoreData, CancellationToken.None);

            var result = await PackageRestoreManager.RestoreMissingPackagesAsync(packageRestoreContext, new EmptyNuGetProjectContext());

            return result.Restored ? 0 : 1;
        }

        internal void DetermineRestoreMode()
        {
            if (string.IsNullOrEmpty(ProjectFile.Value))
            {
                // look for solution files first
                _solutionFileFullPath = GetSolutionFile(Directory.GetCurrentDirectory());
                if (_solutionFileFullPath != null)
                {
                    _restoringForSolution = true;
                    return;
                }

                // look for packages.config file
                if (File.Exists(ProjectManagement.Constants.PackageReferenceFile))
                {
                    _restoringForSolution = false;
                    _packagesConfigFileFullPath = Path.GetFullPath(Constants.PackageReferenceFile);
                    return;
                }

                throw new InvalidOperationException("Error_NoSolutionFileNorePackagesConfigFile");
            }
            else
            {
                if (string.Equals(Path.GetFileName(ProjectFile.Value), ProjectManagement.Constants.PackageReferenceFile, StringComparison.OrdinalIgnoreCase))
                {
                    // restoring from packages.config file
                    _restoringForSolution = false;
                    _packagesConfigFileFullPath = Path.GetFullPath(ProjectFile.Value);
                }
                else
                {
                    _restoringForSolution = true;
                    _solutionFileFullPath = GetSolutionFile(ProjectFile.Value);
                    if (_solutionFileFullPath == null)
                    {
                        throw new InvalidOperationException("Error_CannotLocateSolutionFile");
                    }
                }
            }
        }

        private string GetPackagesFolderPath(Configuration.ISettings settings)
        {
            if (PackagesDirectory.HasValue())
            {
                return PackagesDirectory.Value();
            }

            // Packages folder needs to be inferred from SolutionFilePath or SolutionDirectory
            var effectiveSolutionDirectory = _restoringForSolution ?
                Path.GetDirectoryName(_solutionFileFullPath) :
                SolutionDirectory.Value();
            if (!String.IsNullOrEmpty(effectiveSolutionDirectory))
            {
                ReadSettings(effectiveSolutionDirectory);
                var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(effectiveSolutionDirectory, settings);
                if (!String.IsNullOrEmpty(packagesFolderPath))
                {
                    return packagesFolderPath;
                }
            }

            throw new InvalidOperationException("RestoreCommandCannotDeterminePackagesFolder");
        }

        /// <summary>
        /// Gets the solution file, in full path format. If <paramref name="solutionFileOrDirectory"/> is a file, 
        /// that file is returned. Otherwise, searches for a *.sln file in
        /// directory <paramref name="solutionFileOrDirectory"/>. If exactly one sln file is found, 
        /// that file is returned. If multiple sln files are found, an exception is thrown. 
        /// If no sln files are found, returns null.
        /// </summary>
        /// <param name="solutionFileOrDirectory">The solution file or directory to search for solution files.</param>
        /// <returns>The full path of the solution file. Or null if no solution file can be found.</returns>
        private string GetSolutionFile(string solutionFileOrDirectory)
        {
            if (File.Exists(solutionFileOrDirectory))
            {
                return Path.GetFullPath(solutionFileOrDirectory);
            }

            // look for solution files
            var slnFiles = Directory.GetFiles(Path.GetDirectoryName(solutionFileOrDirectory), "*.sln");
            if (slnFiles.Length > 1)
            {
                throw new InvalidOperationException("Error_MultipleSolutions");
            }

            if (slnFiles.Length == 1)
            {
                return Path.GetFullPath(slnFiles[0]);
            }

            return null;
        }

        private IEnumerable<Packaging.PackageReference> GetInstalledPackageReferencesFromSolutionFile(string solutionFileFullPath)
        {
            var installedPackageReferences = new HashSet<Packaging.PackageReference>(new PackageReferenceComparer());
            IEnumerable<string> projectFiles = MsBuildUtility.GetAllProjectFileNames(solutionFileFullPath);

            foreach (var projectFile in projectFiles)
            {
                if (!File.Exists(projectFile))
                {
                    continue;
                }

                var projectConfigFilePath = Path.Combine(
                    Path.GetDirectoryName(projectFile),
                    Constants.PackageReferenceFile);

                installedPackageReferences.AddRange(GetInstalledPackageReferences(projectConfigFilePath));
            }

            return installedPackageReferences;
        }

        private IEnumerable<Packaging.PackageReference> GetInstalledPackageReferences(string projectConfigFilePath)
        {
            if (File.Exists(projectConfigFilePath))
            {
                var reader = new PackagesConfigReader(XDocument.Load(projectConfigFilePath));
                return reader.GetPackages();
            }

            return Enumerable.Empty<Packaging.PackageReference>();
        }
    }
}
