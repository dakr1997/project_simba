using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;
using Core.GameManagement;
using Core.AccountManagement;

namespace Core.Network
{
    public class NetworkEventManager : NetworkBehaviour
    {
        [SerializeField] private bool _excludeServerFromPlayerList = true;  // Option to exclude server from player list

        // Singleton pattern for easy access
        public static NetworkEventManager Instance { get; private set; }

        // Events that the LobbyUI can subscribe to
        public event Action<ulong, string> OnPlayerJoined;
        public event Action<ulong> OnPlayerLeft;
        public event Action<ulong, bool> OnPlayerReadyChanged;
        public event Action<float> OnCountdownStarted;
        public event Action OnCountdownStopped;

        // Keep track of all connected players and their ready states
        private Dictionary<ulong, string> _connectedPlayers = new Dictionary<ulong, string>();
        private Dictionary<ulong, bool> _playerReadyStates = new Dictionary<ulong, bool>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("NetworkEventManager initialized as singleton instance");
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Debug.Log($"[NetworkEventManager.cs]NetworkEventManager spawned. IsServer: {IsServer}, IsClient: {IsClient}, IsOwner: {IsOwner}");
        }

        // Called by clients when they join
        [ServerRpc(RequireOwnership = false)]
        public void RegisterPlayerServerRpc(ulong clientId, string playerName)
        {
            Debug.Log($"[NetworkEventManager.cs][Server] Registered player: {clientId} ({playerName})");

            // Check if this is the server's client ID (typically 0 for host)
            bool isServerClient = clientId == NetworkManager.Singleton.LocalClientId &&
                                 (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer);

            // Store player info on the server
            _connectedPlayers[clientId] = playerName;
            _playerReadyStates[clientId] = false;

            // Notify all clients about the new player, unless it's the server and we're excluding it
            if (!(_excludeServerFromPlayerList && isServerClient))
            {
                NotifyPlayerJoinedClientRpc(clientId, playerName);
            }

            // Send existing players to the new client
            if (_connectedPlayers.Count > 1)
            {
                Debug.Log($"[NetworkEventManager.cs][Server] Sending existing players to new client {clientId}");
                foreach (var player in _connectedPlayers)
                {
                    // Don't send the new player info back to them
                    // Also don't send the server's info if we're excluding it
                    if (player.Key != clientId &&
                        !(_excludeServerFromPlayerList && player.Key == NetworkManager.Singleton.LocalClientId &&
                         (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)))
                    {
                        // Send player info
                        SendExistingPlayerInfoClientRpc(player.Key, player.Value, _playerReadyStates[player.Key], new ClientRpcParams
                        {
                            Send = new ClientRpcSendParams
                            {
                                TargetClientIds = new ulong[] { clientId }
                            }
                        });
                    }
                }
            }
        }


        [ServerRpc(RequireOwnership = false)]
        public void SendAccountDataServerRpc(ulong clientId, AccountNetworkData accountData)
        {
            Debug.Log($"[NetworkEventManager] Server received account data from client {clientId}");
            
            // Store the account data in GameManager (server-side only)
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterAccountData(clientId, accountData);
            }
            else
            {
                Debug.LogError("[NetworkEventManager] GameManager.Instance is null!");
            }
        }

        // Called by clients when they change ready status
        [ServerRpc(RequireOwnership = false)]
        public void UpdateReadyStatusServerRpc(ulong clientId, bool isReady)
        {
            Debug.Log($"[NetworkEventManager.cs][Server] Player {clientId} ready status changed to {isReady}");

            // Update ready state on the server
            if (_playerReadyStates.ContainsKey(clientId))
            {
                _playerReadyStates[clientId] = isReady;
            }

            // Broadcast to all clients
            NotifyReadyStatusChangedClientRpc(clientId, isReady);
        }

        // Send existing player info to a specific client (used when new clients join)
        [ClientRpc]
        public void SendExistingPlayerInfoClientRpc(ulong clientId, string playerName, bool isReady, ClientRpcParams clientRpcParams = default)
        {
            Debug.Log($"[NetworkEventManager.cs][Client] Received existing player info: {clientId} ({playerName}), Ready: {isReady}");

            // Trigger the OnPlayerJoined event for the LobbyUI
            OnPlayerJoined?.Invoke(clientId, playerName);

            // If the player is ready, also trigger that event
            if (isReady)
            {
                OnPlayerReadyChanged?.Invoke(clientId, isReady);
            }
        }

        // Client RPC methods that broadcast to all clients
        [ClientRpc]
        public void NotifyPlayerJoinedClientRpc(ulong clientId, string playerName)
        {
            Debug.Log($"[NetworkEventManager.cs][Client] Player joined: {clientId} ({playerName})");

            // Trigger the event for LobbyUI to handle
            OnPlayerJoined?.Invoke(clientId, playerName);
        }

        [ClientRpc]
        public void NotifyReadyStatusChangedClientRpc(ulong clientId, bool isReady)
        {
            Debug.Log($"[NetworkEventManager.cs][Client] Player {clientId} ready status changed to {isReady}");

            // Trigger the event for LobbyUI to handle
            OnPlayerReadyChanged?.Invoke(clientId, isReady);
        }

        [ClientRpc]
        public void NotifyPlayerLeftClientRpc(ulong clientId)
        {
            Debug.Log($"[NetworkEventManager.cs][Client] Player left: {clientId}");

            // Trigger the event for LobbyUI to handle
            OnPlayerLeft?.Invoke(clientId);
        }


        [ClientRpc]
        public void StartCountdownClientRpc(float duration)
        {
            Debug.Log($"[NetworkEventManager.cs][Client {NetworkManager.Singleton.LocalClientId}] StartCountdownClientRpc called with duration: {duration}");
            
            // Check if there are any subscribers
            if (OnCountdownStarted != null)
            {
                Debug.Log($"[NetworkEventManager.cs][Client {NetworkManager.Singleton.LocalClientId}] OnCountdownStarted has {OnCountdownStarted.GetInvocationList().Length} subscribers");
                OnCountdownStarted.Invoke(duration);
            }
            else
            {
                Debug.LogWarning($"[Client {NetworkManager.Singleton.LocalClientId}] OnCountdownStarted has NO subscribers!");
            }
        }


        [ClientRpc]
        public void StopCountdownClientRpc()
        {
            OnCountdownStopped?.Invoke();
        }
        // Server-side method to broadcast when a player disconnects
        public void BroadcastPlayerLeft(ulong clientId)
        {
            // Check if this is running on the server
            if (IsServer)
            {
                // Remove from tracking
                if (_connectedPlayers.ContainsKey(clientId))
                {
                    _connectedPlayers.Remove(clientId);
                }

                if (_playerReadyStates.ContainsKey(clientId))
                {
                    _playerReadyStates.Remove(clientId);
                }

                // Notify all clients
                NotifyPlayerLeftClientRpc(clientId);
            }
            else
            {
                Debug.LogWarning("BroadcastPlayerLeft can only be called on the server");
            }
        }
        
    }
}