﻿using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using CodeHatch.Engine.Networking.Events;
using CodeHatch.Networking.Events.Players;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using uLink;

namespace Oxide.Game.ReignOfKings
{
    /// <summary>
    /// Game hooks and wrappers for the core Reign of Kings plugin
    /// </summary>
    public partial class ReignOfKingsCore
    {
        #region Player Hooks

        /// <summary>
        /// Called when the player is attempting to connect
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        [HookMethod("IOnUserApprove")]
        private object IOnUserApprove(ConnectionRequestEvent evt)
        {
            // Ignore the server
            if (evt.Connection != null && evt.Connection.IsServer)
            {
                return null;
            }

            string playerId = evt.Request.LoginData.PlayerId.ToString();
            string playerName = evt.Request.LoginData.PlayerName;
            string playerIp = evt.Request.IpAddress;

            // Let covalence know player is joining
            Covalence.PlayerManager.PlayerJoin(evt.Request.LoginData.PlayerId, playerName); // TODO: Handle this automatically

            // Call out and see if we should reject
            object loginSpecific = Interface.Call("CanClientLogin", evt);
            object loginCovalence = Interface.Call("CanUserLogin", playerName, playerId, playerIp);
            object canLogin = loginSpecific is null ? loginCovalence : loginSpecific;
            if (canLogin is string || canLogin is bool loginBlocked && !loginBlocked)
            {
                // Reject the player with the message
                return NetworkConnectionError.ApprovalDenied;
            }

            // Call the approval hooks
            object approvedSpecific = Interface.Call("OnUserApprove", evt);
            object approvedCovalence = Interface.Call("OnUserApproved", playerName, playerId, playerIp);
            return approvedSpecific is null ? approvedCovalence : approvedSpecific;
        }

        /// <summary>
        /// Called when the player sends a message
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerChat")]
        private object IOnPlayerChat(PlayerMessageEvent evt)
        {
            // Ignore the server player
            if (evt.PlayerId == 9999999999)
            {
                return null;
            }

            // Call hooks for plugins
            object chatSpecific = Interface.Call("OnPlayerChat", evt);
            object chatCovalence = Interface.Call("OnUserChat", evt.Player.IPlayer, evt.Message);
            object canChat = chatSpecific is null ? chatCovalence : chatSpecific;
            if (canChat != null)
            {
                // Cancel chat message event
                evt.Cancel();
                return true;
            }

            return null;
        }

        /// <summary>
        /// Called when the player has connected
        /// </summary>
        /// <param name="rokPlayer"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerConnected")]
        private void IOnPlayerConnected(Player rokPlayer)
        {
            // Ignore the server player
            if (rokPlayer.Id == 9999999999)
            {
                return;
            }

            string playerId = rokPlayer.Id.ToString();

            // Update name and groups with permissions
            if (permission.IsLoaded)
            {
                permission.UpdateNickname(playerId, rokPlayer.Name);
                OxideConfig.DefaultGroups defaultGroups = Interface.Oxide.Config.Options.DefaultGroups;
                if (!permission.UserHasGroup(playerId, defaultGroups.Players))
                {
                    permission.AddUserGroup(playerId, defaultGroups.Players);
                }
                if (rokPlayer.HasPermission("admin") && !permission.UserHasGroup(playerId, defaultGroups.Administrators))
                {
                    permission.AddUserGroup(playerId, defaultGroups.Administrators);
                }
            }

            // Let covalence know
            Covalence.PlayerManager.PlayerConnected(rokPlayer);

            // Find covalence player
            IPlayer player = Covalence.PlayerManager.FindPlayerById(playerId);
            if (player != null)
            {
                rokPlayer.IPlayer = player;

                // Ignore the server player
                if (rokPlayer.Id != 9999999999)
                {
                    // Call hooks for plugins
                    Interface.Call("OnPlayerConnected", rokPlayer);
                    Interface.Call("OnUserConnected", player);
                }
            }
        }

        /// <summary>
        /// Called when the player has disconnected
        /// </summary>
        /// <param name="player"></param>
        [HookMethod("IOnPlayerDisconnected")]
        private void IOnPlayerDisconnected(Player player)
        {
            // Ignore the server player
            if (player.Id == 9999999999)
            {
                return;
            }

            // Call hooks for plugins
            Interface.Call("OnPlayerDisconnected", player);
            Interface.Call("OnUserDisconnected", player.IPlayer, lang.GetMessage("Unknown", this, player.IPlayer.Id));

            // Let covalence know
            Covalence.PlayerManager.PlayerDisconnected(player);
        }

        /// <summary>
        /// Called when the player is spawning
        /// </summary>
        /// <param name="evt"></param>
        [HookMethod("OnPlayerSpawn")]
        private void OnPlayerSpawn(PlayerFirstSpawnEvent evt)
        {
            // Call hooks for plugins
            Interface.Call("OnUserSpawn", evt.Player.IPlayer);
        }

        /// <summary>
        /// Called when the player has spawned
        /// </summary>
        /// <param name="evt"></param>
        [HookMethod("OnPlayerSpawned")]
        private void OnPlayerSpawned(PlayerPreSpawnCompleteEvent evt)
        {
            // Call hooks for plugins
            Interface.Call("OnUserSpawned", evt.Player.IPlayer);
        }

        /// <summary>
        /// Called when the player is respawning
        /// </summary>
        /// <param name="evt"></param>
        [HookMethod("OnPlayerRespawn")] // Not being called every time?
        private void OnPlayerRespawn(PlayerRespawnEvent evt)
        {
            // Call hooks for plugins
            Interface.Call("OnUserRespawn", evt.Player.IPlayer);
        }

        #endregion Player Hooks

        #region Server Hooks

        [HookMethod("IOnServerCommand")]
        private object IOnServerCommand(ulong playerId, string str)
        {
            if (str.Length == 0)
            {
                return null;
            }

            // Get the full command
            string message = str.TrimStart('/');

            // Parse the command
            ParseCommand(message, out string cmd, out string[] args);
            if (cmd == null)
            {
                return null;
            }

            // Is the command blocked?
            if (Interface.Call("OnServerCommand", cmd, args) != null)
            {
                return true;
            }

            // Check if command is from a player
            Player rokPlayer = CodeHatch.Engine.Networking.Server.GetPlayerById(playerId);
            IPlayer player = Covalence.PlayerManager.FindPlayerById(playerId.ToString());
            if (player == null || rokPlayer == null)
            {
                return null;
            }

            // Is the command blocked?
            object commandSpecific = Interface.Call("OnPlayerCommand", rokPlayer, cmd, args);
            object commandCovalence = Interface.Call("OnUserCommand", player, cmd, args);
            object canBlock = commandSpecific is null ? commandCovalence : commandSpecific;
            if (canBlock != null)
            {
                return true;
            }

            // Is it a valid chat command?
            if (str[0] == '/' && Covalence.CommandSystem.HandleChatMessage(player, str) || cmdlib.HandleChatCommand(rokPlayer, cmd, args))
            {
                return true;
            }

            return null;
        }

        #endregion Server Hooks
    }
}
