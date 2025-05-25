using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections;
using System.Collections.Generic;
using Core.GameManagement;
using Core.AccountManagement;
using Core.Server;

namespace Core.Network
{
    public class NetworkEventManager : NetworkBehaviour
    {
        [SerializeField] private bool _excludeServerFromPlayerList = true;
        
        // Server-specific settings
        [Header("Server Settings")]
        [SerializeField] private bool _isDedicatedServer = false;
        [SerializeField] private int _minPlayersRequired = 2; // Minimum players needed before ready checks
        [SerializeField] private float _countdownDuration = 5f;

        // Singleton pattern for easy access
        public static NetworkEventManager Instance { get; private set; }

        // Events that the LobbyUI can subscribe to (client-side only)
        public event Action<ulong, string> OnPlayerJoined;
        public event Action<ulong> OnPlayerLeft;
        public event Action<ulong, bool> OnPlayerReadyChanged;
        public event Action<float> OnCountdownStarted;
        public event Action OnCountdownStopped;

        // Keep track of all connected players and their ready states
        private Dictionary<ulong, string> _connectedPlayers = new Dictionary<ulong, string>();
        private Dictionary<ulong, bool> _playerReadyStates = new Dictionary<ulong, bool>();
        
        // Track if countdown is active
        private bool _countdownActive = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Check if this is running as a dedicated server
            CheckIfDedicatedServer();
            Debug.Log($"[NetworkEventManager] Initialized as singleton instance. IsDedicatedServer: {_isDedicatedServer}");
        }
        
        private void CheckIfDedicatedServer()
        {
            // Check command line args or other indicators
            string[] args = System.Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg.ToLower() == "-server" || arg.ToLower() == "-batchmode")
                {
                    _isDedicatedServer = true;
                    break;
                }
            }
            
            // Also check if running in batch mode
            if (Application.isBatchMode)
            {
                _isDedicatedServer = true;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Debug.Log($"[NetworkEventManager] NetworkEventManager spawned. IsServer: {IsServer}, IsClient: {IsClient}, IsOwner: {IsOwner}");
            
            // Register for disconnect callbacks
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            
            // Server-specific initialization
            if (IsServer && _isDedicatedServer)
            {
                Debug.Log("[NetworkEventManager] Running as dedicated server - skipping client-side features");
            }
        }
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            
            // Clean up when shutting down
            if (IsServer)
            {
                _connectedPlayers.Clear();
                _playerReadyStates.Clear();
            }
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (IsServer)
            {
                Debug.Log($"[NetworkEventManager] Client disconnected: {clientId}");
                BroadcastPlayerLeft(clientId);
            }
        }
                
        // Called by clients when they join
        [ServerRpc(RequireOwnership = false)]
        public void RegisterPlayerServerRpc(ulong clientId, string playerName)
        {
            Debug.Log($"[NetworkEventManager][Server] Registered player: {clientId} ({playerName})");

            // Check if this is the server's client ID (typically 0 for host)
            bool isServerClient = clientId == NetworkManager.Singleton.LocalClientId &&
                                 (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer);

            // Store player info on the server
            _connectedPlayers[clientId] = playerName;
            _playerReadyStates[clientId] = false;

            // Notify all clients about the new player, unless it's the server and we're excluding it
            if (!(_excludeServerFromPlayerList && isServerClient))
            {
                // Skip client notifications on dedicated server
                {
                    Debug.Log($"[NetworkEventManager][Server] Notifying clients about new player: {clientId} ({playerName})");
                    NotifyPlayerJoinedClientRpc(clientId, playerName);
                }
            }

            // Send existing players to the new client (skip on dedicated server)
            if (_connectedPlayers.Count >= 1)
            {
                Debug.Log($"[NetworkEventManager][Server] Sending existing players to new client {clientId}");
                foreach (var player in _connectedPlayers)
                {
                    if (player.Key != clientId &&
                        !(_excludeServerFromPlayerList && player.Key == NetworkManager.Singleton.LocalClientId &&
                         (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)))
                    {
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
            Debug.Log($"[NetworkEventManager][Server] Player {clientId} ready status changed to {isReady}");

            // Update ready state on the server
            if (_playerReadyStates.ContainsKey(clientId))
            {
                _playerReadyStates[clientId] = isReady;
            }

            // Skip client notifications on dedicated server

            NotifyReadyStatusChangedClientRpc(clientId, isReady);
            
            // Check if all players are ready
            if (IsServer)
            {
                CheckIfAllPlayersReady();
            }
        }
        
        private void CheckIfAllPlayersReady()
        {
            // Need at least the minimum required players
            if (_connectedPlayers.Count < _minPlayersRequired)
            {
                Debug.Log($"[NetworkEventManager][Server] Not enough players to start ({_connectedPlayers.Count}/{_minPlayersRequired})");
                return;
            }
            
            // Check if ALL players are ready
            bool allReady = true;
            int readyCount = 0;
            
            foreach (var kvp in _playerReadyStates)
            {
                if (kvp.Value)
                {
                    readyCount++;
                }
                else
                {
                    allReady = false;
                }
            }
            
            Debug.Log($"[NetworkEventManager][Server] Ready check: {readyCount}/{_connectedPlayers.Count} players ready");
            
            if (allReady && _connectedPlayers.Count > 0)
            {
                Debug.Log("[NetworkEventManager][Server] All players are ready! Starting countdown...");
                _countdownActive = true;
                
                // Start countdown on all clients

                StartCountdownClientRpc(_countdownDuration);

                
                // Start the game after countdown
                StartCoroutine(StartGameAfterCountdown());
            }
            else if (_countdownActive && !allReady)
            {
                // Someone became not ready during countdown, cancel it
                Debug.Log("[NetworkEventManager][Server] Player became not ready, cancelling countdown");
                _countdownActive = false;
                

                StopCountdownClientRpc();

            }
        }
        
        private IEnumerator StartGameAfterCountdown()
        {
            yield return new WaitForSeconds(_countdownDuration);
            
            // Double-check everyone is still ready
            bool stillAllReady = true;
            foreach (var kvp in _playerReadyStates)
            {
                if (!kvp.Value)
                {
                    stillAllReady = false;
                    break;
                }
            }
            
            if (stillAllReady && _countdownActive)
            {
                _countdownActive = false;
                CheckAndStartGame();
            }
        }
        
        private void CheckAndStartGame()
        {
            if (!IsServer)
                return;
                
            Debug.Log("[NetworkEventManager][Server] Starting game transition");
            
            // Use appropriate game manager based on server type
            if (_isDedicatedServer && Core.Server.DedicatedServerManager.Instance != null)
            {
                Core.Server.DedicatedServerManager.Instance.TransitionToGame();
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.StartNewSession();
                GameManager.Instance.TransitionToGame();
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            }
        }

        // Client RPC methods - these are skipped on dedicated server
        [ClientRpc]
        public void SendExistingPlayerInfoClientRpc(ulong clientId, string playerName, bool isReady, ClientRpcParams clientRpcParams = default)
        {  
            Debug.Log($"[NetworkEventManager][Client] Received existing player info: {clientId} ({playerName}), Ready: {isReady}");
            OnPlayerJoined?.Invoke(clientId, playerName);
            if (isReady)
            {
                OnPlayerReadyChanged?.Invoke(clientId, isReady);
            }
        }

        [ClientRpc]
        public void NotifyPlayerJoinedClientRpc(ulong clientId, string playerName)
        {
            Debug.Log($"[NetworkEventManager][Client] Player joined: {clientId} ({playerName})");
            OnPlayerJoined?.Invoke(clientId, playerName);
        }

        [ClientRpc]
        public void NotifyReadyStatusChangedClientRpc(ulong clientId, bool isReady)
        {
            Debug.Log($"[NetworkEventManager][Client] Player {clientId} ready status changed to {isReady}");
            OnPlayerReadyChanged?.Invoke(clientId, isReady);
        }

        [ClientRpc]
        public void NotifyPlayerLeftClientRpc(ulong clientId)
        {          
            Debug.Log($"[NetworkEventManager][Client] Player left: {clientId}");
            OnPlayerLeft?.Invoke(clientId);
        }

        [ClientRpc]
        public void StartCountdownClientRpc(float duration)
        {
            Debug.Log($"[NetworkEventManager][Client {NetworkManager.Singleton.LocalClientId}] StartCountdownClientRpc called with duration: {duration}");
            OnCountdownStarted?.Invoke(duration);
        }

        [ClientRpc]
        public void StopCountdownClientRpc()
        {
            
            OnCountdownStopped?.Invoke();
        }

        // Server-side method to broadcast when a player disconnects
        public void BroadcastPlayerLeft(ulong clientId)
        {
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

                // Notify all clients (skip on dedicated server if we're excluding server)

                NotifyPlayerLeftClientRpc(clientId);


                // If we were counting down and someone left, check if we still meet requirements
                if (_countdownActive)
                {
                    if (_connectedPlayers.Count < _minPlayersRequired)
                    {
                        Debug.Log("[NetworkEventManager][Server] Dropped below minimum players during countdown, cancelling");
                        _countdownActive = false;
                        
                            StopCountdownClientRpc();
                    }
                    else
                    {
                        // Re-check if all remaining players are still ready
                        CheckIfAllPlayersReady();
                    }
                }
            }
        }

        
        // Server-only helper methods
        public int GetConnectedPlayerCount()
        {
            return _connectedPlayers.Count;
        }
        
        public bool AreAllPlayersReady()
        {
            if (_connectedPlayers.Count == 0)
                return false;
                
            foreach (var kvp in _playerReadyStates)
            {
                if (!kvp.Value)
                    return false;
            }
            
            return true;
        }
    }
}