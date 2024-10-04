using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OAuth;

namespace twitterapi
{
    public class TwitterHelper
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.twitter.com/2/";
        private readonly string _consumerKey;
        private readonly string _consumerSecret;
        private readonly string _accessToken;
        private readonly string _accessTokenSecret;

        public TwitterHelper(IConfiguration configuration)
        {
            _consumerKey = configuration["TwitterConfig:ConsumerKey"];
            _consumerSecret = configuration["TwitterConfig:ConsumerSecret"];
            _accessToken = configuration["TwitterConfig:AccessToken"];
            _accessTokenSecret = configuration["TwitterConfig:AccessTokenSecret"];
            _httpClient = new HttpClient();
        }
        /// <summary>
        /// 發推特
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public async Task<string> PostTweetAsync(string text)
        {
            var endpoint = $"{BaseUrl}tweets";
            var payload = new { text = text };
            var jsonContent = JsonConvert.SerializeObject(payload);

            var oAuthRequest = OAuthRequest.ForProtectedResource("POST", _consumerKey, _consumerSecret, _accessToken, _accessTokenSecret);
            oAuthRequest.RequestUrl = endpoint;
            var authHeader = oAuthRequest.GetAuthorizationHeader();

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("Authorization", authHeader);
            // 移除额外的授权头，因为它们可能导致401错误
            // 打印更多调试信息
            Console.WriteLine($"请求方法: {request.Method}");
            Console.WriteLine($"完整的授权头: {authHeader}");
            Console.WriteLine($"Access Token: {_accessToken}");
            Console.WriteLine($"Access Token Secret: {_accessTokenSecret}");

            // 打印请求信息以便调试
            Console.WriteLine($"请求URL: {endpoint}");

            Console.WriteLine($"授权头: {authHeader}");
            Console.WriteLine($"请求内容: {jsonContent}");

            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");


            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private (string Token, string TokenSecret) _requestToken;

        public async Task<string> GetAuthorizationUrlAsync()
        {
            // 1. 獲取請求令牌
            //var requestTokenResponse = await GetRequestTokenAsync();
            //_requestToken = ParseRequestTokenResponse(requestTokenResponse);
            _requestToken = await GetRequestTokenAsync();
            // 2. 構建授權URL
            var authorizationUrl = $"https://api.twitter.com/oauth/authorize?oauth_token={_requestToken.Token}";
            return authorizationUrl;
        }

        /// <summary>
        /// 更新PIN并获取访问令牌
        /// </summary>
        /// <param name="pin">用户输入的PIN码</param>
        /// <returns>访问令牌和访问令牌密钥</returns>
        /// <remarks>
        /// Python代码正确而C#代码有问题的原因可能是:
        /// 1. OAuth流程实现细节不同
        /// 2. 使用的库和API不同
        /// 3. 错误处理和异常捕获的差异
        /// 4. 配置和密钥管理方式不同
        /// 建议仔细比对两种实现,确保C#版本遵循正确的OAuth流程
        /// </remarks>
        public async Task<string> InputPinAndUpdateTokensAsync(string pin)
        {
            if (_requestToken.Token == null)
            {
                throw new InvalidOperationException("請先調用 GetAuthorizationUrlAsync 方法獲取授權URL");
            }

            // 使用PIN獲取訪問令牌
            var accessTokenResponse = await GetAccessTokenAsync(_requestToken.Token, _requestToken.TokenSecret, pin);
            var accessToken = ParseAccessTokenResponse(accessTokenResponse);

            // 更新配置文件
            UpdateConfigFile(accessToken);

            // 清除臨時存儲的請求令牌
            _requestToken = default;

            return "訪問令牌已更新";
        }



        /// <summary>
        /// 获取授权token
        /// </summary>
        /// <returns></returns>
        private async Task<(string Token, string TokenSecret)> GetRequestTokenAsync()
        {
            var endpoint = "https://api.twitter.com/oauth/request_token?oauth_callback=oob&x_auth_access_type=write";
            var oAuthRequest = OAuthRequest.ForRequestToken(_consumerKey, _consumerSecret);
            oAuthRequest.RequestUrl = endpoint;
            oAuthRequest.Method = "POST";

           var authHeader = oAuthRequest.GetAuthorizationHeader();

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("Authorization", authHeader);

            try
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                var parsedResponse = ParseRequestTokenResponse(responseContent);

                Console.WriteLine($"获取到 OAuth 令牌: {parsedResponse.Token}");

                return parsedResponse;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("获取请求令牌时发生错误。");
                Console.WriteLine($"错误详情: {e.Message}");
                throw;
            }
        }
        private (string Token, string TokenSecret) ParseRequestTokenResponse(string response)
        {
            var parameters = response.Split('&')
                .Select(p => p.Split('='))
                .ToDictionary(p => p[0], p => p[1]);

            return (parameters["oauth_token"], parameters["oauth_token_secret"]);
        }

        private async Task<string> GetAccessTokenAsync(string requestToken, string requestTokenSecret, string pin)
        {
            var endpoint = "https://api.twitter.com/oauth/access_token";
            var oAuthRequest = OAuthRequest.ForAccessToken(_consumerKey, _consumerSecret, requestToken, requestTokenSecret, pin);
            oAuthRequest.RequestUrl = endpoint;
            var authHeader = oAuthRequest.GetAuthorizationHeader();

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("Authorization", authHeader);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        private (string Token, string TokenSecret, string UserId, string ScreenName) ParseAccessTokenResponse(string response)
        {
            var parameters = response.Split('&')
                .Select(p => p.Split('='))
                .ToDictionary(p => p[0], p => p[1]);

            return (
                parameters["oauth_token"],
                parameters["oauth_token_secret"],
                parameters["user_id"],
                parameters["screen_name"]
            );
        }

        /// <summary>
        /// 更新配置文件
        /// </summary>
        /// <param name="accessToken"></param>
        private void UpdateConfigFile((string Token, string TokenSecret, string UserId, string ScreenName) accessToken)
        {
            // 这里需要根据您的配置文件格式进行相应的更新
            // 例如，如果使用 JSON 配置文件：
            var config = new
            {
                TwitterConfig = new
                {
                    ConsumerKey = _consumerKey,
                    ConsumerSecret = _consumerSecret,
                    AccessToken = accessToken.Token,
                    AccessTokenSecret = accessToken.TokenSecret,
                    UserId = accessToken.UserId,
                    ScreenName = accessToken.ScreenName
                }
            };

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText("appsettings.json", json);
        }
    }
}
