using UnityEngine;

// Test tool: plays each level's solver solution (from levels.json) with real game input: walking, pushing,
// pulling (E held), flipping the lever and unlocking doors (E press). Fights cost no HP while it runs.
// Add it to any GameObject in Play mode; Status says how far it got.
public class Autopilot : MonoBehaviour
{
    public string Status { get; private set; } = "starting";
    public int LevelsCompleted { get; private set; }
    public int StopAtStep = -1; // freezes the game (timeScale 0) once this solution step is reached, for inspection

    LevelData level;
    int step;
    float stuckTimer;
    Vector2 lastPos;

    void OnEnable() => GameManager.DebugNoDamage = true;

    void OnDisable()
    {
        GameManager.DebugNoDamage = false;
        GameInput.MoveOverride = null;
        GameInput.InteractHeldOverride = false;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        var grid = gm.Grid;
        if (grid == null) return;
        if (grid.Level != level)
        {
            if (level != null && grid.Level.name != level.name) LevelsCompleted++;
            level = grid.Level;
            step = 0;
            stuckTimer = 0;
        }
        if (!gm.CanAct)
        {
            GameInput.MoveOverride = Vector2.zero;
            GameInput.InteractHeldOverride = false;
            stuckTimer = 0;
            return;
        }

        if (StopAtStep >= 0 && step >= StopAtStep)
        {
            Time.timeScale = 0f;
            GameInput.MoveOverride = Vector2.zero;
            return;
        }

        Vector2Int target;
        bool pull = false;
        if (step < level.solution.Length)
        {
            var s = level.solution[step];
            if (s.toggle || s.unlock)
            {
                GameInput.PendingPresses = 1;
                step++;
                return;
            }
            target = new Vector2Int(s.c, s.r);
            pull = s.pull;
        }
        else target = level.goal.V;

        GameInput.InteractHeldOverride = pull;
        Vector2 pos = gm.PlayerPosition, goal = level.CellCenter(target);
        if ((goal - pos).magnitude < 0.3f && step < level.solution.Length)
        {
            step++;
            GameInput.MoveOverride = Vector2.zero;
        }
        else GameInput.MoveOverride = (goal - pos).normalized;

        // stuck detection
        if ((pos - lastPos).sqrMagnitude > 0.0001f) stuckTimer = 0;
        else stuckTimer += Time.deltaTime;
        lastPos = pos;
        Status = $"{level.name}: step {step}/{level.solution.Length} → {target}" +
                 (stuckTimer > 3f ? $" STUCK at {gm.PlayerCell}" : "");
    }
}
