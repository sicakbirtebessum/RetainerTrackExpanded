/*
 * This file contains source code authored by Anna Clemens from (https://git.annaclemens.io/ascclemens/XivCommon/src/branch/main) which is distributed under MIT license
 * 
 * */
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Messenger.FriendListManager;

/// <summary>
/// An entry in a player's friend list.
/// </summary>
public unsafe struct FriendListEntry
{
    internal InfoProxyCommonList.CharacterData* Data;

    public FriendListEntry(InfoProxyCommonList.CharacterData* data) : this()
    {
        Data = data;
    }

    /// <summary>
    /// The content ID of the friend.
    /// </summary>
    public readonly ulong ContentId => (ulong)Data->ContentId;
    public readonly ushort OnlineStatus => *(ushort*)((nint)Data + 12);
    /// <summary>
    /// The current world of the friend.
    /// </summary>
    public readonly ushort CurrentWorld => Data->CurrentWorld;
    /// <summary>
    /// The home world of the friend.
    /// </summary>
    public readonly ushort HomeWorld => Data->HomeWorld;
    /// <summary>
    /// The job the friend is currently on.
    /// </summary>
    public readonly byte Job => Data->Job;
    /// <summary>
    /// The friend's raw SeString name. See <see cref="Name"/>.
    /// </summary>
    public SeString Name
    {
        get
        {
            return Data->NameString;
        }
    }
    /// <summary>
    /// The friend's free company tag.
    /// </summary>
    public SeString FreeCompany
    {
        get
        {
            return Data->FCTagString;
        }
    }

    public bool IsOnline
    {
        get
        {
            return OnlineStatus != 0;
        }
    }
}
