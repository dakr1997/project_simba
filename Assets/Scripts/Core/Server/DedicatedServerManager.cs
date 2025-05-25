using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections;
using UnityEngine.SceneManagement;

namespace Core.Server
{
    /// <summary>
    /// Dedicated server entry point and manager
    /// Handles server-specific initialization and game loop
    /// </summary>
    public class DedicatedServerManager : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private ushort serverPort = 7777;
        [SerializeField] private int targetFrameRate = 30;
        
        [Header("Scene Configuration")]
        [SerializeField] private string lobbySceneName = "DedicatedServerScene";
        [SerializeField] private string gameSceneName = "GameScene";
        [SerializeField] private string rewardSceneName = "RewardScene";
        
        [Header("Server Settings")]
        [SerializeField] private float sessionTimeout = 300f; // 5 minutes timeout if no players
        
        private float lastPlayerActivityTime;
        private bool isSessionActive = false;
        private Coroutine sessionTimeoutCoroutine;
        
        public static DedicatedServerManager Instance { get; private set; }
        
        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Server performance optimizations
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0;
            
            // Disable graphics rendering on server
            if (Application.isBatchMode)
            {
                Debug.Log("[DedicatedServer] Running in batch mode - graphics disabled");
            }
            
            Debug.Log($"[DedicatedServer] Initializing server on port {serverPort}");
        }
        
        private void Start()
        {
            // Get port from command line args if provided
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-port" && i + 1 < args.Length)
                {
                    if (ushort.TryParse(args[i + 1], out ushort port))
                    {
                        serverPort = port;
                        Debug.Log($"[DedicatedServer] Using port from command line: {serverPort}");
                    }
                }
            }
            
            StartServer();
        }
        
        private void StartServer()
        {
            // Configure NetworkManager
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.ConnectionData.Port = serverPort;
            transport.ConnectionData.Address = "127.0.0.1"; // Listen on all interfaces
            
            // Set max connections
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = false; // You might want to enable this for authentication
            
            // Subscribe to network events
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            
            // Start the server
            if (NetworkManager.Singleton.StartServer())
            {
                Debug.Log($"[DedicatedServer] Server started successfully on port {serverPort}");
            }
            else
            {
                Debug.LogError("[DedicatedServer] Failed to start server!");
                Application.Quit();
            }
        }
        
        private void OnServerStarted()
        {
            Debug.Log("[DedicatedServer] Server is ready and listening for connections");
            
            // Ensure we're in the lobby scene
            if (SceneManager.GetActiveScene().name != lobbySceneName)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
            }
            
            // Start session timeout check
            sessionTimeoutCoroutine = StartCoroutine(CheckSessionTimeout());
        }
        
        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[DedicatedServer] Client {clientId} connected. Total players: {NetworkManager.Singleton.ConnectedClients.Count}");
            
            lastPlayerActivityTime = Time.time;
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[DedicatedServer] Client {clientId} disconnected. Remaining players: {NetworkManager.Singleton.ConnectedClients.Count - 1}");
            
            // If no players left and we're in a game session, return to lobby
            if (NetworkManager.Singleton.ConnectedClients.Count <= 1 && isSessionActive)
            {
                Debug.Log("[DedicatedServer] No players remaining, ending session");
                EndGameSession();
            }
        }
        
        public void TransitionToGame()
        {
            Debug.Log("[DedicatedServer] Transitioning to game scene");
            isSessionActive = true;
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            
            // Notify GameManager to start the session
            StartCoroutine(WaitAndStartGameSession());
        }
        
        private IEnumerator WaitAndStartGameSession()
        {
            yield return new WaitForSeconds(1f); // Wait for scene load
            
            if (GameManagement.GameManager.Instance != null)
            {
                GameManagement.GameManager.Instance.StartNewSession();
            }
        }
        
        public void TransitionToRewards()
        {
            Debug.Log("[DedicatedServer] Transitioning to rewards scene");
            NetworkManager.Singleton.SceneManager.LoadScene(rewardSceneName, LoadSceneMode.Single);
            
            // Schedule return to lobby
            StartCoroutine(ReturnToLobbyAfterRewards());
        }
        
        private IEnumerator ReturnToLobbyAfterRewards()
        {
            yield return new WaitForSeconds(30f); // Show rewards for 30 seconds
            
            Debug.Log("[DedicatedServer] Returning to lobby");
            EndGameSession();
        }
        
        private void EndGameSession()
        {
            isSessionActive = false;
            
            // Clean up session data
            if (GameManagement.GameManager.Instance != null)
            {
                GameManagement.GameManager.Instance.EndSession();
            }
            
            // Return to lobby
            NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
            
            lastPlayerActivityTime = Time.time;
        }
        
        private IEnumerator CheckSessionTimeout()
        {
            while (true)
            {
                yield return new WaitForSeconds(10f); // Check every 10 seconds
                
                if (NetworkManager.Singleton.ConnectedClients.Count == 0 && 
                    Time.time - lastPlayerActivityTime > sessionTimeout)
                {
                    Debug.Log("[DedicatedServer] No players for extended period, considering shutdown");
                    // In a real scenario, you might want to shut down the server instance
                    // For now, we'll just keep it running
                }
            }
        }
        
        private void FixedUpdate()
        {
            // Server-side fixed update for physics and game logic
            // This runs at a fixed timestep, good for deterministic gameplay
            
            if (isSessionActive)
            {
                // Update game logic here if needed
            }
        }
        
        private void OnApplicationQuit()
        {
            Debug.Log("[DedicatedServer] Server shutting down");
            
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
        
        // Public methods for external control
        public void ForceStartGame()
        {
            if (!isSessionActive && NetworkManager.Singleton.ConnectedClients.Count > 0)
            {
                TransitionToGame();
            }
        }
        
        public void ForceEndGame()
        {
            if (isSessionActive)
            {
                EndGameSession();
            }
        }
    }
}