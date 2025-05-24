using UnityEngine;
using System;
using System.Collections.Generic;
using Core.GameManagement; 

namespace Core.AccountManagement
{
    public class AccountManager : MonoBehaviour
    {
        public static AccountManager Instance;

        [Header("UI References")]
        [SerializeField] private GameObject accountCreationPanel;
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private TMPro.TMP_InputField nameInput;
        [SerializeField] private UnityEngine.UI.Button playButton;

        private string uniqueID;
        private AccountData currentAccountData;

        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[AccountManager] Initialized as singleton instance");
        }

        void Start()
        {
            // Check if account exists
            uniqueID = PlayerPrefs.GetString("UniqueID", "");

            if (string.IsNullOrEmpty(uniqueID))
            {
                // First time - show account creation
                Debug.Log("No account found, showing account creation.");
                ShowAccountCreation();
            }
            else
            {
                // Load existing account
                Debug.Log("Account found, loading account.");
                mainMenuPanel.SetActive(true);
                LoadAccount();
            }
        }

        void ShowAccountCreation()
        {
            Debug.Log("Showing account creation panel.");
            accountCreationPanel.SetActive(true);
            mainMenuPanel.SetActive(false);
            playButton.interactable = false;
        }

        public void CreateAccount(string AccountName)
        {
            uniqueID = Guid.NewGuid().ToString();

            currentAccountData = new AccountData
            {
                UniqueID = uniqueID,
                AccountName = AccountName,
                AccountLevel = 1,
                Currency = 0,
            };

            currentAccountData.SavePerks();

            // Subscribe AFTER currentAccountData assigned
            currentAccountData.OnAccountDataChanged += SaveAccount;

            SaveAccount();

            accountCreationPanel.SetActive(false);
            mainMenuPanel.SetActive(true);
        }


        void LoadAccount()
        {
            currentAccountData = new AccountData
            {
                AccountName = PlayerPrefs.GetString("AccountName"),
                AccountLevel = PlayerPrefs.GetInt("AccountLevel", 1),
                Currency = PlayerPrefs.GetInt("Currency", 0),
                Upgrades = LoadUpgrades()
            };

            currentAccountData.Perks.FromDictionary(currentAccountData.Upgrades);

            // Subscribe AFTER currentAccountData assigned
            currentAccountData.OnAccountDataChanged += SaveAccount;

            accountCreationPanel.SetActive(false);
            mainMenuPanel.SetActive(true);
            playButton.interactable = true;
        }

        void SaveAccount()
        {
            Debug.Log($"Saving account for {currentAccountData.AccountName} with ID: {uniqueID}");

            // Sync perks data back to Upgrades dictionary before saving
            currentAccountData.SavePerks();

            PlayerPrefs.SetString("UniqueID", uniqueID);
            PlayerPrefs.SetString("AccountName", currentAccountData.AccountName);
            PlayerPrefs.SetInt("AccountLevel", currentAccountData.AccountLevel);
            PlayerPrefs.SetInt("Currency", currentAccountData.Currency);
            SaveUpgrades(currentAccountData.Upgrades);
            PlayerPrefs.Save();
        }

        Dictionary<string, float> LoadUpgrades()
        {
            var upgrades = new Dictionary<string, float>();
            string upgradeString = PlayerPrefs.GetString("Upgrades", "");

            if (!string.IsNullOrEmpty(upgradeString))
            {
                string[] pairs = upgradeString.Split(',');
                foreach (string pair in pairs)
                {
                    string[] kv = pair.Split(':');
                    if (kv.Length == 2)
                    {
                        upgrades[kv[0]] = float.Parse(kv[1]);
                    }
                }
            }

            return upgrades;
        }

        void SaveUpgrades(Dictionary<string, float> upgrades)
        {
            Debug.Log("[AccountManager] Saving upgrades to PlayerPrefs");
            List<string> pairs = new List<string>();
            foreach (var upgrade in upgrades)
            {
                pairs.Add($"{upgrade.Key}:{upgrade.Value}");
            }
            PlayerPrefs.SetString("Upgrades", string.Join(",", pairs));
        }

        public AccountData GetAccountData() => currentAccountData;

        // New helper methods for perk management
        public void UpgradePerk(string perkType, int cost)
        {
            if (currentAccountData.Currency >= cost)
            {
                currentAccountData.Currency -= cost;
                currentAccountData.Perks.UpgradePerk(perkType);
                SaveAccount();
                Debug.Log($"Upgraded {perkType} perk");
            }
            else
            {
                Debug.Log("Not enough currency!");
            }
        }

        public void AddCurrency(int amount)
        {
            currentAccountData.Currency += amount;
            SaveAccount();
        }

        public void AddAccountExp(int exp)
        {
            // Simple level up system - adjust as needed
            currentAccountData.AccountLevel += exp / 100;
            SaveAccount();
        }

        // For network transmission
        public AccountPerks.PerkData GetPerkDataForNetwork()
        {
            return currentAccountData.Perks.ToNetworkData();
        }
    }
}