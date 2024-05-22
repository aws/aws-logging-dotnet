using System;
using Amazon.Runtime;

namespace AWS.Logger
{
    /// <summary>
    /// Configuration options for logging messages to AWS CloudWatch Logs
    /// </summary>
    public interface IAWSLoggerConfig
    {
        /// <summary>
        /// Gets the LogGroup property. This is the name of the CloudWatch Logs group where 
        /// streams will be created and log messages written to.
        /// </summary>
        string LogGroup { get; }

        /// <summary>
        /// Determines whether or not to create a new Log Group, if the one specified by <see cref="LogGroup"/> doesn't already exist
        /// If false (the default), the Log Group is created if it doesn't already exist. This requires logs:DescribeLogGroups
        /// permission to determine if the group exists, and logs:CreateLogGroup permission to create the group if it doesn't already exist.
        /// If true, creation of Log Groups is disabled. Logging functions only if the specified log group already exists.
        /// When creation of log groups is disabled, logs:DescribeLogGroups permission is NOT required.
        /// </summary>
        bool DisableLogGroupCreation { get; set; }
        
        /// <summary>
        /// Specifies the days to retain events in log groups which are created by this logger, if the one specified by <see cref="LogGroup"/> doesn't already exist
        /// and <see cref="DisableLogGroupCreation"/> is not <c>true</c>. Requires logs:PutRetentionPolicy permission to apply the retention policy to
        /// newly created log groups. The default value of <c>null</c> will not apply a retention policy to new log groups.
        /// <para>
        /// Note that log groups which already exist will not have this retention policy applied for startup performance reasons.
        /// </para>
        /// <para>
        /// Possible values could be found in the CloudWatchLogs API reference at https://docs.aws.amazon.com/AmazonCloudWatchLogs/latest/APIReference/API_PutRetentionPolicy.html#API_PutRetentionPolicy_RequestSyntax
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note that invalid retention policy values will result in the policy not being applied, however this error is non-fatal and the application and will continue without the policy.
        /// </remarks>
        int? NewLogGroupRetentionInDays { get; set; }

        /// <summary>
        /// Gets the Profile property. The profile is used to look up AWS credentials in the profile store.
        /// <para>
        /// For understanding how credentials are determine view the top level documentation for AWSLoggerConfig class.
        /// </para>
        /// </summary>
        string Profile { get;  }

        /// <summary>
        /// Gets the ProfilesLocation property. If this is not set the default profile store is used by the AWS SDK for .NET 
        /// to look up credentials. This is most commonly used when you are running an application of on-priemse under a service account.
        /// <para>
        /// For understanding how credentials are determine view the top level documentation for AWSLoggerConfig class.
        /// </para>
        /// </summary>
        string ProfilesLocation { get;  }

        /// <summary>
        /// Gets the Credentials property. These are the AWS credentials used by the AWS SDK for .NET to make service calls.
        /// <para>
        /// For understanding how credentials are determine view the top level documentation for AWSLoggerConfig class.
        /// </para>
        /// </summary>
        AWSCredentials Credentials { get;  }

        /// <summary>
        /// Gets the Region property. This is the AWS Region that will be used for CloudWatch Logs. If this is not
        /// the AWS SDK for .NET will use its fall back logic to try and determine the region through environment variables and EC2 instance metadata.
        /// If the Region is not set and no region is found by the SDK's fall back logic then an exception will be thrown.
        /// </summary>
        string Region { get;  }

        /// <summary>
        /// Gets and sets of the ServiceURL property. This is an optional property; change
        /// it only if you want to try a different service endpoint. Ex. for LocalStack
        /// </summary>
        string ServiceUrl { get; }

        /// <summary>
        /// Gets the BatchPushInterval property. For performance the log messages are sent to AWS in batch sizes. BatchPushInterval 
        /// dictates the frequency of when batches are sent. If either BatchPushInterval or BatchSizeInBytes are exceeded the batch will be sent.
        /// <para>
        /// The default is 3 seconds.
        /// </para>
        /// </summary>
        TimeSpan BatchPushInterval { get;  }

        /// <summary>
        /// Gets the BatchSizeInBytes property. For performance the log messages are sent to AWS in batch sizes. BatchSizeInBytes 
        /// dictates the total size of the batch in bytes when batches are sent. If either BatchPushInterval or BatchSizeInBytes are exceeded the batch will be sent.
        /// <para>
        /// The default is 100 Kilobytes.
        /// </para>
        /// </summary>
        int BatchSizeInBytes { get; }

        /// <summary>
        /// Gets and sets the MaxQueuedMessages property. This specifies the maximum number of log messages that could be stored in-memory. MaxQueuedMessages 
        /// dictates the total number of log messages that can be stored in-memory. If this is exceeded, incoming log messages will be dropped.
        /// <para>
        /// The default is 10000.
        /// </para>
        /// </summary>
        int MaxQueuedMessages { get; }

        /// <summary>
        /// Gets and sets the LogStreamName property. When this is set the full name of the log stream will be equal to this value,
        /// as opposed to the computed value using <see cref="LogStreamNamePrefix"/> and <see cref="LogStreamNameSuffix"/>, which will be ignored.
        /// </summary>
        /// <para>
        /// The default is an empty string.
        /// </para>
        string LogStreamName { get; }

        /// <summary>
        /// Gets and sets the LogStreamNameSuffix property. The LogStreamName consists of an optional user-defined LogStreamNamePrefix (that can be set here)
        /// followed by a DateTimeStamp as the prefix, and a user defined suffix value
        /// The LogstreamName then follows the pattern '[LogStreamNamePrefix]-[DateTime.Now.ToString("yyyy/MM/ddTHH.mm.ss")]-[LogStreamNameSuffix]'
        /// <para>
        /// The default is new a Guid.
        /// </para>
        /// </summary>
        string LogStreamNameSuffix { get; }

        /// <summary>
        /// Gets and sets the LogStreamNamePrefix property. The LogStreamName consists of an optional user-defined LogStreamNamePrefix (that can be set here)
        /// followed by a DateTimeStamp as the prefix, and a user defined suffix value
        /// The LogstreamName then follows the pattern '[LogStreamNamePrefix]-[DateTime.Now.ToString("yyyy/MM/ddTHH.mm.ss")]-[LogStreamNameSuffix]'
        /// <para>
        /// The default is an empty string.
        /// </para>
        /// </summary>
        string LogStreamNamePrefix { get; set; }

        /// <summary>
        /// Gets and sets the LibraryLogErrors property. This is the boolean value of whether or not you would like this library to log logging errors.
        /// <para>
        /// The default is "true".
        /// </para>
        /// </summary>
        bool LibraryLogErrors { get; set; }
        
        /// <summary>
        /// Gets and sets the LibraryLogFileName property. This is the name (and optional path) of the file into which errors from the AWS.Logger.Core library will be written into.
        /// <para>
        /// The default is going to "aws-logger-errors.txt".
        /// </para>
        /// </summary>
        string LibraryLogFileName { get; }

        /// <summary>
        /// Gets the FlushTimeout property. The value is in milliseconds. When performing a flush of the in-memory queue this is the maximum period of time allowed to send the remaining
        /// messages before it will be aborted. If this is exceeded, incoming log messages will be dropped.
        /// <para>
        /// The default is 30000 milliseconds.
        /// </para>
        /// </summary>
        TimeSpan FlushTimeout { get; }

        /// <summary>
        /// Gets and sets the AuthenticationRegion property. Used in AWS4 request signing, this is an optional property; 
        /// change it only if the region cannot be determined from the service endpoint.
        /// </summary>
        string AuthenticationRegion { get; set; }
    }
}
