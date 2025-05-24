using System;
using System.Collections.Generic;

namespace Core.AccountManagement
{
    [Serializable]
    public class AccountData
    {
        public string UniqueID; // Unique identifier for the account
        public string AccountName;
        public int AccountLevel;
        public int Currency;
        public Dictionary<string, float> Upgrades = new Dictionary<string, float>();
        
        private AccountPerks perks;

        public AccountPerks Perks
        {
            get
            {
                if (perks == null)
                {
                    perks = new AccountPerks();
                    perks.FromDictionary(Upgrades);
                }
                return perks;
            }
        }

        // Save perks back to upgrades dictionary
        public void SavePerks()
        {
            if (perks != null)
            {
                Upgrades = perks.ToDictionary();
            }
        }
    }
}