using UnityEngine;

// How one level looks: its place in the world, light, effects and art. Purely visual – the maze, colliders
// and puzzles never depend on it. Art left null keeps the placeholder shapes (levels without a theme yet).
public class LevelTheme
{
    public string name = "Dunkelheit";

    // light
    public Color ambient = new(0.45f, 0.5f, 0.75f);
    public float ambientIntensity = 0.09f;
    public Color revealColor = new(0.75f, 0.82f, 1f); // level start: the whole maze lit for a moment
    public float revealIntensity = 0.85f;
    public Color playerLight = new(1f, 0.82f, 0.55f);
    public float playerLightRadius = 5f, playerLightIntensity = 1.25f, flicker = 0.06f;

    // 0 = calm … 1 = falling apart: scales vignette, colour split, grain, desaturation
    public float chaos;
    public bool lightning;          // now and then the whole maze flashes up for a heartbeat

    // sound (Resources/Audio): the world's music, a background loop and an occasional one-shot over it
    public string music;
    public string ambientLoop, ambientOneShot;
    public float ambientPitch = 1f;

    // effects
    public Color fogColor = new(0.6f, 0.65f, 0.85f);
    public float fogAlpha;          // drifting mist banks (0 = none)
    public float fogPer10Cells = 3f;
    public int firefliesPer10Cells;
    public Color fireflyColor = new(0.75f, 1f, 0.45f);

    // art
    public Sprite[] floor;          // random 16×16 floor tiles (repeat an entry to make it more common)
    public Color floorTint = Color.white;
    public Sprite[] floorDetail;    // sparse tufts and flowers on top of the floor
    public float floorDetailChance;
    public Sprite[] ripple;         // animated floor detail (water rings, lava vents), placed sparsely
    public float rippleChance;
    public Color? rippleLight;      // lava vents glow
    public Sprite[] hedge;          // 16×16 pieces strung along every wall
    public Color hedgeTint = Color.white; // darker than the floor, so walls read as walls
    public float hedgeTilt;         // degrees of random lean: walls look crooked as the world gets worse
    public Sprite[] crystals;       // glowing pieces mixed into the walls, each with its colour
    public Color[] crystalColors;
    public float crystalChance;
    public Sprite[] junction;       // bigger pieces (trees) where walls meet
    public float junctionChance;
    public Sprite[] lamps;          // light sources on some wall corners (lamp posts, glowing orbs)
    public Color[] lampColors;
    public float lampChance;
    public string patrolSheet, forcedSheet; // monster walk sheets
    public Color? patrolGlow;       // e.g. ghosts give off a cold light
    public Color patrolTint = Color.white;
    public Sprite box, heart, hint;
    public Sprite key, keyBlue;     // gold key for yellow doors, silver key (tinted) for blue ones
    public Sprite door;             // tiled across a locked door's edge; an emblem shows what opens it
    public Sprite glove, shield, plateUp, plateDown, leverOff, leverOn, letterA, letterB;
    public Sprite[] goal;
    public Color goalLight = new(0.55f, 0.9f, 1f);
    public FightLook fight = new();

    public bool HasArt => floor != null;

    public static LevelTheme For(int levelIndex) => levelIndex switch
    {
        0 => Forest(),
        1 => Cemetery(),
        2 => Lake(),
        3 => Caves(),
        4 => Volcano(),
        5 => Temple(),
        _ => new LevelTheme(),
    };

    const string FloorSheet = "Tiles/TilesetFloor", Nature = "Tiles/TilesetNature",
        Element = "Tiles/TilesetElement", Village = "Tiles/TilesetVillageAbandoned";

    // pickups and the exit look the same in every world, so players recognise them
    LevelTheme WithCommonItems()
    {
        box = ArtLib.Whole("Items/Crate");
        heart = ArtLib.Whole("Items/Heart");
        hint = ArtLib.Whole("Items/Scroll");
        key = ArtLib.Whole("Items/Key");
        keyBlue = ArtLib.Whole("Items/KeySilver");
        door = ArtLib.Tile(Element, 3, 11); // iron bars
        glove = ArtLib.Whole("Items/Glove");
        shield = ArtLib.Whole("Items/Shield");
        plateUp = ArtLib.Whole("Items/PlateUp");
        plateDown = ArtLib.Whole("Items/PlateDown");
        leverOff = ArtLib.Whole("Items/LeverOff");
        leverOn = ArtLib.Whole("Items/LeverOn");
        letterA = ArtLib.Tile("Tiles/TilesetLogic", 5, 6);
        letterB = ArtLib.Tile("Tiles/TilesetLogic", 5, 7);
        goal = ArtLib.Strip("FX/MagicCircle", 32);
        return this;
    }

    // Tutorial: the forest edge at night. Calm – moonlight and fireflies.
    static LevelTheme Forest()
    {
        Sprite N(int tx, int ty) => ArtLib.Tile(Nature, tx, ty);
        Sprite grass = ArtLib.Tile(FloorSheet, 11, 12);
        return new LevelTheme
        {
            name = "Waldrand bei Nacht",
            music = "Forest",
            ambientLoop = "Wind",
            ambient = new Color(0.42f, 0.52f, 0.85f),
            ambientIntensity = 0.1f,
            playerLight = new Color(1f, 0.8f, 0.5f),
            chaos = 0f,
            firefliesPer10Cells = 4,
            floor = new[]
            {
                grass, grass, grass, grass, grass, grass,
                ArtLib.Tile(FloorSheet, 12, 12), ArtLib.Tile(FloorSheet, 13, 12), ArtLib.Tile(FloorSheet, 14, 12),
                ArtLib.Tile(FloorSheet, 15, 12),
            },
            floorDetail = new[] { N(3, 10), N(8, 10), N(9, 10), N(0, 11), N(2, 11) },
            floorDetailChance = 0.035f,
            hedge = new[] { N(1, 10), N(10, 10), N(0, 10), N(4, 11), N(5, 11), N(6, 10) },
            hedgeTint = new Color(0.62f, 0.8f, 0.72f),
            junction = new[]
            {
                ArtLib.Cut(Nature, 32, 0, 32, 32), // dark pine
                ArtLib.Cut(Nature, 64, 0, 32, 32), // dead tree
                ArtLib.Cut(Nature, 32, 0, 32, 32),
            },
            junctionChance = 0.55f,
            patrolSheet = "Monsters/Slime2",
            forcedSheet = "Monsters/Mushroom",
            goalLight = new Color(0.5f, 0.85f, 1f),
            fight = new FightLook
            {
                border = new Color(0.72f, 0.92f, 0.62f),
                background = new Color(0.02f, 0.05f, 0.04f),
                glow = new Color(0.45f, 0.9f, 0.4f),
                bullet = ArtLib.Frames("Projectiles/PlantSpike", 4), // thorny seed pods
                bulletSize = 2.2f,
                bulletOrient = true,
            },
        }.WithCommonItems();
    }

    // Level 1: an abandoned village with its cemetery. Ground mist, lamp posts, the dead are awake.
    static LevelTheme Cemetery()
    {
        Sprite V(int tx, int ty) => ArtLib.Tile(Village, tx, ty);
        Sprite E(int tx, int ty) => ArtLib.Tile(Element, tx, ty);
        Sprite dirt = ArtLib.Tile(FloorSheet, 11, 19);
        return new LevelTheme
        {
            name = "Verlassenes Dorf",
            music = "Village",
            ambientLoop = "WindCold",
            ambient = new Color(0.5f, 0.56f, 0.72f),
            ambientIntensity = 0.085f,
            revealColor = new Color(0.78f, 0.82f, 0.92f),
            playerLight = new Color(1f, 0.78f, 0.5f),
            playerLightRadius = 4.8f,
            chaos = 0.15f,
            fogColor = new Color(0.72f, 0.78f, 0.88f),
            fogAlpha = 0.16f,
            fogPer10Cells = 3f,
            floor = new[]
            {
                dirt, dirt, dirt, dirt, dirt,
                ArtLib.Tile(FloorSheet, 12, 19), ArtLib.Tile(FloorSheet, 13, 19), ArtLib.Tile(FloorSheet, 14, 19),
                ArtLib.Tile(FloorSheet, 15, 19),
            },
            floorDetail = new[] { V(6, 3), V(7, 3), V(8, 3), V(9, 3), V(10, 4), ArtLib.Tile(Nature, 3, 10) },
            floorDetailChance = 0.03f,
            hedge = new[] { V(3, 4), V(3, 4), E(4, 2), E(3, 2), E(4, 2), V(4, 5), V(5, 5) },
            hedgeTint = new Color(0.66f, 0.7f, 0.74f),
            junction = new[]
            {
                ArtLib.Cut(Village, 32, 48, 16, 32), // mossy pillar
                ArtLib.Cut(Village, 0, 48, 32, 32),  // mossy statue
                ArtLib.Cut(Nature, 64, 0, 32, 32),   // dead tree
            },
            junctionChance = 0.4f,
            lamps = new[] { ArtLib.Cut(Element, 96, 48, 16, 32) },
            lampColors = new[] { new Color(1f, 0.7f, 0.35f) },
            lampChance = 0.12f,
            patrolSheet = "Monsters/Ghost",
            patrolGlow = new Color(0.55f, 1f, 0.7f),
            patrolTint = new Color(0.72f, 1f, 0.8f, 0.85f), // spectral green, never mistaken for the player
            forcedSheet = "Monsters/Skeleton",
            goalLight = new Color(0.55f, 0.9f, 1f),
            fight = new FightLook
            {
                border = new Color(0.85f, 0.84f, 0.78f), // bone
                background = new Color(0.04f, 0.045f, 0.035f),
                glow = new Color(0.62f, 0.72f, 0.45f),     // sickly grave-green
                bullet = new[] { ArtLib.Whole("Projectiles/Bone") }, // tumbling bones
                bulletTint = new Color(0.95f, 0.93f, 0.85f),
                bulletSize = 2.6f,
                bulletSpin = 540f,
            },
        }.WithCommonItems();
    }

    // Level 2: a misty lake. You wade through shallow water between reeds and rocks; thick fog, cold light.
    static LevelTheme Lake()
    {
        const string water = "Tiles/TilesetWater";
        Sprite N(int tx, int ty) => ArtLib.Tile(Nature, tx, ty);
        Sprite W(int tx, int ty) => ArtLib.Tile(water, tx, ty);
        Sprite shallow = W(1, 1); // plain; the variants below share its colour
        return new LevelTheme
        {
            name = "Nebelsee",
            music = "Lake",
            ambientLoop = "Water",
            ambient = new Color(0.4f, 0.55f, 0.8f),
            ambientIntensity = 0.085f,
            revealColor = new Color(0.7f, 0.82f, 0.95f),
            playerLight = new Color(1f, 0.82f, 0.6f),
            playerLightRadius = 4.6f,
            chaos = 0.3f,
            fogColor = new Color(0.75f, 0.85f, 0.95f),
            fogAlpha = 0.2f,
            fogPer10Cells = 4f,
            firefliesPer10Cells = 1, // will-o'-wisps over the water
            fireflyColor = new Color(0.55f, 0.85f, 1f),
            floor = new[] { shallow, shallow, shallow, shallow, shallow, shallow, shallow, shallow, W(11, 2), W(11, 1), W(11, 4) },
            floorDetail = new[] { W(11, 3), W(11, 3), N(3, 10) }, // lily pads, a tuft of reed
            floorDetailChance = 0.02f,
            ripple = ArtLib.Strip("FX/Ripples", 16),
            rippleChance = 0.02f,
            hedge = new[] { N(6, 12), N(7, 12), N(8, 12), N(3, 10), N(8, 10), N(9, 10), N(3, 10) },
            hedgeTint = new Color(0.55f, 0.75f, 0.78f),
            junction = new[]
            {
                ArtLib.Cut(Nature, 0, 192, 32, 32), // big blue boulder
                ArtLib.Cut(Nature, 64, 0, 32, 32),  // drowned dead tree
            },
            junctionChance = 0.45f,
            patrolSheet = "Monsters/Axolotl",
            forcedSheet = "Monsters/Kappa",
            goalLight = new Color(0.55f, 0.9f, 1f),
            fight = new FightLook
            {
                border = new Color(0.55f, 0.85f, 0.95f),
                background = new Color(0.01f, 0.03f, 0.06f),
                glow = new Color(0.35f, 0.7f, 1f),
                bullet = ArtLib.Frames("Projectiles/EnergyBall", 4), // water drops
                bulletSize = 1.9f,
            },
        }.WithCommonItems();
    }

    // Level 3: crystal caves. Underground – no moon, only your lantern and the crystals. The walls start to lean.
    static LevelTheme Caves()
    {
        const string dungeon = "Tiles/TilesetDungeon";
        Sprite N(int tx, int ty) => ArtLib.Tile(Nature, tx, ty);
        Sprite D(int tx, int ty) => ArtLib.Tile(dungeon, tx, ty);
        Sprite rock = ArtLib.Tile(FloorSheet, 11, 19);
        return new LevelTheme
        {
            name = "Kristallhöhlen",
            music = "Caves",
            ambientLoop = "WindCold",
            ambientPitch = 0.7f,
            ambientOneShot = "Drips",
            ambient = new Color(0.45f, 0.4f, 0.75f),
            ambientIntensity = 0.06f,
            revealColor = new Color(0.72f, 0.65f, 1f), // a crystal flare instead of moonlight
            revealIntensity = 1.15f,
            playerLight = new Color(1f, 0.8f, 0.55f),
            playerLightRadius = 4.4f,
            flicker = 0.14f,
            chaos = 0.5f,
            floor = new[]
            {
                rock, rock, rock, rock, rock,
                ArtLib.Tile(FloorSheet, 12, 19), ArtLib.Tile(FloorSheet, 13, 19), ArtLib.Tile(FloorSheet, 14, 19),
            },
            floorTint = new Color(0.62f, 0.6f, 0.8f),
            floorDetail = new[] { N(10, 12), N(6, 12), N(4, 13) }, // pebbles
            floorDetailChance = 0.02f,
            hedge = new[] { N(4, 16), N(5, 16), N(6, 16), N(4, 16), N(6, 14) },
            hedgeTint = new Color(0.6f, 0.62f, 0.85f),
            hedgeTilt = 9f,
            crystals = new[] { N(1, 14), N(2, 14), N(1, 15), N(2, 15) },
            crystalColors = new[]
            {
                new Color(0.4f, 0.85f, 1f), new Color(0.85f, 0.5f, 1f), new Color(0.4f, 0.85f, 1f), new Color(0.85f, 0.5f, 1f),
            },
            crystalChance = 0.12f,
            junction = new[] { ArtLib.Cut(Nature, 112, 256, 32, 32), ArtLib.Cut(Nature, 112, 224, 32, 32) }, // ore boulders
            junctionChance = 0.35f,
            lamps = new[] { D(4, 2), D(5, 2), D(3, 2) }, // orbs on pedestals
            lampColors = new[] { new Color(0.45f, 0.75f, 1f), new Color(0.9f, 0.9f, 1f), new Color(0.5f, 1f, 0.55f) },
            lampChance = 0.15f,
            patrolSheet = "Monsters/Bat",
            forcedSheet = "Monsters/Cyclops",
            goalLight = new Color(0.75f, 0.6f, 1f),
            fight = new FightLook
            {
                border = new Color(0.78f, 0.68f, 1f),
                background = new Color(0.03f, 0.02f, 0.06f),
                glow = new Color(0.6f, 0.4f, 1f),
                bullet = ArtLib.Frames("Projectiles/IceSpike", 4), // crystal shards
                music = "FightHard",
                bulletTint = new Color(0.85f, 0.7f, 1f),
                bulletSize = 3f,
                bulletOrient = true,
            },
        }.WithCommonItems();
    }

    // Level 4: the volcano. Red glow, lava vents in the basalt, embers, smoke; the lantern shrinks.
    static LevelTheme Volcano()
    {
        const string dungeon = "Tiles/TilesetDungeon";
        Sprite N(int tx, int ty) => ArtLib.Tile(Nature, tx, ty);
        Sprite stone = ArtLib.Tile(FloorSheet, 0, 19);
        return new LevelTheme
        {
            name = "Vulkan",
            music = "Volcano",
            ambientLoop = "WindCold",
            ambientPitch = 0.5f, // a low rumble
            ambientOneShot = "Fire",
            ambient = new Color(0.85f, 0.35f, 0.25f),
            ambientIntensity = 0.1f,
            revealColor = new Color(1f, 0.6f, 0.45f), // an eruption's glare
            revealIntensity = 1.5f,
            playerLight = new Color(1f, 0.85f, 0.65f),
            playerLightRadius = 4.3f,
            flicker = 0.18f,
            chaos = 0.7f,
            fogColor = new Color(0.35f, 0.3f, 0.3f), // smoke
            fogAlpha = 0.22f,
            fogPer10Cells = 3f,
            firefliesPer10Cells = 3, // embers
            fireflyColor = new Color(1f, 0.55f, 0.2f),
            floor = new[]
            {
                stone, stone, stone, stone,
                ArtLib.Tile(FloorSheet, 1, 19), ArtLib.Tile(FloorSheet, 2, 19), ArtLib.Tile(FloorSheet, 3, 19),
                ArtLib.Tile(FloorSheet, 4, 19),
            },
            floorTint = new Color(0.42f, 0.29f, 0.27f),
            floorDetail = new[] { N(4, 13), N(10, 12) },
            floorDetailChance = 0.015f,
            ripple = ArtLib.Frames("FX/Fire", 8), // lava vents
            rippleChance = 0.012f,
            rippleLight = new Color(1f, 0.45f, 0.15f),
            hedge = new[] { N(4, 15), N(5, 15), N(6, 15), N(4, 17), N(6, 17) },
            hedgeTint = new Color(0.5f, 0.36f, 0.33f),
            hedgeTilt = 12f,
            crystals = new[] { N(0, 14), N(0, 15) }, // glowing lava rock
            crystalColors = new[] { new Color(1f, 0.45f, 0.15f), new Color(1f, 0.55f, 0.2f) },
            crystalChance = 0.12f,
            junction = new[] { ArtLib.Cut(Nature, 160, 256, 32, 32), ArtLib.Cut(Nature, 160, 224, 32, 32) },
            junctionChance = 0.35f,
            lamps = new[] { ArtLib.Tile(dungeon, 6, 2), ArtLib.Tile(dungeon, 7, 2) }, // fire orbs
            lampColors = new[] { new Color(1f, 0.3f, 0.2f), new Color(1f, 0.6f, 0.2f) },
            lampChance = 0.12f,
            patrolSheet = "Monsters/FireSpirit",
            forcedSheet = "Monsters/Beast",
            goalLight = new Color(1f, 0.55f, 0.3f),
            fight = new FightLook
            {
                border = new Color(1f, 0.55f, 0.3f),
                background = new Color(0.07f, 0.015f, 0.01f),
                glow = new Color(1f, 0.3f, 0.1f),
                bullet = ArtLib.Frames("Projectiles/Fireball", 4),
                music = "FightHard",
                bulletSize = 2.2f,
                bulletOrient = true,
            },
        }.WithCommonItems();
    }

    // Level 5: the temple in the volcano's heart. Black stone, red runes, lightning; everything falls apart.
    static LevelTheme Temple()
    {
        const string dungeon = "Tiles/TilesetDungeon", relief = "Tiles/TilesetReliefDetail";
        Sprite stone = ArtLib.Tile(FloorSheet, 0, 19);
        return new LevelTheme
        {
            name = "Tempel im Vulkan",
            music = "Temple",
            ambientLoop = "Storm",
            ambient = new Color(0.6f, 0.3f, 0.6f),
            ambientIntensity = 0.1f,
            revealColor = new Color(0.85f, 0.75f, 1f),
            revealIntensity = 1.6f,
            playerLight = new Color(1f, 0.85f, 0.7f),
            playerLightRadius = 4.2f,
            flicker = 0.22f,
            chaos = 1f,
            lightning = true,
            fogColor = new Color(0.3f, 0.2f, 0.35f),
            fogAlpha = 0.2f,
            fogPer10Cells = 3f,
            firefliesPer10Cells = 2, // drifting sparks of the void
            fireflyColor = new Color(0.85f, 0.35f, 1f),
            floor = new[]
            {
                stone, stone, stone, stone, stone,
                ArtLib.Tile(FloorSheet, 1, 19), ArtLib.Tile(FloorSheet, 3, 19), ArtLib.Tile(FloorSheet, 4, 19),
            },
            floorTint = new Color(0.3f, 0.25f, 0.4f),
            ripple = ArtLib.Frames("FX/Fire", 8), // rune fires
            rippleChance = 0.008f,
            rippleLight = new Color(0.8f, 0.25f, 1f),
            hedge = new[] { ArtLib.Tile(relief, 1, 2), ArtLib.Tile(dungeon, 8, 3), ArtLib.Tile(relief, 1, 2), ArtLib.Tile(dungeon, 2, 3) },
            hedgeTint = new Color(0.45f, 0.38f, 0.55f),
            hedgeTilt = 16f,
            crystals = new[] { ArtLib.Tile(dungeon, 6, 2), ArtLib.Tile(Nature, 2, 15) },
            crystalColors = new[] { new Color(1f, 0.25f, 0.3f), new Color(0.8f, 0.35f, 1f) },
            crystalChance = 0.1f,
            junction = new[] { ArtLib.Cut(Village, 32, 48, 16, 32), ArtLib.Cut(Village, 0, 48, 32, 32) }, // pillars, idols
            junctionChance = 0.4f,
            lamps = new[] { ArtLib.Tile(dungeon, 2, 2) }, // braziers
            lampColors = new[] { new Color(1f, 0.35f, 0.5f) },
            lampChance = 0.12f,
            patrolSheet = "Monsters/Skull",
            forcedSheet = "Monsters/Demon",
            goalLight = new Color(1f, 0.85f, 0.6f),
            fight = new FightLook
            {
                border = new Color(0.95f, 0.3f, 0.4f),
                background = new Color(0.04f, 0.01f, 0.05f),
                glow = new Color(0.75f, 0.15f, 0.6f),
                bullet = ArtLib.Frames("Projectiles/CanonBall", 5), // orbs of darkness
                music = "FightHard",
                bulletTint = new Color(0.9f, 0.62f, 1f),
                bulletSize = 1.9f,
            },
        }.WithCommonItems();
    }
}

// The fight screen in a level's colours: frame, background glow, and what the bullets look like.
// Only the look – hitboxes stay the plain circles AttackPatterns spawns.
public class FightLook
{
    public Color border = Color.white;
    public Color background = Color.black;
    public Color glow = new(1f, 0.8f, 0.5f);
    public Sprite[] bullet;           // animation frames; null = plain white dots
    public Color bulletTint = Color.white;
    public float bulletSize = 2f;     // drawn size relative to the hitbox diameter
    public bool bulletOrient;         // turn the sprite along its flight direction (thorns, spikes)
    public string music = "Fight";
    public float bulletSpin;          // degrees per second, for tumbling bullets (bones)
}
