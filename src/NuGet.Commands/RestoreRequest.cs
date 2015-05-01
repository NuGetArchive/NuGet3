// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.DependencyResolver;
using Microsoft.Framework.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using System.IO;

namespace NuGet.Commands
{
    public class RestoreRequest
    {
        public static readonly int DefaultDegreeOfConcurrency = 8;

        private readonly RemoteWalkContext _context;

        public RestoreRequest(PackageSpec project, string packagesDirectory, IEnumerable<PackageSource> sources, IEnumerable<ExternalProjectReference> externalProjects)
        {
            Project = project;
            Sources = sources.ToList();
            ExternalProjects = externalProjects.ToList();
            PackagesDirectory = new PackagesDirectory(packagesDirectory);

            LockFilePath = Path.Combine(project.BaseDirectory, LockFileFormat.LockFileName);
        }

        public RestoreRequest(PackageSpec project, PackagesDirectory packagesDirectory, RemoteWalkContext context)
        {
            Project = project;
            PackagesDirectory = packagesDirectory;

            ExternalProjects = new List<ExternalProjectReference>();
            WriteMSBuildFiles = true;

            _context = context;
        }

        /// <summary>
        /// The project to perform the restore on
        /// </summary>
        public PackageSpec Project { get; }

        /// <summary>
        /// The complete list of sources to retrieve packages from (excluding caches)
        /// </summary>
        public IReadOnlyList<PackageSource> Sources { get; }

        /// <summary>
        /// A list of projects provided by external build systems (i.e. MSBuild)
        /// </summary>
        public IList<ExternalProjectReference> ExternalProjects { get; set; }

        /// <summary>
        /// The path to the lock file to read/write. If not specified, uses the file 'project.lock.json' in the same
        /// directory as the provided PackageSpec
        /// </summary>
        public string LockFilePath { get; set; }

        /// <summary>
        /// The number of concurrent tasks to run during installs. Defaults to
        /// <see cref="DefaultDegreeOfConcurrency" />. Set this to '1' to
        /// run without concurrency.
        /// </summary>
        public int MaxDegreeOfConcurrency { get; set; } = DefaultDegreeOfConcurrency;

        /// <summary>
        /// If set, ignore the cache when downloading packages
        /// </summary>
        public bool NoCache { get; set; }

        /// <summary>
        /// If set, MSBuild files (.targets/.props) will be written for the project being restored
        /// </summary>
        public bool WriteMSBuildFiles { get; set; }

        /// The remote walk context used to perform the restore. This should generally not be set, unless you are writing
        /// tests. It is handled by the constructor.
        /// </summary>
        public RemoteWalkContext WalkContext { get; }

        /// <summary>
        /// The destination to which packages should be installed
        /// </summary>
        public PackagesDirectory PackagesDirectory { get; set; }

        public ILoggerFactory LoggerFactory { get; private set; }

        public virtual RemoteWalkContext CreateWalkContext(ILoggerFactory loggerFactory)
        {
            if(_context != null)
            {
                return _context;
            }

            var log = loggerFactory.CreateLogger<RestoreRequest>();

            // Set up the walk context
            var context = new RemoteWalkContext();

            context.ProjectLibraryProviders.Add(
                new LocalDependencyProvider(
                    new PackageSpecReferenceDependencyProvider(
                        new PackageSpecResolver(Project.BaseDirectory))));

            if (ExternalProjects != null)
            {
                context.ProjectLibraryProviders.Add(
                    new LocalDependencyProvider(
                        new ExternalProjectReferenceDependencyProvider(ExternalProjects)));
            }

            context.LocalLibraryProviders.Add(
                new SourceRepositoryDependencyProvider(PackagesDirectory.SourceRepository, loggerFactory));

            foreach (var provider in Sources.Select(s => CreateProviderFromSource(s, loggerFactory, log, NoCache)))
            {
                context.RemoteLibraryProviders.Add(provider);
            }

            return context;
        }

        private IRemoteDependencyProvider CreateProviderFromSource(PackageSource source, ILoggerFactory loggerFactory, ILogger log, bool noCache)
        {
            log.LogVerbose($"Using source {source.Source}");
            var nugetRepository = Repository.Factory.GetCoreV3(source.Source);
            return new SourceRepositoryDependencyProvider(nugetRepository, loggerFactory, noCache);
        }
    }
}
