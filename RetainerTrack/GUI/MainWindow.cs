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
                MinimumSize = new Vector2(740, 530),
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
        "CurrentName","TotalAccs","TotalRt","Retainers","cId","AccId"
        };

        private readonly string[] FromServerTableSearchPlayersColumn = new string[]
        {
        "CurrentName","cId","AccId"
        };

        private readonly string[] FromServerTableSearchRetainersColumn = new string[]
        {
        "Name","World","Added at","OwnerContentId","cId"
        };

        private readonly string[] ServersTableColumn = new string[]
        {
        "Name","Id","Total Retainers"
        };
        public enum Tabs
        {
            General,
            AllPlayersAndRetainers,
            AllPlayersAndRetainersFromServer,
            Statistics,
            Settings
        }
        private string _searchContent = "SearchQuery";

        public int _UsedNamePluginTotalPlayers_Value = 0;
        public static int _TotalPlayers_Value = 0;
        public static int _TotalRetainers_Value = 0;

        public long FirstUnix = 0;
        public long LastUnix = 0;

        public int TablePlayerMaxLimit = 50;

        public Tabs _CurrentTab = Tabs.General;

        public static void UpdateStatisticsValues()
        {
            _TotalPlayers_Value = PersistenceContext._playerCache.Count;
            _TotalRetainers_Value = PersistenceContext._retainerCache.Count;
        }

        bool _PlayerSearchTextVisible = false;
        int _Devskipplayers = 0;
        int _Devskipretainers = 0;
        List<PostPlayerRequest> _devWillBeUploaded = new List<PostPlayerRequest>();
        public override void Draw()
        {
            if (ImGui.BeginTabBar("Tabs"))
            {
                if (ImGui.BeginTabItem("General"))
                {
                    _CurrentTab = Tabs.General;
                    DrawGeneralTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Search Players & Retainers"))
                {
                    _CurrentTab = Tabs.AllPlayersAndRetainers;
                    DrawSearchPlayersAndRetainersTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Search Players & Retainers (Server)"))
                {
                    _CurrentTab = Tabs.AllPlayersAndRetainersFromServer;
                    DrawSearchPlayersAndRetainers_FromServerTabAsync();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Statistics"))
                {
                    _CurrentTab = Tabs.Statistics;
                    DrawStatisticsTab();
                    ImGui.EndTabItem();
                }
                int time = 1727265602;
                if (ImGui.BeginTabItem("Debug"))
                {
                    _CurrentTab = Tabs.Settings;
                    //DrawSettingsTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        private void DrawGeneralTab()
        {
            if (!Config.IsLoggedIn || string.IsNullOrWhiteSpace(Config.Key))
            {
                Util.ShowColoredMessage("Error: You are not connected.");
                if (ImGui.Button("Open Configuration Menu to connect to Server"))
                {
                    ConfigWindow.Instance.IsOpen = true;
                }
            }
            else
            {
                Util.ShowColoredMessage("You are connected.");
                ImGui.SameLine();
                if (ImGui.Button("Open Configuration Menu"))
                {
                    ConfigWindow.Instance.IsOpen = true;
                }
            }
        }

        private int DataSourceSelection = 0;
        private int selectedComboItem = 0;

        private int selectedComboItem_PlayerOrRetainer = 0;
        private int selectedComboItem_NameorId = 0;

        private string[] selectedComboItems1 = ["Search Player", "Search Retainer"];
        private string[] selectedComboItems2 = ["By Name", "By Id"];

        public Configuration Config = RetainerTrackExpandedPlugin.Instance.Configuration;

        ApiClient _client = ApiClient.Instance;
        private (Dictionary<long, PlayerDto> Players, string Message) _LastPlayerSearchResult = new();
        private (Dictionary<long, API.Models.RetainerDto> Retainers, string Message) _LastRetainerSearchResult = new();
        private bool bIsNetworkProcessing = false;

        private void SetPlayerResult((Dictionary<long, PlayerDto> Players, string Message) PlayerResult)
        {
            _LastRetainerSearchResult = (_LastRetainerSearchResult.Retainers, string.Empty);
            _LastPlayerSearchResult = (PlayerResult.Players, PlayerResult.Message);
        }

        private void SetRetainerResult((Dictionary<long, API.Models.RetainerDto> Retainers, string Message) RetainerResult)
        {
            _LastPlayerSearchResult = (_LastPlayerSearchResult.Players, string.Empty);
            _LastRetainerSearchResult = (RetainerResult.Retainers, RetainerResult.Message);
        }

        public async System.Threading.Tasks.Task DrawSearchPlayersAndRetainers_FromServerTabAsync()
        {
            ImGui.SetNextItemWidth(135);
            if (ImGui.Combo("##serverDB1", ref selectedComboItem_PlayerOrRetainer, selectedComboItems1, 2))
            {
                _searchContent = "";
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);

            if (selectedComboItem_PlayerOrRetainer == 0)
                selectedComboItems2 = ["By Name", "By Id"];
            else
            {
                selectedComboItems2 = ["By Name"];
                selectedComboItem_NameorId = 0;
            }

            using (ImRaii.Disabled(selectedComboItem_PlayerOrRetainer == 1))
            {
                if (ImGui.Combo("##serverDB2", ref selectedComboItem_NameorId, selectedComboItems2, 2))
                {
                    _searchContent = "";
                }
            }

            string SearchByPlayerOrRetainer = selectedComboItem_PlayerOrRetainer == 0 ? "Player" : "Retainer";
            string SearchByNameorId = selectedComboItem_NameorId == 0 ? "Name" : "Id";
            ImGui.SameLine();
            ImGui.Text(" -> ");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            ImGui.InputTextWithHint("##searchC", $"Enter {SearchByPlayerOrRetainer}'s {SearchByNameorId} here", ref _searchContent, 32);

            ImGui.SetNextItemWidth(140);
            ImGui.SameLine();

            string SearchButtonText = (bIsNetworkProcessing ? "Searching..." : "Search");

            using (ImRaii.Disabled(bIsNetworkProcessing))
            {
                if (ImGui.Button(SearchButtonText))
                {
                    if (selectedComboItem_PlayerOrRetainer == 0) //Search Player
                    {
                        if (selectedComboItem_NameorId == 0)
                        {
                            bool regex = Regex.IsMatch(_searchContent, @"^[A-Za-z]+([ '-][A-Za-z]+)?$");
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
            }

            ImGui.SameLine();

            if (!string.IsNullOrWhiteSpace(_LastPlayerSearchResult.Message))
            {
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (_LastPlayerSearchResult.Message.StartsWith("Error:"))
                    textColor = ImGuiColors.DalamudRed;

                ImGui.SetNextItemWidth(100);
                ImGui.TextColored(textColor, $"{_LastPlayerSearchResult.Message}");
            }

            if (!string.IsNullOrWhiteSpace(_LastRetainerSearchResult.Message))
            {
                Vector4 textColor = ImGuiColors.HealerGreen;
                if (_LastRetainerSearchResult.Message.StartsWith("Error:"))
                    textColor = ImGuiColors.DalamudRed;

                ImGui.SetNextItemWidth(100);
                ImGui.TextColored(textColor, $"{_LastRetainerSearchResult.Message}");
            }

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

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
                                ImGui.SetClipboardText(player.AccountId.ToString());
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
                                ImGui.SetClipboardText(((EuropeServerIds)retainer.WorldId).ToString());
                            }
                            ImGui.SameLine();
                        }
                        ImGui.Text(((EuropeServerIds)retainer.WorldId).ToString());

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

        public void DrawSearchPlayersAndRetainersTab()
        {
            ImGui.Text("Search:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            ImGui.InputTextWithHint("##searchContent", "Enter player's name here", ref _searchContent, 250);

            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            ImGui.Combo("##existingDB", ref selectedComboItem, ["All","Players","Retainers"], 3);

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

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

                    if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl))
                    {
                        if (ImGui.Button("c" + $"##{index}"))
                        {
                            ImGui.SetClipboardText(player.Item1.ToString());
                        }
                        ImGui.SameLine();
                    }
                    ImGui.Text(player.Item1.Name);

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

        private void DrawStatisticsTab()
        {
            //------Update GUI-------//
            //-----------------------//
            if (FirstUnix == 0)
            {
                FirstUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                LastUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            LastUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (LastUnix - FirstUnix >= 3)
            {
                UpdateStatisticsValues();
                GetServerRetainersCount();

                FirstUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            //-----------------------//

            ImGui.Text($"Total Players: {_TotalPlayers_Value}  -  ");
            ImGui.SameLine();
            ImGui.Text($"Total Retainers: {_TotalRetainers_Value}");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(200);
            ImGui.Text($"");

            ImGui.BeginChild("Ch");

            if (ImGui.BeginTable($"ServerList##{_searchContent}", ServersTableColumn.Length, ImGuiTableFlags.BordersInner))
            {
                foreach (var t in ServersTableColumn)
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

                foreach (var server in _TempGetServerRetainersCount)
                {
                    if (index > TablePlayerMaxLimit)
                        break;

                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();

                    if (ChaosServerIdList.Contains(server.Key)) //This is a Chaos server
                    {
                        ImGui.Text(((ChaosServerIds)server.Key).ToString()); // Servername column

                        ImGui.TableNextColumn();

                        ImGui.Text(server.Key.ToString()); //ServerId column

                        ImGui.TableNextColumn();

                        ImGui.Text(server.Value.Count().ToString());

                        ImGui.TableNextColumn();
                    }

                    if (LightServerIdList.Contains(server.Key)) //This is a Light server
                    {
                        ImGui.Text(((LightServerIds)server.Key).ToString()); // Servername column

                        ImGui.TableNextColumn();

                        ImGui.Text(server.Key.ToString()); //ServerId column

                        ImGui.TableNextColumn();

                        ImGui.Text(server.Value.Count().ToString());

                        ImGui.TableNextColumn();
                    }
                    index++;
                }
                ImGui.TableNextRow();

                ImGui.EndTable();
            }

            ImGui.EndChild();
        }
        public enum EuropeServerIds
        {
            Cerberus = 80,
            Louisoix = 83,
            Moogle = 71,
            Omega = 39,
            Phantom = 401,
            Ragnarok = 97,
            Sagittarius = 400,
            Spriggan = 85,
            Alpha = 402,
            Lich = 36,
            Odin = 66,
            Phoenix = 56,
            Raiden = 403,
            Shiva = 67,
            Twintania = 33,
            Zodiark = 42
        }

        public enum ChaosServerIds
        {
            Cerberus = 80,
            Louisoix = 83,
            Moogle = 71,
            Omega = 39,
            Phantom = 401,
            Ragnarok = 97,
            Sagittarius = 400,
            Spriggan = 85
        }
        public enum LightServerIds
        {
            Alpha = 402,
            Lich = 36,
            Odin = 66,
            Phoenix = 56,
            Raiden = 403,
            Shiva = 67,
            Twintania = 33,
            Zodiark = 42
        }
        public static ConcurrentDictionary<ushort, List<ulong>> _TempGetServerRetainersCount = new ConcurrentDictionary<ushort, List<ulong>>();
        public static List<ushort> ChaosServerIdList = new List<ushort>();
        public static List<ushort> LightServerIdList = new List<ushort>();
        public static int LastTotalRetainerCount = 0;
        private List<ulong> ServerTotalRetainerCount = new List<ulong>();

        public static ConcurrentDictionary<ushort, List<ulong>> GetServerRetainersCount()
        {
            if (ChaosServerIdList.Count == 0)
            {
                foreach (int i in Enum.GetValues(typeof(ChaosServerIds)))
                {
                    if (!ChaosServerIdList.Contains((ushort)i))
                    {
                        ChaosServerIdList.Add((ushort)i);
                    }

                }
            }
            if (LightServerIdList.Count == 0)
            {
                foreach (int i in Enum.GetValues(typeof(LightServerIds)))
                {
                    if (!LightServerIdList.Contains((ushort)i))
                    {
                        LightServerIdList.Add((ushort)i);
                    }
                }
            }

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

            if (_CurrentTab == Tabs.General)
            {

            }
            else if (_CurrentTab == Tabs.AllPlayersAndRetainers)
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

                if (selectedComboItem == 1)
                    return _TestTempPlayerWithRetainers;

                List<ulong> AddedOwners = new List<ulong>();
                foreach(var retainer in _retainerCache)
                {
                    if (!AddedOwners.Contains(retainer.Value.OwnerLocalContentId))
                    {
                        string retainerName = retainer.Value.Name.ToLower();
                        if (retainerName.Contains(targetName))
                        {
                            PersistenceContext._playerWithRetainersCache.TryGetValue(retainer.Value.OwnerLocalContentId, out var _GetPlayerValues);

                            AddedOwners.Add(retainer.Value.OwnerLocalContentId);
                            if (_GetPlayerValues.Player.AccountId != null && !_AccountIdCache.IsEmpty)
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
            }
            return _TestTempPlayerWithRetainers;
        }


    }
}
