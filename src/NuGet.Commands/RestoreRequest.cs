// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.Logging;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Commands
{
    public class RestoreRequest
    {
        public static readonly int DefaultDegreeOfConcurrency = 8;

        /// <summary>
        /// Creates a new <see cref="RestoreRequest"/>. This constructor is designed to be useful in testing scenarios
        /// as well as other non-file-system scenarios. Most consumers should use <see cref="Create(ILogger, PackageSpec, IEnumerable{PackageSource}, string, bool)"/>.
        /// </summary>
        public RestoreRequest(PackageSpec project, IEnumerable<IRemoteDependencyProvider> sources, PackagesDirectory packagesDirectory)
        {
            Project = project;
            Sources = sources.ToList().AsReadOnly();
            PackagesDirectory = packagesDirectory;

            WriteLockFile = true;
            WriteMSBuildFiles = true;

            ExternalProjects = new List<ExternalProjectReference>();
            CompatibilityProfiles = new HashSet<FrameworkRuntimePair>();
        }

        /// <summary>
        /// Creates a new <see cref="RestoreRequest"/> for the most common scenario (local file system, etc.)
        /// </summary>
        public static RestoreRequest Create(ILogger log, PackageSpec project, IEnumerable<PackageSource> sources, string packagesDirectory, bool noCache)
        {
            return new RestoreRequest(
                project,
                sources.Select(s => CreateProviderFromSource(s, noCache, log)),
                new PackagesDirectory(packagesDirectory));
        }

        /// <summary>
        /// The project to perform the restore on
        /// </summary>
        public PackageSpec Project { get; }

        /// <summary>
        /// Gets the resolver to use to find projects. If not specified, uses a default <see cref="PackageSpecResolver"/>.
        /// </summary>
        public IPackageSpecResolver ProjectResolver { get; }

        /// <summary>
        /// The complete list of sources to retrieve packages from (excluding caches)
        /// </summary>
        public IReadOnlyList<IRemoteDependencyProvider> Sources { get; }

        /// <summary>
        /// The directory in which to install packages
        /// </summary>
        public PackagesDirectory PackagesDirectory { get; }

        /// <summary>
        /// A list of projects provided by external build systems (i.e. MSBuild)
        /// </summary>
        public IList<ExternalProjectReference> ExternalProjects { get; set; }

        /// <summary>
        /// The path to the lock file to read/write. If not specified, uses the file 'project.lock.json' in the same
        /// directory as the provided PackageSpec.
        /// </summary>
        public string LockFilePath { get; set; }

        /// <summary>
        /// Set this to false to prevent the command from writting the lock file (defaults to true)
        /// </summary>
        public bool WriteLockFile { get; set; }

        /// <summary>
        /// The existing lock file to use. If not specified, the lock file will be read from the <see cref="LockFilePath"/>
        /// (or, if that property is not specified, from the default location of the lock file, as specified in the
        /// description for <see cref="LockFilePath"/>)
        /// </summary>
        public LockFile ExistingLockFile { get; set; }

        /// <summary>
        /// The number of concurrent tasks to run during installs. Defaults to
        /// <see cref="DefaultDegreeOfConcurrency" />. Set this to '1' to
        /// run without concurrency.
        /// </summary>
        public int MaxDegreeOfConcurrency { get; set; } = DefaultDegreeOfConcurrency;

        /// <summary>
        /// If set, MSBuild files (.targets/.props) will be written for the project being restored
        /// </summary>
        public bool WriteMSBuildFiles { get; set; }

        /// <summary>
        /// Additional compatibility profiles to check compatibility with.
        /// </summary>
        public ISet<FrameworkRuntimePair> CompatibilityProfiles { get; }

        private static IRemoteDependencyProvider CreateProviderFromSource(PackageSource source, bool noCache, ILogger log)
        {
            log.LogVerbose(Strings.FormatLog_UsingSource(source.Source));

            var nugetRepository = Repository.Factory.GetCoreV3(source.Source);
            return new SourceRepositoryDependencyProvider(nugetRepository, log, noCache);
        }
    }
}
