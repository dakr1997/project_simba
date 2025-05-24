using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Core.AccountManagement;

public class PerksShop : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform _perkContainer;
    [SerializeField] private GameObject _perkEntryPrefab;
    [SerializeField] private GameObject _perkDescriptionPrefab;
    [SerializeField] private TextMeshProUGUI _currencyText;
    [SerializeField] private Vector2 _descriptionPanelOffset = new Vector2(50, 50); // Offset for description panel position
    private bool isHovering = false; // Track if hovering over a perk
    private string currentHoverPerk = ""; // Track currently hovered perk type
    
    [Header("Perk Settings")]
    [SerializeField] private int basePerkCost = 100;
    [SerializeField] private float costMultiplier = 1.5f; // Cost increases per level
    
    private AccountManager accountManager;
    private AccountData accountData;
    private GameObject currentDescriptionPanel;
    private Dictionary<string, PerkEntry> perkEntries = new Dictionary<string, PerkEntry>();
    
    // Perk info storage
    private Dictionary<string, PerkInfo> perkInfos = new Dictionary<string, PerkInfo>
    {
        { AccountPerks.HP_PERK, new PerkInfo("Health Bonus", "Increases your maximum health by 10% per level") },
        { AccountPerks.DAMAGE_PERK, new PerkInfo("Damage Bonus", "Increases your damage output by 10% per level") },
        { AccountPerks.GOLD_PERK, new PerkInfo("Gold Bonus", "Increases gold earned by 5% per level") },
        { AccountPerks.SPEED_PERK, new PerkInfo("Speed Bonus", "Increases movement speed by 5% per level") },
        { AccountPerks.EXP_PERK, new PerkInfo("EXP Bonus", "Increases experience gained by 5% per level") }
    };
    
    private class PerkInfo
    {
        public string name;
        public string description;
        
        public PerkInfo(string name, string description)
        {
            this.name = name;
            this.description = description;
        }
    }
    
    private class PerkEntry
    {
        public GameObject gameObject;
        public Button button;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI costText;
        public Image icon;
        public string perkType;
    }
    
    void Start()
    {
        accountManager = AccountManager.Instance;
        if (accountManager == null)
        {
            Debug.LogError("[PerksShop] AccountManager not found!");
            return;
        }
        
        accountData = accountManager.GetAccountData();
        if (accountData == null)
        {
            Debug.LogError("[PerksShop] AccountData is null!");
            return;
        }
        
        InitializePerkShop();
        UpdateCurrencyDisplay();
        
        // Subscribe to account changes
        accountData.OnAccountDataChanged += OnAccountDataChanged;
    }

    private void Update()
    {
        if (isHovering && currentDescriptionPanel.activeSelf)
        {
            // Get current mouse position in screen space
            Vector2 mousePos = Input.mousePosition;
            UpdateDescriptionPosition(mousePos);
        }
    }
    
    void OnDestroy()
    {
        if (accountData != null)
        {
            accountData.OnAccountDataChanged -= OnAccountDataChanged;
        }
    }
    
    private void InitializePerkShop()
    {
        // Validate references
        if (_perkContainer == null || _perkEntryPrefab == null)
        {
            Debug.LogError("[PerksShop] Missing required UI references!");
            return;
        }
        
        // Create description panel if prefab is provided
        if (_perkDescriptionPrefab != null)
        {
            currentDescriptionPanel = Instantiate(_perkDescriptionPrefab, transform);
            currentDescriptionPanel.SetActive(false);
        }
        
        // Create perk entries
        string[] perkTypes = {
            AccountPerks.HP_PERK,
            AccountPerks.DAMAGE_PERK,
            AccountPerks.GOLD_PERK,
            AccountPerks.SPEED_PERK,
            AccountPerks.EXP_PERK
        };

        foreach (string perkType in perkTypes)
        {
            CreatePerkEntry(perkType);
            Debug.Log($"[PerksShop] Created perk entry for {perkType}");
        }
    }
    
    private void CreatePerkEntry(string perkType)
    {
        GameObject entryGO = Instantiate(_perkEntryPrefab, _perkContainer);
        PerkEntry entry = new PerkEntry
        {
            gameObject = entryGO,
            perkType = perkType
        };
        
        // Get components
        entry.button = entryGO.GetComponent<Button>();
        entry.levelText = entryGO.transform.Find("_perkLevel")?.GetComponent<TextMeshProUGUI>();
        entry.costText = entryGO.transform.Find("_perkCost")?.GetComponent<TextMeshProUGUI>();
        entry.icon = entryGO.transform.Find("_perkIcon")?.GetComponent<Image>();
        
        // Set initial values
        UpdatePerkEntry(entry);
        
        // Setup button click
        if (entry.button != null)
        {
            entry.button.onClick.AddListener(() => OnPerkButtonClicked(perkType));
        }
        
        // Setup hover events
        EventTrigger trigger = entryGO.GetComponent<EventTrigger>() ?? entryGO.AddComponent<EventTrigger>();
        
        // Mouse enter
        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { OnPerkHoverEnter(perkType, (PointerEventData)data); });
        trigger.triggers.Add(enterEntry);
        
        // Mouse exit
        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => { OnPerkHoverExit(); });
        trigger.triggers.Add(exitEntry);
        
        perkEntries[perkType] = entry;

    }
    
    private void UpdatePerkEntry(PerkEntry entry)
    {
        int currentLevel = accountData.Perks.GetPerkLevel(entry.perkType);
        int cost = CalculatePerkCost(currentLevel);
        
        // Update level text
        if (entry.levelText != null)
        {
            entry.levelText.text = $"Lv.{currentLevel}";
        }
        
        // Update cost text
        if (entry.costText != null)
        {
            entry.costText.text = cost.ToString();
        }
        
        // Update button interactability
        if (entry.button != null)
        {
            entry.button.interactable = accountData.Currency >= cost;
        }
    }
    
    private int CalculatePerkCost(int currentLevel)
    {
        // Simple cost scaling: baseCost * (multiplier ^ level)
        return Mathf.RoundToInt(basePerkCost * Mathf.Pow(costMultiplier, currentLevel));
    }
    
    private void OnPerkButtonClicked(string perkType)
    {
        int currentLevel = accountData.Perks.GetPerkLevel(perkType);
        int cost = CalculatePerkCost(currentLevel);
        
        if (accountData.Currency >= cost)
        {
            accountManager.UpgradePerk(perkType, cost);
            Debug.Log($"[PerksShop] Purchased {perkType} upgrade for {cost} currency");
            
            // Update UI will happen through OnAccountDataChanged callback
        }
        else
        {
            Debug.Log($"[PerksShop] Not enough currency to purchase {perkType}");
        }
    }
    
    private void OnPerkHoverEnter(string perkType, PointerEventData eventData)
    {
        if (currentDescriptionPanel == null || !perkInfos.ContainsKey(perkType))
            return;

        currentHoverPerk = perkType;
        isHovering = true;

        // Only update content if this is a new perk or panel was hidden
        if (!currentDescriptionPanel.activeSelf || 
            currentDescriptionPanel.GetComponent<PerkDescriptionPanel>()?.CurrentPerk != perkType)
        {
            // Show and update the panel
            currentDescriptionPanel.SetActive(true);
            UpdateDescriptionText(perkType);
            
            // Track which perk this panel is showing
            var panel = currentDescriptionPanel.GetComponent<PerkDescriptionPanel>();
            if (panel == null) panel = currentDescriptionPanel.AddComponent<PerkDescriptionPanel>();
            panel.CurrentPerk = perkType;
        }

        // Update position immediately
        UpdateDescriptionPosition(eventData.position);
    }

    private void UpdateDescriptionPosition(Vector2 mousePosition)
    {
        if (currentDescriptionPanel == null || !isHovering) return;

        RectTransform rect = currentDescriptionPanel.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.position = mousePosition + _descriptionPanelOffset;
            
            // Keep panel on screen
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                Vector2 anchoredPos = rect.anchoredPosition;
                
                // Clamp to canvas bounds
                float rightEdge = anchoredPos.x + rect.sizeDelta.x;
                float bottomEdge = anchoredPos.y - rect.sizeDelta.y;
                
                if (rightEdge > canvasRect.sizeDelta.x / 2)
                    anchoredPos.x -= rightEdge - canvasRect.sizeDelta.x / 2;
                if (bottomEdge < -canvasRect.sizeDelta.y / 2)
                    anchoredPos.y += -canvasRect.sizeDelta.y / 2 - bottomEdge;
                
                rect.anchoredPosition = anchoredPos;
            }
        }
    }

    
    private void OnPerkHoverExit()
    {
        isHovering = false;
        currentHoverPerk = "";
        if (currentDescriptionPanel != null)
        {
            currentDescriptionPanel.SetActive(false);
        }
    }
    
    private void OnAccountDataChanged()
    {
        // Update all perk entries
        foreach (var kvp in perkEntries)
        {
            UpdatePerkEntry(kvp.Value);
        }
        
        // Update currency display
        UpdateCurrencyDisplay();
        
        // Update description panel if it's showing
        if (currentDescriptionPanel != null && currentDescriptionPanel.activeSelf)
        {
            var panel = currentDescriptionPanel.GetComponent<PerkDescriptionPanel>();
            if (panel != null && !string.IsNullOrEmpty(panel.CurrentPerk))
            {
                UpdateDescriptionText(panel.CurrentPerk);
            }
        }
    }
    
    private void UpdateCurrencyDisplay()
    {
        if (_currencyText != null)
        {
            _currencyText.text = $"Currency: {accountData.Currency}";
        }
    }
    
    private void UpdateDescriptionText(string perkType)
    {
        if (!perkInfos.ContainsKey(perkType)) return;
        
        PerkInfo info = perkInfos[perkType];
        TextMeshProUGUI nameText = currentDescriptionPanel.transform.Find("perkName")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI descText = currentDescriptionPanel.transform.Find("perkDescription")?.GetComponent<TextMeshProUGUI>();
        
        if (nameText != null)
            nameText.text = info.name;
        if (descText != null)
        {
            int currentLevel = accountData.Perks.GetPerkLevel(perkType);
            descText.text = $"{info.description}\n\nCurrent Level: {currentLevel}";
        }
    }
}

public class PerkDescriptionPanel : MonoBehaviour
{
    public string CurrentPerk { get; set; }
}