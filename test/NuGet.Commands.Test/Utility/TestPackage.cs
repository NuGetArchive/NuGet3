using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands.Test
{
    public class TestPackage
    {
        public LibraryIdentity Identity { get; set; }
        public List<string> FileNames { get; set; }
        public Dictionary<NuGetFramework, TestDependencyGroupBuilder> DependencySets { get; set; }
        public Dictionary<NuGetFramework, List<string>> ReferencedAssemblySets { get; set; }
        public Dictionary<NuGetFramework, List<string>> FrameworkAssemblySets { get; set; }
        public RuntimeGraph RuntimeGraph { get; set; }

        public TestPackage()
        {
            FileNames = new List<string>();
            DependencySets = new Dictionary<NuGetFramework, TestDependencyGroupBuilder>();
            ReferencedAssemblySets = new Dictionary<NuGetFramework, List<string>>();
            FrameworkAssemblySets = new Dictionary<NuGetFramework, List<string>>();
        }

        public TestPackage Runtimes(RuntimeGraph graph)
        {
            RuntimeGraph = graph;
            return this;
        }

        public TestPackage Files(params string[] path)
        {
            FileNames.AddRange(path);
            return this;
        }

        public TestPackage References(string framework, params string[] path)
        {
            NuGetFramework fx = null;
            if (string.IsNullOrEmpty(framework))
            {
                fx = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Any);
            }
            else
            {
                fx = NuGetFramework.Parse(framework);
            }
            List<string> list;
            if (!ReferencedAssemblySets.TryGetValue(fx, out list))
            {
                list = new List<string>();
                ReferencedAssemblySets[fx] = list;
            }
            list.AddRange(path);

            return this;
        }

        public TestPackage FrameworkAssemblies(string framework, params string[] path)
        {
            NuGetFramework fx = null;
            if (string.IsNullOrEmpty(framework))
            {
                fx = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Any);
            }
            else
            {
                fx = NuGetFramework.Parse(framework);
            }
            List<string> list;
            if (!FrameworkAssemblySets.TryGetValue(fx, out list))
            {
                list = new List<string>();
                FrameworkAssemblySets[fx] = list;
            }
            list.AddRange(path);

            return this;
        }

        public TestPackage DependsOn(string framework, string id, string versionRange)
        {
            NuGetFramework fx = null;
            if (string.IsNullOrEmpty(framework))
            {
                fx = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Any);
            }
            else
            {
                fx = NuGetFramework.Parse(framework);
            }


            TestDependencyGroupBuilder builder;
            if (!DependencySets.TryGetValue(fx, out builder))
            {
                builder = new TestDependencyGroupBuilder();
                DependencySets[fx] = builder;
            }
            builder.Dependencies.Add(new LibraryDependency()
            {
                LibraryRange = new LibraryRange()
                {
                    Name = id,
                    VersionRange = VersionRange.Parse(versionRange),
                    TypeConstraint = LibraryTypes.Package
                },
                Type = LibraryDependencyType.Default
            });

            return this;
        }
    }

    public class TestDependencyGroupBuilder
    {
        public IList<LibraryDependency> Dependencies { get; set; }

        public TestDependencyGroupBuilder()
        {
            Dependencies = new List<LibraryDependency>();
        }
    }
}