"""Hand-drawn 16×16 sprites the Ninja Adventure pack doesn't have, in its palette (pack's Palette.png).

Usage: python3 Tools/draw_sprites.py   → writes PNGs into Assets/Resources/Art/Items/
Each sprite is a text grid: one character per pixel, '.' = transparent.
"""
import os, struct, zlib

PALETTE = {
    "o": "#141B1B",  # outline
    "d": "#965340", "m": "#BD7959", "h": "#D78B4A",  # leather
    "c": "#543C52", "C": "#8F3E56",                  # cuff
    "r": "#ABC2BC", "R": "#8E7C73",                  # metal rim
    "b": "#2D697B", "B": "#548789",                  # shield blue
    "y": "#FFE18D",                                  # gold emblem
    "s": "#8E7C73", "S": "#B3957F", "D": "#695953",  # stone
    "g": "#56864C", "G": "#ADBC3A",                  # rune dim / lit
    "w": "#9C6546",                                  # wood
    "K": "#A5608B", "L": "#D3A2C0",                  # lever knob / lit
}

GLOVE = """
................
.....o.o.o......
....omomomo.....
....omomomo.....
....omhmhmo.o...
....ommmmmooho..
.o..ommmmmohmo..
ohoooommmmmmmo..
ommhommmmmmmdo..
.ommmmmmmmmmdo..
..ommmmmmmmddo..
...ommmmmmddo...
...ocCCCCCCco...
...oCCCCCCCCo...
...occcccccco...
...oooooooooo...
"""

SHIELD = """
................
..oooooooooooo..
..orrrrrrrrrRo..
..orBBByybbbRo..
..orBByyyybbRo..
..orBBByybbbRo..
..orBBByybbbRo..
..orBBByybbbRo..
...orBByybbRo...
...orBByybbRo...
....orByybRo....
.....orbbRo.....
......orRo......
.......oo.......
................
................
"""

PLATE = """
................
.oooooooooooooo.
.oSSSSSSSSSSSSo.
.oSssssssssssDo.
.oSssgggggggsDo.
.oSssgsssssgsDo.
.oSssgsgggsgsDo.
.oSssgsgsgsgsDo.
.oSssgsgggsgsDo.
.oSssgsssssgsDo.
.oSssgggggggsDo.
.oSssssssssssDo.
.oSDDDDDDDDDDDo.
.oooooooooooooo.
................
................
"""

LEVER = """
................
..oo............
.oKKo...........
.oKKo...........
..oowo..........
....owo.........
.....owo........
......owo.......
.......owo......
....oooooooo....
...osSSSSSSDo...
..osSsssssssDo..
..osssssssssDo..
..oDDDDDDDDDDo..
..oooooooooooo..
................
"""


def grid(art):
    rows = [r for r in art.strip("\n").split("\n")]
    assert all(len(r) == len(rows[0]) for r in rows), [len(r) for r in rows]
    return rows


def write_png(path, rows):
    h, w = len(rows), len(rows[0])
    raw = b""
    for r in rows:
        raw += b"\x00"
        for ch in r:
            if ch == ".":
                raw += b"\x00\x00\x00\x00"
            else:
                c = PALETTE[ch].lstrip("#")
                raw += bytes(int(c[i:i + 2], 16) for i in (0, 2, 4)) + b"\xff"
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


out = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Art", "Items")
sprites = {
    "Glove": grid(GLOVE),
    "Shield": grid(SHIELD),
    "PlateUp": grid(PLATE),
    "PlateDown": grid(PLATE.replace("g", "G")),
    "LeverOff": grid(LEVER),
    "LeverOn": [r[::-1] for r in grid(LEVER.replace("K", "L"))],  # stick thrown to the other side, knob lit
}
for name, rows in sprites.items():
    write_png(os.path.join(out, name + ".png"), rows)
    print(name, f"{len(rows[0])}×{len(rows)}")
