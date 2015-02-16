﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace Log4Slack {
    /// <summary>
    /// Simple client for Stack using incoming webhooks.
    /// </summary>
    public class SlackClient {
        private readonly Uri _uri;
        private readonly Encoding _encoding = Encoding.UTF8;
        private readonly string _username;
        private readonly string _channel;
        private readonly string _iconUrl;
        private readonly ArrayList _requests = ArrayList.Synchronized(new ArrayList(4));

        /// <summary>
        /// Creates a new instance of SlackClient.
        /// </summary>
        /// <param name="urlWithAccessToken">The incoming webhook URL with token.</param>
        public SlackClient(string urlWithAccessToken) {
            _uri = new Uri(urlWithAccessToken);
        }

        /// <summary>
        /// Creates a new instance of SlackClient.
        /// </summary>
        /// <param name="urlWithAccessToken">The incoming webhook URL with token.</param>
        /// <param name="username">The username to post messages as.</param>
        /// <param name="channel">The channel to post messages to.</param>
        /// <param name="iconUrl"></param>
        public SlackClient(string urlWithAccessToken, string username, string channel, string iconUrl = null) {
            _uri = new Uri(urlWithAccessToken);
            _username = username;
            _channel = channel;
            _iconUrl = iconUrl;
        }

        /// <summary>
        /// Post a message to Slack.
        /// </summary>
        /// <param name="text">The text of the message.</param>
        /// <param name="username">If provided, overrides the existing username.</param>
        /// <param name="channel">If provided, overrides the existing channel.</param>
        /// <param name="iconUrl"></param>
        /// <param name="attachments">Optional collection of attachments.</param>
        public void PostMessageAsync(string text, string username = null, string channel = null, string iconUrl = null, List<Attachment> attachments = null) {
            var payload = BuildPayload(text, username, channel, iconUrl, attachments);
            PostPayloadAsync(payload);
        }


        /// <summary>
        /// Builds a payload for Slack.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="username"></param>
        /// <param name="channel"></param>
        /// <param name="iconUrl"></param>
        /// <param name="attachments"></param>
        /// <returns></returns>
        private Payload BuildPayload(string text, string username, string channel, string iconUrl, List<Attachment> attachments = null) {
            username = string.IsNullOrEmpty(username) ? _username : username;
            channel = string.IsNullOrEmpty(channel) ? _channel : channel;
            iconUrl = string.IsNullOrEmpty(channel) ? _iconUrl : iconUrl;

            var payload = new Payload {
                Channel = channel,
                Username = username,
                IconUrl = iconUrl,
                Text = text,
                Attachments = attachments
            };

            return payload;
        }

        /// <summary>
        /// Posts a payload to Slack.
        /// </summary>
        private void PostPayloadAsync(Payload payload) {
            var data = JsonConvert.SerializeObject(payload);
            PostPayloadAsync(data);
        }

        protected virtual void PostPayloadAsync(string json) {
            HttpWebRequest request = null;

            try {
                request = (HttpWebRequest)WebRequest.Create(_uri);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";

                var encodedForm = string.Format("payload={0}", Uri.EscapeDataString(json));
                var data = _encoding.GetBytes(encodedForm);
                request.ContentLength = data.Length;

                // Get the request stream into which the form data is to 
                // be written. This is done asynchronously to free up this
                // thread.
                
                // NOTE: We maintain a (possibly paranoid) list of 
                // outstanding requests and add the request to it so that 
                // it does not get treated as garbage by GC. In effect, 
                // we are creating an explicit root. It is also possible
                // for this module to get disposed before a request
                // completes. During the callback, no other member should
                // be touched except the requests list!

                _requests.Add(request);

                var ar = request.BeginGetRequestStream(OnGetRequestStreamCompleted, AsyncArgs(request, data));
            }
            catch (Exception localException) {
                OnWebPostError(request, localException);
            }
        }

        private void OnWebPostError(WebRequest request, Exception e) {
            if (request != null) _requests.Remove(request);
        }

        private static object[] AsyncArgs(params object[] args) {
            return args;
        }

        private void OnGetRequestStreamCompleted(IAsyncResult ar) {
            if (ar == null) throw new ArgumentNullException("ar");
            var args = (object[])ar.AsyncState;
            OnGetRequestStreamCompleted(ar, (WebRequest)args[0], (byte[])args[1]);
        }

        private void OnGetRequestStreamCompleted(IAsyncResult ar, WebRequest request, byte[] data)
        {
            try {
                using (var output = request.EndGetRequestStream(ar)) {
                    output.Write(data, 0, data.Length);
                }
                request.BeginGetResponse(OnGetResponseCompleted, request);
            }
            catch (Exception e) {
                OnWebPostError(request, e);
            }
        }

        private void OnGetResponseCompleted(IAsyncResult ar) {
            if (ar == null) throw new ArgumentNullException("ar");
            OnGetResponseCompleted(ar, (WebRequest)ar.AsyncState);
        }

        private void OnGetResponseCompleted(IAsyncResult ar, WebRequest request) {
            try {
                request.EndGetResponse(ar).Close(); // Not interested; assume OK
                _requests.Remove(request);
            }
            catch (Exception e) {
                OnWebPostError(request, e);
            }
        }
    }


    /// <summary>
    /// The payload to send to Stack, which will be serialized to JSON before POSTing.
    /// </summary>
    public class Payload {
        [JsonProperty("channel")]
        public string Channel { get; set; }
        [JsonProperty("username")]
        public string Username { get; set; }
        [JsonProperty("icon_url")]
        public string IconUrl { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("attachments")]
        public List<Attachment> Attachments { get; set; }
    }

    /// <summary>
    /// It is possible to create more richly-formatted messages using Attachments.
    /// https://api.slack.com/docs/attachments
    /// </summary>
    public class Attachment {
        /// <summary>
        /// Required text summary of the attachment that is shown by clients that understand attachments but choose not to show them.
        /// </summary>
        [JsonProperty("fallback")]
        public string Fallback { get; set; }

        /// <summary>
        /// Optional text that should appear above the formatted data.
        /// </summary>
        [JsonProperty("pretext")]
        public string PreText { get; set; }

        /// <summary>
        /// Optional text that should appear within the attachment.
        /// </summary>
        [JsonProperty("text")]
        public string Text { get; set; }

        /// <summary>
        /// Can either be one of 'good', 'warning', 'danger', or any hex color code.
        /// </summary>
        [JsonProperty("color")]
        public string Color { get; set; }

        /// <summary>
        /// Fields are displayed in a table on the message.
        /// </summary>
        [JsonProperty("fields")]
        public List<Field> Fields { get; set; }
        [JsonProperty("mrkdwn_in")]
        public List<string> MarkdownIn { get; private set; }

        public Attachment(string fallback) {
            Fallback = fallback;
            MarkdownIn = new List<string> { "fields" };
        }
    }

    /// <summary>
    /// Fields are displayed in a table on the message.
    /// </summary>
    public class Field {
        /// <summary>
        /// The title may not contain markup and will be escaped for you; required.
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; }
        /// <summary>
        /// Text value of the field. May contain standard message markup and must be escaped as normal; may be multi-line.
        /// </summary>
        [JsonProperty("value")]
        public string Value { get; set; }
        /// <summary>
        /// Optional flag indicating whether the <paramref name="Value"/> is short enough to be displayed side-by-side with other values.
        /// </summary>
        [JsonProperty("short")]
        public bool Short { get; set; }

        public Field(string title) {
            Title = title;
        }
    }
}


