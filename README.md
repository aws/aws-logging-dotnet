# AWS Logging .NET

This repository contains plugins for popular .NET logging frameworks that integrate 
with Amazon Web Services. The plugins use the 
Amazon CloudWatch Logs service to write log data to a configured 
log group. The logs can be viewed and searched using the [AWS CloudWatch Console](https://console.aws.amazon.com/cloudwatch/).

For a history of releases view the [release change log](RELEASE.CHANGELOG.md)

### AWS Lambda

These packages batch logging messages in a queue and send messages to CloudWatch Logs using a background thread. 
The use of the background thread means that the messages are not guaranteed to be delivered when used in AWS Lambda.
The reason is because the background thread will be frozen once a Lambda event is processed and 
may not ever be unfrozen if more Lambda events are not received for some time.

When using Lambda it is recommended to use either the `ILambdaContext.Logger.LogLine` or the 
[Amazon.Lambda.Logging.AspNetCore](https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.Logging.AspNetCore) package.


## Supported Logging Frameworks

1. [NLog](#nlog)
2. [Apache log4net](#apache-log4net)
3. [ASP.NET Core Logging](#aspnet-core-logging)

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
    <logger name="*" minlevel="Info" writeTo="aws" />
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

**Note:** Starting with version 2.0.0 of AWS.Logger.AspNetCore this library targets netstandard2.0 and the dependencies have been
upgraded to the ASP.NET Core 2.1 versions. For older versions of .NET Core, which Microsoft has made end of life, use versions before 2.0.0.

The [WebSample](/samples/AspNetCore/WebSample) in this repository demonstrates how to configure
this provider.

The configuration is setup in the [appsettings.json](/samples/AspNetCore/WebSample/appsettings.json) file. In versions before 2.0.0 the `AWS.Logging`
was used as the configuration section root. Starting with 2.0.0 the library has switched to use the standard `Logging` configuration section root.
For backwards compatibility if the `Logging` section does not contain a `LogGroup` then the library will fallback to `AWS.Logging`.

```json
"Logging": {
  "Region": "us-east-1",
  "LogGroup": "AspNetCore.WebSample",
  "IncludeLogLevel": true,
  "IncludeCategory": true,
  "IncludeNewline": true,
  "IncludeException": true,
  "IncludeEventId": false,
  "IncludeScopes": false,
  "LogLevel": {
    "Default": "Debug",
    "System": "Information",
    "Microsoft": "Information"
  }
}
```

In a typical ASP.NET Core application the `Program.cs` file contains a `CreateWebHostBuilder` method. To include AWS.Logger.AspNetCore
add a call to `ConfigureLogging` and in the `Action<ILoggingBuilder>` passed into ConfigureLogging call `AddAWSProvider`. This will look up the configuration
information from the IConfiguration added to the dependency injection system.

```csharp
public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
    WebHost.CreateDefaultBuilder(args)
        .ConfigureLogging(logging =>
        {
            logging.AddAWSProvider();
        })
        .UseStartup<Startup>();
```
