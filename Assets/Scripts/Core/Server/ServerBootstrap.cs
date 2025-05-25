using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Core.Server
{
    /// <summary>
    /// Bootstrap script for dedicated server initialization
    /// This should be attached to a GameObject in your initial scene
    /// </summary>
    public class ServerBootstrap : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private GameObject networkManagerPrefab;
        [SerializeField] private GameObject serverManagerPrefab;
        [SerializeField] private GameObject gameManagerPrefab;
        [SerializeField] private string initialSceneName = "DedicatedServerScene";
        
        private void Awake()
        {
            // Check if we should run as server
            if (!ShouldRunAsServer())
            {
                // Destroy this component if not running as server
                Destroy(this);
                return;
            }
            
            Debug.Log("[ServerBootstrap] Initializing dedicated server...");
            
            // Server-specific settings
            ConfigureServerSettings();
            
            // Initialize required managers
            StartCoroutine(InitializeServer());
        }
        
        private bool ShouldRunAsServer()
        {
            // Check command line arguments
            string[] args = System.Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg.ToLower() == "-server" || arg.ToLower() == "-batchmode")
                {
                    return true;
                }
            }
            
            // Check if running in batch mode
            return Application.isBatchMode;
        }
        
        private void ConfigureServerSettings()
        {
            // Performance settings
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            
            // Disable unnecessary features for server
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.antiAliasing = 0;
            QualitySettings.realtimeReflectionProbes = false;
            
            // Physics settings for deterministic behavior
            Physics.defaultSolverIterations = 2;
            Physics.defaultSolverVelocityIterations = 1;
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
            
            // Disable audio
            AudioListener.pause = true;
            AudioListener.volume = 0f;
            
            Debug.Log("[ServerBootstrap] Server settings configured");
        }
        
        private IEnumerator InitializeServer()
        {
            // Wait a frame to ensure scene is loaded
            yield return null;
            
            // Instantiate NetworkManager if not present
            if (NetworkManager.Singleton == null && networkManagerPrefab != null)
            {
                GameObject nmGO = Instantiate(networkManagerPrefab);
                nmGO.name = "NetworkManager";
                Debug.Log("[ServerBootstrap] NetworkManager instantiated");
            }
            
            // Wait for NetworkManager to initialize
            yield return new WaitUntil(() => NetworkManager.Singleton != null);
            
            // Instantiate DedicatedServerManager
            if (DedicatedServerManager.Instance == null && serverManagerPrefab != null)
            {
                GameObject smGO = Instantiate(serverManagerPrefab);
                smGO.name = "DedicatedServerManager";
                Debug.Log("[ServerBootstrap] DedicatedServerManager instantiated");
            }
            
            // Instantiate GameManager if needed
            if (GameManagement.GameManager.Instance == null && gameManagerPrefab != null)
            {
                GameObject gmGO = Instantiate(gameManagerPrefab);
                gmGO.name = "GameManager";
                Debug.Log("[ServerBootstrap] GameManager instantiated");
            }
            
            // Load initial scene if not already there
            if (SceneManager.GetActiveScene().name != initialSceneName)
            {
                Debug.Log($"[ServerBootstrap] Loading initial scene: {initialSceneName}");
                AsyncOperation loadOp = SceneManager.LoadSceneAsync(initialSceneName);
                yield return loadOp;
            }
            
            Debug.Log("[ServerBootstrap] Server initialization complete");
            
            // Destroy this bootstrap object as it's no longer needed
            Destroy(gameObject);
        }
    }
}