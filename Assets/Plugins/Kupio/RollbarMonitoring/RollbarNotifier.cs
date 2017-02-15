/* MIT License

Copyright (c) 2017 Kupio Limited. Registered in Scotland; SC426881

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. */

namespace com.kupio.rollbarnotifier
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using UnityEngine;

    public class RollbarNotifier : MonoBehaviour
    {
        public string ClientToken;

        public string ProductionEnvironment;
        public string DevelopmentEnvironment;

        public bool Production;
        public bool Development;

        public bool DebugLogForMessages;
        public bool DebugLogForExceptions;

        private Queue<string> queue = new Queue<string>();

        public static RollbarNotifier Instance = null;

        /// <summary>
        /// Live is the currently processing item on the queue,
        /// or null if the way is clear for a request .
        /// </summary>
        private string _live = null;

        private string _userID = null;
        private string _userEmail = null;
        private string _userName = null;
        private string _versionString;
        private bool _isReady = false;

        void Awake()
        {
            if (Instance != null)
            {
                throw new ApplicationException("Can only have one RollbarNotifier in a project");
            }

            Instance = this;

            Application.logMessageReceived += HandleException;

            _isReady = true;
        }

        public bool IsReady
        {
            get
            {
                return _isReady;
            }
        }

        private void HandleException(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception)
            {
                return;
            }

            if (Debug.isDebugBuild)
            {
                if (DebugLogForExceptions)
                {
                    Debug.Log("[Rollbar exception] " + condition);
                }
            }

#if UNITY_EDITOR
            if (!Development)
            {
                return;
            }
#else
            if (!Production)
            {
                return;
            }
#endif

            string level = "error";

            string[] frames = stackTrace.Split('\n');

            List<string> frameMangled = new List<string>();
            for (int i = 0; i < frames.Length; i++)
            {
                string entry = frames[i].Trim().Replace('\'', ' ').Replace('"', ' ');
                if (entry.Length > 0)
                {
                    frameMangled.Add("{\"filename\":\"" + entry + "\"}");
                }
            }

            condition = condition.Trim().Replace('\'', ' ').Replace('"', ' ');
            string[] title = condition.Split(':');
            string clazz = title.Length == 2 ? title[0].Trim() : "exception";
            string msg = title.Length == 2 ? title[1].Trim() : condition;

            string user = GetUserFragment();

            string payload =
                "{" +
                    "\"access_token\": \"" + Instance.ClientToken + "\"," +
                    "\"data\": {" +
#if UNITY_EDITOR
                        "\"environment\": \"" + Instance.DevelopmentEnvironment + "\"," +
#else
                        "\"environment\": \"" + Instance.ProductionEnvironment + "\"," +
#endif
                        "\"body\": {" +
                            "\"trace\": {" +
                                "\"frames\": [" +
                                    string.Join(",", frameMangled.ToArray()) +
                                "]," +
                                "\"exception\": {" +
                                    "\"class\":\"" + clazz + "\"," +
                                    "\"message\":\"" + msg + "\"" +
                                "}" +
                            "}" +
                        "}," +
                        "\"platform\": \"browser\"," +
                        "\"level\": \"" + level + "\"," +
                        "\"notifier\": {\"name\":\"unityclient\",\"version\":\"1.0.0\"}," +
                        user +
                        (_versionString == null ?
                            "" :
                            "\"code_version\": \"" + _versionString + "\",") +
                        "\"timestamp\": \"" + (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + "\"," +
                    "}" +
                "}";

            queue.Enqueue(payload);

        }

        private string GetUserFragment()
        {
            if (_userID != null)
            {
                return "\"person\":{" +
                            (_userEmail == null ?
                                "" :
                                "\"email\": \"" + _userEmail + "\",") +
                            (_userName == null ?
                                "" :
                                "\"username\": \"" + _userName + "\",") +
                            "\"id\":\"" + _userID + "\"" +
                       "}";
            }

            return string.Empty;
        }

        private void _SetUserID(string id, string email, string username)
        {
            if (Debug.isDebugBuild) { Debug.Log("Rollbar will report ID:"+id+", email:"+email+", username:"+username); }

            _userID = id.Trim().Replace('\'', ' ').Replace('"', ' ');
            _userEmail = email == null ? null : email.Trim().Replace('\'', ' ').Replace('"', ' ');
            _userName = username == null ? null : username.Trim().Replace('\'', ' ').Replace('"', ' ');
        }

        private void _SetVersion(string v)
        {
            _versionString = v.Trim().Replace('\'', ' ').Replace('"', ' ');
        }

        public static void SetUserID(string id, string email = null, string username = null)
        {
            if (Instance == null)
            {
                return;
            }
            Instance._SetUserID(id, email, username);
        }

        public static void SetVersion(string v)
        {
            if (Instance == null)
            {
                return;
            }
            Instance._SetVersion(v);
        }

        public static void LogException(Exception e)
        {
            Debug.LogException(e);
        }

        private void Message(string msg, string level, Dictionary<string,string> extra)
        {
            if (Debug.isDebugBuild)
            {
                if (DebugLogForMessages)
                {
                    Debug.Log("[Rollbar " + level + "] " + msg);
                }
            }

#if UNITY_EDITOR
            if (!Development)
            {
                return;
            }
#else
            if (!EnableProduction)
            {
                return;
            }
#endif
            string user = GetUserFragment();

            string custom = null;
            bool first = true;
            if (extra != null)
            {
                custom = "\"custom\":{";

                foreach (string key in extra.Keys)
                {
                    if (!first)
                    {
                        custom += ",";
                    }
                    custom += "\""+
                        key.Trim().Replace('\'', ' ').Replace('"', ' ') +
                        "\":\""+
                        extra[key].Trim().Replace('\'', ' ').Replace('"', ' ') +
                        "\"";
                    first = false;
                }
                custom += "}";
            }

            string payload =
                "{" +
                    "\"access_token\": \"" + Instance.ClientToken + "\"," +
                    "\"data\": {" +
#if UNITY_EDITOR
                        "\"environment\": \""+ Instance.DevelopmentEnvironment + "\","+
#else
                        "\"environment\": \"" + Instance.ProductionEnvironment + "\"," +
#endif
                        "\"body\": {" +
                            "\"message\": {" +
                                "\"body\": \"" + msg.Trim().Replace('\'', ' ').Replace('"', ' ') + "\"" +
                            "}" +
                        "}," +
                        "\"platform\": \"browser\"," +
                        "\"level\": \"" + level + "\"," +
                        "\"notifier\": {\"name\":\"unityclient\",\"version\":\"1.0.0\"}," +
                        user +
                        (_versionString == null ?
                            string.Empty :
                            "\"code_version\": \"" + _versionString + "\",") +
                        "\"timestamp\": \"" + (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + "\"," +
                        (custom == null ?
                            string.Empty :
                            custom) +
                    "}" +
                "}";

            queue.Enqueue(payload);
        }

        void Update()
        {
            if (queue.Count == 0 || _live != null)
            {
                return;
            }

            _live = queue.Dequeue();

            StartCoroutine(SendRequest());
        }

        private IEnumerator SendRequest()
        {
            try
            {
                WWW www = new WWW("https://api.rollbar.com/api/1/item/", Encoding.UTF8.GetBytes(_live));
                yield return www;
            }
            finally
            {
                _live = null;
            }
        }

        /*
         * Example:
         * RollbarNotifier.ErrorMsg("Some error", new Dictionary<string, string>
         * {
         *     { "key1", "value1" },
         *     { "key2", "value2" }
         * });
         *
         */

        public static void CriticalMsg(string msg, Dictionary<string,string> extra = null)
        {
            if (Instance == null)
            {
                return;
            }

            Instance.Message(msg, "critical", extra);
        }

        public static void ErrorMsg(string msg, Dictionary<string,string> extra = null)
        {
            if (Instance == null)
            {
                return;
            }

            Instance.Message(msg, "error", extra);
        }

        public static void WarningMsg(string msg, Dictionary<string, string> extra = null)
        {
            if (Instance == null)
            {
                return;
            }

            Instance.Message(msg, "warning", extra);
        }

        public static void InfoMsg(string msg, Dictionary<string, string> extra = null)
        {
            if (Instance == null)
            {
                return;
            }

            Instance.Message(msg, "info", extra);
        }

        public static void DebugMsg(string msg, Dictionary<string, string> extra = null)
        {
            if (Instance == null)
            {
                return;
            }

            Instance.Message(msg, "debug", extra);
        }
    }
}
