using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CommandLine;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Commands
{
    [Command(typeof(NuGetCommand), "list", "ListCommandDescription",
        UsageSummaryResourceName = "ListCommandUsageSummary", UsageDescriptionResourceName = "ListCommandUsageDescription",
        UsageExampleResourceName = "ListCommandUsageExamples")]
    public class ListCommand : Command
    {
        private const int PageSize = 30;
        private readonly List<string> _sources = new List<string>();

        [Option(typeof(NuGetCommand), "ListCommandSourceDescription")]
        public ICollection<string> Source
        {
            get { return _sources; }
        }

        [Option(typeof(NuGetCommand), "ListCommandVerboseListDescription")]
        public bool Verbose { get; set; }

        [Option(typeof(NuGetCommand), "ListCommandAllVersionsDescription")]
        public bool AllVersions { get; set; }

        [Option(typeof(NuGetCommand), "ListCommandPrerelease")]
        public bool Prerelease { get; set; }

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This call is expensive")]
        public async Task<IEnumerable<SimpleSearchMetadata>> GetPackages()
        {
            var packageSourceProvider = new Configuration.PackageSourceProvider(Settings);
            var configurationSources = packageSourceProvider.LoadPackageSources();
            IEnumerable<Configuration.PackageSource> packageSources;
            if (Source.Count > 0)
            {
                packageSources = Source.Select(s => Common.PackageSourceProviderExtensions.ResolveSource(configurationSources, s));
            }
            else
            {
                packageSources = configurationSources;
            }

            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider,
                Enumerable.Concat(
                    Protocol.Core.v2.FactoryExtensionsV2.GetCoreV2(Repository.Provider),
                    Protocol.Core.v3.FactoryExtensionsV2.GetCoreV3(Repository.Provider)));

            var resourceTasks = new List<Task<SimpleSearchResource>>();
            foreach (var source in packageSources)
            {
                var sourceRepository = sourceRepositoryProvider.CreateRepository(source);
                resourceTasks.Add(sourceRepository.GetResourceAsync<SimpleSearchResource>());
            }
            var resources = await Task.WhenAll(resourceTasks);

            var resultTasks = resources.Where(r => r != null)
                .Select(r => r.Search(
                    Arguments.FirstOrDefault(),
                    new SearchFilter(Enumerable.Empty<string>(), Prerelease, includeDelisted: false),
                    skip: 0,
                    take: PageSize,
                    cancellationToken: CancellationToken.None));

            var results = await Task.WhenAll(resultTasks);

            return results.SelectMany(s => s);
        }

        public override async Task ExecuteCommandAsync()
        {
            if (Verbose)
            {
                Console.WriteWarning(LocalizedResourceManager.GetString("Option_VerboseDeprecated"));
                Verbosity = Verbosity.Detailed;
            }

            var packages = await GetPackages();

            bool hasPackages = false;

            if (packages != null)
            {
                if (Verbosity == Verbosity.Detailed)
                {
                    /***********************************************
                     * Package-Name
                     *  1.0.0.2010
                     *  This is the package Description
                     * 
                     * Package-Name-Two
                     *  2.0.0.2010
                     *  This is the second package Description
                     ***********************************************/
                    foreach (var p in packages)
                    {
                        Console.PrintJustified(0, p.Identity.Id);
                        Console.PrintJustified(1, p.Identity.Version.ToNormalizedString());
                        Console.PrintJustified(1, p.Description);
                        Console.WriteLine();
                        hasPackages = true;
                    }
                }
                else
                {
                    /***********************************************
                     * Package-Name 1.0.0.2010
                     * Package-Name-Two 2.0.0.2010
                     ***********************************************/
                    foreach (var p in packages)
                    {
                        Console.PrintJustified(0, p.Identity.Id + " " + p.Identity.Version);
                        hasPackages = true;
                    }
                }
            }

            if (!hasPackages)
            {
                Console.WriteLine(LocalizedResourceManager.GetString("ListCommandNoPackages"));
            }
        }

        private SourceRepositoryProvider GetSourceRepositoryProvider()
        {
            var packageSourceProvider = new Configuration.PackageSourceProvider(Settings);
            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider,
                Enumerable.Concat(
                    Protocol.Core.v2.FactoryExtensionsV2.GetCoreV2(Repository.Provider),
                    Protocol.Core.v3.FactoryExtensionsV2.GetCoreV3(Repository.Provider)));
            return sourceRepositoryProvider;
        }

        private async Task<IEnumerable<SimpleSearchMetadata>> GetV3Results(
            SourceRepository repository,
            string searchTerm)
        {
            //var searchResource = await repository.GetResourceAsync<SimpleSearchResource>();
            //if (searchResource == null)
            //{
                return Enumerable.Empty<SimpleSearchMetadata>();
            //}
        }

        private Task<IEnumerable<SimpleSearchMetadata>> GetV2Results(
            IPackageRepository packageRepository,
            string searchTerm)
        {
            return Task.Run(() =>
            {
                var packages = packageRepository.Search(searchTerm, Prerelease);
                IEnumerable<IPackage> result;
                if (AllVersions)
                {
                    result = packages
                        .Take(PageSize)
                        .OrderBy(p => p.Id);
                }
                else
                {
                    if (Prerelease && packageRepository.SupportsPrereleasePackages)
                    {
                        packages = packages.Where(p => p.IsAbsoluteLatestVersion);
                    }
                    else
                    {
                        packages = packages.Where(p => p.IsLatestVersion);
                    }

                    result = packages.OrderBy(p => p.Id)
                        .Take(PageSize)
                        .AsEnumerable()
                        .Where(PackageExtensions.IsListed)
                        .Where(p => Prerelease || p.IsReleaseVersion())
                        .AsCollapsed();
                }

                return result.Select(p =>
                    new SimpleSearchMetadata(
                        new PackageIdentity(p.Id, NuGetVersion.Parse(p.Version.ToNormalizedString())),
                        p.Description,
                        Enumerable.Empty<NuGetVersion>()));
            });
        }
    }
}