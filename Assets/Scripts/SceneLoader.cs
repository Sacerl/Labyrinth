using UnityEngine;
using UnityEngine.SceneManagement;

// Every scene change goes through here. Build order (File → Build Profiles → Scene List):
// CharacterSelect, LevelSelect, Level1 … Level6, WinScreen, Fight.
// Lives on the persistent Game object next to the GameManager.
public class SceneLoader : MonoBehaviour
{
    public const string CharacterSelect = "CharacterSelect", LevelSelect = "LevelSelect", Fight = "Fight";

    public static SceneLoader Instance { get; private set; }

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public static string LevelScene(int index) => "Level" + (index + 1);

    public void Load(string scene) => SceneManager.LoadScene(scene);

    public void LoadLevel(int index) => Load(LevelScene(index));

    // The level starts over: the scene is loaded fresh, the GameManager keeps HP and abilities.
    public void Reload() => Load(SceneManager.GetActiveScene().name);

    // Whatever comes next in the build order: Level1 → Level2 … Level6 → WinScreen.
    public void LoadNext() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);

    // The fight is loaded on top of the level, so the maze underneath keeps its state (boxes, enemies, keys).
    public AsyncOperation OpenFight() => SceneManager.LoadSceneAsync(Fight, LoadSceneMode.Additive);

    public void CloseFight()
    {
        if (SceneManager.GetSceneByName(Fight).isLoaded) SceneManager.UnloadSceneAsync(Fight);
    }
}
