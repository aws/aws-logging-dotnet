using AWS.Logger;
using AWS.Logger.AspNetCore;
using Microsoft.Extensions.Configuration;
using System;
#if CORECLR
using System.Runtime.Loader;
#endif
using System.Reflection;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extensions methods for ILoggerFactory to add the AWS logging provider
    /// </summary>
    public static class AWSLoggerFactoryExtensions
    {
        /// <summary>
        /// Adds the AWS logging provider to the log factory using the configuration specified in the AWSLoggerConfig
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="config">Configuration on how to connect to AWS and how the log messages should be sent.</param>
        /// <returns></returns>
        public static ILoggerFactory AddAWSProvider(this ILoggerFactory factory, AWSLoggerConfig config)
        {
            // If config is null. Assuming the logger is being activated in a debug environment
            // and skip adding the provider. We don't want to prevent developers running their application
            // locally because they don't have access or want to use AWS for their local development.
            if (config == null)
            {
                factory.CreateLogger("AWS.Logging.AspNetCore").LogWarning("AWSLoggerConfig is null, skipping adding AWS Logging provider.");
                return factory;
            }

            var provider = new AWSLoggerProvider(config);
            factory.AddProvider(provider);
            return factory;
        }

        /// <summary>
        /// Adds the AWS logging provider to the log factory using the configuration specified in the AWSLoggerConfig
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="configSection">Configuration and loglevels on how to connect to AWS and how the log messages should be sent.</param>
        /// <returns></returns>
        public static ILoggerFactory AddAWSProvider(this ILoggerFactory factory, AWSLoggerConfigSection configSection)
        {
            // If configSection is null. Assuming the logger is being activated in a debug environment
            // and skip adding the provider. We don't want to prevent developers running their application
            // locally because they don't have access or want to use AWS for their local development.
            if (configSection == null)
            {
                factory.CreateLogger("AWS.Logging.AspNetCore").LogWarning("AWSLoggerConfigSection is null. LogGroup is likely not configured in config files. Skipping adding AWS Logging provider.");
                return factory;
            }

            var provider = new AWSLoggerProvider(configSection);
            factory.AddProvider(provider);
            return factory;
        }

        /// <summary>
        /// Adds the AWS logging provider to the log factory using the configuration specified in the AWSLoggerConfig
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="config">Configuration on how to connect to AWS and how the log messages should be sent.</param>
        /// <param name="minLevel">The minimum log level for messages to be written.</param>
        /// <returns></returns>
        public static ILoggerFactory AddAWSProvider(this ILoggerFactory factory, AWSLoggerConfig config, LogLevel minLevel)
        {
            var provider = new AWSLoggerProvider(config,minLevel);
            factory.AddProvider(provider);
            return factory;
        }

        /// <summary>
        /// Adds the AWS logging provider to the log factory using the configuration specified in the AWSLoggerConfig
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="config">Configuration on how to connect to AWS and how the log messages should be sent.</param>
        /// <param name="filter">A filter function that has the logger category name and log level which can be used to filter messages being sent to AWS.</param>
        /// <returns></returns>
        public static ILoggerFactory AddAWSProvider(this ILoggerFactory factory, AWSLoggerConfig config, Func<string, LogLevel, bool> filter)
        {
            var provider = new AWSLoggerProvider(config, filter);
            factory.AddProvider(provider);
            return factory;
        }
    }
}
