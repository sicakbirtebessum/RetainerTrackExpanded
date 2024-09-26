using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network.Structures;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using ImGuiNET;
using Lumina;
using Messenger.FriendListManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RestSharp;
using RetainerTrackExpanded.API;
using RetainerTrackExpanded.API.Models;
using RetainerTrackExpanded.API.Query;
using RetainerTrackExpanded.Database;
using RetainerTrackExpanded.Handlers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase.Delegates;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkUIColorHolder.Delegates;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using static RetainerTrackExpanded.GUI.MainWindow;

namespace RetainerTrackExpanded.GUI
{
    internal class ConfigWindow : Window, IDisposable
    {
        public ConfigWindow() : base("Retainer Track Config", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            if (_instance == null)
            {
                _instance = this;
            }
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(550, 450),
                MaximumSize = new Vector2(9999, 9999)
            };
        }
        private readonly string[] FavoritedPlayersColumn = new string[]
       {
        "Current Name","Content Id","Account Id","Remove"
       };
        public readonly IServiceProvider _serviceProvider;
        private static ConfigWindow _instance = null;
        public static ConfigWindow Instance
        {
            get
            {
                return _instance;
            }
        }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
        public override void OnOpen()
        {
            base.OnOpen();

            bPrivacyStatus = Config.IsProfilePrivate;

            IsRefreshed = false;
            IsSaved = false;
            if (string.IsNullOrWhiteSpace(_client._ServerStatus))
            {
                _client.CheckServerStatus();
            }
        }

        public void CheckLocalPlayer()
        {
            if (PersistenceContext._clientState != null && PersistenceContext._clientState is { IsLoggedIn: true, LocalContentId: > 0 })
            {
                IGameObject? localCharacter = PersistenceContext._clientState.LocalPlayer;
                if (localCharacter == null || localCharacter.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
                    return;

                unsafe
                {
                    var bChar = (BattleChara*)localCharacter.Address;

                    if (_playerName != $"{bChar->NameString}")
                    {
                        if (string.IsNullOrWhiteSpace(_playerName) || string.IsNullOrWhiteSpace(_worldName))
                        {
                            var homeWorld = PersistenceContext._clientState.LocalPlayer?.HomeWorld;
                            _playerName = $"{bChar->NameString}";
                            _worldName = homeWorld.GameData.Name;
                            _worldId = homeWorld.Id;
                            _accountId = (int)bChar->AccountId;
                            _contentId = (long)bChar->ContentId;
                        }
                    }
                }
            }
        }

        string _playerName = string.Empty;
        string _worldName = string.Empty;
        uint _worldId = 0;
        long _contentId = 0;
        int _accountId = 0;
        string _key = string.Empty;
        string _password = string.Empty;

        public Configuration Config = RetainerTrackExpandedPlugin.Instance.Configuration;
        ApiClient _client = ApiClient.Instance;

        public void SaveUserResultToConfig(User user)
        {
            if (user != null)
            {
                User _UserResult = user;

                Config.Username = _UserResult.Name;
                Config.ContentId = _UserResult.LocalContentId;
                Config.AccountId = _UserResult.GameAccountId;
                if (!string.IsNullOrWhiteSpace(_UserResult.Token))
                    Config.Key = _UserResult.Token;

                Config.AppRoleId = _UserResult.AppRoleId;
                Config.DiscordId = _UserResult.DiscordId;
                Config.UploadedPlayersCount = _UserResult.UploadedPlayersCount;
                Config.UploadedPlayerInfoCount = _UserResult.UploadedPlayerInfoCount;
                Config.UploadedRetainersCount = _UserResult.UploadedRetainersCount;
                Config.UploadedRetainerInfoCount = _UserResult.UploadedRetainerInfoCount;
                Config.LastSyncedTime = _UserResult.LastSyncedTime;
                Config.IsProfilePrivate = _UserResult.IsProfilePrivate;
                bPrivacyStatus = _UserResult.IsProfilePrivate;
                Config.IsLoggedIn = true;
                Config.FreshInstall = false;

                Config.Save();
            }
        }

        bool bPrivacyStatus;

        bool IsRefreshed;
        bool IsSaved;

        bool bIsNetworkProcessing = false;
        public string LastNetworkMessage = string.Empty;
        public string LastRegistrationWindowMessage = string.Empty;
        public string LastNetworkMessageTime;
        public override void Draw()
        {
            if (ImGui.BeginTabBar("Tabs"))
            {
                if (ImGui.BeginTabItem("User Info"))
                {
                    DrawUserInfoPage();
                    ImGui.EndTabItem();
                }

                using (ImRaii.Disabled(!Config.IsLoggedIn))
                {
                    if (ImGui.BeginTabItem("Server Stats & Sync Local Database"))
                    {
                        DrawServerStatsPage();
                        ImGui.EndTabItem();
                    }
                }

                if (ImGui.BeginTabItem("My Favorites"))
                {
                    DrawMyFavoritesPage();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
        string? _discordIdField = null;
        bool bShowDiscordIdTextField = false;
        private async void DrawUserInfoPage()
        {
            ServerStatusGui();

            if (!string.IsNullOrWhiteSpace(Config.Key))
            {
                ImGui.Text("Player Name:");
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudYellow, Config.Username);

               // ImGui.SameLine();

                if (Config.AppRoleId < (int)User.Roles.Member)
                {
                    ImGui.TextColored(ImGuiColors.DPSRed, $"Role: {(User.Roles)Config.AppRoleId}");
                    Util.DrawHelp(true, "Ouch! Only \"members\" can send information and receive from the server.");
                }
                else
                {
                    ImGui.Text("Role:"); ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.HealerGreen, $"{(User.Roles)Config.AppRoleId} "); ImGui.SameLine();
                    ImGui.Text("-   Permissions:"); ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.HealerGreen, "(Send & Receive Data)");
                    Util.DrawHelp(true, "As you are whitelisted, you are authorized to send information and receive from the server.");
                }

                if (ImGui.CollapsingHeader("Show your ids"))
                {
                    ImGui.Text("Account Id:");
                    ImGui.SameLine();
                    //ImGui.TextColored(ImGuiColors.TankBlue, Config.AccountId.ToString());
                    Util.TextCopy(ImGuiColors.TankBlue, Config.AccountId.ToString());
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Click to copy AccountId");
                    }

                    ImGui.SameLine();

                    ImGui.Text("LocalContent Id: ");
                    ImGui.SameLine();
                    Util.TextCopy(ImGuiColors.TankBlue, Config.ContentId.ToString());
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Click to copy LocalContent Id");
                    }
                }

                if (ImGui.CollapsingHeader("See your contributions ♥", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    Util.DrawHelp(false,"The total number of players you sent to the server\nwho were not previously in the database.");

                    ImGui.Text("Uploaded Players Count:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.UploadedPlayersCount.ToString());

                    Util.DrawHelp(true, "The number of player info changes you sent to the server\nwho are already in the database but have different names or worlds");
                    ImGui.SameLine();
                    //ImGui.Text("Uploaded Player Info Count:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, $"({Config.UploadedPlayerInfoCount.ToString()})");

                    Util.DrawHelp(false, "The total number of retainers you sent to the server\nwho were not previously in the database.");

                    ImGui.Text("Uploaded Retainers Count:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.UploadedRetainersCount.ToString());
                   
                    Util.DrawHelp(true, " The total number of retainer info changes (name or world) you sent to the server.");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, $"({Config.UploadedRetainerInfoCount.ToString()})");
                }

                ImGui.NewLine();

                ImGui.Text("Account Privacy:");
                ImGui.SameLine();
                ImGui.Checkbox("Is Private", ref bPrivacyStatus);

                if (bPrivacyStatus)
                {
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("No information related to you will appear in search results." +
                            "\nNeither your player data nor your retainer information will be shared with other players.");
                    }
                }
                else
                {
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Information related to you will appear in search results." +
                            "\nYour player data and retainer information can be searched by other players.");
                    }
                }

                Util.DrawHelp(false, "In case you forget your password, you can verify with your Discord ID and reset your password.");
                ImGui.Text("Discord Id:");
                ImGui.SameLine();
                
                ImGui.SetNextItemWidth(185);
                if (bShowDiscordIdTextField)
                {
                    ImGui.InputTextWithHint("", "eg: 560441026784763904", ref _discordIdField, 19, ImGuiInputTextFlags.CharsNoBlank);
                }
                else
                {
                    _discordIdField = Config.DiscordId.ToString();
                    ImGui.TextWrapped(_discordIdField.ToString());
                }

                ImGui.SameLine();

                var _editingText = !bShowDiscordIdTextField ? "Edit" : "Cancel";
                var _editingIcon = !bShowDiscordIdTextField ? FontAwesomeIcon.Edit : FontAwesomeIcon.Backspace;
                
                if (ImGuiComponents.IconButtonWithText(_editingIcon, _editingText))
                {
                    bShowDiscordIdTextField = !bShowDiscordIdTextField;
                }

                ImGui.Text("\n");

                using (ImRaii.Disabled(bIsNetworkProcessing))
                {
                    using (ImRaii.Disabled(bPrivacyStatus == Config.IsProfilePrivate && (_discordIdField == Config.DiscordId.ToString()))) //!bShowDiscordIdTextField && 
                        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Save, "Save Config"))
                        {
                            string? _finalDiscordId = null;
                            if (bShowDiscordIdTextField && _discordIdField != Config.DiscordId.ToString())
                            {       
                                _finalDiscordId = _discordIdField;
                            }

                            _ = Task.Run(() =>
                            {
                                bIsNetworkProcessing = true;
                                var request = _client.UserUpdate(new UserUpdateDto { IsProfilePrivate = bPrivacyStatus, DiscordId = _finalDiscordId }).ConfigureAwait(false).GetAwaiter().GetResult();
                                LastNetworkMessage = request.Message;
                                bIsNetworkProcessing = false;
                                if (request.User != null)
                                {
                                    LastNetworkMessageTime = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
                                    bShowDiscordIdTextField = false;
                                    SaveUserResultToConfig(request.User);
                                }
                            });

                        }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Save your configuration to server");
                    }
                    ImGui.SameLine();

                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, "Refresh Profile Info"))
                    {
                        _ = Task.Run(() =>
                        {
                            bIsNetworkProcessing = true;
                            var request = _client.UserRefreshMyInfo().ConfigureAwait(false).GetAwaiter().GetResult();
                            LastNetworkMessage = request.Message;
                            LastNetworkMessageTime = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
                            bIsNetworkProcessing = false;
                            if (request.User != null)
                            {
                                //LastNetworkMessageTime = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
                                SaveUserResultToConfig(request.User);
                            }
                        });
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Request profile data from the server");
                    }
                }
                if (!string.IsNullOrWhiteSpace(LastNetworkMessage))
                {
                    Util.ColoredTextWrapped($"{LastNetworkMessage} ({LastNetworkMessageTime})");
                }
            }
            else
            {
                if (PersistenceContext._clientState is { IsLoggedIn: true, LocalContentId: > 0 })
                {
                    if (string.IsNullOrWhiteSpace(_playerName) || string.IsNullOrWhiteSpace(_contentId.ToString()) || string.IsNullOrWhiteSpace(_accountId.ToString()))
                    {
                        CheckLocalPlayer();
                    }
                    ImGui.Text("Player Name:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, _playerName);

                    ImGui.SameLine();

                    ImGui.Text("Home World:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, _worldName);

                    ImGui.Text("Account Id:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, _accountId.ToString());

                    ImGui.Text("Content Id:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, _contentId.ToString());

                    ImGui.Text("\n");

                    ImGui.Text("Enter a password (at least 5 characters):");
                    ImGui.InputTextWithHint("##Pass", "Password", ref _password, 50, ImGuiInputTextFlags.Password);
                    //ImGui.SetNextItemWidth(200);
                    using (ImRaii.Disabled(bIsNetworkProcessing))
                    {
                        if (ImGui.Button("Register", new Vector2(100, 30)))
                        {
                            if (_password.Length < 5)
                            {
                                LastRegistrationWindowMessage = "Error: Password must be at least 5 characters long.";
                            }
                            else
                            {
                                _ = Task.Run(() =>
                                {
                                    bIsNetworkProcessing = true;
                                    var request = _client.UserRegister(new UserRegister
                                    {
                                        GameAccountId = _accountId,
                                        UserLocalContentId = _contentId,
                                        Name = _playerName,
                                        Password = Tools.HashPassword(_password)
                                    }).ConfigureAwait(false).GetAwaiter().GetResult();

                                    LastRegistrationWindowMessage = request.Message;
                                    bIsNetworkProcessing = false;
                                    if (request.User != null)
                                    {
                                        SaveUserResultToConfig(request.User);
                                    }
                                });
                            }
                        }
                    }

                    ImGui.SameLine();
                    ImGui.Text("OR");
                    ImGui.SameLine();

                    using (ImRaii.Disabled(bIsNetworkProcessing))
                    {
                        if (ImGui.Button("Login with Pass", new Vector2(115, 30)))
                        {
                            if (_password.Length < 5)
                            {
                                LastRegistrationWindowMessage = "Error: Password must be at least 5 characters long.";
                            }
                            else
                            {
                                _ = Task.Run(() =>
                                {
                                    bIsNetworkProcessing = true;
                                    var request = _client.UserLogin(_accountId, _password).ConfigureAwait(false).GetAwaiter().GetResult();
                                    LastRegistrationWindowMessage = request.Message;
                                    bIsNetworkProcessing = false;
                                    if (request.User != null)
                                    {
                                        SaveUserResultToConfig(request.User);
                                    }
                                });
                            }
                        }
                    }
                    Util.ShowColoredMessage(LastRegistrationWindowMessage);
                }
                else
                {
                    ImGui.TextColored(ImGuiColors.DalamudRed, "You are not logged in.\nPlease login with a character to sign up.");

                }
            }
        }

        bool IsRefreshStatsRequestSent = false;
        public string _LastServerStatsMessage = string.Empty;
        private async void DrawServerStatsPage()
        {
            ServerStatusGui();

            if (!IsRefreshStatsRequestSent && _client._LastServerStats.ServerStats == null)
            {
                IsRefreshStatsRequestSent = true;
                CheckServerStats();
            }

            ImGui.TextColored(ImGuiColors.ParsedGold, "Server Database Stats"); 

            long _refreshButtonCondition = _client._LastServerStats.ServerStats != null ? _client._LastServerStats.ServerStats.LastUpdate : 0;
            using (ImRaii.Disabled(bIsNetworkProcessing || Tools.UnixTime - _refreshButtonCondition < 20))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.SyncAlt, "Refresh Server Stats"))
                {
                    CheckServerStats();
                }
            }
            if (!string.IsNullOrWhiteSpace(_LastServerStatsMessage))
            {
                ImGui.SameLine();
                Util.ColoredTextWrapped(_LastServerStatsMessage);
            }

            ImGui.NewLine();

            ImGui.Text("Player Count:"); ImGui.SameLine();
            if (_client._LastServerStats.ServerStats != null)
            {
                ImGui.TextColored(ImGuiColors.HealerGreen, $"{_client._LastServerStats.ServerStats.TotalPlayerCount.ToString()}");

                if (_client._LastServerStats.ServerStats.TotalPrivatePlayerCount > 0)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, $" (+{_client._LastServerStats.ServerStats.TotalPrivatePlayerCount} private players)");
                }
            }
            else
                ImGui.TextColored(ImGuiColors.DalamudRed, "...");

            ImGui.Text("Retainer Count:"); ImGui.SameLine();
            if (_client._LastServerStats.ServerStats != null)
            {
                ImGui.TextColored(ImGuiColors.HealerGreen, $"{_client._LastServerStats.ServerStats.TotalRetainerCount.ToString()}");

                if (_client._LastServerStats.ServerStats.TotalPrivateRetainerCount > 0)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, $" (+{_client._LastServerStats.ServerStats.TotalPrivateRetainerCount} private retainers)");
                }
            }
            else
                ImGui.TextColored(ImGuiColors.DalamudRed, "...");

            ImGui.Text("User Count:"); ImGui.SameLine();
            if (_client._LastServerStats.ServerStats != null)
                ImGui.TextColored(ImGuiColors.HealerGreen, $"{_client._LastServerStats.ServerStats.TotalUserCount.ToString()}");
            else
                ImGui.TextColored(ImGuiColors.DalamudRed, "...");

            ImGui.Text("Last Updated on:"); ImGui.SameLine();
            if (_client._LastServerStats.ServerStats != null)
                ImGui.TextColored(ImGuiColors.HealerGreen, $"{Tools.UnixTimeConverter((int)_client._LastServerStats.ServerStats.LastUpdate)}");
            else
                ImGui.TextColored(ImGuiColors.DalamudRed, "...");

            ImGui.NewLine();

            var syncText = "Sync Player and Retainer info from Server";
            bool _syncDatabaseButtonCondition = Config.LastSyncedTime != null ? Tools.UnixTime - Config.LastSyncedTime < 300 : true;
            using (ImRaii.Disabled(bIsNetworkProcessing || IsSyncingPlayers || IsSyncingRetainers || IsDbRefreshing || _syncDatabaseButtonCondition)) // 5 minutes
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.UserFriends, syncText))
                {
                    IsSyncingPlayers = true;
                    _cancellationToken = new CancellationTokenSource();
                    var syncPlayers = SyncPlayersWithLocalDb(_cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
                }
                if (_syncDatabaseButtonCondition)
                {
                    var syncAgainTime = Config.LastSyncedTime + 300;
                    ImGui.SameLine();
                    using (ImRaii.Disabled()) { Util.TextWrapped("Can sync again in " + Tools.TimeFromNow((int)syncAgainTime)); }
                }
            }

            if (IsSyncingPlayers || IsSyncingRetainers)
            {
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Stop, "Stop Fetching"))
                {
                    _cancellationToken.Cancel();
                }

                Util.CompletionProgressBar(_playersFetchedFromServer.Count + _retainersFetchedFromServer.Count,
                    (_client._LastServerStats.ServerStats.TotalPlayerCount - _client._LastServerStats.ServerStats.TotalPrivatePlayerCount)
                    + (_client._LastServerStats.ServerStats.TotalRetainerCount - _client._LastServerStats.ServerStats.TotalPrivateRetainerCount));
            }
                
            Util.ShowColoredMessage(_SyncMessage);

            //if (IsDbRefreshing)
            //{
            //    ImGui.NewLine();
            //    Util.ShowColoredMessage("Updating Local Database, please wait...");

            //    ImGui.SameLine();

            //    using (ImRaii.Disabled()) { ImGuiComponents.IconButtonWithText(HourGlass().Icon, $"{UpdatingLocalDbStatus}{HourGlass().Text}"); }
            //}
        }

        private CancellationTokenSource _cancellationToken;

        private (FontAwesomeIcon Icon, string Text) HourGlass()
        {
            int i = DateTime.Now.Second;
            if (i % 2 == 0)
            {
                return (FontAwesomeIcon.HourglassStart, "..");
            }
            else
            {
                return (FontAwesomeIcon.HourglassHalf, "...");
            }
        }

        bool IsSyncingPlayers;
        bool IsSyncingRetainers;
        string _SyncMessage = string.Empty;
        public int _LastCursor = 0;
        public ConcurrentDictionary<long, PlayerDto> _playersFetchedFromServer = new ConcurrentDictionary<long, PlayerDto>();
        public ConcurrentDictionary<long, RetainerDto> _retainersFetchedFromServer = new ConcurrentDictionary<long, RetainerDto>();
        public async Task<bool> SyncPlayersWithLocalDb(CancellationTokenSource cts)
        {
            _ = Task.Run(() =>
            {
            syncplayer:
                PlayerQueryObject query = new PlayerQueryObject() { Cursor = _LastCursor, IsFetching = true };
                var request = ApiClient.Instance.GetPlayers(query).ConfigureAwait(false).GetAwaiter().GetResult();
                if (request.Page.Data != null && !_cancellationToken.IsCancellationRequested)
                {
                    IsSyncingPlayers = true;

                    foreach (var _data in request.Page.Data)
                    {
                        _playersFetchedFromServer[_data.LocalContentId] = _data;
                    }

                    _LastCursor = request.Page.Cursor;
                    if (request.Page.NextCount > 0)
                    {
                        _SyncMessage = $"Fetching Players... ({_playersFetchedFromServer.Count}/{_client._LastServerStats.ServerStats.TotalPlayerCount - _client._LastServerStats.ServerStats.TotalPrivatePlayerCount})";
                        Thread.Sleep(300);
                        goto syncplayer;
                    }
                    else
                    {
                        _LastCursor = 0;
                        IsSyncingPlayers = false;
                        IsSyncingRetainers = true;
                        SyncRetainersWithLocalDb();
                    }
                }
                else
                {
                    if (_cancellationToken.IsCancellationRequested)
                        _SyncMessage = "Error: Stopped fetching.";
                    else
                        _SyncMessage = "Error: Unable to fetch Players.";

                    IsSyncingPlayers = false;
                }
            });

            return true;
        }
        public async Task<bool> SyncRetainersWithLocalDb()
        {
            _ = Task.Run(() =>
            {
            syncretainer:
                RetainerQueryObject query = new RetainerQueryObject() { Cursor = _LastCursor, IsFetching = true };
                var request = ApiClient.Instance.GetRetainers(query).ConfigureAwait(false).GetAwaiter().GetResult();
                if (request.Page.Data != null && !_cancellationToken.IsCancellationRequested)
                {
                    IsSyncingRetainers = true;

                    foreach (var _data in request.Page.Data)
                    {
                        _retainersFetchedFromServer[_data.LocalContentId] = _data;
                    }

                    _LastCursor = request.Page.Cursor;
                    if (request.Page.NextCount > 0)
                    {
                        _SyncMessage = $"Fetching Retainers... ({_retainersFetchedFromServer.Count}/{_client._LastServerStats.ServerStats.TotalRetainerCount - _client._LastServerStats.ServerStats.TotalPrivateRetainerCount})";
                        Thread.Sleep(300);
                        goto syncretainer;
                    }
                    else
                    {
                        int _serverPlayerCount = _client._LastServerStats.ServerStats.TotalPlayerCount - _client._LastServerStats.ServerStats.TotalPrivatePlayerCount;
                        int _serverRetainerCount = _client._LastServerStats.ServerStats.TotalRetainerCount - _client._LastServerStats.ServerStats.TotalPrivateRetainerCount;

                        if (_playersFetchedFromServer.Count > _serverPlayerCount)
                            _serverPlayerCount = _playersFetchedFromServer.Count;
                        if (_retainersFetchedFromServer.Count > _serverRetainerCount)
                            _serverRetainerCount = _retainersFetchedFromServer.Count;

                        _SyncMessage = $"Fetching Complete.\nPlayers: ({_playersFetchedFromServer.Count}/{_serverPlayerCount})" +
                                        $" - Retainers ({_retainersFetchedFromServer.Count}/{_serverRetainerCount})";

                        IsSyncingRetainers = false;
                        _LastCursor = 0;
                        IsDbRefreshing = true;
                        SyncWithLocalDB();
                    }
                }
                else
                {
                    if (_cancellationToken.IsCancellationRequested)
                        _SyncMessage = "Error: Stopped fetching.";
                    else
                        _SyncMessage = "Error: Unable to fetch Retainers.";

                    IsSyncingRetainers = false;
                }
            });

            return true;
        }

        public bool IsDbRefreshing;
        private void SyncWithLocalDB()
        {
            var playerMappings = _playersFetchedFromServer.Select(p => new PlayerMapping {
                ContentId = (ulong)p.Key,
                PlayerName = p.Value.Name,
                AccountId = p.Value.AccountId != null ? (ulong)p.Value.AccountId : null,
            }).ToList();

            var retainerMappings = _retainersFetchedFromServer.Select(r => new Retainer
            {
                LocalContentId = (ulong)r.Key,
                Name = r.Value.Name,
                OwnerLocalContentId = (ulong)r.Value.OwnerLocalContentId,
                WorldId = r.Value.WorldId,
            }).ToList();

            _ = Task.Run(() =>
            {
                PersistenceContext.Instance.HandleContentIdMappingAsync(playerMappings);
                PersistenceContext.Instance.HandleMarketBoardPage(retainerMappings);
            });

            MainWindow.ReloadMainWindowStats();

            _playersFetchedFromServer.Clear(); _retainersFetchedFromServer.Clear();
            IsDbRefreshing = false;

            _ = Task.Run(() =>
            {
                bIsNetworkProcessing = true;
                var request = _client.UserRefreshMyInfo().ConfigureAwait(false).GetAwaiter().GetResult();
                LastNetworkMessage = request.Message;
                LastNetworkMessageTime = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
                bIsNetworkProcessing = false;
                if (request.User != null)
                {
                    SaveUserResultToConfig(request.User);
                }
            });
        }

        public (ServerStatsDto ServerStats, string Message) CheckServerStats()
        {
            if (!bIsNetworkProcessing)
            {
                _ = Task.Run(() =>
                {
                    bIsNetworkProcessing = true;
                   // _client._LastServerStats.ServerStats = null;

                    var request = _client.CheckServerStats().ConfigureAwait(false).GetAwaiter().GetResult();
                    _LastServerStatsMessage = request.Message;

                    bIsNetworkProcessing = false;
                    return request;
                });
            }
            return (null, string.Empty);
        }

        int TablePlayerMaxLimit = 50;
        private async void DrawMyFavoritesPage()
        {
            ServerStatusGui();

            var players = Config.FavoritedPlayer;
            if (players == null) return;
            if (ImGui.BeginTable($"FavoritedPlayersTable", FavoritedPlayersColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
            {
                foreach (var t in FavoritedPlayersColumn)
                {
                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                }
                ImGui.TableHeadersRow();
                var index = 0;

                foreach (var (localContentId, player) in players)
                {
                    if (index > TablePlayerMaxLimit)
                        break;
                    if (player == null)
                        continue;
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); 

                    if (ImGui.Button("Load Details" + $"##{index}"))
                    {
                        DetailsWindow.Instance.IsOpen = true;
                        DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)localContentId, true);
                    }
                    ImGui.SameLine();

                    if (!string.IsNullOrWhiteSpace(player.Name)) // PlayerName column
                    {
                        if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                        {
                            if (ImGui.Button("c" + $"##{index}"))
                            {
                                ImGui.SetClipboardText(player.Name);
                            }
                            ImGui.SameLine();
                        }
                        ImGui.Text(player.Name);
                    }
                    else
                    {
                        ImGui.Text("---");
                    }

                    ImGui.TableNextColumn();  //cId column

                    if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                    {
                        if (ImGui.Button("c" + $"###{index}"))
                        {
                            ImGui.SetClipboardText(localContentId.ToString());
                        }
                        ImGui.SameLine();
                    }
                    ImGui.Text(localContentId.ToString());

                    ImGui.TableNextColumn(); //AccId column

                    if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                    {
                        if (ImGui.Button("c" + $"###{index}"))
                        {
                            ImGui.SetClipboardText(player.AccountId.ToString());
                        }
                        ImGui.SameLine();
                    }
                    ImGui.Text(player.AccountId.ToString());

                    ImGui.TableNextColumn(); //Remove column

                    if (ImGui.Button("X" + $"###{index}"))
                    {
                        Config.FavoritedPlayer.Remove(localContentId, out _);
                    }

                    index++;
                }
                ImGui.EndTable();
            }
        }

        string? _serverIpAdressField = null;
        bool bShowServerIpAdressTextField;
        public void ServerStatusGui()
        {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Database, "Ip"))
            {
                bShowServerIpAdressTextField = !bShowServerIpAdressTextField;
            }

            if (bShowServerIpAdressTextField)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(215);

                ImGui.InputTextWithHint("", "Server Ip", ref _serverIpAdressField, 50, ImGuiInputTextFlags.CharsNoBlank);
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Save, "Save"))
                {
                    try
                    {
                        Config.BaseUrl = _serverIpAdressField;
                        Config.Save();
                        ApiClient._restClient = new RestClient(Config.BaseUrl);
                        bShowServerIpAdressTextField = false;

                        _ = Task.Run(() =>
                        {
                            bIsNetworkProcessing = true;
                            var request = _client.CheckServerStatus().ConfigureAwait(false).GetAwaiter().GetResult();
                            bIsNetworkProcessing = false;
                        });
                    }
                    catch (Exception ex) { }
                }
            }
            else
            {
                _serverIpAdressField = Config.BaseUrl.ToString();
                //ImGui.TextWrapped(_serverIpAdressField.ToString());
                ImGui.SameLine();
            }

            var _checkServerStatusString = "Check Server Status";
            if (_client.IsCheckingServerStatus)
                _checkServerStatusString = "Checking Status...";
            using (ImRaii.Disabled(_client.IsCheckingServerStatus || bIsNetworkProcessing))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, _checkServerStatusString))
                {
                    _ = Task.Run(() =>
                    {
                        bIsNetworkProcessing = true;
                        var request = _client.CheckServerStatus().ConfigureAwait(false).GetAwaiter().GetResult();
                        bIsNetworkProcessing = false;
                    });
                }
            }
            ImGui.SameLine();
            if (!string.IsNullOrWhiteSpace(_client._ServerStatus))
            {
                if (_client._ServerStatus == "ONLINE")
                {
                    Util.TextWrapped(ImGuiColors.DalamudWhite, "Server Status:"); ImGui.SameLine();
                    Util.TextWrapped(ImGuiColors.HealerGreen, $"ONLINE"); ImGui.SameLine();
                    Util.TextWrapped(ImGuiColors.DalamudWhite, "Ping:"); ImGui.SameLine();
                    Util.TextWrapped(ImGuiColors.HealerGreen, $"{_client._LastPingValue}");
                }
                else
                {
                    Util.TextWrapped(ImGuiColors.DalamudWhite, "Server Status:"); ImGui.SameLine();
                    Util.TextWrapped(ImGuiColors.DalamudRed, _client._ServerStatus.ToString());
                }
            }

            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(4.0f);
        }
    }
}
