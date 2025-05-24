using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Core.Network;
using Core.GameManagement;
using Core.AccountManagement;  // Add this
using Unity.Netcode.Transports.UTP;
using System;

/// <summary>
/// Handles the Lobby UI and player management
/// </summary>
/// 
namespace Core.UI.Lobby
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("Network Settings")]
        [SerializeField] private string _serverIP = "127.0.0.1";
        [SerializeField] private ushort _serverPort = 7777;

        [Header("UI References")]
        [SerializeField] private TMP_InputField _ipInputField;
        [SerializeField] private TextMeshProUGUI _playerNameDisplay;  // Changed from input to display
        [SerializeField] private TextMeshProUGUI _playerLevelDisplay; // Add level display
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _clientButton;
        [SerializeField] private Button _disconnectButton;
        [SerializeField] private Button _readyButton;
        [SerializeField] private TextMeshProUGUI _countdownText;
        [SerializeField] private Transform _playerEntriesParent;
        [SerializeField] private GameObject _playerEntryPrefab;
        [SerializeField] private GameObject _lobbyPanel;  // The entire lobby panel

        [Header("Game Settings")]
        [SerializeField] private float _readyCountdownDuration = 5f;
        [SerializeField] private string _gameSceneName = "GameScene";

        // Player tracking
        private Dictionary<ulong, PlayerInfo> _connectedPlayers = new Dictionary<ulong, PlayerInfo>();
        private List<GameObject> _playerEntries = new List<GameObject>();
        private bool _isReady = false;
        private bool _isConnected = false;
        private Coroutine _countdownCoroutine = null;
        
        // Account data
        private AccountData _localAccountData;

        private void Awake()
        {
            Debug.Log("LobbyUI Awake starting...");

            // Safely set initial UI state with null checks
            if (_countdownText != null)
            {
                _countdownText.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogError("Countdown Text is not assigned in the inspector!");
            }

            // Safely add button listeners with null checks
            if (_hostButton != null)
            {
                _hostButton.onClick.AddListener(OnHostButtonClicked);
            }
            else
            {
                Debug.LogError("Host Button is not assigned in the inspector!");
            }

            if (_clientButton != null)
            {
                _clientButton.onClick.AddListener(OnClientButtonClicked);
            }
            else
            {
                Debug.LogError("Client Button is not assigned in the inspector!");
            }

            if (_disconnectButton != null)
            {
                _disconnectButton.onClick.AddListener(OnDisconnectButtonClicked);
            }
            else
            {
                Debug.LogError("Disconnect Button is not assigned in the inspector!");
            }

            if (_readyButton != null)
            {
                _readyButton.onClick.AddListener(OnReadyButtonClicked);
            }
            else
            {
                Debug.LogError("Ready Button is not assigned in the inspector!");
            }

            Debug.Log("LobbyUI Awake completed.");
        }

        private void Start()
        {
            // Initialize IP field with default
            if (_ipInputField != null)
            {
                _ipInputField.text = _serverIP;
            }

            // Load account data and update UI
            LoadAccountData();

            // Subscribe to network events once they're available
            InvokeRepeating("TrySubscribeToNetworkEvents", 0.5f, 0.5f);
        }

        private void LoadAccountData()
        {
            // Get player data from AccountManager
            if (AccountManager.Instance != null)
            {
                _localAccountData = AccountManager.Instance.GetAccountData();
                
                if (_localAccountData != null)
                {
                    // Update UI with account data
                    if (_playerNameDisplay != null)
                    {
                        _playerNameDisplay.text = _localAccountData.AccountName;
                    }
                    
                    if (_playerLevelDisplay != null)
                    {
                        _playerLevelDisplay.text = $"{_localAccountData.AccountLevel}";
                    }
                    
                    Debug.Log($"[LobbyUI.cs] Loaded account data: {_localAccountData.AccountName}, Level {_localAccountData.AccountLevel}");
                }
                else
                {
                    Debug.LogError("Player data is null from AccountManager!");
                }
            }
            else
            {
                Debug.LogError("AccountManager.Instance is null! Make sure AccountManager is in the scene.");
            }
        }

        private void TrySubscribeToNetworkEvents()
        {
            if (NetworkEventManager.Instance != null)
            {
                Debug.Log("Found NetworkEventManager instance, subscribing to events...");

                // Unsubscribe first to avoid duplicate subscriptions
                NetworkEventManager.Instance.OnPlayerJoined -= HandlePlayerJoined;
                NetworkEventManager.Instance.OnPlayerLeft -= HandlePlayerLeft;
                NetworkEventManager.Instance.OnPlayerReadyChanged -= HandlePlayerReadyChanged;
                NetworkEventManager.Instance.OnCountdownStarted -= HandleCountdownStarted;
                NetworkEventManager.Instance.OnCountdownStopped -= HandleCountdownStopped;

                // Subscribe to network events
                NetworkEventManager.Instance.OnPlayerJoined += HandlePlayerJoined;
                NetworkEventManager.Instance.OnPlayerLeft += HandlePlayerLeft;
                NetworkEventManager.Instance.OnPlayerReadyChanged += HandlePlayerReadyChanged;
                NetworkEventManager.Instance.OnCountdownStarted += HandleCountdownStarted;
                NetworkEventManager.Instance.OnCountdownStopped += HandleCountdownStopped;

                Debug.Log("Subscribed to countdown events");

                // Cancel the repeating invoke once subscribed
                CancelInvoke("TrySubscribeToNetworkEvents");
                Debug.Log("Successfully subscribed to network events");
            }
            else
            {
                Debug.Log("NetworkEventManager.Instance is still null, will try again...");
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from network events
            if (NetworkEventManager.Instance != null)
            {
                NetworkEventManager.Instance.OnPlayerJoined -= HandlePlayerJoined;
                NetworkEventManager.Instance.OnPlayerLeft -= HandlePlayerLeft;
                NetworkEventManager.Instance.OnPlayerReadyChanged -= HandlePlayerReadyChanged;
            }

            // Unsubscribe from network callbacks
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        #region UI Event Handlers

        private void OnHostButtonClicked()
        {
            // Ensure we have account data
            if (_localAccountData == null)
            {
                Debug.LogError("No account data available! Cannot start host.");
                return;
            }

            Debug.Log($"[LobbyUI.cs]Starting host with account: {_localAccountData.AccountName}");

            // Set up network manager
            SetupNetworkManager();

            // Start as host
            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("Host started successfully");

                // Update button states
                _hostButton.interactable = false;
                _clientButton.interactable = false;
            }
            else
            {
                Debug.LogError("Failed to start host");
            }
        }

        private void OnClientButtonClicked()
        {
            // Ensure we have account data
            if (_localAccountData == null)
            {
                Debug.LogError("No account data available! Cannot connect as client.");
                return;
            }

            string serverIP = _ipInputField.text;
            Debug.Log($"[LobbyUI.cs]Connecting to {serverIP} with account: {_localAccountData.AccountName}");

            // Set up network manager
            SetupNetworkManager();

            // Set server IP if provided
            if (!string.IsNullOrEmpty(serverIP))
            {
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    transport.ConnectionData.Address = serverIP;
                }
            }

            // Connect as client
            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("Client started successfully");

                // Update button states
                _hostButton.interactable = false;
                _clientButton.interactable = false;
            }
            else
            {
                Debug.LogError("Failed to start client");
            }
        }

        private void OnDisconnectButtonClicked()
        {
            Debug.Log("Disconnecting...");

            // Cancel countdown if running
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
                _countdownText.gameObject.SetActive(false);
            }

            // Disconnect
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // Reset UI
            _isReady = false;
            _isConnected = false;
            _connectedPlayers.Clear();

            foreach (var entry in _playerEntries)
            {
                Destroy(entry);
            }
            _playerEntries.Clear();

            // Reset button states
            _hostButton.interactable = true;
            _clientButton.interactable = true;
            _readyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Ready";
        }

        private void OnReadyButtonClicked()
        {
            _isReady = !_isReady;
            Debug.Log($"[LobbyUI.cs]Player is now {(_isReady ? "ready" : "not ready")}");

            // Update button text
            _readyButton.GetComponentInChildren<TextMeshProUGUI>().text = _isReady ? "Not Ready" : "Ready";

            if (_isConnected)
            {
                // Notify server of ready status change
                if (NetworkEventManager.Instance != null)
                {
                    NetworkEventManager.Instance.UpdateReadyStatusServerRpc(NetworkManager.Singleton.LocalClientId, _isReady);
                }
            }
            else
            {
                Debug.LogWarning("Not connected to a server, cannot update ready status.");
            }
        }

        #endregion

        #region Network Event Handlers

        private void HandlePlayerJoined(ulong clientId, string playerName)
        {
            Debug.Log($"[LobbyUI.cs]HandlePlayerJoined called for client {clientId} with name {playerName}");

            // Skip if this is our own client ID and we already have an entry
            if (clientId == NetworkManager.Singleton.LocalClientId && _connectedPlayers.ContainsKey(clientId))
            {
                Debug.Log($"[LobbyUI.cs]Skipping duplicate player join for local client {clientId}");
                return;
            }

            // Add player to dictionary and UI
            if (!_connectedPlayers.ContainsKey(clientId))
            {
                _connectedPlayers[clientId] = new PlayerInfo(playerName);
                Debug.Log($"[LobbyUI.cs]Added player {playerName} to connected players dictionary. Count: {_connectedPlayers.Count}");

                // Check if player entry prefab is assigned
                if (_playerEntryPrefab == null)
                {
                    Debug.LogError("Player Entry Prefab is not assigned in the inspector!");
                    return;
                }

                // Check if player entries parent is assigned
                if (_playerEntriesParent == null)
                {
                    Debug.LogError("Player Entries Parent is not assigned in the inspector!");
                    return;
                }

                // Create UI entry
                GameObject playerEntry = Instantiate(_playerEntryPrefab, _playerEntriesParent);
                Debug.Log($"[LobbyUI.cs]Instantiated player entry for {playerName}");

                // Set player name
                TextMeshProUGUI nameText = playerEntry.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = playerName;
                    Debug.Log($"[LobbyUI.cs]Set name text to {playerName}");
                }
                else
                {
                    Debug.LogError("NameText child not found in player entry prefab!");
                }

                // Set ready status
                TextMeshProUGUI statusText = playerEntry.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
                if (statusText != null)
                {
                    statusText.text = "Not Ready";
                    statusText.color = Color.red;
                    Debug.Log("Set status text to Not Ready");
                }
                else
                {
                    Debug.LogError("StatusText child not found in player entry prefab!");
                }

                // Highlight local player
                if (clientId == NetworkManager.Singleton.LocalClientId)
                {
                    Image background = playerEntry.GetComponent<Image>();
                    if (background != null)
                    {
                        background.color = new Color(0.8f, 0.9f, 1f, 0.7f);
                        Debug.Log("Highlighted local player entry");
                    }
                    else
                    {
                        Debug.LogError("Image component not found on player entry prefab!");
                    }
                }

                // Store reference
                _playerEntries.Add(playerEntry);
                playerEntry.name = $"PlayerEntry_{clientId}";

                Debug.Log($"[LobbyUI.cs]Added player {playerName} (ID: {clientId}) to the lobby. Total entries: {_playerEntries.Count}");
            }
            else
            {
                Debug.Log($"[LobbyUI.cs]Player {playerName} (ID: {clientId}) already in the lobby");
            }
        }

        private void HandlePlayerLeft(ulong clientId)
        {
            // Remove from dictionary and UI
            RemovePlayer(clientId);
        }

        private void HandlePlayerReadyChanged(ulong clientId, bool isReady)
        {
            // Update dictionary
            if (_connectedPlayers.ContainsKey(clientId))
            {
                _connectedPlayers[clientId].IsReady = isReady;
            }

            // Update UI
            foreach (GameObject entry in _playerEntries)
            {
                if (entry.name == $"PlayerEntry_{clientId}")
                {
                    TextMeshProUGUI statusText = entry.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
                    if (statusText != null)
                    {
                        statusText.text = isReady ? "Ready" : "Not Ready";
                        statusText.color = isReady ? Color.green : Color.red;
                    }
                    break;
                }
            }

            // Check if all players are ready
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                if (AreAllPlayersReady())
                {
                    // Start countdown
                    Debug.Log("All players are ready, server starting countdown for all clients!");
                    NetworkEventManager.Instance.StartCountdownClientRpc(_readyCountdownDuration);

                    // Start local countdown as well
                    _countdownText.gameObject.SetActive(true);
                    if (_countdownCoroutine != null)
                    {
                        StopCoroutine(_countdownCoroutine);
                    }
                    _countdownCoroutine = StartCoroutine(CountdownCoroutine());
                }
                else if (_countdownCoroutine != null)
                {
                    // Cancel countdown if any player is not ready
                    Debug.Log("Not all players are ready, server stopping countdown for all clients");
                    NetworkEventManager.Instance.StopCountdownClientRpc();

                    // Stop local countdown
                    StopCoroutine(_countdownCoroutine);
                    _countdownCoroutine = null;
                    _countdownText.gameObject.SetActive(false);
                }
            }
        }

        private void HandleCountdownStarted(float duration)
        {
            Debug.Log($"[LobbyUI.cs]HandleCountdownStarted called with duration: {duration}");
            // Cancel any existing countdown
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
            }

            // Start new countdown
            _countdownText.gameObject.SetActive(true);
            _countdownCoroutine = StartCoroutine(CountdownCoroutine());
        }

        private void HandleCountdownStopped()
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
                _countdownText.gameObject.SetActive(false);
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[LobbyUI.cs]OnClientConnected: Client connected: {clientId}");

            // If this is the local client
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("Local client connected");
                _isConnected = true;

                // Register with the server using account data
                if (NetworkEventManager.Instance != null && _localAccountData != null)
                {
                    Debug.Log($"[LobbyUI.cs]Registering player with server: {clientId} - {_localAccountData.AccountName}");

                    // Register player name for lobby display
                    NetworkEventManager.Instance.RegisterPlayerServerRpc(clientId, _localAccountData.AccountName);
                    
                    // Register full player data with GameManager for game use
                    GameManager.Instance.RegisterAccountData(clientId, _localAccountData);
                }
                else
                {
                    Debug.LogError("Cannot register player - NetworkEventManager.Instance or _localAccountData is null!");
                }
            }
            DebugLogConnectedPlayers();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[LobbyUI.cs]Client disconnected: {clientId}");

            // If this is the local client
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("Local client disconnected");
                _isConnected = false;

                // Clear all player entries
                foreach (var entry in _playerEntries)
                {
                    Destroy(entry);
                }
                _playerEntries.Clear();
                _connectedPlayers.Clear();

                // Reset button states
                _hostButton.interactable = true;
                _clientButton.interactable = true;
                _readyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Ready";
            }
            else if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                // If server, broadcast player left
                if (NetworkEventManager.Instance != null)
                {
                    NetworkEventManager.Instance.BroadcastPlayerLeft(clientId);
                }

                // Remove player
                RemovePlayer(clientId);

                // Cancel countdown if running and not all players are ready
                if (_countdownCoroutine != null && !AreAllPlayersReady())
                {
                    StopCoroutine(_countdownCoroutine);
                    _countdownCoroutine = null;
                    _countdownText.gameObject.SetActive(false);
                }
            }

            DebugLogConnectedPlayers();
        }

        #endregion

        #region Helper Methods

        private void SetupNetworkManager()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("NetworkManager.Singleton is null!");
                return;
            }

            // Subscribe to network callbacks
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            // Set up transport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.ConnectionData.Port = _serverPort;
            }
        }

        private void RemovePlayer(ulong clientId)
        {
            if (_connectedPlayers.ContainsKey(clientId))
            {
                _connectedPlayers.Remove(clientId);

                // Remove UI entry
                GameObject entryToRemove = null;
                foreach (GameObject entry in _playerEntries)
                {
                    if (entry.name == $"PlayerEntry_{clientId}")
                    {
                        entryToRemove = entry;
                        break;
                    }
                }

                if (entryToRemove != null)
                {
                    _playerEntries.Remove(entryToRemove);
                    Destroy(entryToRemove);
                }

                Debug.Log($"[LobbyUI.cs]Removed player with ID: {clientId} from the lobby");
            }
        }

        private bool AreAllPlayersReady()
        {
            if (_connectedPlayers.Count == 0)
            {
                return false;
            }

            foreach (var player in _connectedPlayers.Values)
            {
                if (!player.IsReady)
                {
                    return false;
                }
            }

            return true;
        }

        private void DebugLogConnectedPlayers()
        {
            Debug.Log($"[LobbyUI.cs]Connected players count: {_connectedPlayers.Count}");
            foreach (var player in _connectedPlayers)
            {
                Debug.Log($"[LobbyUI.cs]Player ID: {player.Key}, Name: {player.Value.Name}, Ready: {player.Value.IsReady}");
            }
        }

        private IEnumerator CountdownCoroutine()
        {
            float countdown = _readyCountdownDuration;

            while (countdown > 0)
            {
                _countdownText.text = $"Starting in {countdown:0}";
                yield return new WaitForSeconds(1f);
                countdown -= 1f;
            }

            _countdownText.text = "Starting game...";

            // Disable the entire lobby panel
            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(false);
            }

            // Load the game scene
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                Debug.Log("[LobbyUI.cs] Using GameManager to transition to game...");
                GameManager.Instance.StartNewSession();
                GameManager.Instance.TransitionToGame();
            }

            _countdownCoroutine = null;
        }

        // Public method to refresh account data (useful after purchasing from shop)
        public void RefreshAccountData()
        {
            LoadAccountData();
        }

        #endregion
    }

    /// <summary>
    /// Class to store player information
    /// </summary>
    [System.Serializable]
    public class PlayerInfo
    {
        public string Name;
        public bool IsReady;

        public PlayerInfo(string name)
        {
            Name = name;
            IsReady = false;
        }
    }
}