using UnityEngine;

public class PanelToggler : MonoBehaviour
{
    // Optional key to toggle the panel with keyboard
    [SerializeField] private KeyCode toggleKey = KeyCode.Space;
    [SerializeField] private GameObject panelToToggle;
    
    // Optional flag to determine if panel starts active or inactive
    [SerializeField] private bool startActive = true;
    
    private void Start()
    {
        // Set initial state based on the startActive flag
        panelToToggle.SetActive(startActive);
    }
    
    private void Update()
    {
        // Check if toggle key was pressed
        if (Input.GetKeyDown(toggleKey))
        {
            TogglePanel();
        }
    }
    
    // Public method to toggle panel state
    public void TogglePanel()
    {
        panelToToggle.SetActive(!panelToToggle.activeSelf);
    }
    
}