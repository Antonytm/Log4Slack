using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using log4net.spi;

namespace Log4Slack {
    public class SlackAppender : log4net.Appender.AppenderSkeleton {
        private readonly Process _currentProcess = Process.GetCurrentProcess();

        /// <summary>
        /// Slack token.
        /// https://api.slack.com/
        /// </summary>
        ////public string Token { get; set; }

        /// <summary>
        /// Slack webhook URL, with token.
        /// </summary>
        public string WebhookUrl { get; set; }

        /// <summary>
        /// Slack channel to send log events to.
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Username to post to Slack as.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The URL of the icon to use, if any.
        /// </summary>
        public string IconUrl { get; set; }

        /// <summary>
        /// The name of the Emoji icon to use, if any.
        /// </summary>
        public string IconEmoji { get; set; }

        /// <summary>
        /// Indicates whether or not to include additional details in message attachments.
        /// </summary>
        public bool AddAttachment { get; set; }

        /// <summary>
        /// Indicates whether or not to include the exception traces as fields on message attachments.
        /// Requires AddAttachment be true.
        /// </summary>
        public bool AddExceptionTraceField { get; set; }

        /// <summary>
        /// Indicates whether or not to append the logger name to the Stack username.
        /// </summary>
        public bool UsernameAppendLoggerName { get; set; }

        protected override void Append(LoggingEvent loggingEvent) {
            // Initialze the Slack client
            var slackClient = new SlackClient(WebhookUrl);
            var attachments = new List<Attachment>();

            if (AddAttachment) {
                // Set fallback string
                var theAttachment = new Attachment(string.Format("[{0}] {1} in {2} on {3}", loggingEvent.Level.Name, loggingEvent.LoggerName, _currentProcess.ProcessName, Environment.MachineName));

                // Determine attachment color
                switch (loggingEvent.Level.Name) {
                    case "warn":
                        theAttachment.Color = "warning";
                        break;
                    case "error":
                    case "fatal":
                        theAttachment.Color = "danger";
                        break;
                }

                // Add attachment fields
                theAttachment.Fields = new List<Field> {
                    new Field("Process") {Value = _currentProcess.ProcessName, Short = true},
                    new Field("Machine") {Value = Environment.MachineName, Short = true}
                };
                if (!UsernameAppendLoggerName)
                    theAttachment.Fields.Insert(0, new Field("Logger") {Value = loggingEvent.LoggerName, Short = true});

                // Add exception fields if exception occurred
                
                theAttachment.Fields.Insert(0, new Field("Exception Message") { Value = loggingEvent.GetExceptionStrRep(), Short = true });

                attachments.Add(theAttachment);
            }

            String formattedMessage = loggingEvent.RenderedMessage;
            
            var username = Username;
            if (UsernameAppendLoggerName)
                username += " - " + loggingEvent.LoggerName;

            slackClient.PostMessageAsync(formattedMessage, username, Channel, IconUrl, IconEmoji, attachments);
        }

    }
}
