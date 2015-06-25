//// Copyright (c) .NET Foundation. All rights reserved.
//// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.Framework.Runtime.Common.CommandLine;
//using NuGet.Protocol.Core.v3;

//namespace NuGet.CommandLIne
//{
//    internal class PushCommandLineCommand
//    {
//        private const string ApiKeysSectionName = "apikeys";
//        private const string ServiceEndpoint = "/api/v2/package";
//        private const string ApiKeyHeader = "X-NuGet-ApiKey";
//        private const int MaxRediretionCount = 20;

//        public PushCommandLineCommand(CommandLineApplication command, Logging.ILogger logger)
//        {
//            command.Description = "Restores packages for a project and writes a lock file";
//        }

//        public CommandOption Source { get; }

//        public CommandOption ApiKey { get; }

//        public CommandOption ConfigFile { get; }

//        public CommandOption Timeout { get; }

//        public CommandArgument PackagePath { get; }

//        public CommandArgument ApiKeyArgument { get; }

//        public CommandOption MaxDegreesOfConcurrency { get; }

//        public async Task<int> Execute()
//        {
//            var packagePath = PackagePath.Value;
//            var settings = GetSettings();

//            // Don't push symbols by default
//            var source = ResolveSource(packagePath, Configuration.ConfigurationDefaults.Instance.DefaultPushSource);

//            var apiKey = GetApiKey(source, settings);
//            // Default to 5 minutes
//            var timeout = TimeSpan.FromMinutes(5);
//            int value;
//            if (Timeout.HasValue() && int.TryParse(Timeout.Value(), out value) && value > 0)
//            {
//                timeout = TimeSpan.FromSeconds(value);
//            }

//            await PushPackageAsync(packagePath, source, apiKey, timeout);

//            if (source.Equals(Configuration.NuGetConstants.DefaultGalleryServerUrl, StringComparison.OrdinalIgnoreCase))
//            {
//                PushSymbols(packagePath, timeout);
//            }
//        }

//        public string ResolveSource(string packagePath, string configurationDefaultPushSource = null)
//        {
//            string source = Source;

//            if (String.IsNullOrEmpty(source))
//            {
//                source = Settings.GetConfigValue("DefaultPushSource");
//            }

//            if (String.IsNullOrEmpty(source))
//            {
//                source = configurationDefaultPushSource;
//            }

//            if (!String.IsNullOrEmpty(source))
//            {
//                source = SourceProvider.ResolveAndValidateSource(source);
//            }
//            else
//            {
//                source = packagePath.EndsWith(PackCommand.SymbolsExtension, StringComparison.OrdinalIgnoreCase)
//                    ? NuGetConstants.DefaultSymbolServerUrl
//                    : NuGetConstants.DefaultGalleryServerUrl;
//            }
//            return source;
//        }

//        private void PushSymbols(string packagePath, TimeSpan timeout)
//        {
//            // Get the symbol package for this package
//            string symbolPackagePath = GetSymbolsPath(packagePath);

//            // Push the symbols package if it exists
//            if (File.Exists(symbolPackagePath))
//            {
//                string source = NuGetConstants.DefaultSymbolServerUrl;

//                // See if the api key exists
//                string apiKey = GetApiKey(source);

//                if (String.IsNullOrEmpty(apiKey))
//                {
//                    Console.WriteWarning(LocalizedResourceManager.GetString("Warning_SymbolServerNotConfigured"), Path.GetFileName(symbolPackagePath), LocalizedResourceManager.GetString("DefaultSymbolServer"));
//                }
//                PushPackageAsync(symbolPackagePath, source, apiKey, timeout);
//            }
//        }

//        /// <summary>
//        /// Get the symbols package from the original package. Removes the .nupkg and adds .symbols.nupkg
//        /// </summary>
//        private static string GetSymbolsPath(string packagePath)
//        {
//            string symbolPath = Path.GetFileNameWithoutExtension(packagePath) + PackCommand.SymbolsExtension;
//            string packageDir = Path.GetDirectoryName(packagePath);
//            return Path.Combine(packageDir, symbolPath);
//        }

//        private async Task PushPackageAsync(string packagePath, string source, string apiKey, TimeSpan timeout)
//        {
//            var packagesToPush = GetPackagesToPush(packagePath);
//            EnsurePackageFileExists(packagePath, packagesToPush);

//            int maxDegreesOfConcurrency;

//            if (!MaxDegreesOfConcurrency.HasValue() ||
//                !int.TryParse(MaxDegreesOfConcurrency.Value(), out maxDegreesOfConcurrency))
//            {
//                maxDegreesOfConcurrency = 8;
//            }

//            var tasks = new List<Task>();

//            using (var semaphoreSlim = new SemaphoreSlim(0, maxDegreesOfConcurrency))
//            {
//                foreach (var package in packagesToPush)
//                {
//                    var task = Task.Run(async () =>
//                    {
//                        await semaphoreSlim.WaitAsync();

//                        try
//                        {
                            
//                        }
//                        catch
//                        {
//                            semaphoreSlim.Release();
//                            throw;
//                        }
//                    });

//                    tasks.Add(task);
//                }
//            }

//            await Task.WhenAll(tasks);
//        }

//        private static IEnumerable<string> GetPackagesToPush(string packagePath)
//        {
//            // Ensure packagePath ends with *.nupkg
//            packagePath = EnsurePackageExtension(packagePath);
//            return PathResolver.PerformWildcardSearch(Environment.CurrentDirectory, packagePath);
//        }

//        internal static string EnsurePackageExtension(string packagePath)
//        {
//            if (packagePath.IndexOf('*') == -1)
//            {
//                // If there's no wildcard in the path to begin with, assume that it's an absolute path.
//                return packagePath;
//            }
//            // If the path does not contain wildcards, we need to add *.nupkg to it.
//            if (!packagePath.EndsWith(Constants.PackageExtension, StringComparison.OrdinalIgnoreCase))
//            {
//                if (packagePath.EndsWith("**", StringComparison.OrdinalIgnoreCase))
//                {
//                    packagePath = packagePath + Path.DirectorySeparatorChar + '*';
//                }
//                else if (!packagePath.EndsWith("*", StringComparison.OrdinalIgnoreCase))
//                {
//                    packagePath = packagePath + '*';
//                }
//                packagePath = packagePath + Constants.PackageExtension;
//            }
//            return packagePath;
//        }

//        private static void EnsurePackageFileExists(string packagePath, IEnumerable<string> packagesToPush)
//        {
//            if (!packagesToPush.Any())
//            {
//                throw new InvalidOperationException("String.Format(CultureInfo.CurrentCulture, LocalizedResourceManager.GetString(\"UnableToFindFile\"), packagePath)");
//            }
//        }

//        private string GetApiKey(string source, Configuration.ISettings settings)
//        {
//            if (ApiKey.HasValue())
//            {
//                return ApiKey.Value();
//            }

//            // Second argument, if present, should be the API Key
//            if (!string.IsNullOrEmpty(ApiKeyArgument.Value))
//            {
//                return ApiKeyArgument.Value;
//            }

//            // If the user did not pass an API Key look in the config file
//            return Configuration.SettingsUtility.GetDecryptedValue(settings, ApiKeysSectionName, source);
//        }

//        /// <summary>
//        /// Indicates whether the specified source is a file source, such as: \\a\b, c:\temp, etc.
//        /// </summary>
//        /// <param name="source">The source to test.</param>
//        /// <returns>true if the source is a file source; otherwise, false.</returns>
//        private static bool IsFileSource(string source)
//        {
//            Uri uri;
//            if (Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out uri))
//            {
//                return uri.IsFile;
//            }
//            else
//            {
//                return false;
//            }
//        }

//        private Configuration.ISettings GetSettings()
//        {
//            Configuration.ISettings settings;
//            if (!ConfigFile.HasValue())
//            {
//                settings = Configuration.Settings.LoadDefaultSettings(
//                    Directory.GetCurrentDirectory(),
//                    configFileName: null,
//                    machineWideSettings: null);
//            }
//            else
//            {
//                var configFilePath = Path.GetFullPath(ConfigFile.Value());
//                settings = Configuration.Settings.LoadDefaultSettings(
//                    Path.GetDirectoryName(configFilePath),
//                    Path.GetFileName(configFilePath),
//                    machineWideSettings: null);
//            }

//            return settings;
//        }


//        private void PushPackageToServer(
//            string apiKey,
//            FileInfo packageFileInfo,
//            int timeout,
//            bool disableBuffering)
//        {
//            int redirectionCount = 0;

//            while (true)
//            {
//                HttpClient client = GetClient("", "PUT", "application/octet-stream");
//                client.DisableBuffering = disableBuffering;

//                client.SendingRequest += (sender, e) =>
//                {
//                    var request = (HttpWebRequest)e.Request;

//                    // Set the timeout
//                    if (timeout <= 0)
//                    {
//                        timeout = request.ReadWriteTimeout; // Default to 5 minutes if the value is invalid.
//                    }

//                    request.Timeout = timeout;
//                    request.ReadWriteTimeout = timeout;
//                    if (!String.IsNullOrEmpty(apiKey))
//                    {
//                        request.Headers.Add(ApiKeyHeader, apiKey);
//                    }

//                    var multiPartRequest = new MultipartWebRequest();
//                    multiPartRequest.AddFile(packageFileInfo.OpenRead, "package", packageFileInfo.Length);

//                    multiPartRequest.CreateMultipartRequest(request);
//                };

//                // When AllowWriteStreamBuffering is set to false, redirection will not be handled
//                // automatically by HttpWebRequest. So we need to check redirect status code and
//                // update _baseUri and retry if redirection happens.
//                if (EnsureSuccessfulResponse(client))
//                {
//                    return;
//                }

//                ++redirectionCount;
//                if (redirectionCount > MaxRediretionCount)
//                {
//                    throw new InvalidOperationException("NuGetResources.Error_TooManyRedirections");
//                }
//            }
//        }
//    }
//}