using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Core.AccountManagement;

namespace Core.GameManagement
{
    public class GameManager : NetworkBehaviour
    {
        #region Singleton
        private static GameManager _instance;
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("GameManager");
                        _instance = go.AddComponent<GameManager>();
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Account Data Storage
        [Header("Account Data Storage")]
        private Dictionary<ulong, AccountNetworkData> _playerAccountData = new Dictionary<ulong, AccountNetworkData>();
        #endregion

        #region Scene Names
        [Header("Scene Configuration")]
        [SerializeField] private string _menuSceneName = "MenuScene";
        [SerializeField] private string _gameSceneName = "GameScene";
        [SerializeField] private string _rewardSceneName = "RewardScene";
        #endregion

        #region Game States
        public enum GameState
        {
            Menu,
            InGame,
            Rewards,
            Transitioning
        }

        public enum GameSceneState
        {
            Building,
            Wave,
            BetweenWaves,
            GameOver
        }

        private GameState _currentGameState = GameState.Menu;
        private GameSceneState _currentGameSceneState = GameSceneState.Building;
        
        public GameState CurrentGameState => _currentGameState;
        public GameSceneState CurrentGameSceneState => _currentGameSceneState;
        #endregion

        #region Events
        public event Action<GameState> OnGameStateChanged;
        public event Action<GameSceneState> OnGameSceneStateChanged;
        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoadCompleted;
        public event Action OnGameReset;
        #endregion

        #region Persistent Data
        private GameSessionData _sessionData;
        private Dictionary<ulong, PlayerSessionData> _playerSessionData = new Dictionary<ulong, PlayerSessionData>();
        
        public GameSessionData SessionData => _sessionData;
        public Dictionary<ulong, PlayerSessionData> PlayerSessionData => _playerSessionData;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeGameManager();
        }

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
        #endregion

        #region Initialization
        private void InitializeGameManager()
        {
            _sessionData = new GameSessionData();
            Debug.Log("GameManager initialized");
        }
        #endregion

        #region Account Data Management
        public void RegisterPlayerData(ulong clientId, PlayerData playerData)
        {
            if (!_playerSessionData.ContainsKey(clientId))
            {
                _playerSessionData[clientId] = new PlayerSessionData
                {
                    ClientId = clientId,
                    PlayerData = playerData,
                    PerformanceData = new PerformanceData(),
                    RewardData = new RewardData()
                };
            }
            else
            {
                _playerSessionData[clientId].PlayerData = playerData;
            }

            Debug.Log($"[GameManager] Registered player data for client {clientId}: {playerData.PlayerName}, Level {playerData.AccountLevel}, Currency {playerData.Currency}");
        }

        // Register the network-optimized account data (with perks)
        public void RegisterAccountData(ulong clientId, AccountNetworkData accountData)
        {
            // Store the account network data
            _playerAccountData[clientId] = accountData;
            
            // Also create/update PlayerData for compatibility with existing systems
            var playerData = new PlayerData
            {
                PlayerName = accountData.accountName,  // Note: lowercase 'a' in accountName
                AccountLevel = accountData.accountLevel,
                Currency = 0,  // AccountNetworkData doesn't include currency
                Upgrades = new Dictionary<string, float>()
            };
            
            // Convert perk levels to upgrades dictionary for backward compatibility
            playerData.Upgrades[AccountPerks.HP_PERK] = accountData.hpPerkLevel;
            playerData.Upgrades[AccountPerks.DAMAGE_PERK] = accountData.damagePerkLevel;
            playerData.Upgrades[AccountPerks.GOLD_PERK] = accountData.goldPerkLevel;
            playerData.Upgrades[AccountPerks.SPEED_PERK] = accountData.speedPerkLevel;
            playerData.Upgrades[AccountPerks.EXP_PERK] = accountData.expPerkLevel;

            // Register the player data
            RegisterPlayerData(clientId, playerData);
            
            Debug.Log($"[GameManager] Registered account data for client {clientId}: {accountData.accountName}, " +
                      $"Level {accountData.accountLevel}, Perks: HP={accountData.hpPerkLevel}, " +
                      $"DMG={accountData.damagePerkLevel}, GOLD={accountData.goldPerkLevel}, " +
                      $"SPD={accountData.speedPerkLevel}, EXP={accountData.expPerkLevel}");
        }

        // Get the account data for a specific player
        public AccountNetworkData GetPlayerAccountData(ulong clientId)
        {
            return _playerAccountData.ContainsKey(clientId) ? _playerAccountData[clientId] : null;
        }

        // Helper methods for quick access to multipliers
        public float GetPlayerHpMultiplier(ulong clientId)
        {
            var data = GetPlayerAccountData(clientId);
            return data?.GetHpMultiplier() ?? 1f;
        }

        public float GetPlayerDamageMultiplier(ulong clientId)
        {
            var data = GetPlayerAccountData(clientId);
            return data?.GetDamageMultiplier() ?? 1f;
        }

        public float GetPlayerGoldMultiplier(ulong clientId)
        {
            var data = GetPlayerAccountData(clientId);
            return data?.GetGoldMultiplier() ?? 1f;
        }

        public float GetPlayerExpMultiplier(ulong clientId)
        {
            var data = GetPlayerAccountData(clientId);
            return data?.GetExpMultiplier() ?? 1f;
        }

        public float GetPlayerSpeedMultiplier(ulong clientId)
        {
            var data = GetPlayerAccountData(clientId);
            return data?.GetSpeedMultiplier() ?? 1f;
        }

        public PlayerSessionData GetPlayerSessionData(ulong clientId)
        {
            return _playerSessionData.ContainsKey(clientId) ? _playerSessionData[clientId] : null;
        }

        public void UpdatePlayerPerformance(ulong clientId, PerformanceData performance)
        {
            if (_playerSessionData.ContainsKey(clientId))
            {
                _playerSessionData[clientId].PerformanceData = performance;
            }
        }

        public void UpdatePlayerRewards(ulong clientId, RewardData rewards)
        {
            if (_playerSessionData.ContainsKey(clientId))
            {
                _playerSessionData[clientId].RewardData = rewards;
                
                // Update persistent player data with rewards
                _playerSessionData[clientId].PlayerData.AccountLevel += rewards.ExperienceGained / 100; // Example conversion
                _playerSessionData[clientId].PlayerData.Currency += rewards.CurrencyEarned;
                
                // Apply exp multiplier if applicable
                float expMultiplier = GetPlayerExpMultiplier(clientId);
                if (expMultiplier > 1f)
                {
                    int bonusExp = Mathf.RoundToInt(rewards.ExperienceGained * (expMultiplier - 1f));
                    _playerSessionData[clientId].PlayerData.AccountLevel += bonusExp / 100;
                    Debug.Log($"[GameManager] Applied EXP multiplier {expMultiplier}x for client {clientId}, bonus EXP: {bonusExp}");
                }
                
                // Apply gold multiplier if applicable
                float goldMultiplier = GetPlayerGoldMultiplier(clientId);
                if (goldMultiplier > 1f)
                {
                    int bonusGold = Mathf.RoundToInt(rewards.CurrencyEarned * (goldMultiplier - 1f));
                    _playerSessionData[clientId].PlayerData.Currency += bonusGold;
                    Debug.Log($"[GameManager] Applied Gold multiplier {goldMultiplier}x for client {clientId}, bonus Gold: {bonusGold}");
                }
            }
        }
        #endregion

        #region Scene Transitions
        public void TransitionToGame()
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                StartCoroutine(LoadSceneAsync(_gameSceneName));
            }
        }

        public void TransitionToRewards()
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                StartCoroutine(LoadSceneAsync(_rewardSceneName));
            }
        }

        public void TransitionToMenu(bool fullReset = false)
        {
            if (fullReset)
            {
                PrepareFullReset();
            }

            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                StartCoroutine(LoadSceneAsync(_menuSceneName));
            }
        }

        private IEnumerator LoadSceneAsync(string sceneName)
        {
            _currentGameState = GameState.Transitioning;
            OnGameStateChanged?.Invoke(_currentGameState);
            OnSceneLoadStarted?.Invoke(sceneName);

            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                // For networked games, use NetworkManager's scene loading
                NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            else
            {
                // For single player or if not the host
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[GameManager] Scene loaded: {scene.name}");
            
            // Update game state based on loaded scene
            if (scene.name == _menuSceneName)
            {
                _currentGameState = GameState.Menu;
            }
            else if (scene.name == _gameSceneName)
            {
                _currentGameState = GameState.InGame;
                _currentGameSceneState = GameSceneState.Building;
            }
            else if (scene.name == _rewardSceneName)
            {
                _currentGameState = GameState.Rewards;
            }

            OnGameStateChanged?.Invoke(_currentGameState);
            OnSceneLoadCompleted?.Invoke(scene.name);
        }
        #endregion

        #region Game Scene State Management
        public void SetGameSceneState(GameSceneState newState)
        {
            if (_currentGameState != GameState.InGame)
            {
                Debug.LogWarning("Cannot change game scene state when not in game!");
                return;
            }

            _currentGameSceneState = newState;
            OnGameSceneStateChanged?.Invoke(_currentGameSceneState);

            // Sync state across network if needed
            if (NetworkManager.Singleton.IsServer)
            {
                SyncGameSceneStateClientRpc(newState);
            }
        }

        [ClientRpc]
        private void SyncGameSceneStateClientRpc(GameSceneState newState)
        {
            _currentGameSceneState = newState;
            OnGameSceneStateChanged?.Invoke(_currentGameSceneState);
        }
        #endregion

        #region Session Management
        public void StartNewSession()
        {
            _sessionData = new GameSessionData
            {
                SessionId = Guid.NewGuid().ToString(),
                StartTime = DateTime.Now,
                Seed = UnityEngine.Random.Range(0, int.MaxValue)
            };

            Debug.Log($"[GameManager] Started new session: {_sessionData.SessionId}");
        }

        public void EndSession()
        {
            _sessionData.EndTime = DateTime.Now;
            _sessionData.Duration = _sessionData.EndTime - _sessionData.StartTime;
            
            Debug.Log($"[GameManager] Session ended. Duration: {_sessionData.Duration}");
        }

        private void PrepareFullReset()
        {
            // Save any data that needs to persist (account progress, etc.)
            SavePersistentData();

            // Clear session data
            _sessionData = new GameSessionData();
            
            // Clear performance data but keep player data
            foreach (var playerData in _playerSessionData.Values)
            {
                playerData.PerformanceData = new PerformanceData();
                playerData.RewardData = new RewardData();
            }

            // Clear account data cache (will be re-sent on next connection)
            _playerAccountData.Clear();

            OnGameReset?.Invoke();
        }

        private void SavePersistentData()
        {
            // Save to PlayerPrefs or your save system
            foreach (var kvp in _playerSessionData)
            {
                var data = kvp.Value.PlayerData;
                string key = $"Player_{kvp.Key}";
                
                PlayerPrefs.SetString($"{key}_Name", data.PlayerName);
                PlayerPrefs.SetInt($"{key}_Level", data.AccountLevel);
                PlayerPrefs.SetInt($"{key}_Currency", data.Currency);
                PlayerPrefs.SetString($"{key}_Character", data.SelectedCharacter);
            }
            
            PlayerPrefs.Save();
        }

        public void LoadPersistentData(ulong clientId)
        {
            string key = $"Player_{clientId}";
            
            if (PlayerPrefs.HasKey($"{key}_Name"))
            {
                var playerData = new PlayerData
                {
                    PlayerName = PlayerPrefs.GetString($"{key}_Name"),
                    AccountLevel = PlayerPrefs.GetInt($"{key}_Level"),
                    Currency = PlayerPrefs.GetInt($"{key}_Currency"),
                    SelectedCharacter = PlayerPrefs.GetString($"{key}_Character")
                };
                
                RegisterPlayerData(clientId, playerData);
            }
        }
        #endregion

        #region Network Sync Methods
        [ServerRpc(RequireOwnership = false)]
        public void RequestPlayerDataSyncServerRpc(ulong clientId)
        {
            if (_playerSessionData.ContainsKey(clientId))
            {
                var data = _playerSessionData[clientId];
                
                // Send individual components instead of the full object
                SyncPlayerDataClientRpc(
                    clientId,
                    data.PlayerData.PlayerName,
                    data.PlayerData.AccountLevel,
                    data.PlayerData.Currency,
                    data.PlayerData.SelectedCharacter,
                    new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { clientId }
                        }
                    }
                );
            }
        }

        [ClientRpc]
        private void SyncPlayerDataClientRpc(
            ulong clientId,
            string playerName,
            int accountLevel,
            int currency,
            string selectedCharacter,
            ClientRpcParams clientRpcParams = default)
        {
            if (!_playerSessionData.ContainsKey(clientId))
            {
                _playerSessionData[clientId] = new PlayerSessionData
                {
                    ClientId = clientId,
                    PlayerData = new PlayerData(),
                    PerformanceData = new PerformanceData(),
                    RewardData = new RewardData()
                };
            }

            var playerData = _playerSessionData[clientId].PlayerData;
            playerData.PlayerName = playerName;
            playerData.AccountLevel = accountLevel;
            playerData.Currency = currency;
            playerData.SelectedCharacter = selectedCharacter;
        }
        #endregion
    }

    #region Data Classes
    [System.Serializable]
    public class GameSessionData
    {
        public string SessionId;
        public DateTime StartTime;
        public DateTime EndTime;
        public TimeSpan Duration;
        public int Seed;
        public int CurrentWave;
        public int TotalWaves;
    }

    [System.Serializable]
    public class PlayerSessionData
    {
        public ulong ClientId;
        public PlayerData PlayerData;
        public PerformanceData PerformanceData;
        public RewardData RewardData;

        public PlayerSessionData()
        {
            PlayerData = new PlayerData();
            PerformanceData = new PerformanceData();
            RewardData = new RewardData();
        }
    }

    [System.Serializable]
    public class PlayerData
    {
        public string PlayerName;
        public int AccountLevel;
        public int Currency;
        public int HighScore;
        public string SelectedCharacter;
        public List<string> UnlockedCharacters = new List<string>();
        public Dictionary<string, int> Inventory = new Dictionary<string, int>();
        public Dictionary<string, float> Upgrades = new Dictionary<string, float>();
    }

    [System.Serializable]
    public class PerformanceData
    {
        public int Score;
        public int EnemiesKilled;
        public int WavesCompleted;
        public int BuildingsBuilt;
        public int BuildingsLost;
        public float DamageDone;
        public float DamageTaken;
        public int ResourcesCollected;
        public float PlayTime;
    }

    [System.Serializable]
    public class RewardData
    {
        public int ExperienceGained;
        public int CurrencyEarned;
        public List<string> ItemsUnlocked = new List<string>();
        public Dictionary<string, int> ResourcesEarned = new Dictionary<string, int>();
    }
    #endregion
}