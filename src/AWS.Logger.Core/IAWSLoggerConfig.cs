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
        bool DontCreateLogGroup { get; set; }

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
        /// dictates the total number of log messages that can be stored in-memory. If this exceeded, incoming log messages will be dropped.
        /// <para>
        /// The default is 10000.
        /// </para>
        /// </summary>
        int MaxQueuedMessages { get; }

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
        /// Gets and sets the LibraryLogFileName property. This is the name of the file into which errors from the AWS.Logger.Core library will be wriiten into.
        /// <para>
        /// The default is going to "aws-logger-errors.txt".
        /// </para>
        /// </summary>
        string LibraryLogFileName { get; }
    }
}
