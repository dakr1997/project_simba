using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.AccountManagement
{
    [Serializable]
    public class AccountPerks
    {
        public const string HP_PERK = "HpBonus";
        public const string DAMAGE_PERK = "DamageBonus";
        public const string GOLD_PERK = "GoldBonus";
        public const string SPEED_PERK = "SpeedBonus";
        public const string EXP_PERK = "ExpBonus";

        private Dictionary<string, int> perkLevels = new Dictionary<string, int>();

        // Reference to owning AccountData for auto syncing

        [NonSerialized]
        private AccountData owner;

        // Modified constructor to take owner reference
        public AccountPerks(AccountData ownerAccountData)
        {
            owner = ownerAccountData;
            InitializePerks();
        }

        private void InitializePerks()
        {
            Debug.Log("[AccountPerks] Initializing perks");
            perkLevels[HP_PERK] = 0;
            perkLevels[DAMAGE_PERK] = 0;
            perkLevels[GOLD_PERK] = 0;
            perkLevels[SPEED_PERK] = 0;
            perkLevels[EXP_PERK] = 0;
        }

        public int GetPerkLevel(string perkType) =>
            perkLevels.ContainsKey(perkType) ? perkLevels[perkType] : 0;

        public void SetPerkLevel(string perkType, int level)
        {
            if (perkLevels.ContainsKey(perkType))
            {
                perkLevels[perkType] = Mathf.Max(0, level);
                owner?.NotifyDataChanged();
            }
        }

        public void UpgradePerk(string perkType)
        {
            if (perkLevels.ContainsKey(perkType))
            {
                perkLevels[perkType]++;
                owner?.NotifyDataChanged(); // Notify AccountData that something changed
            }
        }

        public float GetHpMultiplier() => 1f + (GetPerkLevel(HP_PERK) * 0.1f);
        public float GetDamageMultiplier() => 1f + (GetPerkLevel(DAMAGE_PERK) * 0.1f);
        public float GetGoldMultiplier() => 1f + (GetPerkLevel(GOLD_PERK) * 0.05f);
        public float GetSpeedMultiplier() => 1f + (GetPerkLevel(SPEED_PERK) * 0.05f);
        public float GetExpMultiplier() => 1f + (GetPerkLevel(EXP_PERK) * 0.05f);

        public Dictionary<string, float> ToDictionary()
        {
            var dict = new Dictionary<string, float>();
            foreach (var perk in perkLevels)
            {
                dict[perk.Key] = perk.Value;
            }
            return dict;
        }

        public void FromDictionary(Dictionary<string, float> dict)
        {
            if (dict == null || dict.Count == 0)
            {
                Debug.Log("[AccountPerks] FromDictionary: Empty or null dictionary provided. Keeping default perks.");
                return; // Don't reset perks if dictionary is empty
            }

            foreach (var perk in dict)
            {
                if (perkLevels.ContainsKey(perk.Key))
                {
                    perkLevels[perk.Key] = Mathf.RoundToInt(perk.Value);
                }
            }
        }

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

        public PerkData ToNetworkData() => new PerkData(this);
    }
}
