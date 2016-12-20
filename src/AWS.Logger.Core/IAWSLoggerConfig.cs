using System;
using Amazon.Runtime;

namespace AWS.Logger
{
    public interface IAWSLoggerConfig
    {
        /// <summary>
        /// Gets the LogGroup property. This is the name of the CloudWatch Logs group where 
        /// streams will be created and log messages written to.
        /// </summary>
        string LogGroup { get; }

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

        int MaxQueuedMessages { get; }
    }
}
