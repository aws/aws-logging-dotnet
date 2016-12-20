/*
 * Copyright 2012-2013 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 * 
 *  http://aws.amazon.com/apache2.0
 * 
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using System.Diagnostics;
using AWS.Logger.Core;
using System;
using Amazon.Runtime;

namespace AWS.Logger.SystemDiagnostics
{
    public class AWSTraceListener: TraceListener,IAWSLoggerConfig
    {
        AWSLoggerConfig _config = new AWSLoggerConfig();
        AWSLoggerCore _core = null;

        /// <summary>
        /// Gets and sets the LogGroup property. This is the name of the CloudWatch Logs group where 
        /// streams will be created and log messages written to.
        /// </summary>
        public string LogGroup
        {
            get { return _config.LogGroup; }
            set { _config.LogGroup = value; }
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

        public AWSTraceListener()
        {

        }
        public AWSTraceListener(AWSLoggerConfig config)
        {
            _core = new AWSLoggerCore(config);
        }
        public AWSTraceListener(string config)
        {
            string[] clientConfig = config.Split(',');
            foreach(string i in clientConfig)
            {
                string[] propertyNameValue = i.Split('=');
                if (String.Equals(propertyNameValue[0], "logGroup") && (propertyNameValue[1] != null))
                {
                   LogGroup = propertyNameValue[1];
                }
                if (String.Equals(propertyNameValue[0], "region") && (propertyNameValue[1] != null))
                {
                    Region = propertyNameValue[1];
                }
                if (String.Equals(propertyNameValue[0], "logPushInterval") && (propertyNameValue[1] != null))
                {
                    BatchPushInterval = TimeSpan.FromMilliseconds(Int32.Parse(propertyNameValue[1]));
                }
                if (String.Equals(propertyNameValue[0], "logPushSizeInBytes") && (propertyNameValue[1] != null))
                {
                    BatchSizeInBytes = Int32.Parse(propertyNameValue[1]);
                }
            }
        }
        private void Log(TraceEventCache eventCache, string source, TraceEventType eventType, int eventId, params object[] data)
        {
            _core.AddMessage(data[0].ToString());
        }
        private TraceEventCache TraceEventCache { get { return new TraceEventCache(); } }

        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, object data)
        {
            Log(eventCache, source, eventType, id, data);
        }
        public override void WriteLine(string message)
        {
            TraceData(TraceEventCache, this.Name, TraceEventType.Information, 0, message);
        }
        public override void Write(string message)
        {
            TraceData(TraceEventCache, this.Name, TraceEventType.Information, 0, message);
        }
    }
}
