using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AvatarDatabase", menuName = "Poker/Avatar Database")]
public class AvatarDatabase : ScriptableObject
{
    public List<AvatarData> Avatars = new List<AvatarData>();

    public AvatarData GetAvatar(int index)
    {
        if (Avatars == null || Avatars.Count == 0)
            return null;
        
        // Clamp index to valid range
        index = Mathf.Clamp(index, 0, Avatars.Count - 1);
        return Avatars[index];
    }

    public int AvatarCount => Avatars != null ? Avatars.Count : 0;

    public int GetNextIndex(int currentIndex)
    {
        if (Avatars == null || Avatars.Count == 0) return 0;
        return (currentIndex + 1) % Avatars.Count;
    }

    public int GetPreviousIndex(int currentIndex)
    {
        if (Avatars == null || Avatars.Count == 0) return 0;
        return (currentIndex - 1 + Avatars.Count) % Avatars.Count;
    }
}

[System.Serializable]
public class AvatarData
{
    public string AvatarName;
    public Sprite AvatarSprite;
    public Color AvatarColor = Color.white;  // Optional tint color
    
    // For future 3D support
    // public GameObject AvatarPrefab;
}
