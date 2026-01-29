using UnityEngine;
using UnityEngine.UI;

public class AvatarDisplay : MonoBehaviour
{
    public AvatarDatabase avatarDatabase;
    public Image avatarImage;
    
    private int currentAvatarIndex = 0;

    void Awake()
    {
        if (avatarImage == null)
            avatarImage = GetComponent<Image>();
    }

    public void SetAvatar(int avatarIndex)
    {
        currentAvatarIndex = avatarIndex;
        UpdateDisplay();
    }

    public void SetAvatarFromAccount()
    {
        if (AccountManager.Instance != null && AccountManager.Instance.IsLoggedIn)
        {
            SetAvatar(AccountManager.Instance.CurrentPlayer.AvatarId);
        }
    }

    void UpdateDisplay()
    {
        if (avatarDatabase == null || avatarImage == null) return;

        var avatar = avatarDatabase.GetAvatar(currentAvatarIndex);
        if (avatar != null)
        {
            avatarImage.sprite = avatar.AvatarSprite;
            avatarImage.color = avatar.AvatarColor;
        }
    }

    // Call this to get a random avatar (for AI)
    public static int GetRandomAvatarIndex(AvatarDatabase database)
    {
        if (database == null || database.AvatarCount == 0)
            return 0;
        
        return Random.Range(0, database.AvatarCount);
    }
}
