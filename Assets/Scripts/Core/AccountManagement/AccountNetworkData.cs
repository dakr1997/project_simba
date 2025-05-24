using System;
using UnityEngine;

namespace Core.AccountManagement
{
    // Simple serializable class for one-time network transmission
    [Serializable]
    public class AccountNetworkData
    {
        public string accountName;
        public string uniqueID;
        public int accountLevel;
        
        // Perk levels - kept simple for network efficiency
        public int hpPerkLevel;
        public int damagePerkLevel;
        public int goldPerkLevel;
        public int speedPerkLevel;
        public int expPerkLevel;

        // Constructor to create from PlayerData
        public AccountNetworkData(string id, AccountData accountData)
        {
            uniqueID = id;
            playerName = accountData.AccountName;
            accountLevel = accountData.AccountLevel;
            
            // Get perk levels
            var perks = accountDataPerks;
            hpPerkLevel = perks.GetPerkLevel(AccountPerks.HP_PERK);
            damagePerkLevel = perks.GetPerkLevel(AccountPerks.DAMAGE_PERK);
            goldPerkLevel = perks.GetPerkLevel(AccountPerks.GOLD_PERK);
            speedPerkLevel = perks.GetPerkLevel(AccountPerks.SPEED_PERK);
            expPerkLevel = perks.GetPerkLevel(AccountPerks.EXP_PERK);
        }

        // Quick access to multipliers for the server
        public float GetHpMultiplier() => 1f + (hpPerkLevel * 0.1f);
        public float GetDamageMultiplier() => 1f + (damagePerkLevel * 0.1f);
        public float GetGoldMultiplier() => 1f + (goldPerkLevel * 0.05f);
        public float GetSpeedMultiplier() => 1f + (speedPerkLevel * 0.05f);
        public float GetExpMultiplier() => 1f + (expPerkLevel * 0.05f);
    }
}