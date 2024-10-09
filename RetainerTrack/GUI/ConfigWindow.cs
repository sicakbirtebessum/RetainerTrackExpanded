using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network.Structures;
using Dalamud.Game.Text.SeStringHandling.Payloads;
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
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using ImGuiNET;
using Lumina;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
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
using static FFXIVClientStructs.FFXIV.Client.UI.AddonJobHudMNK1.ChakraGauge;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkUIColorHolder.Delegates;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using static RetainerTrackExpanded.API.Models.User;
using static RetainerTrackExpanded.Configuration;
using static RetainerTrackExpanded.GUI.MainWindow;

namespace RetainerTrackExpanded.GUI
{
    public class ConfigWindow : Window, IDisposable
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

            IsRefreshed = false;
            IsSaved = false;
            if (string.IsNullOrWhiteSpace(_client._ServerStatus))
            {
                _client.CheckServerStatus();
            }

            RefreshUserProfileInfo();
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

                            Config.ContentId = _contentId;
                            Config.AccountId = _accountId;
                            Config.Username = _playerName;
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

        public Configuration Config = RetainerTrackExpandedPlugin.Instance.Configuration;
        ApiClient _client = ApiClient.Instance;

        public void SaveUserResultToConfig(User user)
        {
            if (user != null)
            {
                LastUserInfo = user;

                Config.Username = LastUserInfo.Name;
                Config.ContentId = LastUserInfo.LocalContentId;
                Config.AccountId = LastUserInfo.GameAccountId;
                Config.AppRoleId = LastUserInfo.AppRoleId;
                
                Config.UploadedPlayersCount = LastUserInfo.NetworkStats?.UploadedPlayersCount;
                Config.UploadedPlayerInfoCount = LastUserInfo.NetworkStats?.UploadedPlayerInfoCount;
                Config.UploadedRetainersCount = LastUserInfo.NetworkStats?.UploadedRetainersCount;
                Config.UploadedRetainerInfoCount = LastUserInfo.NetworkStats?.UploadedRetainerInfoCount;
                Config.FetchedPlayerInfoCount = LastUserInfo.NetworkStats?.FetchedPlayerInfoCount;
                Config.SearchedNamesCount = LastUserInfo.NetworkStats?.SearchedNamesCount;
                Config.LastSyncedTime = LastUserInfo.NetworkStats?.LastSyncedTime;
  
                LastUserCharacters = LastUserInfo.Characters;

                if (LastUserInfo.Characters != null && LastUserInfo.Characters.Count > 0)
                {
                    foreach (var character in LastUserInfo.Characters)
                    {
                        if (character.Privacy == null)
                        {
                            character.Privacy = new CharacterPrivacySettingsDto();
                        }
                        _LocalUserCharacters[(long)character.LocalContentId] = new UserCharacters { Name = character.Name, Privacy = character.Privacy };
                    }
                }
   
                Config.LoggedIn = true;
                Config.FreshInstall = false;

                Config.Save();
            }
        }

        User LastUserInfo { get; set; }
        List<User.UserCharacterDto?> LastUserCharacters { get; set; }

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
                    DrawUserInfoTab();
                    ImGui.EndTabItem();
                }

                using (ImRaii.Disabled(!Config.LoggedIn))
                {
                    if (ImGui.BeginTabItem("Server Stats & Sync Local Database"))
                    {
                        DrawServerStatsTab();
                        ImGui.EndTabItem();
                    }
                }
                using (ImRaii.Disabled(!Config.LoggedIn))
                {
                    if (ImGui.BeginTabItem("My Characters & Privacy"))
                    {
                        DrawMyCharactersTab();
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
            }
        }

        private async void DrawUserInfoTab()
        {
            ServerStatusGui();

            if (!string.IsNullOrWhiteSpace(Config.Key) && Config.LoggedIn)
            {
                ImGui.Text("Player Name:");
                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudYellow, Config.Username);

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
                   
                    Util.TextCopy(ImGuiColors.TankBlue, Config.AccountId.ToString());
                    Util.SetHoverTooltip("Click to copy AccountId");

                    ImGui.SameLine();

                    ImGui.Text("LocalContent Id: ");
                    ImGui.SameLine();
                    Util.TextCopy(ImGuiColors.TankBlue, Config.ContentId.ToString());
                    Util.SetHoverTooltip("Click to copy LocalContent Id");
                }

                if (ImGui.CollapsingHeader("See your contributions ♥", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    Util.DrawHelp(false,"The total number of players you sent to the server\nwho were not previously in the database.");

                    ImGui.Text("Uploaded Players Count:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.UploadedPlayersCount.ToString());

                    Util.DrawHelp(true, "The number of player info changes you sent to the server\nwho are already in the database but have different names or worlds");

                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, $"({Config.UploadedPlayerInfoCount.ToString()})");

                    Util.DrawHelp(false, "The total number of retainers you sent to the server\nwho were not previously in the database.");

                    ImGui.Text("Uploaded Retainers Count:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.UploadedRetainersCount.ToString());
                   
                    Util.DrawHelp(true, "The total number of retainer info changes (name or world) you sent to the server.");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, $"({Config.UploadedRetainerInfoCount.ToString()})");

                    Util.DrawHelp(false, "The total number of players whose profile details you have viewed.");

                    ImGui.Text("Fetched Player Info Count:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.FetchedPlayerInfoCount.ToString());

                    Util.DrawHelp(false, "The total number of player and retainer search count.");

                    ImGui.Text("Total number of searched Player & Retainer names:");
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.TankBlue, Config.SearchedNamesCount.ToString());
                }

                ImGui.NewLine();

                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, "Refresh Profile Info"))
                {
                    RefreshUserProfileInfo();
                }
                Util.SetHoverTooltip("Request profile data from the server");

                if (!string.IsNullOrWhiteSpace(LastNetworkMessage))
                {
                    ImGui.SameLine();
                    Util.ColoredErrorTextWrapped($"{LastNetworkMessage} ({LastNetworkMessageTime})");
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

                    //ImGui.SetNextItemWidth(200);
                    using (ImRaii.Disabled(bIsNetworkProcessing || ClickedLoginButton || _client._ServerStatus != "ONLINE"))
                    {
                        if (ImGui.Button("Login with Discord", new Vector2(150, 30)))
                        {
                            ClickedLoginButton = true;
                            _ = Task.Run(() =>
                            {
                                bIsNetworkProcessing = true;
                                var request = _client.DiscordAuth(new UserRegister
                                {
                                    GameAccountId = _accountId,
                                    UserLocalContentId = _contentId,
                                    Name = _playerName,
                                    ClientId = Config.Key,
                                    Version = Util.clientVer
                                }).ConfigureAwait(false).GetAwaiter().GetResult();

                                LastRegistrationWindowMessage = request.Message;
                                bIsNetworkProcessing = false;
                            });
                        }
                    }
                    if (ClickedLoginButton)
                    {
                        ImGui.SameLine();
                        if (ImGui.Button("Continue", new Vector2(120, 30)))
                        {
                            _ = Task.Run(() =>
                            {
                                bIsNetworkProcessing = true;
                                var request = _client.UserLogin(new UserRegister
                                {
                                    GameAccountId = _accountId,
                                    UserLocalContentId = _contentId,
                                    Name = _playerName,
                                    ClientId = Config.Key,
                                    Version = Util.clientVer,
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
                    Util.ColoredErrorTextWrapped(LastRegistrationWindowMessage);
                }
                else
                {
                    ImGui.TextColored(ImGuiColors.DalamudRed, "You are not logged in.\nPlease login with a character to continue.");

                }
            }
        }
        bool ClickedLoginButton = false;
        bool IsRefreshStatsRequestSent = false;
        public string _LastServerStatsMessage = string.Empty;
        private async void DrawServerStatsTab()
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
                Util.ColoredErrorTextWrapped(_LastServerStatsMessage);
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
        }

        private ConcurrentDictionary<long, UserCharacters> _LocalUserCharacters = new();
        public class UserCharacters
        {
            public string? Name { get; set; }
            public User.CharacterPrivacySettingsDto? Privacy { get; init; }
        }

        private List<long?> EditedCharactersPrivacy = new List<long?>();
        private async void DrawMyCharactersTab()
        {
            ImGui.TextWrapped($"You can configure the privacy settings of your characters in this tab.");

            ImGui.NewLine();

            ImGui.TextWrapped($"A total of {_LocalUserCharacters.Count} characters of yours were found.");

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.BeginGroup();

            if (LastUserInfo != null && LastUserInfo.Characters != null && LastUserInfo.Characters.Count > 0 && _LocalUserCharacters.Count > 0)
            {

                var index = 0;
                foreach (var character in LastUserInfo.Characters)
                {
                    if (ImGui.Button("Load Details" + $"##{index}"))
                    {
                        DetailsWindow.Instance.IsOpen = true;
                        DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)character.LocalContentId, true);
                    }

                    ImGui.SameLine();
                    var charName = character.Name != null || !string.IsNullOrWhiteSpace(character.Name) ? character.Name : "[NAME NOT FOUND]";
                    var headerText = $"{charName}";

                    int visitCount = character.ProfileVisitInfo != null ? (int)character.ProfileVisitInfo.ProfileTotalVisitCount : 0;
                    if (visitCount > 0)
                    {
                        headerText += $" | Total Visit Count: {visitCount}";
                    }

                    if (ImGui.CollapsingHeader($"{headerText}"))
                    {
                        if (visitCount > 0)
                        {
                            var lastVisitDateString = $"{Tools.UnixTimeConverter(character.ProfileVisitInfo.LastProfileVisitDate)} ({Tools.ToTimeSinceString((int)character.ProfileVisitInfo.LastProfileVisitDate)})";
                            Util.ColoredTextWrapped(ImGuiColors.ParsedBlue, "Someone visited your profile on: " + lastVisitDateString);
                        }
                        if (character.Privacy != null)
                        {
                            _LocalUserCharacters.TryGetValue((long)character.LocalContentId, out var _getLocalChara);

                            bool _bHideFullProfile = _getLocalChara.Privacy.HideFullProfile;
                            bool _bHideTerritoryInfo = _getLocalChara.Privacy.HideTerritoryInfo;
                            bool _bHideCustomizations = _getLocalChara.Privacy.HideCustomizations;
                            bool _bHideInSearchResults = _getLocalChara.Privacy.HideInSearchResults;
                            bool _bHideRetainersInfo = _getLocalChara.Privacy.HideRetainersInfo;
                            bool _bHideAltCharacters = _getLocalChara.Privacy.HideAltCharacters;

                            if (ImGui.Checkbox("Hide Full Profile" + $"##{index}", ref _bHideFullProfile))
                            {
                                _getLocalChara.Privacy.HideFullProfile = _bHideFullProfile;
                                if (!EditedCharactersPrivacy.Contains(character.LocalContentId)) EditedCharactersPrivacy.Add(character.LocalContentId);
                            }
                            Util.SetHoverTooltip("Other players will NOT be able to see your profile."
                                            + "\nHowever, if they have seen you before, they can find you by searching in the Local PC section.");

                            ImGui.SameLine();

                            using (ImRaii.Disabled(_bHideFullProfile))
                            {
                                if (ImGui.Checkbox("Hide Territory History" + $"##{index}", ref _bHideTerritoryInfo))
                                {
                                    _getLocalChara.Privacy.HideTerritoryInfo = _bHideTerritoryInfo;
                                    if (!EditedCharactersPrivacy.Contains(character.LocalContentId)) EditedCharactersPrivacy.Add(character.LocalContentId);
                                }
                                Util.SetHoverTooltip("Other players will NOT be able to see your location history.");

                                ImGui.SameLine();

                                if (ImGui.Checkbox("Hide Customization History" + $"##{index}", ref _bHideCustomizations))
                                {
                                    _getLocalChara.Privacy.HideCustomizations = _bHideCustomizations;
                                    if (!EditedCharactersPrivacy.Contains(character.LocalContentId)) EditedCharactersPrivacy.Add(character.LocalContentId);
                                }
                                Util.SetHoverTooltip("Other players will NOT be able to see your customization history.");

                                if (ImGui.Checkbox("Dont Appear in Search Results" + $"##{index}", ref _bHideInSearchResults))
                                {
                                    _getLocalChara.Privacy.HideInSearchResults = _bHideInSearchResults;
                                    if (!EditedCharactersPrivacy.Contains(character.LocalContentId)) EditedCharactersPrivacy.Add(character.LocalContentId);//  character.HideInSearchResults = !character.HideInSearchResults;
                                }
                                Util.SetHoverTooltip("Your name will NOT appear in search results.");

                                ImGui.SameLine();

                                if (ImGui.Checkbox("Hide Alt Characters" + $"##{index}", ref _bHideAltCharacters))
                                {
                                    _getLocalChara.Privacy.HideAltCharacters = _bHideAltCharacters;
                                    if (!EditedCharactersPrivacy.Contains(character.LocalContentId)) EditedCharactersPrivacy.Add(character.LocalContentId);//  character.HideInSearchResults = !character.HideInSearchResults;
                                }
                                Util.SetHoverTooltip("None of your alt characters will be visible to other players in the detailed player window.");
                            }
                           
                            ImGui.SameLine();

                            if (ImGui.Checkbox("Hide Retainers" + $"##{index}", ref _bHideRetainersInfo))
                            {
                                _getLocalChara.Privacy.HideRetainersInfo = _bHideRetainersInfo;
                                if (!EditedCharactersPrivacy.Contains(character.LocalContentId)) EditedCharactersPrivacy.Add(character.LocalContentId);//  character.HideInSearchResults = !character.HideInSearchResults;
                            }
                            Util.SetHoverTooltip("None of your retainers will be visible to other players and will NOT appear in search results.");
                        }
                        ImGuiHelpers.ScaledDummy(5.0f);
                        ImGui.Separator();
                        ImGuiHelpers.ScaledDummy(5.0f);
                    }
                    index++;
                }
            }

            ImGui.EndGroup();

            ImGui.NewLine();

            using (ImRaii.Disabled(bIsNetworkProcessing))
            {
                using (ImRaii.Disabled(EditedCharactersPrivacy.Count == 0))
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Save, "Save Config"))
                    {
                        var updateCharacters = new List<UserCharacterDto?>();
                        foreach (var chara in _LocalUserCharacters)
                        {
                            if (EditedCharactersPrivacy.Contains(chara.Key))
                            {
                                updateCharacters.Add(new UserCharacterDto { LocalContentId = chara.Key, Name = chara.Value.Name, Privacy = chara.Value.Privacy });
                            }
                        }
                        _ = Task.Run(() =>
                        {
                            bIsNetworkProcessing = true;
                            
                            var response = _client.UserUpdate(new UserUpdateDto { Characters = updateCharacters }).ConfigureAwait(false).GetAwaiter().GetResult();
                            LastNetworkMessage = response;
                            bIsNetworkProcessing = false;
                            RefreshUserProfileInfo();
                        });

                        EditedCharactersPrivacy.Clear();
                    }
                Util.SetHoverTooltip("Save your configuration to server");

                ImGui.SameLine();

                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Sync, "Refresh Profile Info"))
                {
                    RefreshUserProfileInfo();
                }
                Util.SetHoverTooltip("Request profile data from the server");

                ImGui.SameLine();

                if (!string.IsNullOrWhiteSpace(LastNetworkMessage))
                {
                    Util.ColoredErrorTextWrapped($"{LastNetworkMessage} ({LastNetworkMessageTime})");
                }
            }
        }

        private void RefreshUserProfileInfo()
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
                    LastNetworkMessageTime = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
                    SaveUserResultToConfig(request.User);
                }
            });
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
            });

            _ = Task.Run(() =>
            {
                PersistenceContext.Instance.HandleMarketBoardPage(retainerMappings);
            });

            _playersFetchedFromServer.Clear(); _retainersFetchedFromServer.Clear();
            IsDbRefreshing = false;

            RefreshUserProfileInfo();
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
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Save, "Connect"))
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
