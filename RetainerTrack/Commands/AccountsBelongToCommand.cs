using System;
using System.Xml.Linq;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using RetainerTrackExpanded.GUI;
using RetainerTrackExpanded.Handlers;

namespace RetainerTrackExpanded.Commands
{
    internal sealed class AccountBelongToCommand : IDisposable
    {
        private readonly ICommandManager _commandManager;
        private readonly IClientState _clientState;
        private readonly ITargetManager _targetManager;
        private readonly IChatGui _chatGui;
        private readonly PersistenceContext _persistenceContext;

        public AccountBelongToCommand(ICommandManager commandManager, IClientState clientState, ITargetManager targetManager,
            IChatGui chatGui, PersistenceContext persistenceContext)
        {
            _commandManager = commandManager;
            _clientState = clientState;
            _targetManager = targetManager;
            _chatGui = chatGui;
            _persistenceContext = persistenceContext;

            _commandManager.AddHandler("/accounts", new CommandInfo(ProcessCommand)
            {
                HelpMessage = "Shows the accounts belong to same person (or if no target, yourself)"
            });
        }

        private void ProcessCommand(string command, string arguments)
        {
            IGameObject? character = _targetManager.Target ?? _clientState.LocalPlayer;
            if (character == null || character.ObjectKind != ObjectKind.Player)
                return;

            unsafe
            {
                var bc = (BattleChara*)character.Address;

                var accounts = _persistenceContext.GetAllAccountNamesForCharacter(bc->ContentId);
                if (accounts.Count > 1)
                {
                    _chatGui.Print($"* Account names for {character.Name}:");
                    foreach (var accountName in accounts)
                        _chatGui.Print($" - {accountName}");
                }
                else
                {
                    _chatGui.Print($"* {character.Name} has only one account.");
                }
            }
        }

        public void Dispose()
        {
            _commandManager.RemoveHandler("/accounts");
        }
    }
}