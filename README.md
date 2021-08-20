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

### Required IAM Permissions

Regardless of the framework used, the following permissions must be allowed (via [IAM](https://aws.amazon.com/iam)) for the provided AWS credentials.

```
logs:CreateLogGroup
logs:CreateLogStream
logs:PutLogEvents
logs:DescribeLogGroups
logs:DescribeLogStreams
```

The practice of granting least privilege access is recommended when setting up credentials. You can further reduce access by limiting permission scope to specific resources (such as a Log Stream) by referencing its ARN during policy creation.

For more information and a sample JSON policy template, please see [Amazon CloudWatch Logs and .NET Logging Frameworks](https://aws.amazon.com/blogs/developer/amazon-cloudwatch-logs-and-net-logging-frameworks/) on the AWS Developer Blog.


## Supported Logging Frameworks

1. [NLog](#nlog)
2. [Apache log4net](#apache-log4net)
3. [ASP.NET Core Logging](#aspnet-core-logging)
4. [Serilog](#serilog)

### NLog

* NuGet Package: [AWS.Logger.Nlog](https://www.nuget.org/packages/AWS.Logger.NLog/)

NLog uses targets that can be configured to receive log messages. Targets can be configured either 
through a config file or through code. The default config file that NLog will automatically search for
is **NLog.config**. Here is an example config file that configures the AWS Region and the CloudWatch Logs log group.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
	  throwConfigExceptions="true">
  <extensions>
    <add assembly="NLog.AWS.Logger" />
  </extensions>
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

            // When you need logging below set the minimum level. Otherwise the logging framework will default to Informational for external providers.
            logging.SetMinimumLevel(LogLevel.Debug);
        })
        .UseStartup<Startup>();
```

### Serilog

* NuGet Package: [AWS.Logger.SeriLog](https://www.nuget.org/packages/AWS.Logger.SeriLog/)

Serilog can be configured with sinks to receive log messages either through a config file or through code. To use a config file with Serilog, follow the instructions [here](https://github.com/serilog/serilog/wiki/Configuration-Basics)
to install the necessary extensions and NuGet packages. In the json file, make sure **AWS.Logger.SeriLog** is in the **Using** 
array. Set the **LogGroup** and **Region** under the **Serilog** node, and add **AWSSeriLog** as a sink under the **WriteTo** node. Here is an example.

```json
{
  "Serilog": {
    "Using": [
      "AWS.Logger.SeriLog"
    ],
    "LogGroup": "Serilog.ConfigExample",
    "Region": "us-east-1",
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "AWSSeriLog"
      }
    ]
  }
}
```

Add the following code to configure the logger to read from the json file.

```csharp
var configuration = new ConfigurationBuilder()
.AddJsonFile("appsettings.json")
.Build();

var logger = new LoggerConfiguration()
.ReadFrom.Configuration(configuration)
.CreateLogger();
```

The AWS Credentials will be found using the standard .NET credentials search path. It will search for a profile named default, environment variables, or an instance profile on an EC2 instance.
In order to use a profile other than default, add a **Profile** under the **Serilog** node. 

Below is an example of doing the same configuration as above via code. The AWS sink can be added to the logger by using the WriteTo
method. 

```csharp
AWSLoggerConfig configuration = new AWSLoggerConfig("Serilog.ConfigExample");
configuration.Region = "us-east-1";

var logger = new LoggerConfiguration()
.WriteTo.AWSSeriLog(configuration)
.CreateLogger();
```

Checkout the [Serilog samples](/samples/Serilog) for examples of how you can use AWS and Serilog together.
