using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.AccountManagement
{
    [Serializable]
    public class AccountPerks
    {
        // Perk types as constants for easy reference
        public const string HP_PERK = "HpBonus";
        public const string DAMAGE_PERK = "DamageBonus";
        public const string GOLD_PERK = "GoldBonus";
        public const string SPEED_PERK = "SpeedBonus";
        public const string EXP_PERK = "ExpBonus";

        // Dictionary to store perk levels
        private Dictionary<string, int> perkLevels = new Dictionary<string, int>();

        // Constructor
        public AccountPerks()
        {
            InitializePerks();
        }

        private void InitializePerks()
        {
            // Initialize all perks at level 0
            perkLevels[HP_PERK] = 0;
            perkLevels[DAMAGE_PERK] = 0;
            perkLevels[GOLD_PERK] = 0;
            perkLevels[SPEED_PERK] = 0;
            perkLevels[EXP_PERK] = 0;
        }

        // Get perk level
        public int GetPerkLevel(string perkType)
        {
            return perkLevels.ContainsKey(perkType) ? perkLevels[perkType] : 0;
        }

        // Set perk level
        public void SetPerkLevel(string perkType, int level)
        {
            if (perkLevels.ContainsKey(perkType))
            {
                perkLevels[perkType] = Mathf.Max(0, level);
            }
        }

        // Upgrade a perk by 1 level
        public void UpgradePerk(string perkType)
        {
            if (perkLevels.ContainsKey(perkType))
            {
                perkLevels[perkType]++;
            }
        }

        // Calculate actual bonus values
        public float GetHpMultiplier() => 1f + (GetPerkLevel(HP_PERK) * 0.1f);
        public float GetDamageMultiplier() => 1f + (GetPerkLevel(DAMAGE_PERK) * 0.1f);
        public float GetGoldMultiplier() => 1f + (GetPerkLevel(GOLD_PERK) * 0.05f);
        public float GetSpeedMultiplier() => 1f + (GetPerkLevel(SPEED_PERK) * 0.05f);
        public float GetExpMultiplier() => 1f + (GetPerkLevel(EXP_PERK) * 0.05f);

        // Convert to dictionary for saving/loading (compatible with your existing system)
        public Dictionary<string, float> ToDictionary()
        {
            var dict = new Dictionary<string, float>();
            foreach (var perk in perkLevels)
            {
                dict[perk.Key] = perk.Value;
            }
            return dict;
        }

        // Load from dictionary (compatible with your existing system)
        public void FromDictionary(Dictionary<string, float> dict)
        {
            InitializePerks();
            foreach (var perk in dict)
            {
                if (perkLevels.ContainsKey(perk.Key))
                {
                    perkLevels[perk.Key] = Mathf.RoundToInt(perk.Value);
                }
            }
        }

        // For network transmission - create a simple serializable data structure
        [Serializable]
        public class PerkData
        {
            public int hpLevel;
            public int damageLevel;
            public int goldLevel;
            public int speedLevel;
            public int expLevel;

            public PerkData(AccountPerks perks)
            {
                hpLevel = perks.GetPerkLevel(HP_PERK);
                damageLevel = perks.GetPerkLevel(DAMAGE_PERK);
                goldLevel = perks.GetPerkLevel(GOLD_PERK);
                speedLevel = perks.GetPerkLevel(SPEED_PERK);
                expLevel = perks.GetPerkLevel(EXP_PERK);
            }
        }

        // Convert to network-friendly format
        public PerkData ToNetworkData()
        {
            return new PerkData(this);
        }
    }
}