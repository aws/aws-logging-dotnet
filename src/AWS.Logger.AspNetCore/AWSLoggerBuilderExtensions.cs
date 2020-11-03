using AWS.Logger;
using AWS.Logger.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Linq;

// Same namespace as ILoggingBuilder, to make these extensions appear
// without the user needing to including our namespace first.
namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// ILoggingBuilder extensions
    /// </summary>
    public static class AWSLoggerBuilderExtensions
    {
        /// <summary>
        /// Adds the AWS logging provider to the log builder using the configuration specified in the AWSLoggerConfig
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="config">Configuration on how to connect to AWS and how the log messages should be sent.</param>
        /// <param name="formatter">A custom formatter which accepts a LogLevel, a state, and an exception and returns the formatted log message.</param>
        /// <returns></returns>
        public static ILoggingBuilder AddAWSProvider(this ILoggingBuilder builder, AWSLoggerConfig config, Func<LogLevel, object, Exception, string> formatter = null)
        {
            // If config is null. Assuming the logger is being activated in a debug environment
            // and skip adding the provider. We don't want to prevent developers running their application
            // locally because they don't have access or want to use AWS for their local development.
            if (config == null)
            {
                return builder;
            }

            var provider = new AWSLoggerProvider(config, formatter);
            builder.AddProvider(provider);
            return builder;
        }

        /// <summary>
        /// Adds the AWS logging provider to the log builder using the configuration specified in the AWSLoggerConfig
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configSection">Configuration and loglevels on how to connect to AWS and how the log messages should be sent.</param>
        /// <param name="formatter">A custom formatter which accepts a LogLevel, a state, and an exception and returns the formatted log message.</param>
        /// <returns></returns>
        public static ILoggingBuilder AddAWSProvider(this ILoggingBuilder builder, AWSLoggerConfigSection configSection, Func<LogLevel, object, Exception, string> formatter = null)
        {
            // If configSection is null. Assuming the logger is being activated in a debug environment
            // and skip adding the provider. We don't want to prevent developers running their application
            // locally because they don't have access or want to use AWS for their local development.
            if (configSection == null)
            {
                return builder;
            }

            var provider = new AWSLoggerProvider(configSection, formatter);
            builder.AddProvider(provider);
            return builder;
        }

        /// <summary>
        /// Adds the AWS logging provider to the log builder by looking up configuration information from the IConfiguration added to the service collection.
        /// If configuration information can not be found then the AWS logging provider will not be added.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="formatter">A custom formatter which accepts a LogLevel, a state, and an exception and returns the formatted log message.</param>
        /// <returns></returns>
        public static ILoggingBuilder AddAWSProvider(this ILoggingBuilder builder, Func<LogLevel, object, Exception, string> formatter = null)
        {
            var configuration = GetConfiguration(builder.Services);

            // If configuration or configSection is null. Assuming the logger is being activated in a debug environment
            // and skip adding the provider. We don't want to prevent developers running their application
            // locally because they don't have access or want to use AWS for their local development.
            if (configuration == null)
            {
                return builder;
            }

            var configSection = configuration.GetAWSLoggingConfigSection();
            if (configSection == null)
            {
                return builder;
            }

            return AddAWSProvider(builder, configSection, formatter);
        }

        private static IConfiguration GetConfiguration(IServiceCollection services)
        {
            var serviceDescriptor = services.FirstOrDefault(x => x.ServiceType == typeof(IConfiguration));
            if (serviceDescriptor == null)
            {
                return null;
            }

            var configuration = serviceDescriptor.ImplementationInstance as IConfiguration;
            if (configuration == null && serviceDescriptor.ImplementationFactory != null)
            {
                var provider = services.BuildServiceProvider();
                configuration = serviceDescriptor.ImplementationFactory(provider) as IConfiguration;
            }

            return configuration;
        }

        /// <summary>
        /// Adds the AWS logging provider to the log builder using the configuration specified in the AWSLoggerConfig
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="config">Configuration on how to connect to AWS and how the log messages should be sent.</param>
        /// <param name="minLevel">The minimum log level for messages to be written.</param>
        /// <returns></returns>
        public static ILoggingBuilder AddAWSProvider(this ILoggingBuilder builder, AWSLoggerConfig config, LogLevel minLevel)
        {
            var provider = new AWSLoggerProvider(config, minLevel);
            builder.AddProvider(provider);
            return builder;
        }

        /// <summary>
        /// Adds the AWS logging provider to the log builder using the configuration specified in the AWSLoggerConfig
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="config">Configuration on how to connect to AWS and how the log messages should be sent.</param>
        /// <param name="filter">A filter function that has the logger category name and log level which can be used to filter messages being sent to AWS.</param>
        /// <returns></returns>
        public static ILoggingBuilder AddAWSProvider(this ILoggingBuilder builder, AWSLoggerConfig config, Func<string, LogLevel, bool> filter)
        {
            var provider = new AWSLoggerProvider(config, filter);
            builder.AddProvider(provider);
            return builder;
        }
    }
}
