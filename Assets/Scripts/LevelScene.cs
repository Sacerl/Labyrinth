using UnityEngine;

// Sits in every level scene (Level1 … Level6) and builds that level from levels.json when the scene starts.
public class LevelScene : MonoBehaviour
{
    [SerializeField] int levelIndex; // 0 = Tutorial

    void Start() => GameManager.Instance.BeginLevel(levelIndex);
}
