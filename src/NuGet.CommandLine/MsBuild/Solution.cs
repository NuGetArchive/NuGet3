// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace NuGet.CommandLine
{
    /// <summary>
    /// Represents the solution loaded from a sln file. We use the internal class 
    /// Microsoft.Build.Construction.SolutionParser to parse sln files.
    /// </summary>
    internal class Solution
    {
        private static readonly Type _solutionParserType = GetSolutionParserType();
        private static readonly PropertyInfo _solutionReaderProperty = GetSolutionReaderProperty();
        private static readonly MethodInfo _parseSolutionMethod = GetParseSolutionMethod();
        private static readonly PropertyInfo _projectsProperty = GetProjectsProperty();

        public List<ProjectInSolution> Projects { get; private set; }

        public Solution(string solutionFileName)
        {
            var solutionParser = _solutionParserType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, types: Type.EmptyTypes, modifiers: null).Invoke(null);
            using (var streamReader = new StreamReader(File.OpenRead(solutionFileName)))
            {
                _solutionReaderProperty.SetValue(solutionParser, streamReader, index: null);
                _parseSolutionMethod.Invoke(solutionParser, parameters: null);
            }

            var projects = new List<ProjectInSolution>();
            foreach (var proj in (object[])_projectsProperty.GetValue(solutionParser, index: null))
            {
                projects.Add(new ProjectInSolution(proj));
            }

            Projects = projects;
        }

        private static Type GetSolutionParserType()
        {
            var assembly = typeof(Microsoft.Build.Construction.ProjectElement).Assembly;
            var solutionParserType = assembly.GetType("Microsoft.Build.Construction.SolutionParser");

            if (solutionParserType == null)
            {
                throw new InvalidOperationException("Error_CannotLoadTypeSolutionParser");
            }

            return solutionParserType;
        }

        private static PropertyInfo GetSolutionReaderProperty()
        {
            if (_solutionParserType != null)
            {
                return _solutionParserType.GetProperty("SolutionReader", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return null;
        }

        private static MethodInfo GetParseSolutionMethod()
        {
            if (_solutionParserType != null)
            {
                return _solutionParserType.GetMethod("ParseSolution", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return null;
        }

        private static PropertyInfo GetProjectsProperty()
        {
            if (_solutionParserType != null)
            {
                return _solutionParserType.GetProperty("Projects", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return null;
        }
    }
}