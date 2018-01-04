### Release 2018-01-04 00:02 UTC
* Modified AssemblyInfo.cs with correct version numbers for all the libraries

### Release 2017-12-11 00:02 UTC
* **AWS.Logger.SeriLog (1.0.0)**
    * Added support for SeriLog logging library.
* **AWS.Logger.Log4net (1.1.4)**
    * Added support for Log4net logging library for NetStandard1.5 framework.

### Release 2017-09-28 00:02 UTC
* **AWS.Logger.AspNetCore (1.2.4)**
    * Added exception logging for default ASP.NET Core formatter.

### Release 2017-08-22 00:02 UTC
* **AWS.Logger.AspNetCore (1.2.2)**
    * Fixed issue with **Profile** setting not getting used.

### Release 2017-07-25 10:30 UTC
* **AWS.Logger.Core (1.1.3)**
    * Updated the logic in AWSLoggerCore for creating a logstream. AWSLoggerCore library will try to use the sequence token in the response message of a log PutLogEventsAsync request for a maximum of 5 attempts, before creating a new logstream.
* **AWS.Logger.AspNetCore (1.2.2)**
    * Updated dependency to latest AWS.Logger.Core
* **AWS.Logger.Log4net (1.1.3)**
    * Updated dependency to latest AWS.Logger.Core
* **AWS.Logger.NLog (1.1.3)**
    * Updated dependency to latest AWS.Logger.Core

### Release 2017-06-23 21:30 UTC
* **AWS.Logger.Core (1.1.2)**
    * Pull request [#19](https://github.com/aws/aws-logging-dotnet/pull/19), fixing a NPE. Thanks to [Andrew Kazyrevich](https://github.com/andreister)
* **AWS.Logger.AspNetCore (1.2.1)**
    * Updated dependency to latest AWS.Logger.Core
* **AWS.Logger.Log4net (1.1.2)**
    * Updated dependency to latest AWS.Logger.Core
* **AWS.Logger.NLog (1.1.2)**
    * Updated dependency to latest AWS.Logger.Core
	
### Release 2017-06-22 21:30 UTC
* **AWS.Logger.AspNetCore (1.2.0)**
    * Pull request [#14](https://github.com/aws/aws-logging-dotnet/pull/14), adding support for custom formatters. Thanks to [Peter Deme](https://github.com/peterdeme).

### Release 2017-05-18 14:30 UTC
* **AWS.Logger.Core (1.1.1)**
    * Added LibraryLogFileName property to log errors encountered by AWSLoggerCore
    * Upgraded library to NetStandard1.5 framework to support System.Runtime.Loader v4.0.0
* **AWS.Logger.AspNetCore (1.1.1)**
    * Added LibraryLogFileName property to log errors encountered by AWSLoggerCore
    * Upgraded library to NetStandard1.5 framework to match AWS.Logger.Core framework
* **AWS.Logger.Log4net (1.1.1)**
    * Added LibraryLogFileName property to log errors encountered by AWSLoggerCore
* **AWS.Logger.NLog (1.1.1)**
    * Added LibraryLogFileName property to log errors encountered by AWSLoggerCore
    * Upgraded library to NetStandard1.5 framework to match AWS.Logger.Core framework
    * Corrected NLog nuspec file to add NLog 4.4.9 for framework v4.5.2
    * Upgraded AWS.Logger.NLog netcore package NLog dependency from v4.4.0-beta5 to 5.0.0-beta07

### Release 2017-05-08 14:30 UTC
* **AWS.Logger.Core (1.1.0)**
    * Added LogStreamNameSuffix property to custom name logStream on CloudWatchLogs
* **AWS.Logger.AspNetCore (1.1.0)**
    * Added LogStreamNameSuffix property to custom name logStream on CloudWatchLogs
* **AWS.Logger.Log4net (1.1.0)**
    * Added LogStreamNameSuffix property to custom name logStream on CloudWatchLogs
* **AWS.Logger.NLog (1.1.0)**
    * Added LogStreamNameSuffix property to custom name logStream on CloudWatchLogs
    * Corrected NLog nuspec file to show dependency on the AWS.Logger.Core

### Release 2016-12-20 09:00 UTC
* **AWS.Logger.Core (1.0.0)**
    * Initial Release
* **AWS.Logger.AspNetCore (1.0.0)**
    * Initial Release
* **AWS.Logger.Log4net (1.0.0)**
    * Initial Release
* **AWS.Logger.NLog (1.0.0)**
    * Initial Release



