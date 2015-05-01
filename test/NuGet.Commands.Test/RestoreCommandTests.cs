using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.Logging;
using NuGet.DependencyResolver;
using NuGet.ProjectModel;
using NuGet.Versioning;
using NuGet.RuntimeModel;
using Xunit;

namespace NuGet.Commands.Test
{
    public class RestoreCommandTests
    {
        [Fact]
        public async Task SimpleRestore()
        {
            // Arrange
            var loggerFactory = new LoggerFactory();

            var source = new TestPackageFeed()
                .Package("System.Banana", "1.0.0", package =>
                {
                    package.Files("lib/dnxcore50/System.Banana.dll");
                });
            var projects = new[] {
                new ProjectBuilder("Test.Project", "dnxcore50")
                    .DependsOn("System.Banana", "1.0.0")
                    .Build()
            };
            var packages = new TestPackagesDirectory(source.Packages);

            // Act
            var result = await RunTestRestore(source, "Test.Project", projects, packages, loggerFactory);

            // Assert
            Assert.True(result.Success);
            Assert.True(packages.IsInstalled("System.Banana", "1.0.0"));
        }

        [Fact]
        public async Task SimpleRuntimeRestore()
        {
            // Arrange
            var loggerFactory = new LoggerFactory();

            var source = new TestPackageFeed()
                .Package("System.Banana.win8-x86", "1.0.0", package =>
                    package.Files("runtimes/win8-x86/lib/dnxcore50/System.Banana.dll"))
                .Package("System.Banana", "1.0.0", package =>
                    package.Files("lib/dnxcore50/System.Banana.dll")
                        .Runtimes(new RuntimeGraph(new[] {
                            new RuntimeDescription("win8-x86", new []
                            {
                                new RuntimeDependencySet("System.Banana", new []
                                {
                                    new RuntimePackageDependency("System.Banana.win8-x86", VersionRange.Parse("1.0.0"))
                                })
                            })
                        })));
            var projects = new[] {
                new ProjectBuilder("Test.Project", "dnxcore50")
                    .Runtime("win8-x86")
                    .DependsOn("System.Banana", "1.0.0")
                    .Build()
            };
            var packages = new TestPackagesDirectory(source.Packages);

            // Act
            var result = await RunTestRestore(source, "Test.Project", projects, packages, loggerFactory);

            // Assert
            Assert.True(result.Success);
            Assert.True(packages.IsInstalled("System.Banana", "1.0.0"));
            Assert.True(packages.IsInstalled("System.Banana.win8-x86", "1.0.0"));
        }

        private Task<RestoreResult> RunTestRestore(TestPackageFeed source, string projectName, IEnumerable<PackageSpec> projects, TestPackagesDirectory packagesDirectory, ILoggerFactory loggerFactory)
        {
            // Set up the walk context
            var context = new RemoteWalkContext();

            var projectResolver = new TestPackageSpecResolver(projects);
            PackageSpec project;
            Assert.True(projectResolver.TryResolvePackageSpec(projectName, out project));

            context.ProjectLibraryProviders.Add(
                new LocalDependencyProvider(
                    new PackageSpecReferenceDependencyProvider(projectResolver)));

            context.RemoteLibraryProviders.Add(
                new TestPackageDependencyProvider(source.Packages));

            var request = new RestoreRequest(project, packagesDirectory, context);
            var command = new RestoreCommand(loggerFactory);
            return command.ExecuteAsync(request);
        }
    }
}
