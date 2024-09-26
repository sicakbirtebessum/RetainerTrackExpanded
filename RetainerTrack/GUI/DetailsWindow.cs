using Dalamud.Interface.Utility.Table;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Linq;
using System.Collections.Concurrent;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using Lumina;
using System.Threading.Tasks;
using static Dalamud.Interface.Windowing.Window;
using RetainerTrackExpanded.Handlers;
using static RetainerTrackExpanded.Handlers.PersistenceContext;
using static RetainerTrackExpanded.GUI.MainWindow;
using RetainerTrackExpanded.Database;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using System.Security.Principal;
using static FFXIVClientStructs.ThisAssembly.Git;
using RetainerTrackExpanded.API.Models;
using RetainerTrackExpanded.API;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkUIColorHolder.Delegates;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using static System.Net.Mime.MediaTypeNames;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Utility;
namespace RetainerTrackExpanded.GUI
{
    public class DetailsWindow : Window, IDisposable
    {
        public DetailsWindow() : base("Retainer Track Details", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        {
            if (_instance == null)
            {
                _instance = this;
            }
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(600, 550),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }
        public Configuration Config = RetainerTrackExpandedPlugin.Instance.Configuration;
        private static DetailsWindow _instance = null;
        public static DetailsWindow Instance
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

        public bool AccountsFetched = false;
        public ulong SelectedPlayerContentId = 0;
        private string _searchContent = "";

        

        private readonly string[] PlayerTableColumn = new string[]
        {
        "Player Name","AccId","cId"
        };

        private readonly string[] RetainerTableColumn = new string[]
        {
        "Retainer Name","cId","Server Id","Owner Name"
        };

        private readonly string[] DetailedPlayerCId_AccIdTableColumn = new string[]
        {
        "Content Id","Account Id"
        };

        private readonly string[] DetailedPlayerLastSeenZoneTableColumn = new string[]
        {
        "Zone Name","Region", "Added at"
        };

        private readonly string[] DetailedPlayerNamesTableColumn = new string[]
        {
        "Player Name","Added at"
        };

        private readonly string[] DetailedPlayerWorldsTableColumn = new string[]
        {
        "World","Added at"
        };

        private readonly string[] DetailedPlayerRetainerTableColumn = new string[]
       {
        "Retainer Name","World","Last Seen","Added at","Owner Name","cId"
       };

        private readonly string[] AltCharPlayerTableColumn = new string[]
        {
        "Player Name","World","cId"
        };

        ApiClient _client = ApiClient.Instance;
        public void OpenDetailedPlayerWindow(ulong PlayerContentId, bool GetInfoFromServer)
        {
            _TestTempPlayerWithRetainers.Clear();

            _LastMessage = string.Empty;
            _seeExtraDetailsOfRetainer = null;
            //_LastPlayerDetailedInfo = new();

            if (GetInfoFromServer)
            {
                IsDataFromServer = true;

                _ = System.Threading.Tasks.Task.Run(() =>
                {
                    var request = _client.GetPlayerById((long)PlayerContentId).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (request.Player == null)
                        _LastMessage = request.Message;
                    else
                    {
                        _LastPlayerDetailedInfo = (request.Player, request.Message);
                        _LastMessage = string.Empty;
                    }
                });
            }
            else
            {
                IsDataFromServer = false;
                AccountsFetched = false;
                //_TestTempPlayerWithRetainers.Clear();

                SelectedPlayerContentId = PlayerContentId;
            }
        }

        bool IsDataFromServer = false;

        PlayerDetailed.RetainerDto _seeExtraDetailsOfRetainer = null;
        string _LastMessage = string.Empty;
        static ConcurrentDictionary<ulong, (CachedPlayer, List<Database.Retainer>)> _TestTempPlayerWithRetainers = new();
        static (PlayerDetailed Player, string Message) _LastPlayerDetailedInfo = new();

        public override void Draw()
        {
            if (IsDataFromServer)
            {
                if (_LastPlayerDetailedInfo.Player != null)
                {
                    var bFavoritesContainPlayer = Config.FavoritedPlayer.ContainsKey(_LastPlayerDetailedInfo.Player.LocalContentId);
                    var AddToFavoriteText = bFavoritesContainPlayer ? "Remove from Favorites" : "Add to Favorites";
                    FontAwesomeIcon _ButtonIcon = bFavoritesContainPlayer ? FontAwesomeIcon.UserMinus : FontAwesomeIcon.UserPlus;

                    if (ImGuiComponents.IconButtonWithText(_ButtonIcon, AddToFavoriteText))
                    {
                        var player = _LastPlayerDetailedInfo.Player; var playerName = _LastPlayerDetailedInfo.Player.PlayerNameHistories.Count > 0 ? _LastPlayerDetailedInfo.Player.PlayerNameHistories.LastOrDefault().Name : "(NO-NAME)";
                        ulong? accountId = player.AccountId != null ? (ulong)player.AccountId : null;
                        if (bFavoritesContainPlayer)
                            Config.FavoritedPlayer.Remove(player.LocalContentId, out _);
                        else
                            Config.FavoritedPlayer.GetOrAdd(player.LocalContentId, new Configuration.CachedFavoritedPlayer { AccountId = accountId, Name = playerName });
                        Config.Save();
                    }
                }

                if (!string.IsNullOrWhiteSpace(_LastMessage))
                {
                    ImGui.TextColored(ImGuiColors.DalamudRed, _LastMessage); //Error message
                }
                else if (_LastPlayerDetailedInfo.Player == null)
                {
                    if (!string.IsNullOrWhiteSpace(_LastPlayerDetailedInfo.Message))
                        ImGui.TextColored(ImGuiColors.DalamudRed, _LastPlayerDetailedInfo.Message);
                    else
                        ImGui.TextColored(ImGuiColors.DalamudWhite, $"Loading...");
                }
                if (_LastPlayerDetailedInfo.Player != null)
                    DrawPlayerDetailsFromServer();
            }
            else
            {
                DrawPlayerDetailsFromLocal();
            }
        }

        private void DrawPlayerDetailsFromLocal()
        {
            if (Config.IsLoggedIn && !string.IsNullOrWhiteSpace(Config.Key))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Server, "Fetch Detailed Player Info From Server"))
                {
                    OpenDetailedPlayerWindow(SelectedPlayerContentId, true);
                }
            }

            ImGui.BeginGroup();

            ImGui.Text("Characters of the player:");
            if (!AccountsFetched)
            {
                GetAllRetainersofAllAccounts(SelectedPlayerContentId);
                AccountsFetched = true;
            }

            if (ImGui.BeginTable($"SocialList2##a{_searchContent}", PlayerTableColumn.Length, ImGuiTableFlags.BordersInner))
            {
                foreach (var t in PlayerTableColumn)
                {
                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                }
                ImGui.TableHeadersRow();
                var index = 0;

                foreach (var account in _TestTempPlayerWithRetainers)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    ImGui.Text(account.Value.Item1.Name); //PlayerName

                    ImGui.TableNextColumn();

                    ImGui.Text(account.Value.Item1.AccountId.ToString()); //AccId column

                    ImGui.TableNextColumn();

                    ImGui.Text(account.Key.ToString()); //cId column
                }

                index++;
            }
            ImGui.EndTable();
            ImGui.EndGroup();

            ImGui.BeginGroup();
            ImGui.Text("\n");
            ImGui.Text("All retainers of the player:");

            if (_TestTempPlayerWithRetainers != null && !_TestTempPlayerWithRetainers.IsEmpty)
            {
                if (ImGui.BeginTable($"SocialList##{_searchContent}", RetainerTableColumn.Length, ImGuiTableFlags.BordersInner))
                {
                    foreach (var t in RetainerTableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();
                    var index = 0;

                    foreach (var Item in _TestTempPlayerWithRetainers)
                    {
                        foreach (var retainer in Item.Value.Item2)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();

                            ImGui.Text(retainer.Name); // RetainerName column

                            ImGui.TableNextColumn();

                            ImGui.Text(retainer.LocalContentId.ToString()); //cId column

                            ImGui.TableNextColumn();

                            if (MainWindow.ChaosServerIdList.Contains(retainer.WorldId)) //This is a Chaos server
                            {
                                ImGui.Text($"{((ChaosServerIds)retainer.WorldId)} ({retainer.WorldId})"); // Servername column
                            }
                            else if (MainWindow.LightServerIdList.Contains(retainer.WorldId)) //This is a Light server
                            {
                                ImGui.Text($"{((LightServerIds)retainer.WorldId)} ({retainer.WorldId})"); // Servername column
                            }

                            ImGui.TableNextColumn();

                            ImGui.Text(Item.Value.Item1.Name); //OwnerName column
                        }
                    }

                    index++;
                }
                ImGui.EndTable();
            }
            ImGui.EndGroup();
        }
        private void DrawPlayerDetailsFromServer()
        {
            var player = _LastPlayerDetailedInfo.Player;

            ImGui.BeginGroup();
            ImGui.Text("Showing results for:"); ImGui.SameLine();

            if (ImGui.BeginTable($"_Char", DetailedPlayerCId_AccIdTableColumn.Length, ImGuiTableFlags.BordersInner))
            {
                foreach (var t in DetailedPlayerCId_AccIdTableColumn)
                {
                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                }
                ImGui.TableHeadersRow();

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.Text(player.LocalContentId.ToString()); //CId column

                ImGui.TableNextColumn();

                ImGui.Text(player.AccountId.ToString()); //AccId column
            }
            ImGui.EndTable();

            if (player.Territory != null)
            {
                var getTerritory = PersistenceContext.Instance._territories.Where(a => a.RowId == player.Territory.TerritoryId).FirstOrDefault();
                ImGui.Text("Player last seen in:"); ImGui.SameLine();
                if (ImGui.BeginTable($"_Lastseen", DetailedPlayerLastSeenZoneTableColumn.Length, ImGuiTableFlags.BordersInner))
                {
                    foreach (var t in DetailedPlayerLastSeenZoneTableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    ImGui.Text(getTerritory.PlaceName.Value.Name.ToString()); //Zone Name column

                    ImGui.TableNextColumn();

                    ImGui.Text(getTerritory.PlaceNameRegion.Value.Name.ToString()); //Region Name column

                    ImGui.TableNextColumn();

                    var addedAtString = $"{Tools.UnixTimeConverter(player.Territory.CreatedAt)} ({Tools.ToTimeSinceString(player.Territory.CreatedAt)})";
                    ImGui.Text(addedAtString); //Added At column
                }
                ImGui.EndTable();
            }
            ImGui.EndGroup();

            if (_LastPlayerDetailedInfo.Player.PlayerNameHistories.Count > 0)
            {
                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.BeginGroup();

                ImGui.Text("Name History:"); ImGui.SameLine();
                
                if (ImGui.BeginTable($"_Names", DetailedPlayerNamesTableColumn.Length, ImGuiTableFlags.BordersInner))
                {
                    foreach (var t in DetailedPlayerNamesTableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();
                    var index = 0;

                    foreach (var name in _LastPlayerDetailedInfo.Player.PlayerNameHistories)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        ImGui.Text(name.Name); //PlayerName

                        ImGui.TableNextColumn();

                        var addedAtString = $"{Tools.UnixTimeConverter(name.CreatedAt)} ({Tools.ToTimeSinceString(name.CreatedAt)})";
                        ImGui.Text(addedAtString); //Added At column
                    }

                    index++;
                }
                ImGui.EndTable();
                ImGui.EndGroup();
            }

            if (_LastPlayerDetailedInfo.Player.PlayerWorldHistories.Count > 0)
            {
                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.BeginGroup();

                ImGui.Text("World History:"); ImGui.SameLine();

                if (ImGui.BeginTable($"_Worlds", DetailedPlayerWorldsTableColumn.Length, ImGuiTableFlags.BordersInner))
                {
                    foreach (var t in DetailedPlayerWorldsTableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();
                    var index = 0;

                    foreach (var world in _LastPlayerDetailedInfo.Player.PlayerWorldHistories)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        ImGui.Text(((EuropeServerIds)world.WorldId).ToString()); //World Name

                        ImGui.TableNextColumn();

                        var addedAtString = $"{Tools.UnixTimeConverter(world.CreatedAt)} ({Tools.ToTimeSinceString(world.CreatedAt)})";
                        ImGui.Text(addedAtString); //Added At column
                    }

                    index++;
                }
                ImGui.EndTable();
                ImGui.EndGroup();
            }
            
            if (player.PlayerAltCharacters.Count > 0)
            {
                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.BeginGroup();

                ImGui.Text("Alt Characters:"); ImGui.SameLine();

                if (ImGui.BeginTable($"AltCharacters", AltCharPlayerTableColumn.Length, ImGuiTableFlags.BordersInner))
                {
                    foreach (var t in AltCharPlayerTableColumn)
                    {
                        ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                    }
                    ImGui.TableHeadersRow();
                    var index = 0;

                    foreach (var altChar in _LastPlayerDetailedInfo.Player.PlayerAltCharacters)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        if (ImGui.Button("Load Details ->" + $"##{index}"))
                        {
                            IsOpen = true;
                            OpenDetailedPlayerWindow((ulong)altChar.LocalContentId, true);
                        }
                        ImGui.SameLine();

                        if (!string.IsNullOrWhiteSpace(altChar.Name))
                            ImGui.Text(altChar.Name); //PlayerName
                        else
                            ImGui.Text("---");

                        ImGui.TableNextColumn();

                        if (!string.IsNullOrWhiteSpace(altChar.WorldId.ToString()))
                            ImGui.Text(((EuropeServerIds)altChar.WorldId).ToString()); //World
                        else
                            ImGui.Text("---");

                        ImGui.TableNextColumn();

                        ImGui.Text(altChar.LocalContentId.ToString()); //CId

                        index++;
                    }
                }
                ImGui.EndTable();
                ImGui.EndGroup();
            }


            var AllRetainers = new List<PlayerDetailed.RetainerDto>(player.Retainers);
            player.PlayerAltCharacters.ToList().ForEach(r =>
            {
                AllRetainers.AddRange(r.Retainers);
            });
            
            if (AllRetainers.Count > 0)
            {
                if (ImGui.CollapsingHeader($"Show Retainers ({AllRetainers.Count})"))
                {
                    ImGui.BeginGroup();

                    if (ImGui.BeginTable($"_Retainers", DetailedPlayerRetainerTableColumn.Length, ImGuiTableFlags.BordersInner))
                    {
                        foreach (var t in DetailedPlayerRetainerTableColumn)
                        {
                            ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                        }
                        ImGui.TableHeadersRow();
                        var index = 0;

                        foreach (var retainer in AllRetainers)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();

                            if (retainer.Names.Count > 1 || retainer.Worlds.Count > 1)
                            {
                                if (ImGui.Button("Info->" + $"##{index}"))
                                {
                                    _seeExtraDetailsOfRetainer = retainer;
                                }
                                ImGui.SameLine();
                            }

                            ImGui.Text(retainer.Names.LastOrDefault().Name); // Name

                            ImGui.TableNextColumn();

                            ImGui.Text(((EuropeServerIds)retainer.Worlds.LastOrDefault().WorldId).ToString()); // World

                            ImGui.TableNextColumn();

                            if (retainer.LastSeen != 0)
                            {
                                var LastSeenString = $"{Tools.UnixTimeConverter(retainer.LastSeen)} ({Tools.ToTimeSinceString(retainer.LastSeen)})";
                                ImGui.Text(LastSeenString); // LastSeen
                            }
                            else
                            {
                                var NamesTimeString = $"{Tools.UnixTimeConverter(retainer.Names.FirstOrDefault().CreatedAt)} ({Tools.ToTimeSinceString(retainer.Names.FirstOrDefault().CreatedAt)})";
                                ImGui.Text(NamesTimeString);
                            }
                                

                            ImGui.TableNextColumn();

                            var addedAtString = $"{Tools.UnixTimeConverter(retainer.Names.First().CreatedAt)} ({Tools.ToTimeSinceString(retainer.Names.First().CreatedAt)})";
                            ImGui.Text(addedAtString); // Added at

                            ImGui.TableNextColumn();

                            var _GetAltCharAsOwner = _LastPlayerDetailedInfo.Player.PlayerAltCharacters.FirstOrDefault(a => a.LocalContentId == retainer.OwnerLocalContentId);
                            string? RetainerOwnerName = _GetAltCharAsOwner != null ? _GetAltCharAsOwner.Name : _LastPlayerDetailedInfo.Player.PlayerNameHistories.LastOrDefault()?.Name;

                            if (!string.IsNullOrWhiteSpace(RetainerOwnerName)) { 
                                ImGui.Text(RetainerOwnerName); // OwnerName
                            }
                            else
                            {
                                ImGui.Text("---");
                            }

                            ImGui.TableNextColumn();

                            ImGui.Text(retainer.LocalContentId.ToString()); // CId

                            index++;
                        }
                    }
                    ImGui.EndTable();
                    ImGui.EndGroup();

                    if (_seeExtraDetailsOfRetainer != null)
                    {
                        if (_seeExtraDetailsOfRetainer.Names.Count > 0)
                        {
                            ImGuiHelpers.ScaledDummy(5.0f);
                            ImGui.Separator();
                            ImGuiHelpers.ScaledDummy(5.0f);

                            ImGui.BeginGroup();

                            ImGui.Text("Name History:"); ImGui.SameLine();

                            if (ImGui.BeginTable($"_RetainerNames", DetailedPlayerNamesTableColumn.Length, ImGuiTableFlags.BordersInner))
                            {
                                foreach (var t in DetailedPlayerNamesTableColumn)
                                {
                                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                                }
                                ImGui.TableHeadersRow();

                                foreach (var name in _seeExtraDetailsOfRetainer.Names)
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();

                                    ImGui.Text(name.Name); // Name

                                    ImGui.TableNextColumn();

                                    var addedAtString = $"{Tools.UnixTimeConverter(name.CreatedAt)} ({Tools.ToTimeSinceString(name.CreatedAt)})";
                                    ImGui.Text(addedAtString); //Created at column
                                }
                            }
                            ImGui.EndTable();
                            ImGui.EndGroup();
                        }

                        ImGui.SameLine();

                        ImGui.Text("\n");
                        ImGui.SameLine();
                        if (_seeExtraDetailsOfRetainer.Worlds.Count > 0)
                        {
                            ImGui.BeginGroup();

                            ImGui.Text("World History:"); ImGui.SameLine();

                            if (ImGui.BeginTable($"_RetainerWorlds", DetailedPlayerWorldsTableColumn.Length, ImGuiTableFlags.BordersInner))
                            {
                                foreach (var t in DetailedPlayerWorldsTableColumn)
                                {
                                    ImGui.TableSetupColumn(t, ImGuiTableColumnFlags.WidthFixed);
                                }
                                ImGui.TableHeadersRow();

                                foreach (var world in _seeExtraDetailsOfRetainer.Worlds)
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();

                                    ImGui.Text(((EuropeServerIds)world.WorldId).ToString()); //World Name

                                    ImGui.TableNextColumn();

                                    var addedAtString = $"{Tools.UnixTimeConverter(world.CreatedAt)} ({Tools.ToTimeSinceString(world.CreatedAt)})";
                                    ImGui.Text(addedAtString); //Created at column
                                }
                            }
                            ImGui.EndTable();
                            ImGui.EndGroup();
                        }
                    }
                }
                
            }
        }

        List<KeyValuePair<ulong, (CachedPlayer, List<Database.Retainer>)>> FetchedAccounts = new();

        public static void GetAllRetainersofAllAccounts(ulong PlayerContentId)
        {
            var GetRetainers = PersistenceContext._playerWithRetainersCache.Where(p => PlayerContentId == p.Key)
                .SelectMany(player => PersistenceContext._playerWithRetainersCache.Where(x => (x.Value.Player.AccountId == player.Value.Player.AccountId && x.Value.Player.AccountId != null) || 
                                                                                               x.Key == PlayerContentId))
           .ToList();


            foreach(var player in GetRetainers)
            {
                var _GetRetainers = PersistenceContext._playerWithRetainersCache.TryGetValue(player.Key, out var GetPlayer);
                if (_GetRetainers)
                {
                    _TestTempPlayerWithRetainers.GetOrAdd(player.Key, _ => (GetPlayer.Player, GetPlayer.Retainers));
                }
                else
                {
                    _TestTempPlayerWithRetainers.GetOrAdd(player.Key, _ => (GetPlayer.Player, new List<Database.Retainer>()));
                }
            }
        }

    }
}
