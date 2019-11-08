﻿using My9GAG.Logic.Logger;
using My9GAG.Models.Comment;
using My9GAG.Models.Post;
using My9GAG.Models.Post.Media;
using My9GAG.Models.Request;
using My9GAG.Models.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace My9GAG.Logic.Client
{
    public class ClientService : IClientService
    {
        #region Constructors

        public ClientService(ILogger logger)
        {
            _logger = logger;

            _timestamp  = RequestUtils.GetTimestamp();
            _appId      = RequestUtils.APP_ID;
            _token      = RequestUtils.GetSha1(_timestamp);
            _deviceUuid = RequestUtils.GetUuid();
            _signature  = RequestUtils.GetSignature(_timestamp, _appId, _deviceUuid);

            Posts = new List<Post>();
            Comments = new List<Comment>();
            User = new User();
        }

        #endregion

        #region Properties

        public List<Post> Posts { get; private set; }
        public List<Comment> Comments { get; private set; }
        public User User { get; private set; }

        #endregion

        #region Methods

        public async Task<RequestStatus> LoginWithCredentialsAsync(string userName, string password)
        {
            var args = new Dictionary<string, string>()
            {
                { "loginMethod", "9gag" },
                { "loginName", userName },
                { "password", RequestUtils.GetMd5(password) },
                { "language", "en_US" },
                { "pushToken", _token }
            };

            var loginStatus = await LoginAsync(args);

            if (loginStatus.IsSuccessful)
            {
                User.LoginStatus = LoginStatus.Credentials;
            }

            return loginStatus;
        }
        public async Task<RequestStatus> LoginWithGoogleAsync(string token)
        {
            var args = new Dictionary<string, string>()
            {
                { "userAccessToken", token },
                { "loginMethod", "google-plus" },
                { "language", "en_US" },
                { "pushToken", _token }
            };

            var loginStatus = await LoginAsync(args);

            if (loginStatus.IsSuccessful)
            {
                User.LoginStatus = LoginStatus.Google;
            }            

            return loginStatus;
        }
        public async Task<RequestStatus> LoginWithFacebookAsync(string token)
        {
            var args = new Dictionary<string, string>()
            {
                { "loginMethod", "facebook" },
                { "userAccessToken", token },                
                { "language", "en_US" },
                { "pushToken", _token }
            };

            var loginStatus = await LoginAsync(args);

            if (loginStatus.IsSuccessful)
            {
                User.LoginStatus = LoginStatus.Facebook;
            }

            return loginStatus;
        }
        public async Task<RequestStatus> GetPostsAsync(PostCategory postCategory, int count, string olderThan = "")
        {
            string type = postCategory.ToString().ToLower();
            var args = new Dictionary<string, string>()
            {
                { "group", "1" },
                { "type", type },
                { "itemCount", count.ToString() },
                { "entryTypes", "animated,photo,video" },
                { "offset", "10" }
            };

            if (!string.IsNullOrEmpty(olderThan))
                args["olderThan"] = olderThan;
            
            var request = FormRequest(RequestUtils.API, RequestUtils.POSTS_PATH, args);
            var requestStatus = new RequestStatus();

            try
            {
                using (var response = (HttpWebResponse)(await request.GetResponseAsync()))
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string responseText = await reader.ReadToEndAsync();
                    requestStatus = ValidateResponse(responseText);

                    if (requestStatus.IsSuccessful)
                    {
                        Posts = new List<Post>();

                        var jsonData = JObject.Parse(responseText);
                        var rawPosts = jsonData["data"]["posts"];
                        
                        foreach (var item in rawPosts)
                        {
                            Post post = item.ToObject<Post>();
                            string url = string.Empty;

                            switch (post.Type)
                            {
                                case PostType.Photo:
                                    url = item["images"]["image700"]["url"].ToString();
                                    break;
                                case PostType.Animated:
                                    url = item["images"]["image460sv"]["url"].ToString();
                                    break;
                                case PostType.Video:
                                    url = item["videoId"].ToString();
                                    break;
                                default:
                                    break;
                            }
                            
                            post.PostMedia = PostMediaFactory.CreatePostMedia(post.Type, url);
                            Posts.Add(post);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                requestStatus.IsSuccessful = false;
                requestStatus.Message = e.Message;

                _logger.LogIntoConsole(e.Message, e.StackTrace);
            }

            return requestStatus;
        }
        public async Task<RequestStatus> GetCommentsAsync(string postUrl, uint count)
        {
            var args = new Dictionary<string, string>();

            string path = 
                "v1/topComments.json?" +
                "appId=a_dd8f2b7d304a10edaf6f29517ea0ca4100a43d1b" +
                "&urls=" + postUrl +
                "&commentL1=" + count +
                "&pretty=0";

            var request = FormRequest(RequestUtils.COMMENT_CDN, path, args);
            var requestStatus = new RequestStatus();

            try
            {
                using (var response = (HttpWebResponse)(await request.GetResponseAsync()))
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string responseText = await reader.ReadToEndAsync();
                    requestStatus = ValidateResponse(responseText);

                    if (requestStatus.IsSuccessful)
                    {
                        Comments = new List<Comment>();
                        var jsonData = JObject.Parse(responseText);
                        var comments = jsonData.SelectToken("payload.data.[0].comments");

                        foreach (var item in comments)
                        {
                            Comment comment = item.ToObject<Comment>();
                            comment.MediaUrl = GetUrlFromJsonComment(item);
                            Comments.Add(comment);
                        }

                        Comments = Comments.OrderByDescending(c => c.LikesCount).ToList();
                    }
                }
            }
            catch (Exception e)
            {
                requestStatus.IsSuccessful = false;
                requestStatus.Message = e.Message;

                _logger.LogIntoConsole(e.Message, e.StackTrace);
            }

            return requestStatus;
        }

        public void SaveState(IDictionary<string, object> dictionary)
        {
            dictionary["User"] = User;
        }
        public void RestoreState(IDictionary<string, object> dictionary)
        {
            User = GetDictionaryEntry(dictionary, "User", User);
        }

        #endregion

        #region Implementation

        protected T GetDictionaryEntry<T>(IDictionary<string, object> dictionary, string key, T defaultValue)
        {
            if (dictionary.ContainsKey(key))
            {
                return (T)dictionary[key];
            }

            return defaultValue;
        }

        private HttpWebRequest FormRequest(string api, string path, Dictionary<string, string> args)
        {
            var headers = new Dictionary<string, string>()
            {
                { "9GAG-9GAG_TOKEN", _token },
                { "9GAG-TIMESTAMP", _timestamp },
                { "9GAG-APP_ID", _appId },
                { "X-Package-ID", _appId },
                { "9GAG-DEVICE_UUID", _deviceUuid },
                { "X-Device-UUID", _deviceUuid },
                { "9GAG-DEVICE_TYPE", "android" },
                { "9GAG-BUCKET_NAME", "MAIN_RELEASE" },
                { "9GAG-REQUEST-SIGNATURE", _signature }
            };

            var argsStrings = new List<string>();

            foreach (var entry in args)
            {
                argsStrings.Add(String.Format("{0}/{1}", entry.Key, entry.Value));
            }

            var urlItems = new List<string>()
            {
                api,
                path,
                String.Join("/", argsStrings)
            };

            string url = String.Join("/", urlItems);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            var headerCollection = new WebHeaderCollection();

            foreach (var entry in headers)
            {
                headerCollection.Add(entry.Key, entry.Value);
            }

            request.Headers = headerCollection;
            request.Method = WebRequestMethods.Http.Get;
            request.UserAgent = ".NET";
            request.ContentType = "application/json";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Accept = "*/*";

            return request;
        }
        private RequestStatus ValidateResponse(string response)
        {
            var requestStatus = new RequestStatus();

            try
            {
                var jsonData = JObject.Parse(response);

                if (jsonData.ContainsKey("meta"))
                {
                    if (jsonData["meta"]["status"].ToString() == "Success")
                    {
                        requestStatus.IsSuccessful = true;
                        requestStatus.Message = "";
                    }
                    else
                    {
                        requestStatus.IsSuccessful = false;
                        requestStatus.Message = jsonData["meta"]["errorMessage"].ToString();
                    }
                }
                else if (jsonData.ContainsKey("status"))
                {
                    if (jsonData["status"].ToString() == "ERROR")
                    {
                        requestStatus.IsSuccessful = false;
                        requestStatus.Message = jsonData["error"].ToString();
                    }
                    else if (jsonData["status"].ToString() == "OK")
                    {
                        requestStatus.IsSuccessful = true;
                        requestStatus.Message = "";
                    }
                }
            }
            catch (Exception e)
            {
                requestStatus.IsSuccessful = false;
                requestStatus.Message = e.Message;

                _logger.LogIntoConsole(e.Message, e.StackTrace);
            }

            return requestStatus;
        }
        private async Task<RequestStatus> LoginAsync(Dictionary<string, string> args)
        {
            var request = FormRequest(RequestUtils.API, RequestUtils.LOGIN_PATH, args);
            var loginStatus = new RequestStatus();

            try
            {
                using (var response = (HttpWebResponse)(await request.GetResponseAsync()))
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string responseText = await reader.ReadToEndAsync();
                    loginStatus = ValidateResponse(responseText);

                    if (loginStatus.IsSuccessful)
                    {
                        var jsonData = JObject.Parse(responseText);

                        _token = jsonData["data"]["userToken"].ToString();
                        _userData = jsonData["data"].ToString();

                        string readStateParams = jsonData["data"]["noti"]["readStateParams"].ToString();

                        _generatedAppId = RequestUtils.ExtractValueFromUrl(readStateParams, "appId");
                    }
                }
            }
            catch (Exception e)
            {
                loginStatus.IsSuccessful = false;
                loginStatus.Message = e.Message;

                _logger.LogIntoConsole(e.Message, e.StackTrace);
            }

            return loginStatus;
        }
        private string GetUrlFromJsonComment(JToken token)
        {
            var urlToken = 
                token.SelectToken("media.[0].imageMetaByType.animated.url") ??
                token.SelectToken("media.[0].imageMetaByType.image.url") ??
                string.Empty;

            return urlToken.ToString();
        }

        #endregion

        #region Fields

        private ILogger _logger;

        private string _timestamp = "";
        private string _appId = "";
        private string _token = "";
        private string _deviceUuid = "";
        private string _signature = "";
        private string _userData = "";
        private string _generatedAppId = "";

        #endregion
    }
}
