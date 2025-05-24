using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class SceneBasedPlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab; // Your player prefab
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private bool excludeServerFromSpawning = true;

    private Dictionary<ulong, bool> playerSpawnState = new Dictionary<ulong, bool>();
    
    private void Awake()
    {
        // Make sure this object persists between scenes
        DontDestroyOnLoad(this.gameObject);
    }
    
    private void Start()
    {
        // Disable the default player prefab in NetworkManager
        if (NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null)
        {
            // Store the reference but remove it from auto-spawning
            playerPrefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null;
        }
        
        // Subscribe to events
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnClientConnected(ulong clientId)
    {
        // Add to our tracking dictionary but don't spawn yet
        if (!playerSpawnState.ContainsKey(clientId))
        {
            playerSpawnState.Add(clientId, false);
        }
        
        // If already in game scene, spawn the player
        if (SceneManager.GetActiveScene().name == gameSceneName && NetworkManager.Singleton.IsServer)
        {
            SpawnPlayerForClient(clientId);
        }
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        
        if (scene.name == gameSceneName)
        {
            // Give the scene a moment to fully load
            StartCoroutine(SpawnAllPlayersDelayed());
        }
        else if (scene.name == lobbySceneName)
        {
            // If we go back to lobby, mark all players as not spawned
            foreach (var clientId in playerSpawnState.Keys)
            {
                playerSpawnState[clientId] = false;
            }
        }
    }
    
    private IEnumerator SpawnAllPlayersDelayed()
    {
        // Wait a frame to ensure everything is set up
        yield return null;
        
        // Spawn all connected players
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayerForClient(clientId);
        }
    }
    
    private void SpawnPlayerForClient(ulong clientId)
    {


        if (excludeServerFromSpawning && clientId == NetworkManager.Singleton.LocalClientId && 
       (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
        {
            Debug.Log($"[SceneBasedPlayerSpawner.cs.cs]Skipping spawn for server client {clientId}");
            return;
        }
        
        Debug.Log($"[SceneBasedPlayerSpawner.cs.cs]Attempting to spawn player for client {clientId}");
        if (!playerSpawnState.ContainsKey(clientId) || playerSpawnState[clientId])
        {
            // Skip if already spawned or not in our dict
            Debug.Log($"[SceneBasedPlayerSpawner.cs.cs]Player for client {clientId} already spawned or not tracked.");
            return;

        }
        
        if (playerPrefab != null && NetworkManager.Singleton.IsServer)
        {
            Debug.Log($"[SceneBasedPlayerSpawner.cs.cs]Spawning player for client {clientId}");
            GameObject playerInstance = Instantiate(playerPrefab);
            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
            
            if (networkObject != null)
            {
                // Mark as spawned before actual spawn to prevent double-spawning
                playerSpawnState[clientId] = true;
                
                // Spawn as player object with client ownership
                networkObject.SpawnAsPlayerObject(clientId);
                
                Debug.Log($"[SceneBasedPlayerSpawner.cs.cs]Spawned player for client {clientId} in {SceneManager.GetActiveScene().name}");
            }
        }
    }
    
    // Call this to transition to game scene
    public void StartGame()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
    }
    
    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
        
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}