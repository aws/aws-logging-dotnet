# AWS Logging .NET

This repository contains plugins for popular .NET logging frameworks that integrate 
with Amazon Web Services. The plugins use the 
Amazon CloudWatch Logs service to write log data to a configured 
log group. The logs can be viewed and searched using the [AWS CloudWatch Console](https://console.aws.amazon.com/cloudwatch/).

For a history of releases view the [release change log](RELEASE.CHANGELOG.md)
## Supported Logging Frameworks

1. [NLog](#nlog)
2. [Apache log4net](#log4net)
3. [ASP.NET Core Logging](#asp.net-core-logging)

### NLog

* NuGet Package: [AWS.Logger.Nlog](https://www.nuget.org/packages/AWS.Logger.NLog/)

NLog uses targets that can be configured to receive log messages. Targets can be configured either 
through a config file or through code. The default config file that NLog will automatically search for
is **NLog.config**. Here is an example config file that configures the AWS Region and the CloudWatch Logs log group.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      throwExceptions="true">
  <targets>
    <target name="aws" type="AWSTarget" logGroup="NLog.ConfigExample" region="us-east-1"/>
  </targets>
  <rules>
    <logger name="*" minlevel="Info" writeTo="logfile,aws" />
  </rules>
</nlog>
```

The AWS credentials will be found using the standard AWS SDK for .NET credentials search path. In this case
it will look for a profile named default, search for environment variables or search for an instance profile on an 
EC2 instance. To use a specific AWS credential profile use the **profile** attribute on the target.

Here is an example of performing the same configuration via code.

```csharp
var config = new LoggingConfiguration();

var awsTarget = new AWSTarget()
{
    LogGroup = "NLog.ProgrammaticConfigurationExample",
    Region = "us-east-1"
};
config.AddTarget("aws", awsTarget);

config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, awsTarget));

LogManager.Configuration = config;
```

Checkout the [NLog samples](/samples/NLog) for examples on how you can use AWS and NLog together. 

### Apache log4net

* NuGet Package: [AWS.Logger.Log4net](https://www.nuget.org/packages/AWS.Logger.Log4net/)

Log4net configures appenders to receive log messages. Appenders can be configured either 
through a config file or through code. To use a config file add a file to your project. 
The file can be named anything but for this example call it log4net.config. Make sure
that **Copy to Output Directory** is set to copy. Here is an example config file setting
the CloudWatch Log log group and the AWS Region. 

```xml
<?xml version="1.0" encoding="utf-8" ?>
<log4net>
  <appender name="AWS" type="AWS.Logger.Log4net.AWSAppender,AWS.Logger.Log4net">

    <LogGroup>Log4net.ConfigExample</LogGroup>
    <Region>us-east-1</Region>
    
    <layout type="log4net.Layout.PatternLayout">
      <conversionPattern value="%-4timestamp [%thread] %-5level %logger %ndc - %message%newline" />
    </layout>
  </appender>

  <root>
    <level value="DEBUG" />
    <appender-ref ref="AWS" />
  </root>
</log4net>
```

The AWS credentials will be found using the standard AWS SDK for .NET credentials search path. In this case
it will look for a profile named default, search for environment variables or search for an instance profile on an 
EC2 instance. To use a specific AWS credential profile add a **Profile** under the **appender** node.

Add the following code during the startup of the application to have log4net read the configuration file.
```
// log4net is configured in the log4net.config file which adds the AWS appender.
XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));
```

Here is an example of performing the same configuration via code.

```csharp
static void ConfigureLog4net()
{
    Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
    PatternLayout patternLayout = new PatternLayout();

    patternLayout.ConversionPattern = "%-4timestamp [%thread] %-5level %logger %ndc - %message%newline";
    patternLayout.ActivateOptions();

    AWSAppender appender = new AWSAppender();
    appender.Layout = patternLayout;

    // Set log group and region. Assume credentials will be found using the default profile or IAM credentials.
    appender.LogGroup = "Log4net.ProgrammaticConfigurationExample";
    appender.Region = "us-east-1";

    appender.ActivateOptions();
    hierarchy.Root.AddAppender(appender);

    hierarchy.Root.Level = Level.All;
    hierarchy.Configured = true;
}

```

Checkout the [Log4net samples](/samples/Log4net) for examples of how you can use AWS and log4net together. 

### ASP.NET Core Logging

* NuGet Package: [AWS.Logger.AspNetCore](https://www.nuget.org/packages/AWS.Logger.AspNetCore/)

ASP.NET Core introduced a new [logging framework](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging) that has providers configured to send logs to destinations. 
The AWS.Logger.AspNetCore NuGet package provides a log provider which adds CloudWatch Logs as a destination for the logs.

The [WebSample](/samples/AspNetCore/WebSample) in this repository demonstrates how to configure
this provider.

The configuration is setup in the [appsettings.json](/samples/AspNetCore/WebSample/appsettings.json) file

```json
"AWS.Logging": {
  "Region": "us-east-1",
  "LogGroup": "AspNetCore.WebSample",
  "LogLevel": {
    "Default": "Debug",
    "System": "Information",
    "Microsoft": "Information"
  }
}
```

In Startup.cs the configuration is built using the config files and assigned to the Configuration property.

```csharp
public Startup(IHostingEnvironment env)
{
    // Read the appsetting.json file for the configuration details
    var builder = new ConfigurationBuilder()
        .SetBasePath(env.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables();
    Configuration = builder.Build();
}
```

The `Configure` method is used to configure the services added to the dependency injection system. This is where 
log providers are configured. For the `AWS.Logger.AspNetCore` the configuration for the provider is loaded from 
the Configuration property and the AWS provider is added to the `ILoggerFactory`.

```csharp
public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
{
    // Create a logging provider based on the configuration information passed through the appsettings.json
    loggerFactory.AddAWSProvider(this.Configuration.GetAWSLoggingConfigSection());

    ...
```