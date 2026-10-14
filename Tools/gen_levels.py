"""Labyrinth level generator: paper prototype (Excalidraw) + Unity export (levels.json).

Usage: python3 Tools/gen_levels.py levels-plan.excalidraw [Assets/Resources/levels.json]

Every level is verified: S→Z solvable (Sokoban-style solver over player, boxes, keys, lever),
every puzzle gate really blocks the way, must-fight enemies can't be avoided, patrols can.
"""
import json, random, sys
from collections import deque

CAM_W, CAM_H = 5, 3  # camera view in maze cells
KEY_COLORS = ["#f59f00", "#1c7ed6"]
KEY_NAMES = ["gelb", "blau"]
PLATE_COLOR, LEVER_COLOR, SWITCH_B_COLOR = "#0ca678", "#7048e8", "#ae3ec9"

LEVELS = [
    # forced: route fractions of must-fight patrols · deep: (min depth, count) of dead ends
    # gates: puzzles on the route in order (kind, route fraction)
    # abilities: picked up in this level · start_abilities: carried over from earlier levels
    # tier/waves: fight difficulty (0 = tutorial … 3.5 = finale)
    # fetch: min depth of the dead end holding a key/lever, which also must lie off-screen from its gate
    #        (default: key 3, lever 2 — the solution sits right beside the obstacle, fine for teaching)
    dict(name="Tutorial", W=8, H=5, loops=2, forced=[0.7], avoid=1, deep=(3, 1), tutorial=True, patrol_len=2,
         gates=[("box", 0.35)], tier=0, waves=1,
         new="Bewegen, Kämpfe, Kisten schieben", known="–"),
    dict(name="1 – Schlüssel", W=12, H=8, loops=2, forced=[0.35], avoid=2, deep=(5, 2),
         gates=[("door", 0.55), ("box", 0.8)], tier=0.7, waves=2,
         new="Schlüssel + Türen", known="Kisten"),
    dict(name="2 – Handschuh", W=14, H=9, loops=3, forced=[0.25, 0.7], avoid=2, deep=(5, 3), lurker=True,
         gates=[("plate", 0.45), ("door", 0.8)], abilities=["pull"], tier=1.4, waves=3,
         new="Ziehen (Handschuh) + Druckplatte", known="Kisten, Schlüssel"),
    dict(name="3 – Hebel", W=16, H=10, loops=4, forced=[0.2, 0.62], avoid=3, deep=(6, 3), lurker=True,
         gates=[("door", 0.25), ("door", 0.45), ("plate", 0.62), ("lever", 0.82)], start_abilities=["pull"], fetch=5,
         tier=2.1, waves=3, new="Hebel + Wände A/B, zwei Türen", known="Kisten, Ziehen, Druckplatte"),
    dict(name="4 – Schild", W=18, H=11, loops=4, forced=[0.2, 0.42, 0.62], avoid=3, deep=(7, 3), lurker=True,
         gates=[("plate", 0.3), ("door", 0.5), ("lever", 0.8)], start_abilities=["pull"], abilities=["shield"], fetch=6,
         tier=2.8, waves=3, new="Schild (im Kampf)", known="Kisten, Schlüssel, Ziehen, Platte, Hebel"),
    dict(name="5 – Finale", W=20, H=12, loops=5, forced=[0.2, 0.42, 0.6], avoid=4, deep=(8, 3), lurker=True,
         bang=True, gates=[("door", 0.3), ("door", 0.5), ("plate", 0.7), ("lever", 0.8)], fetch=7,
         start_abilities=["pull", "shield"], tier=3.5, waves=3,
         new="alles kombiniert + Doppelkampf (!)", known="alles"),
]
SEE_FIRST = 3  # the obstacle must have been on screen before you pick up its solution
FAR_CAP = 6  # with fetch: key/lever at least min(fetch, FAR_CAP) cells from its gate as the crow flies (7 fits no finale)

# --- maze logic --------------------------------------------------------------
DIRS = ((1, 0), (-1, 0), (0, 1), (0, -1))

def nbrs4(r, c, W, H):
    for dr, dc in DIRS:
        if 0 <= r + dr < H and 0 <= c + dc < W:
            yield r + dr, c + dc

def wall_segments(adj, W, H):
    segs = []
    for y in range(H + 1):
        for c in range(W):
            if y in (0, H) or (y, c) not in adj[(y - 1, c)]:
                segs.append(((y, c), (y, c + 1)))
    for x in range(W + 1):
        for r in range(H):
            if x in (0, W) or (r, x) not in adj[(r, x - 1)]:
                segs.append(((r, x), (r + 1, x)))
    return segs

def has_floating_wall(adj, W, H):
    g = {}
    for a, b in wall_segments(adj, W, H):
        g.setdefault(a, []).append(b); g.setdefault(b, []).append(a)
    start = [p for p in g if p[0] in (0, H) or p[1] in (0, W)]
    seen, q = set(start), deque(start)
    while q:
        for n in g[q.popleft()]:
            if n not in seen:
                seen.add(n); q.append(n)
    return len(seen) != len(g)

def make_maze(W, H, loops, rnd):
    adj = {(r, c): set() for r in range(H) for c in range(W)}
    stack, seen = [(H - 1, 0)], {(H - 1, 0)}
    while stack:
        cur = stack[-1]
        opts = [n for n in nbrs4(*cur, W, H) if n not in seen]
        if not opts:
            stack.pop(); continue
        n = rnd.choice(opts)
        adj[cur].add(n); adj[n].add(cur); seen.add(n); stack.append(n)
    closed = [(a, b) for a in adj for b in nbrs4(*a, W, H) if a < b and b not in adj[a]]
    rnd.shuffle(closed)
    added = 0
    for a, b in closed:
        if added == loops:
            break
        adj[a].add(b); adj[b].add(a)
        if has_floating_wall(adj, W, H):
            adj[a].discard(b); adj[b].discard(a)
        else:
            added += 1
    return adj if added == loops else None

def bfs(adj, start, blocked=frozenset(), blocked_edges=frozenset()):
    prev, q = {start: None}, deque([start])
    while q:
        cur = q.popleft()
        for n in adj[cur]:
            if n not in prev and n not in blocked and frozenset((cur, n)) not in blocked_edges:
                prev[n] = cur; q.append(n)
    return prev

def path_to(prev, goal):
    out = []
    while goal is not None:
        out.append(goal); goal = prev[goal]
    return out[::-1]

def straight_runs(adj, W, H, length):
    runs = []
    for r in range(H):
        for c in range(W - length + 1):
            cells = [(r, c + i) for i in range(length)]
            if all(cells[i + 1] in adj[cells[i]] for i in range(length - 1)):
                runs.append(cells)
    for c in range(W):
        for r in range(H - length + 1):
            cells = [(r + i, c) for i in range(length)]
            if all(cells[i + 1] in adj[cells[i]] for i in range(length - 1)):
                runs.append(cells)
    return runs

def side_branches(adj, route):
    """Components hanging off the route by exactly one edge = dead-end branches."""
    on = set(route); seen = set(); out = []
    for p in adj:
        if p in on or p in seen:
            continue
        comp, q = {p}, deque([p])
        while q:
            for n in adj[q.popleft()]:
                if n not in on and n not in comp:
                    comp.add(n); q.append(n)
        seen |= comp
        mouths = [(a, b) for a in comp for b in adj[a] if b in on]
        if len(mouths) != 1:
            continue
        entry, mouth = mouths[0]
        dist = {entry: 1}; q = deque([entry])
        while q:
            cur = q.popleft()
            for n in adj[cur]:
                if n in comp and n not in dist:
                    dist[n] = dist[cur] + 1; q.append(n)
        far = max(dist, key=dist.get)
        out.append(dict(cells=comp, entry=entry, mouth=mouth, depth=dist[far], far=far, dist=dist))
    return out

def manhattan_near(a_cells, b_cells, d):
    return any(abs(a[0] - b[0]) + abs(a[1] - b[1]) < d for a in a_cells for b in b_cells)

E = lambda a, b: frozenset((a, b))

# --- puzzle solver -------------------------------------------------------------
def solve(adj, S, Z, P, start_pull, glove=True, limit=400_000, via=None):
    """BFS over (player, boxes, keys, lever, has_pull). Returns the states of a shortest solution, or None.
    With `via`, that cell must be visited on the way to Z (the heart: reachable without breaking the level).

    Doors open with their key; plate gates are open while a box sits on the plate; switch walls:
    set A is closed at lever 0, set B is closed at lever 1. Standing on the lever toggles it (an action).
    Pulling needs the Pull ability: from the start, or picked up on the glove cell (unless glove=False)."""
    doors = {E(*d["edge"]): d["color"] for d in P["doors"]}
    gates = {E(*g["edge"]): g["plate"] for g in P["plates"]}
    sw_a = {E(*e) for e in P["switch_a"]}
    sw_b = {E(*e) for e in P["switch_b"]}
    keys_at = {k["cell"]: k["color"] for k in P["keys"]}
    lever = P["lever"]
    glove_at = P["glove"] if glove else None

    def passable(a, b, keys, boxes, lev):
        if b not in adj[a]:
            return False
        e = E(a, b)
        if e in doors and doors[e] not in keys: return False
        if e in gates and gates[e] not in boxes: return False
        if e in sw_a and lev == 0: return False
        if e in sw_b and lev == 1: return False
        return True

    start = (S, frozenset(P["boxes"]), frozenset(), 0, start_pull)
    if via is not None:
        start = start + (False,)
    prev = {start: None}
    q = deque([start])
    while q:
        st = q.popleft()
        p, boxes, keys, lev, pull = st[:5]
        if p == Z and (via is None or st[5]):
            out = []
            while st is not None:
                out.append(st[:5]); st = prev[st]
            return out[::-1]
        if len(prev) > limit:
            return None
        nxt = []
        if p == lever:
            nxt.append((p, boxes, keys, 1 - lev, pull))
        for dr, dc in DIRS:
            q1 = (p[0] + dr, p[1] + dc)
            if q1 not in adj or not passable(p, q1, keys, boxes, lev):
                continue
            k1 = keys | {keys_at[q1]} if q1 in keys_at else keys
            pull1 = pull or q1 == glove_at
            if q1 in boxes:  # push
                q2 = (q1[0] + dr, q1[1] + dc)
                if q2 in adj and q2 not in boxes and passable(q1, q2, keys, boxes, lev):
                    nxt.append((q1, boxes - {q1} | {q2}, k1, lev, pull1))
                continue
            nxt.append((q1, boxes, k1, lev, pull1))
            back = (p[0] - dr, p[1] - dc)
            if pull and back in boxes and passable(back, p, keys, boxes, lev):
                nxt.append((q1, boxes - {back} | {p}, k1, lev, pull1))
        for s in nxt:
            if via is not None:
                s = s + (st[5] or s[0] == via,)
            if s not in prev:
                prev[s] = st; q.append(s)
    return None

def box_reach(adj, player, box, pull, blocked_edges):
    """Cells one box can be moved to (push, optionally pull), with the given edges closed."""
    ok = lambda a, b: b in adj[a] and E(a, b) not in blocked_edges
    start = (player, box)
    seen, q, cells = {start}, deque([start]), {box}
    while q:
        p, b = q.popleft()
        for dr, dc in DIRS:
            q1 = (p[0] + dr, p[1] + dc)
            if q1 not in adj or not ok(p, q1):
                continue
            nxt = []
            if q1 == b:
                q2 = (b[0] + dr, b[1] + dc)
                if q2 in adj and ok(b, q2):
                    nxt.append((q1, q2))
            else:
                nxt.append((q1, b))
                back = (p[0] - dr, p[1] - dc)
                if pull and back == b and ok(b, p):
                    nxt.append((q1, p))
            for st in nxt:
                if st not in seen:
                    seen.add(st); q.append(st); cells.add(st[1])
    return cells

# --- level design ---------------------------------------------------------------
def design(L, seed):
    rnd = random.Random(seed)
    W, H = L["W"], L["H"]
    adj = make_maze(W, H, L["loops"], rnd)
    if adj is None:
        return None
    S, Z = (H - 1, 0), (0, W - 1)
    if len(bfs(adj, S, frozenset([Z]))) != W * H - 1:
        return None  # nothing may lie behind the exit: touching Z ends the level
    route = path_to(bfs(adj, S), Z)
    n = len(route)
    idx = {p: i for i, p in enumerate(route)}
    cut = {p for p in route[1:-1] if Z not in bfs(adj, S, frozenset([p]))}
    bridges = [i for i in range(n - 1)
               if Z not in bfs(adj, S, blocked_edges=frozenset([E(route[i], route[i + 1])]))]

    branches = side_branches(adj, route)
    dmin, dcount = L["deep"]
    deep = [b for b in branches if b["depth"] >= dmin]
    if len(deep) < dcount:
        return None
    in_start_view = lambda p: p[0] >= H - CAM_H and p[1] < CAM_W
    heart_branch = max((b for b in deep if not in_start_view(b["far"])), key=lambda b: b["depth"], default=None)
    if heart_branch is None:
        return None
    heart = heart_branch["far"]
    taken = {S, Z, heart}
    used_branches = [heart_branch]

    # 1. gates along the route, in order
    gates = []  # dict(kind, i) — gate sits between route[i] and route[i+1] (box: on route[i])
    gets_pull_here = "pull" in L.get("abilities", [])
    need_beside = {"door": L.get("fetch", 3), "lever": L.get("fetch", 2), "plate": 3 if gets_pull_here else 0}
    far_off = "fetch" in L  # the key/lever must not be visible from its gate

    def off_screen(b, i):
        """With fetch: the key/lever is out of sight of its gate and not just behind a wall from it."""
        return not far_off or (not (on_screen(route[i], [b["far"]]) or on_screen(route[i + 1], [b["far"]]))
                               and not manhattan_near([b["far"]], route[i:i + 2], min(L["fetch"], FAR_CAP)))

    def has_beside(i, depth, after):
        # a dead end branching off just before position i: its solution is found right next to the obstacle
        return any(i - SEE_FIRST <= idx[b["mouth"]] <= i and idx[b["mouth"]] > after and b["depth"] >= depth
                   and off_screen(b, i) and not b["cells"] & taken and all(b is not u for u in used_branches)
                   for b in branches)

    for kind, frac in L["gates"]:
        target = frac * n
        lo = gates[-1]["i"] + 4 if gates else 3
        after = gates[-1]["i"] + 1 if gates else 0
        if kind == "box":
            pool = [idx[p] for p in cut if idx[p] >= lo and abs(idx[p] - target) < 0.15 * n]
        else:
            pool = [i for i in bridges if i >= lo and abs(i - target) < 0.15 * n
                    and (not need_beside[kind] or has_beside(i, need_beside[kind], after))]
        if not pool:
            return None
        i = min(pool, key=lambda i: abs(i - target))
        gates.append(dict(kind=kind, i=i))
        taken |= {route[i], route[i + 1]}

    def blockers(from_k):
        cells, edges = set(), set()
        for g in gates[from_k:]:
            if g["kind"] == "box":
                cells.add(route[g["i"]])
            else:
                edges.add(E(route[g["i"]], route[g["i"] + 1]))
        return frozenset(cells), frozenset(edges)

    reach = [set(bfs(adj, S, *blockers(k))) for k in range(len(gates))]

    def free_branches(k, min_depth):
        return [b for b in branches if b["cells"] <= reach[k] and b["depth"] >= min_depth
                and not b["cells"] & taken and all(b is not u for u in used_branches)]

    def beside_gate(k, i, min_depth):
        """Dead ends that branch off at most SEE_FIRST cells before gate i: the obstacle is on screen there."""
        return [b for b in free_branches(k, min_depth) if i - SEE_FIRST <= idx[b["mouth"]] <= i and off_screen(b, i)]

    has_pull_start = "pull" in L.get("start_abilities", [])
    gets_pull = "pull" in L.get("abilities", [])

    # 2. puzzle parts for each gate
    P = dict(boxes=[], keys=[], doors=[], plates=[], switch_a=[], switch_b=[], lever=None, glove=None,
             plate_candidates=[], order=[])
    door_no = 0
    for k, g in enumerate(gates):
        i = g["i"]
        a, b = route[i], route[i + 1]
        if g["kind"] == "box":
            P["boxes"].append(a)
        elif g["kind"] == "door":
            pool = beside_gate(k, i, need_beside["door"])
            if not pool:
                return None
            br = max(pool, key=lambda br: br["depth"]); used_branches.append(br)
            color = KEY_COLORS[door_no]; door_no += 1
            P["keys"].append(dict(cell=br["far"], color=color))
            P["doors"].append(dict(edge=(a, b), color=color))
            P["order"].append(dict(color=color, steps=[("door", (a, b)), ("key", br["far"])]))
            taken.add(br["far"])
        elif g["kind"] == "plate":
            glove_br = None
            if gets_pull:  # the glove lies right next to the closed gate
                pool = beside_gate(k, i, 3)
                if not pool:
                    return None
                glove_br = max(pool, key=lambda br: br["depth"]); used_branches.append(glove_br)
                P["glove"] = glove_br["far"]; taken.add(glove_br["far"])
            # the box is stuck at the tip of a dead end you pass before the glove's dead end
            limit = idx[glove_br["mouth"]] if glove_br else i
            pool = [br for br in free_branches(k, 2) if idx[br["mouth"]] < limit]
            if not pool:
                return None
            box_br = max(pool, key=lambda br: br["depth"]); used_branches.append(box_br)
            box = box_br["far"]
            P["boxes"].append(box); taken.add(box)
            # plate: a cell the box can really be moved to, off the main route, as far from the box as possible
            closed = blockers(k)[1]
            spots = box_reach(adj, box_br["entry"] if box_br["entry"] != box else box_br["mouth"], box,
                              has_pull_start or gets_pull, closed)
            dist = bfs(adj, box)
            spots = [c for c in spots if c not in route and c not in taken and c in reach[k]
                     and len(path_to(dist, c)) >= 4]  # the box has to travel at least 3 cells
            spots.sort(key=lambda c: -len(path_to(dist, c)))
            if not spots:
                return None
            P["plate_candidates"] = spots[:4]
            P["plates"].append(dict(edge=(a, b), plate=None))
            steps = [("door", (a, b))] + ([("glove", P["glove"])] if glove_br else []) + [("box", box), ("plate", None)]
            P["order"].append(dict(color=PLATE_COLOR, steps=steps))
        elif g["kind"] == "lever":
            pool = beside_gate(k, i, need_beside["lever"])
            if not pool:
                return None
            br = rnd.choice(pool); used_branches.append(br)
            P["lever"] = br["far"]; taken.add(br["far"])
            P["switch_a"].append((a, b))
            P["order"].append(dict(color=LEVER_COLOR, steps=[("door", (a, b)), ("lever", br["far"])]))
            # wall B closes the heart's dead end once the lever is pulled: grab the heart first
            if not heart_branch["cells"] <= reach[k]:
                return None  # B must close something worth having, never an empty dead end
            P["switch_b"].append((heart_branch["entry"], heart_branch["mouth"]))

    abilities = [dict(kind="pull", cell=P["glove"])] if P["glove"] else []

    # 3. must-fight enemies between the gates, "!" pair, patrols
    forced = []
    cut_runs = [r for k in (3, 2) for r in straight_runs(adj, W, H, k) if set(r) <= cut]
    for frac in L["forced"]:
        target = frac * n
        pool = [r for r in cut_runs if abs(idx[r[len(r) // 2]] - target) < 0.12 * n
                and not manhattan_near(r, taken, 2)]
        if not pool:
            return None
        run = min(pool, key=lambda r: (-len(r), abs(idx[r[len(r) // 2]] - target)))
        forced.append(run); taken |= set(run)
    bang = None
    if L.get("bang"):
        lo = max(0.82 * n, gates[-1]["i"] + 3)
        late = [p for p in cut if lo < idx[p] < n - 2 and not manhattan_near([p], taken, 2)]
        pairs = [(a, b) for a in late for b in late if 2 <= idx[b] - idx[a] <= 3]
        if not pairs:
            return None
        bang = rnd.choice(pairs)
        forced += [[bang[0]], [bang[1]]]; taken |= set(bang)

    # 4. shield: mid-level, after two fights without it, so the next fight is the practice round
    if "shield" in L.get("abilities", []):
        lo, hi = max(idx[c] for c in forced[1]) + 2, min(idx[c] for c in forced[2]) - 2
        pool = [p for p in route[lo:hi] if p not in taken and not manhattan_near([p], taken, 2)]
        if not pool:
            return None
        cell = pool[len(pool) // 2]
        abilities.append(dict(kind="shield", cell=cell)); taken.add(cell)

    avoidable = lambda run: Z in bfs(adj, S, frozenset(run))
    patrols = []
    if L.get("lurker"):
        hb = heart_branch["cells"]
        pool = [r for r in straight_runs(adj, W, H, 3) if set(r) <= hb and not set(r) & taken
                and min(heart_branch["dist"][c] for c in r) <= 3]
        if not pool:
            return None
        patrols.append(rnd.choice(pool)); taken |= set(patrols[-1])
    in_branch = set().union(*(b["cells"] for b in branches)) if branches else set()
    # relevant = on the main route, or guarding a dead end with something worth fetching
    relevant = set(route).union(*(b["cells"] for b in used_branches))
    cand = [r for r in straight_runs(adj, W, H, L.get("patrol_len", 3)) if (set(r) & set(route) or set(r) <= in_branch)
            and not set(r) & taken and avoidable(r)]
    rnd.shuffle(cand)
    cand.sort(key=lambda r: not set(r) & relevant)
    for run in cand:
        if len(patrols) == L["avoid"]:
            break
        if not manhattan_near(run, taken, 2):
            patrols.append(run); taken |= set(run)
    if len(patrols) < L["avoid"] or not any(set(r) & relevant for r in patrols):
        return None

    # 5. solve (picking a working plate spot if the level has a plate gate)
    candidates = P["plate_candidates"] if P["plates"] else [None]
    solution = None
    for plate in candidates:
        if plate is not None:
            P["plates"][0]["plate"] = plate
        solution = solve(adj, S, Z, P, has_pull_start)
        if solution:
            break
    if not solution:
        return None
    if gets_pull and solve(adj, S, Z, P, has_pull_start, glove=False):
        return None  # the glove must be necessary
    if not solve(adj, S, Z, P, has_pull_start, via=heart):
        return None  # the heart must be reachable with what you have, without making the level unsolvable

    # "see it, then get it": each obstacle was on screen before its solution is picked up
    events = see_then_get(solution, P)
    if events is None:
        return None

    # 6. checks
    assert not has_floating_wall(adj, W, H)
    for d in P["doors"] + P["plates"]:
        assert Z not in bfs(adj, S, blocked_edges=frozenset([E(*d["edge"])])), "gate is avoidable"
    for e in P["switch_a"]:
        assert Z not in bfs(adj, S, blocked_edges=frozenset([E(*e)])), "lever wall is avoidable"
    for b in P["boxes"]:
        if b in route:
            assert Z not in bfs(adj, S, frozenset([b])), "blocking box is avoidable"
    for run in forced:
        assert all(Z not in bfs(adj, S, frozenset([c])) for c in run), "forced enemy is avoidable"
    for run in patrols:
        assert avoidable(run), "patrol blocks the only route"

    return dict(adj=adj, S=S, Z=Z, route=route, forced=forced, patrols=patrols, heart=heart, bang=bang,
                P=P, abilities=abilities, solution=solution, events=events,
                deep=sorted(b["depth"] for b in deep))

def on_screen(player, cells):
    """Camera rule (5×3 cells around the player): is any of `cells` visible?"""
    return any(abs(c[0] - player[0]) <= CAM_H // 2 and abs(c[1] - player[1]) <= CAM_W // 2 for c in cells)

def see_then_get(solution, P):
    """Step numbers of 'obstacle seen' and 'solution picked up' per puzzle; None if a solution comes first."""
    players = [s[0] for s in solution]
    first = lambda pred: next((t for t, p in enumerate(players) if pred(p)), None)
    events = []
    for puzzle in P["order"]:
        obstacle = puzzle["steps"][0][1]
        seen = first(lambda p: on_screen(p, obstacle))
        for kind, cell in puzzle["steps"][1:2]:
            if kind not in ("key", "glove", "lever"):
                continue  # plate puzzles without a glove: the box is visible from the start
            got = first(lambda p: p == cell)
            if seen is None or got is None or got < seen:
                return None
            events.append((puzzle["color"], seen, got))
    return events

# --- excalidraw drawing -------------------------------------------------------
CELL, T = 40, 7
els, rnd = [], random.Random(1)

def base(t, x, y, w, h, **kw):
    e = dict(id=f"e{len(els)}", type=t, x=x, y=y, width=w, height=h, angle=0,
             strokeColor="#1e1e1e", backgroundColor="transparent", fillStyle="solid",
             strokeWidth=1, strokeStyle="solid", roughness=1, opacity=100, groupIds=[],
             frameId=None, roundness=None, seed=rnd.randint(1, 2**31), version=1,
             versionNonce=rnd.randint(1, 2**31), isDeleted=False, boundElements=None,
             updated=1, link=None, locked=False)
    e.update(kw); els.append(e); return e

def text(x, y, s, size=20, color="#1e1e1e", align="left"):
    lines = s.split("\n")
    w = max(len(l) for l in lines) * size * 0.55
    h = len(lines) * size * 1.25
    if align == "center":
        x -= w / 2; y -= h / 2
    return base("text", x, y, w, h, text=s, originalText=s, fontSize=size, fontFamily=5,
                textAlign=align, verticalAlign="top", containerId=None, lineHeight=1.25,
                autoResize=True, strokeColor=color)

def line(pts, kind="line", **kw):
    x0, y0 = pts[0]
    rel = [[x - x0, y - y0] for x, y in pts]
    xs, ys = [p[0] for p in rel], [p[1] for p in rel]
    return base(kind, x0, y0, max(xs) - min(xs), max(ys) - min(ys), points=rel,
                lastCommittedPoint=None, startBinding=None, endBinding=None,
                startArrowhead=kw.pop("start", None),
                endArrowhead=kw.pop("end", "arrow" if kind == "arrow" else None), **kw)

def dot(x, y, d, fill, stroke):
    base("ellipse", x - d / 2, y - d / 2, d, d, backgroundColor=fill, strokeColor=stroke, fillStyle="solid")

def badge(x, y, num):
    dot(x, y, 22, "#fff3bf", "#f08c00")
    text(x, y, str(num), 14, "#1e1e1e", "center")

def simplify(pts):
    return [pts[0]] + [p for i, p in enumerate(pts[1:-1], 1)
                       if not (pts[i - 1][0] == p[0] == pts[i + 1][0]) and
                       not (pts[i - 1][1] == p[1] == pts[i + 1][1])] + [pts[-1]]

def box_tracks(solution):
    """Per box: the cells it moves through in the solution."""
    tracks = {b: [b] for b in solution[0][1]}
    for s0, s1 in zip(solution, solution[1:]):
        b0, b1 = s0[1], s1[1]
        if b0 != b1:
            (old,), (new,) = b0 - b1, b1 - b0
            for start, tr in tracks.items():
                if tr[-1] == old:
                    tr.append(new)
    return [tr for tr in tracks.values() if len(tr) > 1]

TUTORIAL_HINTS = {
    "S": "Bewegen mit WASD / Pfeiltasten",
    "box": "Kiste: hineinlaufen = schieben, ab in eine Nische",
    "patrol": "Patrouille: Abstand halten, Umweg nehmen",
    "heart": "♥ einsammeln = +1 HP",
    "forced": "Pflicht-Kampf (leicht): mit ♥ ausweichen",
    "Z": "Ziel erreichen → Level 1",
}

def tutorial_marks(D):
    route, idx = D["route"], {p: i for i, p in enumerate(D["route"])}
    near = lambda c: min(range(len(route)), key=lambda i: abs(route[i][0] - c[0]) + abs(route[i][1] - c[1]))
    marks = [("S", D["S"], 0), ("box", D["P"]["boxes"][0], idx.get(D["P"]["boxes"][0], 0)),
             ("patrol", D["patrols"][0][1], near(D["patrols"][0][1])),
             ("heart", D["heart"], near(D["heart"])),
             ("forced", D["forced"][0][len(D["forced"][0]) // 2], near(D["forced"][0][0])),
             ("Z", D["Z"], len(route))]
    return sorted(marks, key=lambda m: m[2])

def draw_maze(L, D, X0, Y0):
    W, H, adj, P = L["W"], L["H"], D["adj"], D["P"]
    cx = lambda c: X0 + c * CELL + CELL / 2
    cy = lambda r: Y0 + r * CELL + CELL / 2

    nf, na = len(D["forced"]), len(D["patrols"])
    text(X0, Y0 - 80, f'{L["name"]}  ({W}×{H})', 28)
    text(X0, Y0 - 46, f'neu: {L["new"]}  ·  bekannt: {L["known"]}', 15, "#1e1e1e")
    text(X0, Y0 - 24, f"{nf}× Pflicht-Gegner ★ · {na}× Patrouille ●", 14, "#495057")

    for r in range(H + 1):
        line([(X0, Y0 + r * CELL), (X0 + W * CELL, Y0 + r * CELL)], strokeColor="#e9ecef", roughness=0)
    for c in range(W + 1):
        line([(X0 + c * CELL, Y0), (X0 + c * CELL, Y0 + H * CELL)], strokeColor="#e9ecef", roughness=0)

    wall = dict(backgroundColor="#495057", strokeColor="#343a40", fillStyle="hachure", roughness=0)
    for y in range(H + 1):
        c = 0
        while c < W:
            closed = lambda c: y in (0, H) or (y, c) not in adj[(y - 1, c)]
            if closed(c):
                s = c
                while c < W and closed(c):
                    c += 1
                base("rectangle", X0 + s * CELL - T / 2, Y0 + y * CELL - T / 2, (c - s) * CELL + T, T, **wall)
            else:
                c += 1
    for x in range(W + 1):
        r = 0
        while r < H:
            closed = lambda r: x in (0, W) or (r, x) not in adj[(r, x - 1)]
            if closed(r):
                s = r
                while r < H and closed(r):
                    r += 1
                base("rectangle", X0 + x * CELL - T / 2, Y0 + s * CELL - T / 2, T, (r - s) * CELL + T, **wall)
            else:
                r += 1

    def edge_bar(a, b, color, dashed=False, label=None):
        (r1, c1), (r2, c2) = a, b
        th = 9
        if r1 == r2:  # vertical bar on column line
            x = X0 + max(c1, c2) * CELL
            el = base("rectangle", x - th / 2, Y0 + r1 * CELL + 4, th, CELL - 8, backgroundColor=color,
                      strokeColor=color, fillStyle="solid" if not dashed else "cross-hatch",
                      strokeStyle="dashed" if dashed else "solid", roughness=0, strokeWidth=2)
            lx, ly = x + 6, Y0 + r1 * CELL - 4
        else:  # horizontal bar on row line
            y = Y0 + max(r1, r2) * CELL
            el = base("rectangle", X0 + c1 * CELL + 4, y - th / 2, CELL - 8, th, backgroundColor=color,
                      strokeColor=color, fillStyle="solid" if not dashed else "cross-hatch",
                      strokeStyle="dashed" if dashed else "solid", roughness=0, strokeWidth=2)
            lx, ly = X0 + c1 * CELL + CELL - 6, y + 4
        if label:
            text(lx, ly, label, 13, color)
        return el

    # route + box solution tracks
    line([(cx(c) - 7, cy(r) + 7) for r, c in simplify(D["route"])], "arrow", strokeColor="#1971c2",
         strokeStyle="dashed", strokeWidth=1.5, roughness=0)
    for tr in box_tracks(D["solution"]):
        line([(cx(c) + 6, cy(r) - 6) for r, c in simplify(tr)], "arrow", strokeColor="#868e96",
             strokeWidth=1.5, roughness=0)

    base("rectangle", X0 + 2, Y0 + (H - CAM_H) * CELL + 2, CAM_W * CELL - 4, CAM_H * CELL - 4,
         strokeColor="#0c8599", strokeStyle="dashed", strokeWidth=2, roughness=0)
    text(X0 + 6, Y0 + (H - CAM_H) * CELL + 4, "Kamera", 13, "#0c8599")

    S, Z = D["S"], D["Z"]
    text(cx(S[1]) + 6, cy(S[0]), "S", 26, "#2f9e44", "center")
    text(cx(Z[1]), cy(Z[0]), "Z", 26, "#e03131", "center")
    text(cx(D["heart"][1]), cy(D["heart"][0]), "♥", 24, "#e64980", "center")

    # puzzles
    for d in P["doors"]:
        edge_bar(*d["edge"], d["color"])
    for k in P["keys"]:
        r, c = k["cell"]
        base("diamond", cx(c) - 11, cy(r) - 11, 22, 22, backgroundColor=k["color"], strokeColor="#1e1e1e",
             fillStyle="solid", roughness=0)
        text(cx(c), cy(r), "K", 12, "#1e1e1e", "center")
    for pl in P["plates"]:
        edge_bar(*pl["edge"], PLATE_COLOR, dashed=True)
        r, c = pl["plate"]
        base("rectangle", cx(c) - 13, cy(r) - 13, 26, 26, strokeColor=PLATE_COLOR, strokeWidth=2, roughness=0)
        dot(cx(c), cy(r), 7, PLATE_COLOR, PLATE_COLOR)
    for e in P["switch_a"]:
        edge_bar(*e, LEVER_COLOR, dashed=True, label="A")
    for e in P["switch_b"]:
        edge_bar(*e, SWITCH_B_COLOR, dashed=True, label="B")
    if P["lever"]:
        r, c = P["lever"]
        base("ellipse", cx(c) - 12, cy(r) - 12, 24, 24, strokeColor=LEVER_COLOR, strokeWidth=2, roughness=0)
        text(cx(c), cy(r), "L", 15, LEVER_COLOR, "center")
    for b in P["boxes"]:
        r, c = b
        base("rectangle", cx(c) - 13, cy(r) - 13, 26, 26, backgroundColor="#b08968", strokeColor="#7f5539",
             fillStyle="solid", strokeWidth=2, roughness=0)
    for ab in D["abilities"]:
        r, c = ab["cell"]
        label = {"pull": "Ziehen", "shield": "Schild"}[ab["kind"]]
        w = len(label) * 8 + 10
        base("rectangle", cx(c) - w / 2, cy(r) - 11, w, 22, backgroundColor="#c5f6fa", strokeColor="#1098ad",
             fillStyle="solid", roughness=0, roundness={"type": 3})
        text(cx(c), cy(r), label, 13, "#0b7285", "center")

    def patrol(run, fill, stroke):
        (r0, c0), (r1, c1) = run[0], run[-1]
        mid = run[len(run) // 2]
        if len(run) > 1:
            off = (0, 12) if r0 == r1 else (12, 0)
            line([(cx(c0) + off[0], cy(r0) + off[1]), (cx(c1) + off[0], cy(r1) + off[1])], "arrow",
                 start="arrow", strokeColor=stroke, strokeWidth=2, roughness=0)
        dot(cx(mid[1]), cy(mid[0]) - 3, 20, fill, stroke)
        return mid

    for run in D["patrols"]:
        patrol(run, "#fd7e14", "#e8590c")
    for run in D["forced"]:
        m = patrol(run, "#e03131", "#c92a2a")
        text(cx(m[1]) + 13, cy(m[0]) - 15, "★", 16, "#9c36b5", "center")
    if D["bang"]:
        a, b = D["bang"]
        xs = [cx(a[1]), cx(b[1])]; ys = [cy(a[0]), cy(b[0])]
        pad = 30
        base("ellipse", min(xs) - pad, min(ys) - pad, max(xs) - min(xs) + 2 * pad, max(ys) - min(ys) + 2 * pad,
             strokeColor="#e03131", strokeStyle="dashed", strokeWidth=2)
        text(max(xs) + pad + 4, min(ys) - pad - 8, "!", 44, "#e03131")

    # order markers: 1 = obstacle you see first, 2 = what solves it, 3 = where it's used
    for puzzle in P["order"]:
        for num, (kind, where) in enumerate(puzzle["steps"], 1):
            if kind == "door":
                (r1, c1), (r2, c2) = where
                x, y = X0 + (c1 + c2 + 1) / 2 * CELL, Y0 + (r1 + r2 + 1) / 2 * CELL
                x, y = (x + 12, y - 14) if r1 == r2 else (x - 14, y + 12)
            else:
                r, c = P["plates"][0]["plate"] if kind == "plate" else where
                x, y = cx(c) + 13, cy(r) - 15
            base("ellipse", x - 9, y - 9, 18, 18, backgroundColor="#ffffff", strokeColor=puzzle["color"],
                 fillStyle="solid", strokeWidth=2, roughness=0)
            text(x, y, str(num), 12, puzzle["color"], "center")

    if L.get("tutorial"):
        offs = {"S": (26, -14), "box": (0, -24), "patrol": (0, -26), "heart": (-26, 6),
                "forced": (-24, -20), "Z": (6, 28)}
        for num, (kind, p, _) in enumerate(tutorial_marks(D), 1):
            dx, dy = offs[kind]
            badge(cx(p[1]) + dx, cy(p[0]) + dy, num)

def draw_tutorial_hints(X, Y, D):
    text(X, Y, "Tutorial-Hinweise", 20)
    lines = [f"{i}  {TUTORIAL_HINTS[k]}" for i, (k, _, _) in enumerate(tutorial_marks(D), 1)]
    text(X, Y + 32, "\n".join(lines), 15, "#343a40")

def panel(X, Y, title, body, size=16):
    text(X, Y, title, 24)
    text(X, Y + 38, body, size, "#343a40")

def draw_legend(X, Y):
    text(X, Y, "Legende", 24)
    rows = [("S / Z", "#2f9e44", "Start / Ziel"), ("▬", "#495057", "Wand"),
            ("●↔", "#e03131", "Pflicht-Gegner ★ (einziger Weg)"), ("●↔", "#fd7e14", "Patrouille (Umweg möglich)"),
            ("!", "#e03131", "schwierigste Stelle"), ("♥", "#e64980", "+1 HP (tiefste Sackgasse)"),
            ("■", "#b08968", "Kiste (schieben / ziehen)"), ("→", "#868e96", "Lösungsweg der Kiste"),
            ("◆K", KEY_COLORS[0], "Schlüssel ..."), ("▮", KEY_COLORS[0], "... öffnet Tür gleicher Farbe"),
            ("▣", PLATE_COLOR, "Druckplatte → öffnet Tor (gestrichelt)"),
            ("Ⓛ", LEVER_COLOR, "Hebel: schaltet Wände A ↔ B"),
            ("Ziehen", "#1098ad", "Fähigkeit einsammeln"), ("- ->", "#1971c2", "Hauptweg S → Z"),
            ("▭", "#0c8599", "Kamera-Sichtfeld 5×3"),
            ("1 2 3", "#495057", "Reihenfolge: sehen → holen → benutzen")]
    for i, (sym, col, label) in enumerate(rows):
        y = Y + 40 + i * 27
        text(X, y, sym, 18, col)
        text(X + 70, y + 2, label, 16)

def draw_encounter(X, Y):
    text(X, Y, "★ Begegnung = Ausweich-Kampf", 24)
    bx, by, bw, bh = X, Y + 45, 300, 170
    base("rectangle", bx, by, bw, bh, backgroundColor="#1e1e1e", strokeColor="#1e1e1e", strokeWidth=4, roughness=0)
    base("rectangle", bx + 12, by + 12, bw - 24, bh - 24, strokeColor="#ffffff", strokeWidth=3, roughness=0)
    text(bx + bw / 2, by + bh / 2 + 10, "♥", 26, "#e03131", "center")
    for (dx, dy) in [(40, 40), (90, 30), (230, 50), (250, 120), (60, 125), (180, 135)]:
        dot(bx + dx, by + dy, 10, "#ffffff", "#ffffff")
    line([(bx + 30, by + 85), (bx + 120, by + 85)], "arrow", strokeColor="#ffffff", strokeWidth=2, roughness=0)
    line([(bx + 270, by + 30), (bx + 200, by + 70)], "arrow", strokeColor="#ffffff", strokeWidth=2, roughness=0)
    hy = by + bh + 16
    text(bx, hy, "HP", 20)
    for i in range(5):
        base("rectangle", bx + 40 + i * 26, hy + 3, 20, 20, roughness=0,
             backgroundColor="#e03131" if i < 4 else "transparent", strokeColor="#c92a2a")
    text(bx, hy + 40,
         "Gegner berühren → Kampf-Bildschirm\n"
         "3 Angriffswellen (~10 s) ausweichen, Treffer −1 HP\n"
         "HP bleiben zwischen Kämpfen, 0 HP → Neustart\n"
         "Kämpfe werden pro Level schwerer", 16, "#343a40")

designs = []
for L in LEVELS:
    for seed in range(1, 40000):
        D = design(L, seed)
        if D:
            designs.append(D)
            print(f'{L["name"]}: seed {seed}, route {len(D["route"])}, solution {len(D["solution"]) - 1} steps, '
                  f'forced {len(D["forced"])}, patrols {len(D["patrols"])}, gates {[g for g, _ in L["gates"]]}, '
                  f'deep dead ends {D["deep"]}', file=sys.stderr)
            names = {KEY_COLORS[0]: "gelbe Tür", KEY_COLORS[1]: "blaue Tür", PLATE_COLOR: "Tor",
                     LEVER_COLOR: "Wand A"}
            for color, seen, got in D["events"]:
                print(f'    {names[color]}: gesehen @{seen} → Lösung geholt @{got}', file=sys.stderr)
            break
    else:
        sys.exit(f'no valid seed for {L["name"]}')

text(0, -150, "Labyrinth – Pen-and-Paper-Prototyp", 36)
# 3×2 grid, mazes top-aligned per row; the tutorial hints sit under the (small) tutorial maze
GAP, ROW_GAP, COLS = 140, 190, 3
y, right = 0, 0
for row in range(0, len(LEVELS), COLS):
    x = 0
    for L, D in zip(LEVELS[row:row + COLS], designs[row:row + COLS]):
        draw_maze(L, D, x, y)
        if L.get("tutorial"):
            draw_tutorial_hints(x, y + L["H"] * CELL + 20, D)
        x += L["W"] * CELL + GAP
    right = max(right, x)
    y += max(L["H"] for L in LEVELS[row:row + COLS]) * CELL + ROW_GAP

X1 = right + 60
X2 = X1 + 560
draw_legend(X1, -60)
panel(X1, 440, "Kamera",
      "Level-Start: Kamera zeigt kurz das ganze Labyrinth\n"
      "und zoomt dann auf den Spieler (5×3 Felder).\n"
      "Nur in diesem Moment sieht man alles – merken!\n"
      "Die Kamera stoppt am Rand des Labyrinths.")
panel(X1, 600, "Progression: erst sehen, dann holen",
      "Tutorial: bewegen, Patrouille, ♥, erster Kampf, Kiste schieben.\n"
      "1 Schlüssel: verschlossene Tür → Schlüssel in der Sackgasse\n"
      "   direkt daneben → zurück zur Tür.\n"
      "2 Handschuh: Kiste steckt in Sackgasse fest, Tor ist zu →\n"
      "   neben dem Tor liegt der Handschuh (Ziehen) → zurück,\n"
      "   Kiste rausziehen → auf die Druckplatte.\n"
      "3 Hebel: Wand A sperrt → Hebel in einer Sackgasse abseits\n"
      "   (B schließt das ♥!), dazu zwei Türen (gelb → blau).\n"
      "Ab Level 3 liegt die Lösung außer Sicht und weiter weg:\n"
      "   lange Sackgasse statt direkt neben dem Hindernis.\n"
      "4 Schild: zwei Kämpfe ohne Schild, dann liegt er im Gang,\n"
      "   der nächste Kampf ist die Übungsrunde.\n"
      "5 Finale: alles kombiniert + Doppelkampf (!) vor dem Ziel.\n"
      "Fähigkeiten bleiben für alle späteren Level.")
draw_encounter(X2, -60)
panel(X2, 400, "Fähigkeiten",
      "Ziehen (Handschuh, Level 2 neben dem Tor):\n"
      "   E halten + weggehen = Kiste mitziehen\n"
      "Schild (Level 4, nach 2 Kämpfen): Leertaste im\n"
      "   Kampf blockt 1 Treffer pro Welle\n"
      "Hebel: E drücken · R = Level neu starten")
panel(X2, 600, "MDA",
      "Mechanics: Labyrinth, kleine Kamera, Gegner, Ausweich-Kämpfe,\n"
      "   HP + Herzen, Kisten, Schlüssel/Türen, Druckplatten, Hebel,\n"
      "   Fähigkeiten Ziehen + Schild\n"
      "Dynamics: erkunden, sich das Labyrinth vom Start merken,\n"
      "   Sackgassen nach Schlüsseln absuchen, Kisten-Züge planen,\n"
      "   ♥ holen bevor der Hebel die Sackgasse schließt\n"
      "   Metroidvania-Prinzip: Hindernis sehen → Lösung holen → zurück\n"
      "Aesthetics: spannend, herausfordernd, konzentriert, 'Aha!'\n"
      "! (Finale): zwei Pflicht-Gegner direkt vor Z – mit wenig HP,\n"
      "   ohne Pause. Man muss Muster lesen und das Schild nutzen.")

doc = dict(type="excalidraw", version=2, source="https://excalidraw.com", elements=els,
           appState=dict(viewBackgroundColor="#ffffff", gridSize=None), files={})
with open(sys.argv[1], "w") as f:
    json.dump(doc, f, ensure_ascii=False)
print(f"{len(els)} elements -> {sys.argv[1]}", file=sys.stderr)

# --- Unity export -------------------------------------------------------------
def wall_runs(adj, W, H):
    runs = []
    for y in range(H + 1):
        c = 0
        while c < W:
            if y in (0, H) or (y, c) not in adj[(y - 1, c)]:
                s = c
                while c < W and (y in (0, H) or (y, c) not in adj[(y - 1, c)]):
                    c += 1
                runs.append(dict(horizontal=True, line=y, start=s, end=c))
            else:
                c += 1
    for x in range(W + 1):
        r = 0
        while r < H:
            if x in (0, W) or (r, x) not in adj[(r, x - 1)]:
                s = r
                while r < H and (x in (0, W) or (r, x) not in adj[(r, x - 1)]):
                    r += 1
                runs.append(dict(horizontal=False, line=x, start=s, end=r))
            else:
                r += 1
    return runs

if len(sys.argv) > 2:
    cell = lambda p: dict(r=p[0], c=p[1])
    edge = lambda e: dict(a=cell(e[0]), b=cell(e[1]))
    out = []
    for L, D in zip(LEVELS, designs):
        route, P = D["route"], D["P"]
        hints = []
        if L.get("tutorial"):
            for kind, p, i in tutorial_marks(D):
                if kind == "Z":
                    continue
                at = route[max(0, min(i, len(route) - 1) - 2)] if kind in ("box", "forced") else p
                hints.append(dict(cell=cell(at), text=TUTORIAL_HINTS[kind]))
        out.append(dict(
            name=L["name"], width=L["W"], height=L["H"],
            walls=wall_runs(D["adj"], L["W"], L["H"]),
            start=cell(D["S"]), goal=cell(D["Z"]), heart=cell(D["heart"]),
            forced=[dict(cells=[cell(p) for p in run]) for run in D["forced"]],
            patrols=[dict(cells=[cell(p) for p in run]) for run in D["patrols"]],
            hints=hints,
            boxes=[cell(b) for b in P["boxes"]],
            keys=[dict(cell=cell(k["cell"]), color=k["color"]) for k in P["keys"]],
            doors=[dict(edge=edge(d["edge"]), color=d["color"]) for d in P["doors"]],
            plates=[dict(cell=cell(p["plate"]), gate=edge(p["edge"])) for p in P["plates"]],
            levers=[cell(P["lever"])] if P["lever"] else [],
            switchWalls=[dict(edge=edge(e), set="A") for e in P["switch_a"]] +
                        [dict(edge=edge(e), set="B") for e in P["switch_b"]],
            abilities=[dict(kind=a["kind"], cell=cell(a["cell"])) for a in D["abilities"]],
            startAbilities=L.get("start_abilities", []),
            fightTier=L["tier"], waves=L["waves"], intro=f'Neu: {L["new"]}',
            # solver solution: player cell per step; toggle = lever flipped, pull = a box was pulled along
            solution=[dict(r=s1[0][0], c=s1[0][1], toggle=s1[3] != s0[3],
                           pull=s1[1] != s0[1] and s1[0] not in s0[1])
                      for s0, s1 in zip(D["solution"], D["solution"][1:])],
        ))
    with open(sys.argv[2], "w") as fh:
        json.dump(dict(levels=out), fh, ensure_ascii=False, indent=1)
    print(f"{len(out)} levels -> {sys.argv[2]}", file=sys.stderr)
