// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.Framework.Runtime.Common.CommandLine;
using NuGet.Configuration;

namespace NuGet.CommandLine
{
    internal class ConfigCommandLineCommand
    {
        private const string HttpPasswordKey = "http_proxy.password";
        public ConfigCommandLineCommand(CommandLineApplication application, Logging.ILogger logger)
        {
            Logger = logger;
            Set = application.Option(
                "-set <namevalue>",
                "One or more config values to set.",
                CommandOptionType.MultipleValue);

            AsPath = application.Option(
                "-aspath",
                "Set the config value as a path",
                CommandOptionType.NoValue);

            ConfigFile = application.Option(
                "-configfile <path>",
                "Specifies the config file path.",
                CommandOptionType.SingleValue);

            ShouldCreateConfigFile = application.Option(
                "-shouldcreateconfigfile",
                "Determines if the config file needs to be created if it doesn't already exist.",
                CommandOptionType.NoValue);

            Argument = application.Argument(
                "[get]",
                "The config value to read.");
        }

        public Logging.ILogger Logger { get; }

        public CommandOption Set { get; }

        public CommandOption AsPath { get; }

        public CommandOption ConfigFile { get; }

        public CommandOption ShouldCreateConfigFile { get; }

        public CommandArgument Argument { get; }

        public int Execute()
        {
            var settings = GetConfig();

            if (Set.HasValue())
            {
                foreach (var item in Set.Values)
                {
                    var result = item.Split(new[] { '=' }, 2);
                    if (result.Length != 2)
                    {
                        Logger.LogError($"Invalid value {item}.");
                        return 1;
                    }

                    var key = result[0];
                    var value = result[1];

                    if (string.IsNullOrEmpty(value))
                    {
                        Logger.LogInformation($"Deleting entry {key}.");
                        SettingsUtility.DeleteConfigValue(settings, key);
                    }
                    else
                    {
                        Logger.LogInformation($"Setting entry {key} with value {value}.");
                        if (string.Equals(HttpPasswordKey, key, StringComparison.OrdinalIgnoreCase))
                        {
                            SettingsUtility.SetEncryptedValue(settings, "config", key, value);
                        }
                        else
                        {
                            SettingsUtility.SetConfigValue(settings, key, value);
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(Argument.Value))
            {
                var value = SettingsUtility.GetConfigValue(settings, Argument.Value, isPath: AsPath.HasValue());
                if (string.IsNullOrEmpty(value))
                {
                    Logger.LogWarning("ConfigCommandKeyNotFound" + Argument.Value);
                }
                else
                {
                    Console.WriteLine(value);
                }
            }

            return 0;
        }

        private Configuration.ISettings GetConfig()
        {
            Configuration.ISettings settings;
            if (!ConfigFile.HasValue())
            {
                settings = Configuration.Settings.LoadDefaultSettings(
                    Directory.GetCurrentDirectory(),
                    configFileName: null,
                    machineWideSettings: null);
            }
            else
            {
                var configFilePath = Path.GetFullPath(ConfigFile.Value());

                // Create the config file when neccessary
                if (!File.Exists(configFilePath) && ShouldCreateConfigFile.HasValue())
                {
                    var document = new XDocument(new XElement("configuration"));
                    document.Save(configFilePath);
                }

                settings = Configuration.Settings.LoadDefaultSettings(
                    Path.GetDirectoryName(configFilePath),
                    Path.GetFileName(configFilePath),
                    machineWideSettings: null);
            }

            return settings;
        }
    }
}
