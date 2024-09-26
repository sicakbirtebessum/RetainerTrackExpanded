using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Microsoft.Extensions.Logging;
using RetainerTrackExpanded.API.Models;

namespace RetainerTrackExpanded.Handlers;

internal sealed class ObjectTableHandler : IDisposable
{
    private readonly IObjectTable _objectTable;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly ILogger<ObjectTableHandler> _logger;
    private readonly PersistenceContext _persistenceContext;

    private long _lastUpdate;

    public ObjectTableHandler(IObjectTable objectTable, IFramework framework, IClientState clientState, ILogger<ObjectTableHandler> logger, PersistenceContext persistenceContext)
    {
        _objectTable = objectTable;
        _framework = framework;
        _clientState = clientState;
        _logger = logger;
        _persistenceContext = persistenceContext;

        _framework.Update += FrameworkUpdate;
    }

    private unsafe void FrameworkUpdate(IFramework framework)
    {
        long now = Environment.TickCount64;
        if (!_clientState.IsLoggedIn || now - _lastUpdate < 20_000) //_clientState.IsPvPExcludingDen
            return;

        _lastUpdate = now;

        List<PlayerMapping> playerMappings = new();
        foreach (var obj in _objectTable)
        {
            if (obj.ObjectKind == ObjectKind.Player)
            {
                var bc = (BattleChara*)obj.Address;
                if (bc->ContentId == 0 || bc->AccountId == 0)
                    continue;

                playerMappings.Add(new PlayerMapping
                {
                    ContentId = bc->ContentId,
                    AccountId = bc->AccountId,
                    PlayerName = bc->NameString,
                    WorldId = bc->HomeWorld,
                });

                PersistenceContext.AddPlayerUploadData(bc->ContentId, new PostPlayerRequest
                {
                    LocalContentId = bc->ContentId,
                    Name = bc->NameString,
                    AccountId = (int?)bc->AccountId,
                    WorldId = bc->HomeWorld,
                    TerritoryId = (short)PersistenceContext._clientState.TerritoryType,
                    CreatedAt = Tools.UnixTime,
                });
            }
        }

        if (playerMappings.Count > 0)
            Task.Run(() => _persistenceContext.HandleContentIdMapping(playerMappings));

        _logger.LogTrace("ObjectTable handling for {Count} players took {TimeMs}", playerMappings.Count, TimeSpan.FromMilliseconds(Environment.TickCount64 - now));
    }

    public void Dispose()
    {
        _framework.Update -= FrameworkUpdate;
    }
}