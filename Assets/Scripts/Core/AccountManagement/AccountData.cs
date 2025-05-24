using System;
using System.Collections.Generic;
using UnityEngine;
using Core.AccountManagement;

[Serializable]
public class AccountData
{
    public string UniqueID;
    public string AccountName;
    public int AccountLevel;
    public int Currency;

    public event Action OnAccountDataChanged;

    public Dictionary<string, float> Upgrades = new Dictionary<string, float>
    {
        { AccountPerks.HP_PERK, 0 },
        { AccountPerks.DAMAGE_PERK, 0 },
        { AccountPerks.GOLD_PERK, 0 },
        { AccountPerks.SPEED_PERK, 0 },
        { AccountPerks.EXP_PERK, 0 }
    };

    private AccountPerks perks;

    public AccountPerks Perks
    {
        get
        {
            if (perks == null)
            {
                perks = new AccountPerks(this);
                perks.FromDictionary(Upgrades);
            }
            return perks;
        }
    }

    // Sync perks dictionary but DO NOT invoke event here!
    public void SavePerks()
    {
        if (perks != null)
        {
            Upgrades = perks.ToDictionary();
        }
    }

    // Call this method to notify changes externally
    public void NotifyDataChanged()
    {
        OnAccountDataChanged?.Invoke();
    }
}
