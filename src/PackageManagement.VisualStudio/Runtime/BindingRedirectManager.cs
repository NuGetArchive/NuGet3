﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Class that manages the binding redirect config section
    /// </summary>
    public class BindingRedirectManager
    {
        private static readonly XName AssemblyBindingName = AssemblyBinding.GetQualifiedName("assemblyBinding");
        private static readonly XName DependentAssemblyName = AssemblyBinding.GetQualifiedName("dependentAssembly");

        private string Root { get; set; }
        private string ConfigurationPath { get; set; }
        private INuGetProjectContext NuGetProjectContext { get; set; }

        public BindingRedirectManager(string root, string configurationPath, INuGetProjectContext nuGetProjectContext)
        {
            if (String.IsNullOrEmpty(root))
            {
                throw new ArgumentException(NuGet.ProjectManagement.Strings.Argument_Cannot_Be_Null_Or_Empty, "root");
            }
            if (String.IsNullOrEmpty(configurationPath))
            {
                throw new ArgumentException(NuGet.ProjectManagement.Strings.Argument_Cannot_Be_Null_Or_Empty, "configurationPath");
            }
            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException("nuGetProjectContext");
            }

            Root = root;
            ConfigurationPath = configurationPath;
            NuGetProjectContext = nuGetProjectContext;
        }

        public void AddBindingRedirects(IEnumerable<AssemblyBinding> bindingRedirects)
        {
            if (bindingRedirects == null)
            {
                throw new ArgumentNullException("bindingRedirects");
            }

            // Do nothing if there are no binding redirects to add, bail out
            if (!bindingRedirects.Any())
            {
                return;
            }

            // Get the configuration file
            XDocument document = GetConfiguration();

            // Get the runtime element
            XElement runtime = document.Root.Element("runtime");

            if (runtime == null)
            {
                // Add the runtime element to the configuration document
                runtime = new XElement("runtime");
                document.Root.AddIndented(runtime);
            }

            // Get all of the current bindings in config
            ILookup<AssemblyBinding, XElement> currentBindings = GetAssemblyBindings(document);

            XElement assemblyBindingElement = null;
            foreach (var bindingRedirect in bindingRedirects)
            {
                // Look to see if we already have this in the list of bindings already in config.
                if (currentBindings.Contains(bindingRedirect))
                {
                    var existingBindings = currentBindings[bindingRedirect];
                    if (existingBindings.Any())
                    {
                        // Remove all but the first assembly binding elements
                        foreach (var bindingElement in existingBindings.Skip(1))
                        {
                            RemoveElement(bindingElement);
                        }

                        UpdateBindingRedirectElement(existingBindings.First(), bindingRedirect);
                        // Since we have a binding element, the assembly binding node (parent node) must exist. We don't need to do anything more here.
                        continue;
                    }
                }

                if (assemblyBindingElement == null)
                {
                    // Get an assembly binding element to use
                    assemblyBindingElement = GetAssemblyBindingElement(runtime);
                }
                // Add the binding to that element

                assemblyBindingElement.AddIndented(bindingRedirect.ToXElement());
            }

            // Save the file
            Save(document);
        }

        public void RemoveBindingRedirects(IEnumerable<AssemblyBinding> bindingRedirects)
        {
            if (bindingRedirects == null)
            {
                throw new ArgumentNullException("bindingRedirects");
            }

            // Do nothing if there are no binding redirects to remove, bail out
            if (!bindingRedirects.Any())
            {
                return;
            }

            // Get the configuration file
            XDocument document = GetConfiguration();

            // Get all of the current bindings in config
            ILookup<AssemblyBinding, XElement> currentBindings = GetAssemblyBindings(document);

            if (!currentBindings.Any())
            {
                return;
            }

            foreach (var bindingRedirect in bindingRedirects)
            {
                if (currentBindings.Contains(bindingRedirect))
                {
                    foreach (var bindingElement in currentBindings[bindingRedirect])
                    {
                        RemoveElement(bindingElement);
                    }
                }
            }

            // Save the file
            Save(document);
        }

        private static void RemoveElement(XElement element)
        {
            // Hold onto the parent element before removing the element
            XElement parentElement = element.Parent;

            // Remove the element from the document if we find a match
            element.RemoveIndented();

            if (!parentElement.HasElements)
            {
                parentElement.RemoveIndented();
            }
        }


        private static XElement GetAssemblyBindingElement(XElement runtime)
        {
            // Pick the first assembly binding element or create one if there aren't any
            XElement assemblyBinding = runtime.Elements(AssemblyBindingName).FirstOrDefault();
            if (assemblyBinding != null)
            {
                return assemblyBinding;
            }

            assemblyBinding = new XElement(AssemblyBindingName);
            runtime.AddIndented(assemblyBinding);

            return assemblyBinding;
        }

        private void Save(XDocument document)
        {
            FileSystemUtility.AddFile(Root, ConfigurationPath, document.Save, NuGetProjectContext);
        }

        private static ILookup<AssemblyBinding, XElement> GetAssemblyBindings(XDocument document)
        {
            XElement runtime = document.Root.Element("runtime");

            IEnumerable<XElement> assemblyBindingElements = Enumerable.Empty<XElement>();
            if (runtime != null)
            {
                assemblyBindingElements = GetAssemblyBindingElements(runtime);
            }

            // We're going to need to know which element is associated with what binding for removal
            var assemblyElementPairs = from dependentAssemblyElement in assemblyBindingElements
                                       select new
                                       {
                                           Binding = AssemblyBinding.Parse(dependentAssemblyElement),
                                           Element = dependentAssemblyElement
                                       };

            // Return a mapping from binding to element
            return assemblyElementPairs.ToLookup(p => p.Binding, p => p.Element);
        }

        private static IEnumerable<XElement> GetAssemblyBindingElements(XElement runtime)
        {
            return runtime.Elements(AssemblyBindingName)
                          .Elements(DependentAssemblyName);
        }

        private XDocument GetConfiguration()
        {
            return XmlUtility.GetOrCreateDocument("configuration", Root, ConfigurationPath, NuGetProjectContext);
        }

        private static void UpdateBindingRedirectElement(XElement element, AssemblyBinding bindingRedirect)
        {
            var bindingRedirectElement = element.Element(AssemblyBinding.GetQualifiedName("bindingRedirect"));
            // Since we've successfully parsed this node, it has to be valid and this child must exist.
            Debug.Assert(bindingRedirectElement != null);
            bindingRedirectElement.Attribute("oldVersion").SetValue(bindingRedirect.OldVersion);
            bindingRedirectElement.Attribute("newVersion").SetValue(bindingRedirect.NewVersion);
        }
    }
}
