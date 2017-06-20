﻿using System;

namespace AWS.Logger
{
    /// <summary>
    /// This class contains all the configuration options for logging messages to AWS. As messages from the application are 
    /// sent to the logger they are queued up in a batch. The batch will be sent when either BatchPushInterval or BatchSizeInBytes
    /// are exceeded.
    /// </summary>
    public class AWSLoggerConfig : IAWSLoggerConfig
    {
        #region Public Properties

        /// <summary>
        /// Gets and sets the LogGroup property. This is the name of the CloudWatch Logs group where 
        /// streams will be created and log messages written to.
        /// </summary>
        public string LogGroup { get; set; }

        /// <summary>
        /// Gets the CheckLogGroupExistance property. If this is set to True, some checks are
        /// performed to ensure that the specified LogGroup exists. If not, the LogGroup is created.
        /// <para>
        /// The default is False.
        /// </para>
        /// </summary>
        public bool CheckLogGroupExistance { get; set; } = false;

        /// <summary>
        /// Gets the LogStream property. This is the name of the CloudWatch Logs stream within the
        /// specified LogGroup. If a LogStream is not specified, one gets created automatically.
        /// </summary>
        public string LogStream { get; set; }

        /// <summary>
        /// Gets and sets the BatchPushInterval property. For performance the log messages are sent to AWS in batch sizes. BatchPushInterval 
        /// dictates the frequency of when batches are sent. If either BatchPushInterval or BatchSizeInBytes are exceeded the batch will be sent.
        /// <para>
        /// The default is 3 seconds.
        /// </para>
        /// </summary>
        public TimeSpan BatchPushInterval { get; set; } = TimeSpan.FromMilliseconds(3000);

        /// <summary>
        /// Gets and sets the BatchSizeInBytes property. For performance the log messages are sent to AWS in batch sizes. BatchSizeInBytes 
        /// dictates the total size of the batch in bytes when batches are sent. If either BatchPushInterval or BatchSizeInBytes are exceeded the batch will be sent.
        /// <para>
        /// The default is 100 Kilobytes.
        /// </para>
        /// </summary>
        public int BatchSizeInBytes { get; set; } = 102400;

        /// <summary>
        /// Gets and sets the MaxQueuedMessages property. This specifies the maximum number of log messages that could be stored in-memory. MaxQueuedMessages 
        /// dictates the total number of log messages that can be stored in-memory. If this exceeded, incoming log messages will be dropped.
        /// <para>
        /// The default is 10000.
        /// </para>
        /// </summary>
        public int MaxQueuedMessages { get; set; } = 10000;

        /// <summary>
        /// Internal MonitorSleepTime property. This specifies the timespan after which the Monitor wakes up. MonitorSleepTime 
        /// dictates the timespan after which the Monitor checks the size and time constarint on the batch log event and the existing in-memory buffer for new messages. 
        /// <para>
        /// The value is 500 Milliseconds.
        /// </para>
        /// </summary>
        internal TimeSpan MonitorSleepTime = TimeSpan.FromMilliseconds(500);
        #endregion

        /// <summary>
        /// Default Constructor
        /// </summary>
        public AWSLoggerConfig() { }

        /// <summary>
        /// Construct instance and sets the LogGroup
        /// </summary>
        /// <param name="logGroup">The CloudWatch Logs log group.</param>
        public AWSLoggerConfig(string logGroup)
        {
            LogGroup = logGroup;
        }

        internal void ShutDown()
        {
            MonitorSleepTime = TimeSpan.FromMilliseconds(0);
            BatchPushInterval = TimeSpan.FromSeconds(0);
        }

        /// <summary>
        /// Gets and sets the LogStreamNameSuffix property. The LogStreamName consists of a DateTimeStamp as the prefix and a user defined suffix value that can 
        /// be set using the LogStreamNameSuffix property defined here.
        /// The LogstreamName then follows the pattern '[DateTime.Now.ToString("yyyy/MM/ddTHH.mm.ss")]-[LogstreamNameSuffix]'
        /// <para>
        /// The default is going to a Guid.
        /// </para>
        /// </summary>
        public string LogStreamNameSuffix { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets and sets the LibraryLogFileName property. This is the name of the file into which errors from the AWS.Logger.Core library will be wriiten into.
        /// <para>
        /// The default is "aws-logger-errors.txt".
        /// </para>
        /// </summary>
        public string LibraryLogFileName { get; set; } = "aws-logger-errors.txt";
    }
}
