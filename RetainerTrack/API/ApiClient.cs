using FFXIVClientStructs.FFXIV.Common.Lua;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using RetainerTrackExpanded.API.Models;
using RetainerTrackExpanded.API.Query;
using RetainerTrackExpanded.GUI;
using RetainerTrackExpanded.Handlers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RetainerTrackExpanded.API
{
    public class ApiClient
    {
        public static IRestClient _restClient = new RestClient();
        //private const string BaseUrl = "https://localhost:5001/api/v1/";
        public Configuration Config = RetainerTrackExpandedPlugin.Instance.Configuration;
        public HttpStatusCode LastHttpStatus;
        internal static ApiClient Instance { get; private set; } = null!;
        public ApiClient()
        {
            if (Uri.IsWellFormedUriString(Config.BaseUrl, UriKind.Absolute))
                _restClient = new RestClient(Config.BaseUrl);
            Instance = this;
        }

        //---Server---//
        public string _ServerStatus = string.Empty;
        public bool IsCheckingServerStatus = false;
        public long _LastPingValue = -1;

        public async Task<bool> CheckServerStatus()
        {
            try
            {
                IsCheckingServerStatus = true;

                var request = new RestRequest($"server").AddHeader("api-key", $"{Token}").AddHeader("V", Util.clientVer);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);
                long pingValue = -1;
                
                using (Ping pp = new Ping())
                {
                    Uri uri = new Uri($"{Config.BaseUrl}");
                    PingReply reply = pp.Send(uri.Host, 1000);

                    pingValue = reply.RoundtripTime;
                }

                if (response.IsSuccessful)
                {
                    _ServerStatus = "ONLINE"; _LastPingValue = pingValue;
                    IsCheckingServerStatus = false;
                    return true;
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    _ServerStatus = $"Error: {response.Content}";
                else if (response.StatusCode == 0)
                    _ServerStatus = $"Error: {HttpStatusCode.ServiceUnavailable.ToString()}";
                else
                    _ServerStatus = $"Error: {response.StatusCode.ToString()}";

                IsCheckingServerStatus = false;
                return false;
            }
            catch (Exception ex)
            {
                _ServerStatus = ex.Message; 
                _LastPingValue = -1;
                IsCheckingServerStatus = false; return false;
            }
        }
        public (ServerStatsDto ServerStats, string Message) _LastServerStats = new();
        public async Task<(ServerStatsDto ServerStats, string Message)> CheckServerStats()
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"server/stats").AddHeader("api-key", $"{Token}").AddHeader("V", Util.clientVer);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<ServerStatsDto>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = "Stats refreshed.";
                        _LastServerStats = (_JsonResponse, Message);
                        return (_JsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"Error: {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"Error: {HttpStatusCode.ServiceUnavailable.ToString()}";
                else
                    Message = $"Error: {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"Error: {ex.Message}";
                return (null, Message);
            }
        }
        //---Players---//
        //public ConcurrentDictionary<long, PlayerDto> _LastPlayerSearchResults = new ConcurrentDictionary<long, PlayerDto>();
        //public ConcurrentDictionary<long, PlayerDetailed> _LastPlayerByIdSearchResults = new ConcurrentDictionary<long, PlayerDetailed>();
        public string Token => $"{Config.Key}-{Config.AccountId}";
        public async Task<(PlayerWithPaginationDto Page, string Message)> GetPlayers(PlayerQueryObject query)
        {
            var _GetPlayerSearchResult = new Dictionary<long, PlayerDto>();
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"players").AddHeader("api-key", $"{Token}").AddHeader("V", Util.clientVer);
                if (!string.IsNullOrWhiteSpace(query.Name))
                    request.AddQueryParameter("Name", query.Name, true);
                if (!string.IsNullOrWhiteSpace(query.LocalContentId.ToString()))
                    request.AddQueryParameter("LocalContentId", query.LocalContentId.ToString(), true);
                if (!string.IsNullOrWhiteSpace(query.Cursor.ToString()))
                    request.AddQueryParameter("Cursor", query.Cursor.ToString(), true);
                request.AddQueryParameter("IsFetching", query.IsFetching, true);

                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);
               
                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<PlayerWithPaginationDto>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = "- Total Found: " + (_JsonResponse.Data.Count + _JsonResponse.NextCount).ToString();
                        return (_JsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"Error: {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"Error: {HttpStatusCode.ServiceUnavailable.ToString()}";
                else
                    Message = $"Error: {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"Error: {ex.Message}";
                return (null, Message);
            }
        }

        public async Task<(PlayerDetailed Player, string Message)> GetPlayerById(long id)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"players/{id}").AddHeader("api-key", $"{Token}").AddHeader("V", Util.clientVer);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<PlayerDetailed>(response.Content!);
                    Message = "Player found.";
                    return (_JsonResponse, Message);
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"Error: {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"Error: {HttpStatusCode.ServiceUnavailable.ToString()}";
                else
                    Message = $"Error: {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"Error: {ex.Message}";
                return (null, Message);
            }
        }

        public async Task<bool> PostPlayers(List<PostPlayerRequest> players)
        {
            try
            {
                var request = new RestRequest($"players").AddHeader("api-key", $"{Token}").AddHeader("V", Util.clientVer);
                request.AddJsonBody(players);
                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        //---Retainers---//
        public async Task<(RetainerWithPaginationDto Page, string Message)> GetRetainers(RetainerQueryObject query)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"retainers").AddHeader("api-key", $"{Token}").AddHeader("V", Util.clientVer); ;
                if (!string.IsNullOrWhiteSpace(query.Name))
                    request.AddQueryParameter("Name", query.Name, true);
                if (!string.IsNullOrWhiteSpace(query.Cursor.ToString()))
                    request.AddQueryParameter("Cursor", query.Cursor.ToString(), true);
                request.AddQueryParameter("IsFetching", query.IsFetching, true);

                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(false);
                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<RetainerWithPaginationDto>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = "- Total Found: " + (_JsonResponse.Data.Count + _JsonResponse.NextCount).ToString();
                        return (_JsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"Error: {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"Error: {HttpStatusCode.ServiceUnavailable.ToString()}";
                else
                    Message = $"Error: {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"Error: {ex.Message}";
                return (null, Message);
            }
        }
        public async Task<bool> PostRetainers(List<PostRetainerRequest> retainers)
        {
            try
            {
                var request = new RestRequest($"retainers").AddHeader("api-key", $"{Token}").AddHeader("V", Util.clientVer);
                request.AddJsonBody(retainers);
                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        //---Users---//
        public async Task<(User? User, string Message)> LoginTest(UserRegister register)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"users/register").AddHeader("V", Util.clientVer);
                request.AddJsonBody(register);
                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<User>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = "Signed up successfully.";
                        return (_JsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"Error: {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"Error: {HttpStatusCode.ServiceUnavailable.ToString()}";
                else
                    Message = $"Error: {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"Error: {ex.Message}";
                return (null, Message);
            }
        }

        public async Task<(User? User, string Message)> DiscordAuth(UserRegister register)
        {
            string Message = string.Empty;
            try
            {
                string output = JsonConvert.SerializeObject(register);
                byte[] bytes = Encoding.UTF8.GetBytes(output);
                string data = System.Convert.ToBase64String(bytes);
                string url = Config.BaseUrl.Replace("api/v1/", "Auth/DiscordAuth?") + data;
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                });
                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"Error: {ex.Message}";
                return (null, Message);
            }
        }
        public async Task<(User? User, string Message)> UserLogin(UserRegister loginUser)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"users/login");
                request.AddJsonBody(loginUser);
                
                //request.AddBody(new
                //{
                //    gameAccountId = loginUser.GameAccountId.ToString(),
                //    clientId = loginUser.ClientId,
                //    name = loginUser.Name,
                //});

                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<User>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = "Logged in successfully.";
                        return (_JsonResponse, Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"Error: {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"Error: {HttpStatusCode.ServiceUnavailable.ToString()}";
                else
                    Message = $"Error: {response.StatusCode.ToString()}";

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"Error: {ex.Message}";
                return (null, Message);
            }
        }
        public async Task<string> UserUpdate(UserUpdateDto config)
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"users/update").AddHeader("api-key", $"{Token}").AddHeader("V", Util.clientVer);
                request.AddJsonBody(config);
                var response = await _restClient.ExecutePostAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
                {
                    return response.Content;
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"Error: {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"Error: {HttpStatusCode.ServiceUnavailable.ToString()}";
                else
                    Message = $"Error: {response.StatusCode.ToString()}";

                return Message;
            }
            catch (Exception ex)
            {
                Message = $"Error: {ex.Message}";
                return Message;
            }
        }
        public string _RefreshMyInfoStatus = string.Empty;
        public async Task<(User? User, string Message)> UserRefreshMyInfo()
        {
            string Message = string.Empty;
            try
            {
                var request = new RestRequest($"users/me").AddHeader("api-key", $"{Token}").AddHeader("V", Util.clientVer);
                var response = await _restClient.ExecuteGetAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful)
                {
                    var _JsonResponse = JsonConvert.DeserializeObject<User>(response.Content!);
                    if (_JsonResponse != null)
                    {
                        Message = "Profile refreshed.";
                        return (_JsonResponse,Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(response.Content))
                    Message = $"Error: {response.Content}";
                else if (response.StatusCode == 0)
                    Message = $"Error: {HttpStatusCode.ServiceUnavailable.ToString()}";
                else
                    Message = $"Error: {response.StatusCode.ToString()}"; 

                return (null, Message);
            }
            catch (Exception ex)
            {
                Message = $"Error: {ex.Message}";
                return (null, Message);
            }
        }
    }
}
