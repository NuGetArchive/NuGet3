using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands.Test
{
    internal class ProjectBuilder
    {
        public string Name { get; }
        public RuntimeGraph RuntimeGraph { get; private set; } = RuntimeGraph.Empty;
        public List<LibraryDependency> Dependencies { get; } = new List<LibraryDependency>();
        public Dictionary<NuGetFramework, List<LibraryDependency>> Frameworks { get; }

        public ProjectBuilder(string name, params string[] frameworks)
        {
            Name = name;
            Frameworks = frameworks.ToDictionary(f => NuGetFramework.Parse(f), _ => new List<LibraryDependency>());
        }

        public PackageSpec Build()
        {
            var spec = new PackageSpec(new JObject());
            spec.Name = Name;
            spec.Dependencies = Dependencies;
            spec.RuntimeGraph = RuntimeGraph;
            spec.TargetFrameworks.AddRange(Frameworks.Select(pair => new TargetFrameworkInformation()
            {
                FrameworkName = pair.Key,
                Dependencies = pair.Value
            }));
            return spec;
        }

        public ProjectBuilder Runtime(string name)
        {
            RuntimeGraph = RuntimeGraph.Merge(RuntimeGraph, new RuntimeGraph(new[]
            {
                new RuntimeDescription(name)
            }));
            return this;
        }

        public ProjectBuilder DependsOn(string id, string version)
        {
            return DependsOn(null, id, version);
        }

        public ProjectBuilder DependsOn(string framework, string id, string version)
        {
            var dep = new LibraryDependency()
            {
                LibraryRange = new LibraryRange()
                {
                    Name = id,
                    VersionRange = VersionRange.Parse(version),
                    TypeConstraint = null
                },
                Type = LibraryDependencyType.Default
            };

            if (string.IsNullOrEmpty(framework))
            {
                Dependencies.Add(dep);
            }
            else
            {
                var fx = NuGetFramework.Parse(framework);

                List<LibraryDependency> deps;
                if (!Frameworks.TryGetValue(fx, out deps))
                {
                    throw new InvalidOperationException("Cannot add dependency for framework, the project does not target it: " + framework);
                }
                deps.Add(dep);
            }

            return this;
        }
    }
}