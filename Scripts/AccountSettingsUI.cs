using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class AccountSettingsUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject settingsPanel;

    [Header("Avatar Selection")]
    public AvatarDatabase avatarDatabase;
    public Image avatarPreviewImage;
    public TMP_Text avatarNameText;
    public Slider avatarSlider;

    [Header("Player Info Display")]
    public TMP_Text usernameText;
    public TMP_Text bankrollText;

    [Header("Settings Toggles")]
    public Toggle soundToggle;
    public Toggle musicToggle;
    public Slider soundVolumeSlider;
    public Slider musicVolumeSlider;

    [Header("Buttons")]
    public Button saveButton;
    public Button closeButton;
    public Button logoutButton;

    [Header("Settings")]
    public string loginSceneName = "Login";

    private int currentAvatarIndex = 0;

    void Start()
    {
        // Set up avatar slider
        if (avatarSlider != null)
        {
            avatarSlider.wholeNumbers = true;
            avatarSlider.onValueChanged.AddListener(OnAvatarSliderChanged);
        }

        if (saveButton != null)
            saveButton.onClick.AddListener(SaveSettings);

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);

        if (logoutButton != null)
            logoutButton.onClick.AddListener(Logout);

        // Set up toggle/slider listeners
        if (soundToggle != null)
            soundToggle.onValueChanged.AddListener(OnSoundToggleChanged);

        if (musicToggle != null)
            musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);

        // Hide panel by default
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void OpenPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        LoadCurrentSettings();
    }

    public void ClosePanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    void LoadCurrentSettings()
    {
        if (AccountManager.Instance == null || !AccountManager.Instance.IsLoggedIn)
            return;

        var player = AccountManager.Instance.CurrentPlayer;

        // Setup avatar slider
        if (avatarSlider != null && avatarDatabase != null)
        {
            avatarSlider.minValue = 0;
            avatarSlider.maxValue = avatarDatabase.AvatarCount - 1;
            avatarSlider.value = player.AvatarId;
        }

        // Load avatar
        currentAvatarIndex = player.AvatarId;
        UpdateAvatarDisplay();

        // Load player info
        if (usernameText != null)
            usernameText.text = player.DisplayName;

        if (bankrollText != null)
            bankrollText.text = $"${player.Bankroll:N0}";

        // Load settings
        if (soundToggle != null)
            soundToggle.isOn = player.SoundEnabled;

        if (musicToggle != null)
            musicToggle.isOn = player.MusicEnabled;

        if (soundVolumeSlider != null)
            soundVolumeSlider.value = player.SoundVolume;

        if (musicVolumeSlider != null)
            musicVolumeSlider.value = player.MusicVolume;
    }

    // === AVATAR SELECTION ===

    void OnAvatarSliderChanged(float value)
    {
        currentAvatarIndex = Mathf.RoundToInt(value);
        UpdateAvatarDisplay();
    }

    void UpdateAvatarDisplay()
    {
        if (avatarDatabase == null) return;

        var avatar = avatarDatabase.GetAvatar(currentAvatarIndex);
        if (avatar == null) return;

        if (avatarPreviewImage != null)
        {
            avatarPreviewImage.sprite = avatar.AvatarSprite;
            avatarPreviewImage.color = avatar.AvatarColor;
        }

        if (avatarNameText != null)
            avatarNameText.text = avatar.AvatarName;
    }

    // === SETTINGS ===

    void OnSoundToggleChanged(bool isOn)
    {
        // Apply immediately for preview
        AudioListener.volume = isOn ? 1f : 0f;
    }

    void OnMusicToggleChanged(bool isOn)
    {
        // Music toggle logic here if you have a music manager
    }

    public void SaveSettings()
    {
        if (AccountManager.Instance == null || !AccountManager.Instance.IsLoggedIn)
            return;

        var player = AccountManager.Instance.CurrentPlayer;

        // Save avatar
        AccountManager.Instance.SetAvatar(currentAvatarIndex);

        // Save settings
        player.SoundEnabled = soundToggle != null ? soundToggle.isOn : true;
        player.MusicEnabled = musicToggle != null ? musicToggle.isOn : true;
        player.SoundVolume = soundVolumeSlider != null ? soundVolumeSlider.value : 1f;
        player.MusicVolume = musicVolumeSlider != null ? musicVolumeSlider.value : 0.5f;

        AccountManager.Instance.UpdateSettings(player);

        Debug.Log("Settings saved!");

        // Update lobby display if needed
        var lobbyManager = FindObjectOfType<LobbyManager>();
        if (lobbyManager != null)
        {
            // Refresh player display
            lobbyManager.SendMessage("UpdatePlayerInfo", SendMessageOptions.DontRequireReceiver);
        }

        ClosePanel();
    }

    // === LOGOUT ===

    public void Logout()
    {
        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.Logout();
        }

        SceneManager.LoadScene(loginSceneName);
    }
}
