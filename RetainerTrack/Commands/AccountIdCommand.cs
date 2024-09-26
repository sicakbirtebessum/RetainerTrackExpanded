using System;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using RetainerTrackExpanded.Handlers;

namespace RetainerTrackExpanded.Commands
{
    internal sealed class AccountIdCommand : IDisposable
    {
        private readonly ICommandManager _commandManager;
        private readonly IClientState _clientState;
        private readonly ITargetManager _targetManager;
        private readonly IChatGui _chatGui;
        private readonly PersistenceContext _persistenceContext;

        public AccountIdCommand(ICommandManager commandManager, IClientState clientState, ITargetManager targetManager,
            IChatGui chatGui, PersistenceContext persistenceContext)
        {
            _commandManager = commandManager;
            _clientState = clientState;
            _targetManager = targetManager;
            _chatGui = chatGui;
            _persistenceContext = persistenceContext;

            _commandManager.AddHandler("/accountid", new CommandInfo(ProcessCommand)
            {
                HelpMessage = "Shows the accountid of your target (or if no target, yourself)"
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
                _chatGui.Print($"{character.Name} has Account Id: {bc->AccountId}, Content Id: {bc->ContentId}");

                _persistenceContext.HandleContentIdMappingAsync([
                    new PlayerMapping
                    {
                        ContentId = bc->ContentId,
                        AccountId = bc->AccountId,
                        PlayerName = bc->NameString,
                    }
                ]);
            }
        }

        public void Dispose()
        {
            _commandManager.RemoveHandler("/accountid");
        }
    }
}