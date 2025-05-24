using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Core.AccountManagement;

public class PerkPanelRuntime : MonoBehaviour
{
    private AccountData accountData;
    private Dictionary<string, Text> perkLevelTexts = new Dictionary<string, Text>();

    private readonly List<string> perkKeys = new List<string>
    {
        AccountPerks.HP_PERK,
        AccountPerks.DAMAGE_PERK,
        AccountPerks.GOLD_PERK,
        AccountPerks.SPEED_PERK,
        AccountPerks.EXP_PERK
    };

    IEnumerator Start()
    {
        // Wait until AccountManager and AccountData are ready
        while (AccountManager.Instance == null || AccountManager.Instance.GetAccountData() == null)
        {
            yield return null; // wait a frame
        }

        accountData = AccountManager.Instance.GetAccountData();

        if (accountData == null)
        {
            Debug.LogError("[PerkPanelRuntime] AccountData is null! Make sure AccountManager is initialized.");
            yield break;
        }

        // Ensure perks are synced from upgrades dictionary
        accountData.Perks.FromDictionary(accountData.Upgrades);

        // Subscribe to data changed event to update UI when perks or data update
        accountData.OnAccountDataChanged += RefreshAllPerkLevels;

        CreateUI();
    }

    void OnDestroy()
    {
        // Unsubscribe event when this object is destroyed to avoid memory leaks
        if (accountData != null)
            accountData.OnAccountDataChanged -= RefreshAllPerkLevels;
    }

    void CreateUI()
    {
        // Create Canvas
        var canvasGO = new GameObject("PerkCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Create Panel
        var panelGO = new GameObject("PerkPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(400, 300);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        var panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Vertical layout group for buttons
        var layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8;

        // Content size fitter
        var fitter = panelGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Create a button for each perk
        foreach (var perkKey in perkKeys)
        {
            GameObject buttonGO = CreatePerkButton(perkKey);
            buttonGO.transform.SetParent(panelGO.transform, false);
        }
    }

    GameObject CreatePerkButton(string perkKey)
    {
        // Button GameObject
        GameObject buttonGO = new GameObject(perkKey + "Button");
        var buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(350, 40);

        // Button Component
        var button = buttonGO.AddComponent<Button>();

        // Button background
        var image = buttonGO.AddComponent<Image>();
        image.color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // Text for perk name
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(buttonGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0, 0);
        labelRT.anchorMax = new Vector2(0.6f, 1);
        labelRT.offsetMin = new Vector2(10, 5);
        labelRT.offsetMax = new Vector2(0, -5);

        var labelText = labelGO.AddComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 18;
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.color = Color.white;
        labelText.text = GetPerkDisplayName(perkKey);

        // Text for level display
        GameObject levelGO = new GameObject("LevelText");
        levelGO.transform.SetParent(buttonGO.transform, false);
        var levelRT = levelGO.AddComponent<RectTransform>();
        levelRT.anchorMin = new Vector2(0.6f, 0);
        levelRT.anchorMax = new Vector2(1, 1);
        levelRT.offsetMin = new Vector2(0, 5);
        levelRT.offsetMax = new Vector2(-10, -5);

        var levelText = levelGO.AddComponent<Text>();
        levelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        levelText.fontSize = 16;
        levelText.alignment = TextAnchor.MiddleRight;
        levelText.color = Color.yellow;

        // Store level text reference for updating
        perkLevelTexts[perkKey] = levelText;
        UpdateLevelText(perkKey, levelText);

        // Button onClick event upgrades perk via AccountPerks,
        // which triggers OnAccountDataChanged event and saves automatically
        button.onClick.AddListener(() =>
        {
            accountData.Perks.UpgradePerk(perkKey);
            // No need to manually call UpdateLevelText here since event triggers it
        });

        return buttonGO;
    }

    string GetPerkDisplayName(string perkKey)
    {
        switch (perkKey)
        {
            case AccountPerks.HP_PERK: return "Health Bonus";
            case AccountPerks.DAMAGE_PERK: return "Damage Bonus";
            case AccountPerks.GOLD_PERK: return "Gold Bonus";
            case AccountPerks.SPEED_PERK: return "Speed Bonus";
            case AccountPerks.EXP_PERK: return "Experience Bonus";
            default: return perkKey;
        }
    }

    void UpdateLevelText(string perkKey, Text levelText)
    {
        int level = accountData.Perks.GetPerkLevel(perkKey);
        levelText.text = $"Level: {level}";
    }

    // Update all perk levels in UI - called on OnAccountDataChanged event
    void RefreshAllPerkLevels()
    {
        foreach (var kvp in perkLevelTexts)
        {
            UpdateLevelText(kvp.Key, kvp.Value);
        }
    }
}
