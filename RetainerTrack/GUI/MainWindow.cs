using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Collections.Concurrent;
using RetainerTrackExpanded.Handlers;
using static RetainerTrackExpanded.Handlers.PersistenceContext;
using RetainerTrackExpanded.API;
using RetainerTrackExpanded.API.Models;
using System.Threading;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using System.Text.RegularExpressions;
using RetainerTrackExpanded.API.Query;
using Dalamud.Interface.Utility.Raii;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections;
using Dalamud.Interface.Components;
using Dalamud.Interface;

namespace RetainerTrackExpanded.GUI
{
    public class MainWindow : Window, IDisposable
    {
        public MainWindow() : base("Retainer Track Main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            if (_instance == null)
            {
                _instance = this;
            }
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(770, 545),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }
        private static MainWindow _instance = null;
        public static MainWindow Instance
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
            ReloadMainWindowStats();
        }

        public static void ReloadMainWindowStats()
        {
            UpdateStatisticsValues();
            GetServerRetainersCount();
            UpdateRetainers();
        }

        private readonly string[] TableColumn = new string[]
        {
        "CurrentName","AccCount","RetainerCount","Retainers","ContentId","AccountId"
        };

        private readonly string[] FromServerTableSearchPlayersColumn = new string[]
        {
        "CurrentName","Content Id","Account Id"
        };

        private readonly string[] FromServerTableSearchRetainersColumn = new string[]
        {
        "Name","World","Added at","OwnerContentId","ContentId"
        };

        private readonly string[] WorldsTableColumn = new string[]
        {
        "World", "DataCenter", "Total Retainers"
        };
        public enum Tabs
        {
            General,
            SearchPlayersAndRetainers
        }
        public string _searchContent = "SearchQuery";

        public static int _TotalPlayers_Value = 0;
        public static int _TotalRetainers_Value = 0;

        public long LastUnix = 0;

        public int TablePlayerMaxLimit = 50;

        public Tabs _CurrentTab = Tabs.General;

        public static void UpdateStatisticsValues()
        {
            _TotalPlayers_Value = PersistenceContext._playerCache.Count;
            _TotalRetainers_Value = PersistenceContext._retainerCache.Count;
        }

        public override void Draw()
        {
            if (ImGui.BeginTabBar("Tabs"))
            {
                if (ImGui.BeginTabItem("General"))
                {
                    _CurrentTab = Tabs.General;
                    DrawStatisticsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Search Players & Retainers"))
                {
                    _CurrentTab = Tabs.SearchPlayersAndRetainers;
                    DrawSearchPlayersAndRetainers_FromServerTabAsync();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        private int lastSelectedPlayerOrRetainerValue = 0;
        private int selectedComboItem_ServerOrLocalDb = 0;
        private int selectedComboItem_PlayerOrRetainer = 0;
        private int selectedComboItem_NameorId = 0;

        private string[] selectedComboItems0 = ["Server", "Local PC"];
        private string[] selectedComboItems1 = ["Player", "Retainer"];
        private string[] selectedComboItems2 = ["By Name", "By Id"];

        public Configuration Config = RetainerTrackExpandedPlugin.Instance.Configuration;

        public ApiClient _client = ApiClient.Instance;
        public (Dictionary<long, PlayerDto> Players, string Message) _LastPlayerSearchResult = new();
        public (Dictionary<long, API.Models.RetainerDto> Retainers, string Message) _LastRetainerSearchResult = new();
        public bool bIsNetworkProcessing = false;

        public void SetPlayerResult((Dictionary<long, PlayerDto> Players, string Message) PlayerResult)
        {
            _LastRetainerSearchResult = (_LastRetainerSearchResult.Retainers, string.Empty);
            _LastPlayerSearchResult = (PlayerResult.Players, PlayerResult.Message);
        }

        public void SetRetainerResult((Dictionary<long, API.Models.RetainerDto> Retainers, string Message) RetainerResult)
        {
            _LastPlayerSearchResult = (_LastPlayerSearchResult.Players, string.Empty);
            _LastRetainerSearchResult = (RetainerResult.Retainers, RetainerResult.Message);
        }

        public async System.Threading.Tasks.Task DrawSearchPlayersAndRetainers_FromServerTabAsync()
        {
            if (lastSelectedPlayerOrRetainerValue != selectedComboItem_PlayerOrRetainer)
            {
                LastTargetName = "###";
                _TestTempPlayerWithRetainers.Clear();
                lastSelectedPlayerOrRetainerValue = selectedComboItem_PlayerOrRetainer;
            }

            ImGui.Text("Source: ");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            ImGui.Combo("##db1", ref selectedComboItem_ServerOrLocalDb, selectedComboItems0, 2);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            ImGui.Combo("##serverDB1", ref selectedComboItem_PlayerOrRetainer, selectedComboItems1, 2);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);

            if (selectedComboItem_PlayerOrRetainer == 0)
                selectedComboItems2 = ["By Name", "By Id"];
            else
            {
                selectedComboItems2 = ["By Name"];
                selectedComboItem_NameorId = 0;
            }

            if (selectedComboItem_ServerOrLocalDb == 1)
                selectedComboItem_NameorId = 0;

            using (ImRaii.Disabled(selectedComboItem_PlayerOrRetainer == 1 || selectedComboItem_ServerOrLocalDb == 1))
            {
                ImGui.Combo("##serverDB2", ref selectedComboItem_NameorId, selectedComboItems2, 2);
            }

            string SearchByPlayerOrRetainer = selectedComboItem_PlayerOrRetainer == 0 ? "Player" : "Retainer";
            string SearchByNameorId = selectedComboItem_NameorId == 0 ? "Name" : "Id";
            ImGui.SameLine();
            ImGui.Text("->");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            ImGui.InputTextWithHint("##searchC", $"Enter {SearchByPlayerOrRetainer}'s {SearchByNameorId} here", ref _searchContent, 32);

            ImGui.SetNextItemWidth(140);
            ImGui.SameLine();

 
            if (selectedComboItem_ServerOrLocalDb == 0)
            {
                string SearchButtonText = (bIsNetworkProcessing ? "Searching..." : "Search");
                using (ImRaii.Disabled(bIsNetworkProcessing))
                {
                    if (ImGui.Button(SearchButtonText) || ImGui.IsKeyPressed(ImGuiKey.Enter))
                    {
                        if (selectedComboItem_PlayerOrRetainer == 0) //Search Player
                        {
                            if (selectedComboItem_NameorId == 0)
                            {
                                bool regex = Regex.IsMatch(_searchContent, @"^[A-Za-z]+(?:[ '-][A-Za-z]+)*[ '-]?$");
                                if (!regex)
                                {
                                    SetPlayerResult((_LastPlayerSearchResult.Players, "Error: Bad search query"));
                                    return;
                                }

                                var query = new PlayerQueryObject() { Name = _searchContent };
                                _ = System.Threading.Tasks.Task.Run(() =>
                                {
                                    bIsNetworkProcessing = true;

                                    var request = _client.GetPlayers(query).ConfigureAwait(false).GetAwaiter().GetResult();
                                    if (request.Page == null)
                                    {
                                        SetPlayerResult((_LastPlayerSearchResult.Players, request.Message));
                                        bIsNetworkProcessing = false;
                                        return;
                                    }

                                    SetPlayerResult((request.Page.Data.ToDictionary(t => t.LocalContentId, t => t), request.Message));
                                    bIsNetworkProcessing = false;
                                });
                            }
                            else if (selectedComboItem_NameorId == 1)
                            {
                                bool isParsedSuccessfully = Int64.TryParse(_searchContent, out long providedLocalContentId);
                                if (!isParsedSuccessfully || _searchContent.Length > 17)
                                {
                                    SetPlayerResult((_LastPlayerSearchResult.Players, "Error: Bad search query"));
                                    return;
                                }
                                _ = System.Threading.Tasks.Task.Run(() =>
                                {
                                    bIsNetworkProcessing = true;
                                    var query = new PlayerQueryObject() { LocalContentId = providedLocalContentId };
                                    var request = _client.GetPlayers(query).ConfigureAwait(false).GetAwaiter().GetResult();

                                    if (request.Page == null)
                                    {
                                        SetPlayerResult((_LastPlayerSearchResult.Players, request.Message));
                                        bIsNetworkProcessing = false;
                                        return;
                                    }

                                    SetPlayerResult((request.Page.Data.ToDictionary(t => t.LocalContentId, t => t), request.Message));
                                    bIsNetworkProcessing = false;
                                });

                            }
                        }

                        else if (selectedComboItem_PlayerOrRetainer == 1) //Search Retainer
                        {
                            bool regex = Regex.IsMatch(_searchContent, @"^[a-zA-Z'-]+$");
                            if (!regex)
                            {
                                SetRetainerResult((_LastRetainerSearchResult.Retainers, "Error: Bad search query"));
                                return;
                            }

                            var query = new RetainerQueryObject() { Name = _searchContent };
                            _ = System.Threading.Tasks.Task.Run(() =>
                            {
                                bIsNetworkProcessing = true;
                                var request = _client.GetRetainers(query).ConfigureAwait(false).GetAwaiter().GetResult();

                                if (request.Page == null)
                                {
                                    SetRetainerResult((_LastRetainerSearchResult.Retainers, request.Message));
                                    bIsNetworkProcessing = false;
                                    return;
                                }

                                SetRetainerResult((request.Page.Data.ToDictionary(t => t.LocalContentId, t => t), request.Message));
                                bIsNetworkProcessing = false;
                            });
                        }
                    }
                    ImGui.SameLine();
                }
            }

            if (!string.IsNullOrWhiteSpace(_LastPlayerSearchResult.Message))
            {
                ImGui.SetNextItemWidth(100);
                Util.ColoredTextWrapped(_LastPlayerSearchResult.Message);
            }

            if (!string.IsNullOrWhiteSpace(_LastRetainerSearchResult.Message))
            {
                ImGui.SetNextItemWidth(100);
                Util.ColoredTextWrapped(_LastRetainerSearchResult.Message);
            }

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            if (selectedComboItem_ServerOrLocalDb == 0)
            {
                if (selectedComboItem_PlayerOrRetainer == 0) //Search Player
                {
                    if (_LastPlayerSearchResult.Players == null) return;
                    if (ImGui.BeginTable($"List##{_searchContent}", FromServerTableSearchPlayersColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
                    {
                        foreach (var t in FromServerTableSearchPlayersColumn)
                        {
                            ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                        }
                        ImGui.TableHeadersRow();
                        var index = 0;

                        foreach (var (localContentId, player) in _LastPlayerSearchResult.Players)
                        {
                            if (index > TablePlayerMaxLimit)
                                break;
                            if (player == null)
                                continue;
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn(); // PlayerName column

                            if (ImGui.Button("Load Details" + $"##{index}"))
                            {
                                DetailsWindow.Instance.IsOpen = true;
                                DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)localContentId, true);
                            }
                            ImGui.SameLine();

                            if (!string.IsNullOrWhiteSpace(player.Name))
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
                                    ImGui.SetClipboardText(player.AccountId?.ToString());
                                }
                                ImGui.SameLine();
                            }
                            ImGui.Text(player.AccountId.ToString());

                            index++;
                        }
                        ImGui.EndTable();
                    }
                }
                else if (selectedComboItem_PlayerOrRetainer == 1) //Search Retainer
                {
                    if (_LastRetainerSearchResult.Retainers == null) return;
                    if (ImGui.BeginTable($"List3", FromServerTableSearchRetainersColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
                    {
                        foreach (var t in FromServerTableSearchRetainersColumn)
                        {
                            ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                        }
                        ImGui.TableHeadersRow();
                        var index = 0;

                        foreach (var (localContentId, retainer) in _LastRetainerSearchResult.Retainers)
                        {
                            if (index > TablePlayerMaxLimit)
                                break;
                            if (retainer == null)
                                continue;
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn(); // RetainerName column

                            if (ImGui.Button("Load Details" + $"##{index}"))
                            {
                                DetailsWindow.Instance.IsOpen = true;
                                DetailsWindow.Instance.OpenDetailedPlayerWindow((ulong)retainer.OwnerLocalContentId, true);
                            }
                            ImGui.SameLine();

                            if (!string.IsNullOrWhiteSpace(retainer.Name))
                            {
                                if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                                {
                                    if (ImGui.Button("c" + $"##{index}"))
                                    {
                                        ImGui.SetClipboardText(retainer.Name);
                                    }
                                    ImGui.SameLine();
                                }
                                ImGui.Text(retainer.Name);
                            }
                            else
                            {
                                ImGui.Text("---");
                            }

                            ImGui.TableNextColumn();  //World column

                            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                            {
                                if (ImGui.Button("c" + $"###{index}"))
                                {
                                    ImGui.SetClipboardText(Util.GetWorld(retainer.WorldId).Name.ToString());
                                }
                                ImGui.SameLine();
                            }
                            ImGui.Text(Util.GetWorld(retainer.WorldId).Name.ToString());

                            ImGui.TableNextColumn(); //Created At column

                            var _CreatedAt = Tools.UnixTimeConverter(retainer.CreatedAt).ToString();
                            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                            {
                                if (ImGui.Button("c" + $"###{index}"))
                                {
                                    ImGui.SetClipboardText(_CreatedAt);
                                }
                                ImGui.SameLine();
                            }
                            ImGui.Text(_CreatedAt);

                            ImGui.TableNextColumn(); //OwnerContentId column

                            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                            {
                                if (ImGui.Button("c" + $"###{index}"))
                                {
                                    ImGui.SetClipboardText(retainer.OwnerLocalContentId.ToString());
                                }
                                ImGui.SameLine();
                            }
                            ImGui.Text(retainer.OwnerLocalContentId.ToString());

                            ImGui.TableNextColumn(); //RetainerContentId column

                            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                            {
                                if (ImGui.Button("c" + $"###{index}"))
                                {
                                    ImGui.SetClipboardText(retainer.LocalContentId.ToString());
                                }
                                ImGui.SameLine();
                            }
                            ImGui.Text(retainer.LocalContentId.ToString());

                            index++;
                        }
                        ImGui.EndTable();
                    }
                }
            }
            else
            {
                if (_searchContent.Length <= 1)
                    return;

                if (ImGui.BeginTable($"SocialList##{_searchContent}", TableColumn.Length, ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY))
                {
                    foreach (var t in TableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();
                    var index = 0;

                    foreach (var (contentId, player) in SearchPlayer(_searchContent))
                    {
                        if (index > TablePlayerMaxLimit)
                            break;

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); // PlayerName column

                        if (ImGui.Button("Show" + $"##{index}"))
                        {
                            DetailsWindow.Instance.IsOpen = true;
                            DetailsWindow.Instance.OpenDetailedPlayerWindow(contentId, false);
                        }
                        ImGui.SameLine();

                        if (player.Item1.Name != null)
                        {
                            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                            {
                                if (ImGui.Button("c" + $"##{index}"))
                                {
                                    ImGui.SetClipboardText(player.Item1.ToString());
                                }
                                ImGui.SameLine();
                            }
                            ImGui.Text(player.Item1.Name);
                        }
                        else
                            ImGui.Text("-");

                        ImGui.TableNextColumn(); //TotalACCS column

                        if (player.TotalAccCount >= 0)
                            ImGui.Text(player.TotalAccCount.ToString());
                        else
                            ImGui.Text(string.Empty);

                        ImGui.TableNextColumn();

                        if (player.Item2 != null && player.Item2.Count > 0) // TotalRetainerCount column
                        {
                            ImGui.Text(player.Item2.Count.ToString());
                        }
                        else
                        {
                            ImGui.Text(string.Empty);
                        }

                        ImGui.TableNextColumn(); //RetainerNames column

                        if (player.Item2 != null && player.Item2.Count > 0)
                        {
                            List<string> TempRetainerNameList = player.Item2.Select(o => o.Name).ToList();
                            String[] str = TempRetainerNameList.ToArray();
                            ImGui.Text(string.Join(", ", str));
                        }

                        ImGui.TableNextColumn();  //cId column

                        if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                        {
                            if (ImGui.Button("c" + $"###{index}"))
                            {
                                ImGui.SetClipboardText(contentId.ToString());
                            }
                            ImGui.SameLine();
                        }
                        ImGui.Text(contentId.ToString());

                        ImGui.TableNextColumn(); //AccId column

                        if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                        {
                            if (ImGui.Button("c" + $"###{index}"))
                            {
                                ImGui.SetClipboardText(player.Item1.AccountId.ToString());
                            }
                            ImGui.SameLine();
                        }
                        ImGui.Text(player.Item1.AccountId.ToString());

                        index++;
                    }
                    ImGui.EndTable();
                }
            }
        }
        private async void DrawStatisticsTab()
        {
            if (Tools.UnixTime - LastUnix >= 2)
            {
                UpdateStatisticsValues();
                GetServerRetainersCount();
                LastUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            if (!Config.IsLoggedIn || string.IsNullOrWhiteSpace(Config.Key))
            {
                Util.ShowColoredMessage("Error: You are not connected.");
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Lock, "Open Configuration Menu to connect to Server"))
                {
                    ConfigWindow.Instance.IsOpen = true;
                }
            }
            else
            {
                Util.ShowColoredMessage("You are connected. You can sync from the server.");
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.LockOpen, "Open Configuration Menu"))
                {
                    ConfigWindow.Instance.IsOpen = true;
                }
            }

            ImGui.Text($"Total Players: {_TotalPlayers_Value}  -  ");
            ImGui.SameLine();
            ImGui.Text($"Total Retainers: {_TotalRetainers_Value}");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(200);
            ImGui.Text($"");

            ImGui.BeginChild("Ch");

            if (ImGui.BeginTable($"ServerList##{_searchContent}", WorldsTableColumn.Length, ImGuiTableFlags.BordersInner))
            {
                foreach (var t in WorldsTableColumn)
                {
                    int length = 150;
                    if (t == "Id")
                    {
                        length = 30;
                    }
                    else if (t == "Total Retainers")
                    {
                        length = 150;
                    }

                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.NoResize | ImGuiTableColumnFlags.WidthFixed, length);
                }
                ImGui.TableHeadersRow();
                var index = 0;

                foreach (var server in _TempGetServerRetainersCount.OrderByDescending(a => a.Value.Count))
                {
                    if (index > TablePlayerMaxLimit)
                        break;

                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();

                    ImGui.Text(Util.GetWorld(server.Key).Name); // Servername column

                    ImGui.TableNextColumn();

                    ImGui.Text(Util.GetWorld(server.Key).DataCenter.Value.Name); //DataCenter column

                    ImGui.TableNextColumn();

                    ImGui.Text(server.Value.Count().ToString()); //Retainer Count column

                    ImGui.TableNextColumn();

                    index++;
                }
                ImGui.TableNextRow();

                ImGui.EndTable();
            }

            ImGui.EndChild();
        }

        public static ConcurrentDictionary<ushort, List<ulong>> _TempGetServerRetainersCount = new ConcurrentDictionary<ushort, List<ulong>>();
        public static int LastTotalRetainerCount = 0;

        public static ConcurrentDictionary<ushort, List<ulong>> GetServerRetainersCount()
        {
            if (LastTotalRetainerCount != PersistenceContext._retainerCache.Keys.Count)
            {
                foreach (var retainer in PersistenceContext._retainerCache.Values)
                {
                    var GetPlayerRetainers = _TempGetServerRetainersCount.GetOrAdd(retainer.WorldId, _ => new List<ulong>() { retainer.LocalContentId });

                    if (!GetPlayerRetainers.Contains(retainer.LocalContentId))
                    {
                        _TempGetServerRetainersCount[retainer.WorldId].Add(retainer.LocalContentId);
                    }
                }

                LastTotalRetainerCount = PersistenceContext._retainerCache.Count;
            }
            //var orderedWorlds = _TempGetServerRetainersCount.OrderBy(a => a.Value.Count).ToDictionary();
           // var finalWorlds = _TempGetServerRetainersCount = new ConcurrentDictionary<ushort, List<ulong>>(orderedWorlds);
            return _TempGetServerRetainersCount;
        }


        ConcurrentDictionary<ulong, (PersistenceContext.CachedPlayer, List<Database.Retainer>, int TotalAccCount)> _TestTempPlayerWithRetainers = new();
        string LastTargetName = "###";

        ConcurrentDictionary<ulong, (PersistenceContext.CachedPlayer, List<Database.Retainer>, int TotalAccCount)> SearchPlayer(string targetName)
        {
            targetName = targetName.ToLower();

            if (LastTargetName == targetName)
                return _TestTempPlayerWithRetainers;
            else
                _TestTempPlayerWithRetainers.Clear();

            bool Compare(string fullname)
            {
                return fullname.ToLower().Contains(targetName);
            }

            if (_CurrentTab == Tabs.SearchPlayersAndRetainers && selectedComboItem_ServerOrLocalDb == 1) //localdb
            {
                if (selectedComboItem_PlayerOrRetainer == 0) //Search Players in localdb
                {
                    foreach (var player in PersistenceContext._playerWithRetainersCache)
                    {
                        var playerName = player.Value.Player.Name.ToLower(); //PlayerName
                        var cId = player.Key; //cId

                        if (Compare(playerName) || Compare(cId.ToString()))
                        {
                            if (player.Value.Player.AccountId != null && !_AccountIdCache.IsEmpty)
                            {
                                _AccountIdCache.TryGetValue((ulong)player.Value.Player.AccountId, out var GetAccountsContentIds);
                                if (GetAccountsContentIds != null)
                                {
                                    _TestTempPlayerWithRetainers.GetOrAdd(cId, _ => (player.Value.Player, player.Value.Retainers, GetAccountsContentIds.Count));
                                }
                            }
                            else
                            {
                                _TestTempPlayerWithRetainers.GetOrAdd(cId, _ => (player.Value.Player, player.Value.Retainers, 0));
                            }
                        }
                    }
                    LastTargetName = targetName;

                    return _TestTempPlayerWithRetainers;
                }
                else //Search Retainers in localdb
                {
                    List<ulong> AddedOwners = new List<ulong>();
                    foreach (var retainer in _retainerCache)
                    {
                        if (!AddedOwners.Contains(retainer.Value.OwnerLocalContentId))
                        {
                            string retainerName = retainer.Value.Name.ToLower();
                            if (retainerName.Contains(targetName))
                            {
                                PersistenceContext._playerWithRetainersCache.TryGetValue(retainer.Value.OwnerLocalContentId, out var _GetPlayerValues);

                                AddedOwners.Add(retainer.Value.OwnerLocalContentId);
                                if (_GetPlayerValues.Player != null && _GetPlayerValues.Player.AccountId != null && !_AccountIdCache.IsEmpty)
                                {
                                    _AccountIdCache.TryGetValue((ulong)_GetPlayerValues.Player.AccountId, out var GetAccountsContentIds);
                                    if (GetAccountsContentIds != null)
                                    {
                                        _TestTempPlayerWithRetainers.GetOrAdd(retainer.Value.OwnerLocalContentId, _ => (_GetPlayerValues.Player, _GetPlayerValues.Retainers, GetAccountsContentIds.Count));
                                    }
                                }
                                else
                                {
                                    _TestTempPlayerWithRetainers.GetOrAdd(retainer.Value.OwnerLocalContentId, _ => (_GetPlayerValues.Player, _GetPlayerValues.Retainers, 0));
                                }
                            }

                        }
                    }

                    LastTargetName = targetName;
                }
            }
            return _TestTempPlayerWithRetainers;
        }
    }
}
