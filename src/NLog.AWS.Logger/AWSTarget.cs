using System;

using NLog;
using NLog.Targets;
using NLog.Common;
using NLog.Config;

using AWS.Logger;
using AWS.Logger.Core;
using Amazon.Runtime;

namespace NLog.AWS.Logger
{
    /// <summary>
    /// An NLog target that can be used with the NLog logging library to send messages to AWS.
    /// </summary>
    [Target("AWSTarget")]
    public class AWSTarget : TargetWithLayout, IAWSLoggerConfig
    {
        AWSLoggerConfig _config = new AWSLoggerConfig();
        AWSLoggerCore _core = null;
		
        /// <summary>
        /// Default Constructor
        /// </summary>
        public AWSTarget()
        {
            this.OptimizeBufferReuse = true;
        }

        /// <summary>
        /// Gets and sets the LogGroup property. This is the name of the CloudWatch Logs group where 
        /// streams will be created and log messages written to.
        /// </summary>
        [RequiredParameter]
        public string LogGroup
        {
            get { return _config.LogGroup; }
            set { _config.LogGroup = value; }
        }

        /// <summary>
        /// Determines whether or not to create a new Log Group, if the one specified by <see cref="LogGroup"/> doesn't already exist
        /// <seealso cref="AWSLoggerConfig.DisableLogGroupCreation"/>
        /// </summary>
        public bool DisableLogGroupCreation
        {
            get { return _config.DisableLogGroupCreation; }
            set { _config.DisableLogGroupCreation = value; }
        }

        /// <summary>
        /// Gets and sets the Profile property. The profile is used to look up AWS credentials in the profile store.
        /// <para>
        /// For understanding how credentials are determine view the top level documentation for AWSLoggerConfig class.
        /// </para>
        /// </summary>
        public string Profile
        {
            get { return _config.Profile; }
            set { _config.Profile = value; }
        }


        /// <summary>
        /// Gets and sets the ProfilesLocation property. If this is not set the default profile store is used by the AWS SDK for .NET 
        /// to look up credentials. This is most commonly used when you are running an application of on-priemse under a service account.
        /// <para>
        /// For understanding how credentials are determine view the top level documentation for AWSLoggerConfig class.
        /// </para>
        /// </summary>
        public string ProfilesLocation
        {
            get { return _config.ProfilesLocation; }
            set { _config.ProfilesLocation = value; }
        }


        /// <summary>
        /// Gets and sets the Credentials property. These are the AWS credentials used by the AWS SDK for .NET to make service calls.
        /// <para>
        /// For understanding how credentials are determine view the top level documentation for AWSLoggerConfig class.
        /// </para>
        /// </summary>
        public AWSCredentials Credentials
        {
            get { return _config.Credentials; }
            set { _config.Credentials = value; }
        }


        /// <summary>
        /// Gets and sets the Region property. This is the AWS Region that will be used for CloudWatch Logs. If this is not
        /// the AWS SDK for .NET will use its fall back logic to try and determine the region through environment variables and EC2 instance metadata.
        /// If the Region is not set and no region is found by the SDK's fall back logic then an exception will be thrown.
        /// </summary>
        public string Region
        {
            get { return _config.Region; }
            set { _config.Region = value; }
        }

        /// <summary>
        /// Gets and sets of the ServiceURL property. This is an optional property; change
        /// it only if you want to try a different service endpoint. Ex. for LocalStack
        /// </summary>
        public string ServiceUrl
        {
            get { return _config.ServiceUrl; }
            set { _config.ServiceUrl = value; }
        }

        /// <summary>
        /// Gets and sets the BatchPushInterval property. For performance the log messages are sent to AWS in batch sizes. BatchPushInterval 
        /// dictates the frequency of when batches are sent. If either BatchPushInterval or BatchSizeInBytes are exceeded the batch will be sent.
        /// <para>
        /// The default is 3 seconds.
        /// </para>
        /// </summary>
        public TimeSpan BatchPushInterval
        {
            get { return _config.BatchPushInterval; }
            set { _config.BatchPushInterval = value; }
        }


        /// <summary>
        /// Gets and sets the BatchSizeInBytes property. For performance the log messages are sent to AWS in batch sizes. BatchSizeInBytes 
        /// dictates the total size of the batch in bytes when batches are sent. If either BatchPushInterval or BatchSizeInBytes are exceeded the batch will be sent.
        /// <para>
        /// The default is 100 Kilobytes.
        /// </para>
        /// </summary>
        public int BatchSizeInBytes
        {
            get { return _config.BatchSizeInBytes; }
            set { _config.BatchSizeInBytes = value; }
        }

        /// <summary>
        /// Gets and sets the MaxQueuedMessages property. This specifies the maximum number of log messages that could be stored in-memory. MaxQueuedMessages 
        /// dictates the total number of log messages that can be stored in-memory. If this exceeded, incoming log messages will be dropped.
        /// <para>
        /// The default is 10000.
        /// </para>
        /// </summary>
        public int MaxQueuedMessages
        {
            get { return _config.MaxQueuedMessages; }
            set { _config.MaxQueuedMessages = value; }
        }

        /// <summary>
        /// Gets and sets the LogStreamNameSuffix property. The LogStreamName consists of an optional user-defined prefix segment, then a DateTimeStamp as the
        /// system-defined prefix segment, and a user defined suffix value that can be set using the LogStreamNameSuffix property defined here.
        /// <para>
        /// The default is going to a Guid.
        /// </para>
        /// </summary>
        public string LogStreamNameSuffix
        {
            get { return _config.LogStreamNameSuffix; }
            set { _config.LogStreamNameSuffix = value; }
        }

        /// <summary>
        /// Gets and sets the LogStreamNamePrefix property. The LogStreamName consists of an optional user-defined prefix segment (defined here), then a
        /// DateTimeStamp as the system-defined prefix segment, and a user defined suffix value that can be set using the LogStreamNameSuffix property.
        /// <para>
        /// The default will use an empty string for this user-defined portion, meaning the log stream name will start with the system-defined portion of the prefix (yyyy/MM/dd ... )
        /// </para>
        /// </summary>
        public string LogStreamNamePrefix
        {
            get { return _config.LogStreamNamePrefix; }
            set { _config.LogStreamNamePrefix = value; }
        }

        /// <summary>
        /// Gets and sets the LibraryLogErrors property. This is the boolean value of whether or not you would like this library to log logging errors.
        /// <para>
        /// The default is "true".
        /// </para>
        /// </summary>
        public bool LibraryLogErrors
        {
            get { return _config.LibraryLogErrors; }
            set { _config.LibraryLogErrors = value; }
        }

        /// <summary>
        /// Gets and sets the LibraryLogFileName property. This is the name of the file into which errors from the AWS.Logger.Core library will be written into.
        /// <para>
        /// The default is "aws-logger-errors.txt".
        /// </para>
        /// </summary>
        public string LibraryLogFileName
        {
            get { return _config.LibraryLogFileName; }
            set { _config.LibraryLogFileName = value; }
        }

        /// <summary>
        /// Gets the FlushTimeout property. When performing a flush of the in-memory queue this is the maximum period of time allowed to send the remaining
        /// messages before it will be aborted. If this is exceeded, incoming log messages will be dropped.
        /// <para>
        /// The default is 30 seconds.
        /// </para>
        /// </summary>
        public TimeSpan FlushTimeout
        {
            get { return _config.FlushTimeout; }
            set { _config.FlushTimeout = value; }
        }

        /// <inheritdoc/>
        protected override void InitializeTarget()
        {
            if (_core != null)
            {
                _core.Close();
                _core = null;
            }

            var config = new AWSLoggerConfig(RenderSimpleLayout(LogGroup, nameof(LogGroup)))
            {
                DisableLogGroupCreation = DisableLogGroupCreation,
                Region = RenderSimpleLayout(Region, nameof(Region)),
                ServiceUrl = RenderSimpleLayout(ServiceUrl, nameof(ServiceUrl)),
                Credentials = Credentials,
                Profile = RenderSimpleLayout(Profile, nameof(Profile)),
                ProfilesLocation = RenderSimpleLayout(ProfilesLocation, nameof(ProfilesLocation)),
                BatchPushInterval = BatchPushInterval,
                BatchSizeInBytes = BatchSizeInBytes,
                MaxQueuedMessages = MaxQueuedMessages,
                LogStreamNameSuffix = RenderSimpleLayout(LogStreamNameSuffix, nameof(LogStreamNameSuffix)),
                LogStreamNamePrefix = RenderSimpleLayout(LogStreamNamePrefix, nameof(LogStreamNamePrefix)),
                LibraryLogErrors = LibraryLogErrors,
                LibraryLogFileName = LibraryLogFileName,
                FlushTimeout = FlushTimeout
            };
            _core = new AWSLoggerCore(config, "NLog");
            _core.LogLibraryAlert += AwsLogLibraryAlert;
        }

        private string RenderSimpleLayout(string simpleLayout, string propertyName)
        {
            try
            {
                return string.IsNullOrEmpty(simpleLayout) ? string.Empty : new Layouts.SimpleLayout(simpleLayout).Render(LogEventInfo.CreateNullEvent());
            }
            catch (Exception ex)
            {
                InternalLogger.Debug(ex, "AWSTarget(Name={0}) - Could not render Layout for {1}", Name, propertyName);
                return simpleLayout;
            }
        }

        private void AwsLogLibraryAlert(object sender, AWSLoggerCore.LogLibraryEventArgs e)
        {
            InternalLogger.Error(e.Exception, "AWSTarget(Name={0}) - CloudWatch Network Error - ServiceUrl={1}", Name, e.ServiceUrl);
        }

        /// <inheritdoc/>
        protected override void Write(LogEventInfo logEvent)
        {
            var message = RenderLogEvent(this.Layout, logEvent);
            _core.AddMessage(message);
        }

        /// <inheritdoc/>
        protected override void FlushAsync(AsyncContinuation asyncContinuation)
        {
            try
            {
                _core.Flush();
                asyncContinuation(null);
            }
            catch (Exception ex)
            {
                asyncContinuation(ex);
            }
        }
    }
}
