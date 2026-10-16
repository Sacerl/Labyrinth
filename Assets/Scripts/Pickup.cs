using UnityEngine;

// Maze trigger: heart (+1 HP), goal (next level), tutorial hint, key (text = color) or ability (text = kind).
public class Pickup : MonoBehaviour
{
    public enum Kind { Heart, Goal, Hint, Key, Ability }

    public Kind kind;
    public string text;

    void OnTriggerEnter2D(Collider2D other) => Touch(other);

    // The player can spawn inside a hint (tutorial start); it fires once the level intro is over.
    void OnTriggerStay2D(Collider2D other)
    {
        if (kind == Kind.Hint) Touch(other);
    }

    void Touch(Collider2D other)
    {
        if (!other.GetComponent<PlayerController>()) return;
        var gm = GameManager.Instance;
        if (!gm.CanAct) return;
        switch (kind)
        {
            case Kind.Heart:
                gm.CollectHeart();
                break;
            case Kind.Goal:
                gm.ReachGoal();
                return;
            case Kind.Hint:
                Sound.Play("Scroll", 0.6f);
                gm.ShowHint(text);
                break;
            case Kind.Key:
                gm.AddKey(text);
                break;
            case Kind.Ability:
                gm.AddAbility(text);
                break;
        }
        Destroy(gameObject);
    }
}
