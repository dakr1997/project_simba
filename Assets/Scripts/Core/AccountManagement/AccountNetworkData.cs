using System;
using UnityEngine;
using Unity.Netcode;

namespace Core.AccountManagement
{
    // Simple serializable class for one-time network transmission
    [Serializable]
    public class AccountNetworkData : INetworkSerializable
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

        // Parameterless constructor required for network serialization
        public AccountNetworkData()
        {
        }

        // Constructor to create from AccountData
        public AccountNetworkData(string id, AccountData accountData)
        {
            uniqueID = id;
            accountName = accountData.AccountName;
            accountLevel = accountData.AccountLevel;
            
            // Get perk levels
            var perks = accountData.Perks;
            hpPerkLevel = perks.GetPerkLevel(AccountPerks.HP_PERK);
            damagePerkLevel = perks.GetPerkLevel(AccountPerks.DAMAGE_PERK);
            goldPerkLevel = perks.GetPerkLevel(AccountPerks.GOLD_PERK);
            speedPerkLevel = perks.GetPerkLevel(AccountPerks.SPEED_PERK);
            expPerkLevel = perks.GetPerkLevel(AccountPerks.EXP_PERK);
        }

        // Implement INetworkSerializable
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // If writing, sanitize null strings to empty strings
            if (serializer.IsWriter)
            {
                if (accountName == null)
                    accountName = "";
                if (uniqueID == null)
                    uniqueID = "";
            }
            
            serializer.SerializeValue(ref accountName);
            serializer.SerializeValue(ref uniqueID);
            serializer.SerializeValue(ref accountLevel);
            serializer.SerializeValue(ref hpPerkLevel);
            serializer.SerializeValue(ref damagePerkLevel);
            serializer.SerializeValue(ref goldPerkLevel);
            serializer.SerializeValue(ref speedPerkLevel);
            serializer.SerializeValue(ref expPerkLevel);
        }

        // Quick access to multipliers for the server
        public float GetHpMultiplier() => 1f + (hpPerkLevel * 0.1f);
        public float GetDamageMultiplier() => 1f + (damagePerkLevel * 0.1f);
        public float GetGoldMultiplier() => 1f + (goldPerkLevel * 0.05f);
        public float GetSpeedMultiplier() => 1f + (speedPerkLevel * 0.05f);
        public float GetExpMultiplier() => 1f + (expPerkLevel * 0.05f);
    }
}