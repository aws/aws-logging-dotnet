using System;

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
        /// Gets the CheckLogGroupExistance property. If this is set to True, some checks are
        /// performed to ensure that the specified LogGroup exists. If not, the LogGroup is created.
        /// </summary>
        bool CheckLogGroupExistance { get; }

        /// <summary>
        /// Gets the LogStream property. This is the name of the CloudWatch Logs stream within the
        /// specified LogGroup. If a LogStream is not specified, one gets created automatically.
        /// </summary>
        string LogStream { get; }

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
        /// Gets and sets the LogStreamNameSuffix property. The LogStreamName consists of a DateTimeStamp as the prefix and a user defined suffix value that can 
        /// be set using the LogStreamNameSuffix property defined here.
        /// <para>
        /// The default is going to a Guid.
        /// </para>
        /// </summary>
        string LogStreamNameSuffix { get; }

        /// <summary>
        /// Gets and sets the LibraryLogFileName property. This is the name of the file into which errors from the AWS.Logger.Core library will be wriiten into.
        /// <para>
        /// The default is going to "aws-logger-errors.txt".
        /// </para>
        /// </summary>
        string LibraryLogFileName { get; }
    }
}
