using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LoginUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject registerPanel;

    [Header("Login Fields")]
    public TMP_InputField loginUsernameField;
    public TMP_InputField loginPasswordField;
    public TMP_Text loginErrorText;
    public Button loginButton;
    public Button goToRegisterButton;

    [Header("Register Fields")]
    public TMP_InputField registerUsernameField;
    public TMP_InputField registerPasswordField;
    public TMP_InputField registerConfirmPasswordField;
    public TMP_InputField registerEmailField;
    public TMP_InputField registerDisplayNameField;
    public TMP_Text registerErrorText;
    public Button registerButton;
    public Button goToLoginButton;

    [Header("Settings")]
    public string lobbySceneName = "lobby";

    void Start()
    {
        // Subscribe to events
        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.OnLoginSuccess += HandleLoginSuccess;
            AccountManager.Instance.OnLoginFailed += HandleLoginFailed;
            AccountManager.Instance.OnRegisterSuccess += HandleRegisterSuccess;
            AccountManager.Instance.OnRegisterFailed += HandleRegisterFailed;
        }

        // Set up button listeners
        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginClicked);
        
        if (goToRegisterButton != null)
            goToRegisterButton.onClick.AddListener(ShowRegisterPanel);
        
        if (registerButton != null)
            registerButton.onClick.AddListener(OnRegisterClicked);
        
        if (goToLoginButton != null)
            goToLoginButton.onClick.AddListener(ShowLoginPanel);

        // Set password fields to hide input
        if (loginPasswordField != null)
            loginPasswordField.contentType = TMP_InputField.ContentType.Password;
        
        if (registerPasswordField != null)
            registerPasswordField.contentType = TMP_InputField.ContentType.Password;
        
        if (registerConfirmPasswordField != null)
            registerConfirmPasswordField.contentType = TMP_InputField.ContentType.Password;

        // Start with login panel
        ShowLoginPanel();
        ClearErrors();

        // Check if already logged in
        if (AccountManager.Instance != null && AccountManager.Instance.IsLoggedIn)
        {
            GoToLobby();
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.OnLoginSuccess -= HandleLoginSuccess;
            AccountManager.Instance.OnLoginFailed -= HandleLoginFailed;
            AccountManager.Instance.OnRegisterSuccess -= HandleRegisterSuccess;
            AccountManager.Instance.OnRegisterFailed -= HandleRegisterFailed;
        }
    }

    // === PANEL SWITCHING ===
    public void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (registerPanel != null) registerPanel.SetActive(false);
        ClearErrors();
    }

    public void ShowRegisterPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (registerPanel != null) registerPanel.SetActive(true);
        ClearErrors();
    }

    void ClearErrors()
    {
        if (loginErrorText != null) loginErrorText.text = "";
        if (registerErrorText != null) registerErrorText.text = "";
    }

    // === LOGIN ===
    public void OnLoginClicked()
    {
        ClearErrors();
        
        string username = loginUsernameField != null ? loginUsernameField.text : "";
        string password = loginPasswordField != null ? loginPasswordField.text : "";

        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.Login(username, password);
        }
        else
        {
            ShowLoginError("System error - AccountManager not found");
        }
    }

    void HandleLoginSuccess(PlayerAccount account)
    {
        Debug.Log($"Welcome back, {account.DisplayName}!");
        GoToLobby();
    }

    void HandleLoginFailed(string error)
    {
        ShowLoginError(error);
    }

    void ShowLoginError(string message)
    {
        if (loginErrorText != null)
        {
            loginErrorText.text = message;
            loginErrorText.color = Color.red;
        }
    }

    // === REGISTRATION ===
    public void OnRegisterClicked()
    {
        ClearErrors();

        string username = registerUsernameField != null ? registerUsernameField.text : "";
        string password = registerPasswordField != null ? registerPasswordField.text : "";
        string confirmPassword = registerConfirmPasswordField != null ? registerConfirmPasswordField.text : "";
        string email = registerEmailField != null ? registerEmailField.text : "";
        string displayName = registerDisplayNameField != null ? registerDisplayNameField.text : "";

        // Validate passwords match
        if (password != confirmPassword)
        {
            ShowRegisterError("Passwords do not match");
            return;
        }

        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.Register(username, password, email, displayName);
        }
        else
        {
            ShowRegisterError("System error - AccountManager not found");
        }
    }

    void HandleRegisterSuccess(PlayerAccount account)
    {
        Debug.Log($"Account created for {account.Username}!");
        // Auto-login happens in AccountManager, which triggers HandleLoginSuccess
    }

    void HandleRegisterFailed(string error)
    {
        ShowRegisterError(error);
    }

    void ShowRegisterError(string message)
    {
        if (registerErrorText != null)
        {
            registerErrorText.text = message;
            registerErrorText.color = Color.red;
        }
    }

    // === NAVIGATION ===
    void GoToLobby()
    {
        SceneManager.LoadScene(lobbySceneName);
    }
}
