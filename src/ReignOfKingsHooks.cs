using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using CodeHatch.Networking.Events.Players;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

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
        private object IOnUserApprove(Player player)
        {
            string id = player.Id.ToString();
            string ip = player.Connection.IpAddress;

            // Let covalence know player is joining
            Covalence.PlayerManager.PlayerJoin(player.Id, player.Name); // TODO: Handle this automatically

            // Call out and see if we should reject
            object loginSpecific = Interface.Call("CanClientLogin", player);
            object loginCovalence = Interface.Call("CanUserLogin", player.Name, id, ip);
            object canLogin = loginSpecific ?? loginCovalence; // TODO: Fix 'ReignOfKingsCore' hook conflict when both return

            // Check if player can login
            if (canLogin is string || canLogin is bool && !(bool)canLogin)
            {
                // Reject the player with the message
                player.ShowPopup("Disconnected", canLogin is string ? canLogin.ToString() : "Connection was rejected"); // TODO: Localization
                player.Connection.Close();
                return ConnectionError.NoError;
            }

            // Call the approval hooks
            object approvedSpecific = Interface.Call("OnUserApprove", player);
            object approvedCovalence = Interface.Call("OnUserApproved", player.Name, id, ip);
            return approvedSpecific ?? approvedCovalence; // TODO: Fix 'ReignOfKingsCore' hook conflict when both return
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

            // Call game and covalence hooks
            object chatSpecific = Interface.Call("OnPlayerChat", evt);
            object chatCovalence = Interface.Call("OnUserChat", evt.Player.IPlayer, evt.Message);
            if (chatSpecific != null || chatCovalence != null)
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
        /// <param name="player"></param>
        /// <returns></returns>
        [HookMethod("IOnPlayerConnected")]
        private void IOnPlayerConnected(Player player)
        {
            // Ignore the server player
            if (player.Id == 9999999999)
            {
                return;
            }

            // Update player's permissions group and name
            if (permission.IsLoaded)
            {
                string id = player.Id.ToString();
                permission.UpdateNickname(id, player.Name);
                OxideConfig.DefaultGroups defaultGroups = Interface.Oxide.Config.Options.DefaultGroups;
                if (!permission.UserHasGroup(id, defaultGroups.Players))
                {
                    permission.AddUserGroup(id, defaultGroups.Players);
                }

                if (player.HasPermission("admin") && !permission.UserHasGroup(id, defaultGroups.Administrators))
                {
                    permission.AddUserGroup(id, defaultGroups.Administrators);
                }
            }

            // Call game-specific hook
            Interface.Call("OnPlayerConnected", player);

            // Let covalence know player connected
            Covalence.PlayerManager.PlayerConnected(player);

            // Find covalence player
            IPlayer iplayer = Covalence.PlayerManager.FindPlayerById(player.Id.ToString());
            if (iplayer != null)
            {
                player.IPlayer = iplayer;

                // Call universal hook
                Interface.Call("OnUserConnected", iplayer);
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

            // Call game-specific hook
            Interface.Call("OnPlayerDisconnected", player);

            // Call universal hook
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
            // Call universal hook
            Interface.Call("OnUserSpawn", evt.Player.IPlayer);
        }

        /// <summary>
        /// Called when the player has spawned
        /// </summary>
        /// <param name="evt"></param>
        [HookMethod("OnPlayerSpawned")]
        private void OnPlayerSpawned(PlayerPreSpawnCompleteEvent evt)
        {
            // Call universal hook
            Interface.Call("OnUserSpawned", evt.Player.IPlayer);
        }

        /// <summary>
        /// Called when the player is respawning
        /// </summary>
        /// <param name="evt"></param>
        [HookMethod("OnPlayerRespawn")] // Not being called every time?
        private void OnPlayerRespawn(PlayerRespawnEvent evt)
        {
            // Call universal hook
            Interface.Call("OnUserRespawn", evt.Player.IPlayer);
        }

        #endregion Player Hooks
    }
}
