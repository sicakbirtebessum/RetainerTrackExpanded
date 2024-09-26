using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;
using RetainerTrackExpanded.Handlers;

namespace RetainerTrackExpanded.Commands;

internal sealed class WhoCommand : IDisposable
{
    private readonly PersistenceContext _persistenceContext;
    private readonly ICommandManager _commandManager;
    private readonly IChatGui _chatGui;
    private readonly IClientState _clientState;
    private readonly Dictionary<string, uint> _worlds;

    public WhoCommand(PersistenceContext persistenceContext, ICommandManager commandManager, IChatGui chatGui,
        IClientState clientState, IDataManager dataManager)
    {
        _persistenceContext = persistenceContext;
        _commandManager = commandManager;
        _chatGui = chatGui;
        _clientState = clientState;
        _worlds = dataManager.GetExcelSheet<World>()!.Where(x => x.IsPublic)
            .ToDictionary(x => x.Name.ToString().ToUpperInvariant(), x => x.RowId);
        _commandManager.AddHandler("/rwho", new CommandInfo(ProcessCommand)
        {
            HelpMessage =
                "/rwho Character Name@World → Shows all retainers for the character (will use your current world if no world is specified)"
        });
    }

    private void ProcessCommand(string command, string arguments)
    {
        string[] nameParts = arguments.Split(' ');
        if (nameParts.Length != 2)
        {
            _chatGui.Print($"USAGE: /{command} Character Name@World");
        }
        else if (nameParts[1].Contains('@', StringComparison.Ordinal))
        {
            string[] lastNameParts = nameParts[1].Split('@');
            if (_worlds.TryGetValue(lastNameParts[1].ToUpperInvariant(), out uint worldId))
                ProcessLookup($"{nameParts[0]} {lastNameParts[0]}", worldId);
            else
                _chatGui.PrintError($"Unknown world: {lastNameParts[1]}");
        }
        else
            ProcessLookup(arguments, _clientState?.LocalPlayer?.CurrentWorld?.Id ?? 0);
    }

    private void ProcessLookup(string name, uint world)
    {
        if (world == 0)
            return;

        _chatGui.Print($"Retainer names for {name}: ");
        var retainers = _persistenceContext.GetRetainerNamesForCharacter(name, world);
        foreach (var retainerName in retainers)
            _chatGui.Print($" - {retainerName}");
        if (retainers.Count == 0)
            _chatGui.Print("  (No retainers found)");
    }

    public void Dispose()
    {
        _commandManager.RemoveHandler("/rwho");
    }
}
