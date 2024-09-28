using Dalamud.Configuration;
using Newtonsoft.Json;
using System;
using Dalamud.Interface;
using Dalamud.Plugin;
using RetainerTrackExpanded.GUI;
using System.ComponentModel.DataAnnotations.Schema;
using RetainerTrackExpanded.API.Models;
using System.Collections.Generic;
using System.Collections.Concurrent;
using static RetainerTrackExpanded.Handlers.PersistenceContext;

namespace RetainerTrackExpanded
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;
        public string BaseUrl { get; set; } = "https://localhost:5001/api/v1/";
        public string Username { get; set; } = string.Empty;
        public long ContentId { get; set; }
        public int AccountId { get; set; }
        public string Key { get; set; } = string.Empty;
        public bool LoggedIn { get; set; }
        public bool FreshInstall { get; set; } = true;
        public int AppRoleId { get; set; }
        public int? UploadedPlayersCount { get; set; }
        public int? UploadedPlayerInfoCount { get; set; }
        public int? UploadedRetainersCount { get; set; }
        public int? UploadedRetainerInfoCount { get; set; }
        public int? FetchedPlayerInfoCount { get; set; }
        public int? SearchedNamesCount { get; set; }
        public int? LastSyncedTime { get; set; }
        public ConcurrentDictionary<long, CachedFavoritedPlayer> FavoritedPlayer = new();
        public bool bShowDetailedDate { get; set; } = false;

        public class CachedFavoritedPlayer
        {
            public required ulong? AccountId { get; init; }
            public required string Name { get; init; }
        }

        public string Token()
        {
            return $"{Key}-{AccountId}";
        }
        public void Save()
        {
            RetainerTrackExpandedPlugin.Instance._pluginInterface.SavePluginConfig(this);
        }
    }

}
