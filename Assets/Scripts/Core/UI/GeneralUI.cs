using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core.Network;
using Core.GameManagement;
using Core.AccountManagement;

/// <summary>
/// Handles the Lobby UI and player management
/// </summary>
/// 
namespace Core.UI
{
    public class GeneralUI : MonoBehaviour
    {
        [Header("Account Creation UI")]
        [SerializeField] private TMP_InputField _AccountNameInputField;
        [SerializeField] private Button _playButton;
        public static GeneralUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[GeneralUI] Initialized as singleton instance");
        }


        private void Start()
        {
            _AccountNameInputField.onValueChanged.AddListener(OnAccountNameInputFieldValueChanged);
            _playButton.onClick.AddListener(OnPlayButtonClicked);
        }



        private void OnAccountNameInputFieldValueChanged(string value)
        {
            if (ValidateAccountName())
            {
                _playButton.interactable = true;
            }
            else
            {
                _playButton.interactable = false;
            }
        }

        private bool ValidateAccountName()
        {
            if (_AccountNameInputField.GetComponent<TMP_InputField>().text.Length < 3 ||
                _AccountNameInputField.GetComponent<TMP_InputField>().text.Length > 20)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void OnPlayButtonClicked()
        { 
            AccountManager.Instance.CreateAccount(_AccountNameInputField.text);
        }
    }
}