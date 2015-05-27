using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class AnalyzeRequest
    {
        /// <summary>
        /// The project to perform the restore on
        /// </summary>
        public PackageSpec Project { get; }

        /// <summary>
        /// The framework context in which to run the analysis
        /// </summary>
        public NuGetFramework TargetFramework { get; }

        /// <summary>
        /// The runtime context in which to run the analysis
        /// </summary>
        public string RuntimeIdentifier { get; }

        /// <summary>
        /// The complete list of sources to retrieve packages from (excluding caches)
        /// </summary>
        public IReadOnlyList<PackageSource> Sources { get; }

        /// <summary>
        /// The directory in which to install packages required for analysis, and to find packages already installed
        /// </summary>
        public string PackagesDirectory { get; }

        /// <summary>
        /// A list of projects provided by external build systems (i.e. MSBuild)
        /// </summary>
        public IList<ExternalProjectReference> ExternalProjects { get; set; }

        /// <summary>
        /// The number of concurrent tasks to run during installs. Defaults to
        /// <see cref="DefaultDegreeOfConcurrency" />. Set this to '1' to
        /// run without concurrency.
        /// </summary>
        public int MaxDegreeOfConcurrency { get; set; } = RestoreRequest.DefaultDegreeOfConcurrency;

        /// <summary>
        /// If set, ignore the cache when downloading packages
        /// </summary>
        public bool NoCache { get; set; }

        public AnalyzeRequest(PackageSpec project, IEnumerable<PackageSource> sources, string packagesDirectory, NuGetFramework targetFramework, string runtimeIdentifier)
        {
            Project = project;
            Sources = sources.ToList().AsReadOnly();
            PackagesDirectory = packagesDirectory;
            ExternalProjects = new List<ExternalProjectReference>();
            TargetFramework = targetFramework;
            RuntimeIdentifier = runtimeIdentifier;
        }
    }
}