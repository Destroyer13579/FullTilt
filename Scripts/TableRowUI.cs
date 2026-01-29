using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TableRowUI : MonoBehaviour
{
    [Header("Text References")]
    public TMP_Text tableNameText;
    public TMP_Text stakesText;
    public TMP_Text typeText;
    public TMP_Text playersText;
    public TMP_Text avgPotText;

    [Header("Visual")]
    public Image backgroundImage;
    public Color normalColor = new Color(1f, 1f, 1f, 1f);
    public Color highlightColor = new Color(0.9f, 0.95f, 1f, 1f);
    public Color fullTableColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    private TableData tableData;
    private LobbyManager lobbyManager;
    private Button button;

    void Awake()
    {
        // Try to find references if not assigned
        if (tableNameText == null)
            tableNameText = transform.Find("TableName")?.GetComponent<TMP_Text>();
        if (stakesText == null)
            stakesText = transform.Find("Stakes")?.GetComponent<TMP_Text>();
        if (typeText == null)
            typeText = transform.Find("Type")?.GetComponent<TMP_Text>();
        if (playersText == null)
            playersText = transform.Find("Players")?.GetComponent<TMP_Text>();
        if (avgPotText == null)
            avgPotText = transform.Find("AvgPot")?.GetComponent<TMP_Text>();

        button = GetComponent<Button>();
        backgroundImage = GetComponent<Image>();
    }

    public void Initialize(TableData data, LobbyManager manager)
    {
        tableData = data;
        lobbyManager = manager;

        // Set up button click
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        UpdateDisplay(data);
    }

    public void UpdateDisplay(TableData data)
    {
        tableData = data;

        if (tableNameText != null)
            tableNameText.text = data.TableName;

        if (stakesText != null)
            stakesText.text = data.Stakes.Name;

        if (typeText != null)
            typeText.text = data.GetTypeDisplay();

        if (playersText != null)
        {
            playersText.text = data.GetPlayerCountDisplay();
            
            // Color code based on fullness
            if (data.CurrentPlayers >= data.MaxPlayers)
                playersText.color = Color.red;
            else if (data.CurrentPlayers >= data.MaxPlayers - 1)
                playersText.color = new Color(0.8f, 0.5f, 0f);  // Orange
            else if (data.CurrentPlayers > 0)
                playersText.color = new Color(0f, 0.6f, 0f);    // Green
            else
                playersText.color = Color.gray;
        }

        if (avgPotText != null)
            avgPotText.text = data.GetAvgPotDisplay();

        // Update background color
        if (backgroundImage != null)
        {
            if (data.CurrentPlayers >= data.MaxPlayers)
                backgroundImage.color = fullTableColor;
            else
                backgroundImage.color = normalColor;
        }
    }

    void OnClick()
    {
        if (lobbyManager != null && tableData != null)
        {
            lobbyManager.OnTableClicked(tableData);
        }
    }

    // Hover effects
    public void OnPointerEnter()
    {
        if (backgroundImage != null && tableData.CurrentPlayers < tableData.MaxPlayers)
            backgroundImage.color = highlightColor;
    }

    public void OnPointerExit()
    {
        if (backgroundImage != null)
        {
            if (tableData.CurrentPlayers >= tableData.MaxPlayers)
                backgroundImage.color = fullTableColor;
            else
                backgroundImage.color = normalColor;
        }
    }
}
