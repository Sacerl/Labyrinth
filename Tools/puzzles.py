"""Labyrinth puzzle rules: the solver every level is checked with, and generators for puzzle rooms.

A puzzle room is a small walled-off part of the maze with one way in and one way out, so the route has to cross
it. Its puzzle is solved on its own (local solve) and spliced into the level's solution; for the level solver the
room is plain floor. Room kinds:
  ice     Eishalle: slippery floor and rocks, you slide until something stops you (and so do boxes)
  plates  Zwei Platten, eine Kiste: two plate gates, one box
  lever   Hebelkammer: the lever opens wall A and closes wall B; the box has to come along in the right order
  guard   Wächtergang: a must-fight guard; shove it into its niche with a box and you skip the fight

Rules (the game does the same, see PuzzleGrid.cs):
- Doors stay shut until unlocked (an action beside the door with its key).
- A plate gate is open while a box sits on its plate or the player stands on it.
- Switch walls: set A is closed at lever 0, set B at lever 1; standing on the lever toggles it (an action).
- Rune gate: open once the runes were stepped on in order 0, 1, 2 … Stepping on the rune just done changes nothing,
  the first rune restarts the count at 1, any other wrong rune resets it to 0.
- Ice: entering an ice cell you keep going until the next cell is blocked, or you are on a non-ice cell.
  A box pushed onto ice slides the same way. No pulling on ice.
- Rocks block a cell for everyone. Boxes never cross a room's boundary.
- Guards (safe mode): a guard patrols the run cells of a straight line that ends in a niche (line[-1] is the niche
  entrance, `tip` its end). A run cell is safe only with a box ahead of you on the line: the guard turns at boxes
  and is shoved along by them. Boxes never go onto the tip, that is where the guard ends up.
"""
import random
from collections import deque

DIRS = ((1, 0), (-1, 0), (0, 1), (0, -1))
E = lambda a, b: frozenset((a, b))
add = lambda p, d: (p[0] + d[0], p[1] + d[1])


def empty_puzzle():
    return dict(boxes=[], keys=[], doors=[], plates=[], switch_a=[], switch_b=[], lever=None, glove=None,
                runes=[], rune_gates=[], ice=frozenset(), rocks=frozenset(), room_of={}, guards=[],
                plate_candidates=[], order=[])


class Rules:
    """State: (player, boxes, keys, lever, has_pull, unlocked doors, rune progress)."""

    def __init__(self, adj, P, start_pull, glove=True, safe=False):
        self.adj, self.P, self.start_pull = adj, P, start_pull
        self.doors = {E(*d["edge"]): d["color"] for d in P["doors"]}
        self.doors_at = {}
        for d in P["doors"]:
            for c in d["edge"]:
                self.doors_at.setdefault(c, []).append(d["color"])
        self.gates = {E(*g["edge"]): g["plate"] for g in P["plates"]}
        self.sw_a = {E(*e) for e in P["switch_a"]}
        self.sw_b = {E(*e) for e in P["switch_b"]}
        self.rune_gates = {E(*e) for e in P.get("rune_gates", ())}
        self.rune_at = dict(P.get("runes", ()))
        self.n_runes = len(self.rune_at)
        self.keys_at = {k["cell"]: k["color"] for k in P["keys"]}
        self.lever = P["lever"]
        self.glove_at = P["glove"] if glove else None
        self.ice, self.rocks, self.room_of = P.get("ice", ()), P.get("rocks", ()), P.get("room_of", {})
        self.tips, self.danger = set(), {}
        for g in P.get("guards", ()):
            self.tips.add(g["tip"])
            if safe:
                for c in g["run"]:
                    self.danger[c] = (g["line"], g["line"].index(c))

    def start(self, S):
        return (S, frozenset(self.P["boxes"]), frozenset(), 0, self.start_pull, frozenset(), 0)

    def open_edge(self, a, b, pl, boxes, lev, opened, prog):
        if b not in self.adj[a]:
            return False
        e = E(a, b)
        if e in self.doors and self.doors[e] not in opened: return False
        if e in self.gates and self.gates[e] not in boxes and self.gates[e] != pl: return False
        if e in self.sw_a and lev == 0: return False
        if e in self.sw_b and lev == 1: return False
        if e in self.rune_gates and prog < self.n_runes: return False
        return True

    def safe_cell(self, c, boxes):
        if c not in self.danger:
            return True
        line, i = self.danger[c]
        return any(x in boxes for x in line[i + 1:])

    def rune_step(self, prog, c):
        k = self.rune_at.get(c)
        if k is None or prog >= self.n_runes: return prog
        if k == prog: return prog + 1
        if k == prog - 1: return prog
        return 1 if k == 0 else 0

    def box_ok(self, a, b, pl, boxes, *s):
        return (b in self.adj and b not in self.rocks and b not in boxes and b != pl and b not in self.tips
                and self.room_of.get(a) == self.room_of.get(b) and self.open_edge(a, b, pl, boxes, *s))

    def box_slide(self, b, d, pl, boxes, *s):
        while b in self.ice:
            nb = add(b, d)
            if not self.box_ok(b, nb, pl, boxes, *s):
                break
            b = nb
        return b

    def walk(self, cur, d, boxes, *s):
        """The player has just entered `cur`: on ice, keep sliding. Returns the cells passed (cur first)."""
        path = [cur]
        while cur in self.ice:
            nx = add(cur, d)
            if nx not in self.adj or nx in self.rocks or nx in boxes or not self.open_edge(cur, nx, cur, boxes, *s):
                break
            cur = nx
            path.append(cur)
        return path

    def successors(self, st):
        """[(next state, action, cells passed)]"""
        p, boxes, keys, lev, pull, opened, prog = st
        out = []
        if p == self.lever:
            out.append(((p, boxes, keys, 1 - lev, pull, opened, prog), "toggle", [p]))
        for color in self.doors_at.get(p, ()):
            if color in keys and color not in opened:
                out.append(((p, boxes, keys, lev, pull, opened | {color}, prog), "unlock", [p]))
        s = (lev, opened, prog)
        for d in DIRS:
            q1 = add(p, d)
            if q1 not in self.adj or q1 in self.rocks or not self.open_edge(p, q1, p, boxes, *s):
                continue
            options = []
            if q1 in boxes:  # push
                others = boxes - {q1}
                q2 = add(q1, d)
                if not self.box_ok(q1, q2, p, others, *s):
                    continue
                nb = others | {self.box_slide(q2, d, p, others, *s)}
                options.append((nb, self.walk(q1, d, nb, *s), "push"))
            else:
                path = self.walk(q1, d, boxes, *s)
                options.append((boxes, path, "move"))
                back = (p[0] - d[0], p[1] - d[1])
                if (pull and back in boxes and len(path) == 1 and p not in self.ice and back not in self.ice
                        and q1 not in self.ice and self.box_ok(back, p, q1, boxes - {back}, *s)):
                    options.append((boxes - {back} | {p}, path, "pull"))
            for nb, path, act in options:
                if self.danger and not all(self.safe_cell(c, nb) for c in path):
                    continue
                k1, pull1, prog1 = keys, pull, prog
                for c in path:
                    if c in self.keys_at: k1 = k1 | {self.keys_at[c]}
                    if c == self.glove_at: pull1 = True
                    prog1 = self.rune_step(prog1, c)
                out.append(((path[-1], nb, k1, lev, pull1, opened, prog1), act, path))
        return out


def solve(adj, S, Z, P, start_pull, glove=True, limit=400_000, via=None, safe=False, stats=None):
    """Shortest solution S→Z as [(state, action)] (the first action is None), or None.
    With `via`, that cell must be visited on the way. With `safe`, guards may not be touched.
    `stats` (a dict) receives the number of states explored."""
    R = Rules(adj, P, start_pull, glove, safe)
    start = R.start(S)
    if via is not None:
        start = start + (S == via,)
    prev = {start: None}
    q = deque([start])
    found = None
    while q:
        st = q.popleft()
        if st[0] == Z and (via is None or st[7]):
            found = st
            break
        if len(prev) > limit:
            break
        for s, act, path in R.successors(st[:7]):
            if via is not None:
                s = s + (st[7] or via in path,)
            if s not in prev:
                prev[s] = (st, act)
                q.append(s)
    if stats is not None:
        stats["states"] = len(prev)
    if found is None:
        return None
    out, st = [], found
    while st is not None:
        parent = prev[st]
        out.append((st[:7], parent[1] if parent else None))
        st = parent[0] if parent else None
    return out[::-1]


def dead_states(adj, S, Z, P, start_pull, limit=50_000, safe=False):
    """(reachable, dead): states reachable from S, and how many of them can no longer reach Z (a trap: R restarts).
    None if there are more than `limit` states."""
    R = Rules(adj, P, start_pull, safe=safe)
    start = R.start(S)
    succ = {start: None}
    q = deque([start])
    while q:
        st = q.popleft()
        outs = [s for s, _, _ in R.successors(st)]
        succ[st] = outs
        for s in outs:
            if s not in succ:
                if len(succ) > limit:
                    return None
                succ[s] = None
                q.append(s)
    rev = {}
    for s, outs in succ.items():
        for t in outs or ():
            rev.setdefault(t, []).append(s)
    good = {s for s in succ if s[0] == Z}
    q = deque(good)
    while q:
        for s in rev.get(q.popleft(), ()):
            if s not in good:
                good.add(s)
                q.append(s)
    return len(succ), len(succ) - len(good)


# --- puzzle rooms ------------------------------------------------------------------------------------------
# A room is generated in its own frame: cells (r, c) in an h×w box, plus the two outside cells in front of its
# openings (o_in, o_out). Content: walls inside the room, rocks, ice, items. Room puzzles never use keys or runes.

def room_adj(cells, edges, o_in, e_in, o_out, e_out):
    adj = {c: set() for c in cells}
    adj[o_in], adj[o_out] = {e_in}, {e_out}
    adj[e_in].add(o_in)
    adj[e_out].add(o_out)
    for a, b in edges:
        adj[a].add(b)
        adj[b].add(a)
    return adj


def connected(adj, start, blocked=(), edge=None):
    """Cells reachable from start, not entering `blocked` cells and not crossing `edge`."""
    seen, q = {start}, deque([start])
    while q:
        a = q.popleft()
        for n in adj[a]:
            if n not in seen and n not in blocked and E(a, n) != edge:
                seen.add(n)
                q.append(n)
    return seen


def blocked_edge(e):
    return dict(edge=E(*e))


def grid_edges(cells):
    cs = set(cells)
    return [(a, add(a, d)) for a in cells for d in ((0, 1), (1, 0)) if add(a, d) in cs]


def random_walls(cells, rnd, keep, density):
    """Open floor with some inner walls: drops random edges while every cell stays connected."""
    edges = grid_edges(cells)
    rnd.shuffle(edges)
    kept = set(edges)
    target = int(len(edges) * density)
    removed = 0
    for e in edges:
        if removed >= target:
            break
        if e in keep:
            continue
        kept.discard(e)
        adj = {c: set() for c in cells}
        for a, b in kept:
            adj[a].add(b); adj[b].add(a)
        if len(connected(adj, cells[0])) == len(cells):
            removed += 1
        else:
            kept.add(e)
    return sorted(kept)


def bridges(adj, nodes):
    """Edges whose removal splits `nodes` (brute force, rooms are small)."""
    out = []
    seen = set()
    ns = set(nodes)
    for a in nodes:
        for b in list(adj[a]):
            if b in ns and E(a, b) not in seen:
                seen.add(E(a, b))
                if len(connected(adj, a, edge=E(a, b)) & ns) != len(ns):
                    out.append((a, b))
    return out


def ice_room(frame, rnd, tier, tries=2500):
    """Eishalle: all ice, rocks, optionally one box (tier 3). Scored by the number of moves through it."""
    cells, o_in, e_in, o_out, e_out = frame
    want = {1: (3, 5), 2: (5, 8), 3: (6, 12)}[tier]
    best = None
    inner = [c for c in cells if c not in (e_in, e_out)]
    for _ in range(tries):
        rocks = frozenset(rnd.sample(inner, rnd.randint(1, max(1, len(cells) // 4))))
        box = None
        if tier >= 3:
            free = [c for c in inner if c not in rocks]
            box = rnd.choice(free)
        edges = grid_edges(cells)
        adj = room_adj(cells, edges, o_in, e_in, o_out, e_out)
        if o_out not in connected(adj, o_in, rocks):
            continue  # must stay walkable without the ice (the level solver walks through)
        P = empty_puzzle()
        P["rocks"] = rocks
        P["ice"] = frozenset(c for c in cells if c not in rocks)
        P["room_of"] = {c: 0 for c in cells}
        P["boxes"] = [box] if box else []
        sol = solve(adj, o_in, o_out, P, False, limit=60_000)
        if not sol:
            continue
        moves = len(sol) - 1
        if moves < want[0]:
            continue
        if box:
            P0 = dict(P, boxes=[])
            s0 = solve(adj, o_in, o_out, P0, False, limit=60_000)
            if s0 and len(s0) - 1 >= moves - 1:
                continue  # the box must help (or be needed)
        dead = dead_states(adj, o_in, o_out, P, False, limit=20_000)
        if dead is None or dead[1] > 0:
            continue  # no traps in a slide room: you can always get out again
        score = min(moves, want[1]) * 10 - len(rocks)
        if best is None or score > best[0]:
            best = (score, dict(edges=edges, P=P, solution=sol, moves=moves))
        if moves >= want[1]:
            break
    return best and best[1]


STATS = {}


def count(key):
    STATS[key] = STATS.get(key, 0) + 1


def mini_maze(cells, rnd, loops):
    """A small maze inside the room: random spanning tree plus `loops` extra openings."""
    cs = set(cells)
    start = rnd.choice(cells)
    seen, stack, edges = {start}, [start], set()
    while stack:
        cur = stack[-1]
        opts = [add(cur, d) for d in DIRS if add(cur, d) in cs and add(cur, d) not in seen]
        if not opts:
            stack.pop()
            continue
        n = rnd.choice(opts)
        edges.add(tuple(sorted((cur, n))))
        seen.add(n)
        stack.append(n)
    rest = [e for e in grid_edges(cells) if tuple(sorted(e)) not in edges]
    rnd.shuffle(rest)
    edges |= {tuple(sorted(e)) for e in rest[:loops]}
    return sorted(edges)


def way_bridges(adj, cells, o_in, o_out):
    """Bridges on the way through the room, ordered from the entrance."""
    cut = bridges(adj, cells)
    on_way = [e for e in cut if o_out not in connected(adj, o_in, edge=E(*e))]
    dist = {c: i for i, c in enumerate(bfs_order(adj, o_in))}
    return [tuple(sorted(e, key=lambda c: dist[c])) for e in sorted(on_way, key=lambda e: min(dist[c] for c in e))], cut


def loose_walls(cells, edges):
    """Inner walls of a rectangular room that touch neither the room's outline nor another wall chain reaching it
    (free-standing wall pieces; the maze has none)."""
    r0, c0 = min(r for r, c in cells), min(c for r, c in cells)
    r1, c1 = max(r for r, c in cells) + 1, max(c for r, c in cells) + 1
    open_ = {E(*e) for e in edges}
    g = {}
    for a, b in grid_edges(cells):
        if E(a, b) in open_:
            continue
        (ra, ca), (rb, cb) = a, b
        seg = (((ra, cb), (ra + 1, cb)) if ra == rb else ((rb, ca), (rb, ca + 1)))  # lattice points
        g.setdefault(seg[0], []).append(seg[1]); g.setdefault(seg[1], []).append(seg[0])
    border = [p for p in g if p[0] in (r0, r1) or p[1] in (c0, c1)]
    seen, q = set(border), deque(border)
    while q:
        for n in g[q.popleft()]:
            if n not in seen:
                seen.add(n); q.append(n)
    return len(seen) != len(g)


def bfs_order(adj, start):
    seen, q, out = {start}, deque([start]), []
    while q:
        a = q.popleft()
        out.append(a)
        for n in adj[a]:
            if n not in seen:
                seen.add(n)
                q.append(n)
    return out


def room_checks(adj, o_in, o_out, P, pull, sol, need_gone):
    """Every part in need_gone matters: without it (a puzzle variant), the room can't be solved or gets shorter."""
    for variant, must_fail in need_gone:
        s2 = solve(adj if "adj" not in variant else variant["adj"], o_in, o_out,
                   {k: v for k, v in dict(P, **variant).items() if k != "adj"}, pull, limit=40_000)
        if must_fail and s2:
            return False
        if not must_fail and s2 and len(s2) >= len(sol):
            return False
    return True


def without_edge(adj, e):
    adj2 = {c: set(n) for c, n in adj.items()}
    adj2[e[0]].discard(e[1])
    adj2[e[1]].discard(e[0])
    return adj2


def plates_room(frame, rnd, tier, pull=False, tries=6000):
    """Zwei Platten, eine Kiste. Stand on plate P yourself: gate p stays open while you push the box through it,
    onto plate Q behind it, which opens gate q on the way out. With the box on P instead, p opens for you,
    but the box is then stuck on the wrong side."""
    cells, o_in, e_in, o_out, e_out = frame
    want = {1: 16, 2: 24, 3: 32}[tier]
    best = None
    for _ in range(tries):
        edges = mini_maze(cells, rnd, rnd.randint(1, 3))
        if loose_walls(cells, edges):
            continue
        adj = room_adj(cells, edges, o_in, e_in, o_out, e_out)
        on_way, cut = way_bridges(adj, cells, o_in, o_out)
        if not on_way:
            continue
        gq = rnd.choice(on_way)
        sides = []
        for e in cut:
            if E(*e) == E(*gq):
                continue
            for a, b in (e, e[::-1]):
                far = connected(adj, b, edge=E(a, b))
                if o_in in far or o_out in far:
                    continue
                d = (b[0] - a[0], b[1] - a[1])
                pp = (a[0] - d[0], a[1] - d[1])
                if pp in adj[a] and pp in cells:
                    sides.append(((a, b), pp, b))
        if not sides:
            continue
        gp, pp, pq = rnd.choice(sides)
        near = connected(adj, o_in, edge=E(*gp)) & set(cells)
        free = [c for c in near if c not in (pp, gp[0], pq) and c not in gq]
        if not free:
            continue
        box = rnd.choice(free)
        P = empty_puzzle()
        P["boxes"] = [box]
        P["plates"] = [dict(edge=gp, plate=pp), dict(edge=gq, plate=pq)]
        P["room_of"] = {c: 0 for c in cells}
        sol = solve(adj, o_in, o_out, P, pull, limit=40_000)
        if not sol:
            count("plates unsolvable"); continue
        moves = len(sol) - 1
        if moves < want * 0.6:
            count("plates short"); continue
        if not room_checks(adj, o_in, o_out, P, pull, sol, [
                (dict(adj=without_edge(adj, gp), plates=P["plates"][1:]), True),
                (dict(adj=without_edge(adj, gq), plates=P["plates"][:1]), True),
                (dict(boxes=[]), True)]):
            count("plates part useless"); continue
        dead = dead_states(adj, o_in, o_out, P, pull, limit=30_000)
        if dead is None:
            continue
        count("plates ok")
        score = min(moves, want) * 10 + min(dead[1], 60)
        if best is None or score > best[0]:
            best = (score, dict(edges=edges, P=P, solution=sol, moves=moves, dead=dead))
        if moves >= want and dead[1] > 0:
            break
    return best and best[1]


def lever_room(frame, rnd, tier, pull=True, tries=20000):
    """Hebelkammer: the lever swaps walls A and B, a box has to reach the plate of a gate. Found by search: the
    shortest solution has to flip the lever back and forth (2+ times), so the order of switching is the puzzle."""
    cells, o_in, e_in, o_out, e_out = frame
    want = {1: (16, 2), 2: (26, 2), 3: (30, 3)}[tier]
    best = None
    for _ in range(tries):
        edges = mini_maze(cells, rnd, rnd.randint(1, 4))
        if loose_walls(cells, edges):
            continue
        adj = room_adj(cells, edges, o_in, e_in, o_out, e_out)
        on_way, cut = way_bridges(adj, cells, o_in, o_out)
        walls = [e for e in cut] + rnd.sample(edges, min(3, len(edges)))
        if len(walls) < 3:
            continue
        ea, eb, gp = rnd.sample(walls, 3)
        if len({E(*ea), E(*eb), E(*gp)}) < 3:
            continue
        free = [c for c in cells if c not in gp]
        lever, box, plate = rnd.sample(free, 3)
        P = empty_puzzle()
        P["lever"], P["boxes"] = lever, [box]
        P["plates"] = [dict(edge=gp, plate=plate)]
        P["switch_a"], P["switch_b"] = [ea], [eb]
        P["room_of"] = {c: 0 for c in cells}
        sol = solve(adj, o_in, o_out, P, pull, limit=40_000)
        if not sol:
            count("lever unsolvable"); continue
        moves = len(sol) - 1
        toggles = sum(a == "toggle" for _, a in sol)
        if toggles < want[1] or moves < want[0] * 0.6:
            count("lever too easy"); continue
        if not room_checks(adj, o_in, o_out, P, pull, sol, [
                (dict(boxes=[]), True),
                (dict(adj=without_edge(adj, gp), plates=[]), True)]):
            count("lever part useless"); continue
        dead = dead_states(adj, o_in, o_out, P, pull, limit=30_000)
        if dead is None:
            continue
        count("lever ok")
        score = min(moves, want[0]) * 10 + toggles * 20 + min(dead[1], 60)
        if best is None or score > best[0]:
            best = (score, dict(edges=edges, P=P, solution=sol, moves=moves, dead=dead, toggles=toggles))
        if moves >= want[0] and toggles > want[1]:
            break
    return best and best[1]


# Wächtergang, hand-made (rows top → bottom): the way comes in at the top of n, leaves at the bottom of x.
#        0    1    2    3    4    5    6
#   0   nw    n
#   1    w   C0   c1   c2    J   d1   d2      guard patrols c1..J; d1, d2 its niche
#   2         s1             x
# Tier 1: the box stands on C0 (push it aside into s1 and fight, or go round via nw/w and shove the guard).
# Tier 2: the box sits in the niche s1: pull it out first (needs the glove).
GUARD_CELLS = {"nw": (0, 0), "n": (0, 1), "w": (1, 0), "C0": (1, 1), "c1": (1, 2), "c2": (1, 3), "J": (1, 4),
               "d1": (1, 5), "d2": (1, 6), "s1": (2, 1), "x": (2, 4)}
GUARD_EDGES = [("n", "nw"), ("nw", "w"), ("w", "C0"), ("n", "C0"), ("C0", "s1"), ("C0", "c1"), ("c1", "c2"),
               ("c2", "J"), ("J", "d1"), ("d1", "d2"), ("J", "x")]
GUARD_OPEN = (("n", (-1, 0)), ("x", (1, 0)))  # (cell, direction to the outside): in, out


def transforms():
    """The 8 symmetries of the square as functions on (r, c) offsets."""
    out = []
    for rot in range(4):
        for flip in (False, True):
            def f(p, rot=rot, flip=flip):
                r, c = p
                if flip: c = -c
                for _ in range(rot): r, c = c, -r
                return r, c
            out.append(f)
    return out


def guard_room(frame, tier, start_pull):
    """frame here is the placed template: name → cell, plus o_in/o_out. Returns the room content."""
    cells, o_in, e_in, o_out, e_out, names = frame
    N = names
    edges = [(N[a], N[b]) for a, b in GUARD_EDGES]
    adj = room_adj(cells, edges, o_in, e_in, o_out, e_out)
    P = empty_puzzle()
    P["boxes"] = [N["C0"] if tier == 1 else N["s1"]]
    P["room_of"] = {c: 0 for c in cells}
    run = [N["c1"], N["c2"], N["J"]]
    P["guards"] = [dict(run=run, line=run + [N["d1"]], tip=N["d2"])]
    pull = start_pull or tier >= 2
    sol = solve(adj, o_in, o_out, P, pull, safe=True)
    fight = solve(adj, o_in, o_out, P, pull)
    if not sol or not fight:
        return None
    if solve(adj, o_in, o_out, dict(P, boxes=[]), pull, safe=True):
        return None
    return dict(edges=edges, P=P, solution=sol, moves=len(sol) - 1, run=run)
