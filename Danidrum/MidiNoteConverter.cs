namespace Danidrum;

using System.Collections.Generic;

public enum DrumArticulation
{
    // --- Kick/Bass Drum (General MIDI Notes 35, 36) ---
    // Note: Kick refers to the standard Bass Drum 1 (GM 36 / TD 36)
    Kick = 0,
    AcousticBassDrum, // GM 35 (Acoustic Bass Drum)
    ElectricKick,     // GM 40 (often used for powerful, processed bass drum sounds in electronic kits)

    // --- Snare (General MIDI Notes 37, 38, 39, 40) ---
    SnareHead,         // TD 38 (SNARE (HEAD)) - The primary hit zone
    SnareRimshot,      // TD 40 (SNARE (RIM)) - The rim/head strike
    SnareCrossStick,   // TD 37 (SNARE X-Stick / GM Side Stick)
    AcousticSnare,     // GM 38 (Acoustic Snare)
    ElectricSnare,     // GM 40 (Electric Snare)
    HandClap,          // GM 39 (Necessary for GM completeness)

    // --- Toms (General MIDI Notes 41, 43, 45, 47, 48, 50) ---
    HighTom,          // GM 50
    HiMidTom,         // GM 48
    LowMidTom,        // GM 47
    LowTom,           // GM 45
    HighFloorTom,     // GM 43
    LowFloorTom,      // GM 41

    // --- Hi-Hat (General MIDI Notes 42, 44, 46, + TD articulations) ---
    HiHatClosedGeneric, // GM 42 (Closed Hi-Hat)
    HiHatOpenGeneric,   // GM 46 (Open Hi-Hat)
    HiHatPedal,         // GM 44 / TD 44
    HiHatClosedBow,     // TD 42 (Specific strike zone)
    HiHatClosedEdge,    // TD 22 (Specific strike zone)
    HiHatOpenBow,       // TD 46 (Specific strike zone)
    HiHatOpenEdge,      // TD 26 (Specific strike zone)
    HiHatFootSplash,    // Common articulation not in GM standard, but useful

    // --- Cymbals (General MIDI Notes 49, 51, 52, 53, 55, 57, 59) ---
    CrashCymbal1,     // GM 49
    Crash1Bow,        // TD 49 (Specific strike zone)
    Crash1Edge,       // TD 55 (Roland's default assignment for the edge)
    CrashCymbal2,     // GM 57
    Crash2Bow,        // TD 57 (Specific strike zone)
    Crash2Edge,       // TD 52 (Roland's default assignment for the edge)
    RideCymbal1,      // GM 51
    RideBow,          // TD 51 (Specific strike zone)
    RideBell,         // GM 53 / TD 53
    RideCymbal2,      // GM 59
    RideEdge,         // TD 59 (Specific strike zone)
    SplashCymbal,     // GM 55
    ChinaCymbal,      // GM 52

    // --- Auxiliary Percussion (GM Notes 54, 56, 58, 60-81) ---
    Tambourine,        // GM 54
    Cowbell,           // GM 56
    Vibraslap,         // GM 58
    HiBongo,           // GM 60
    LowBongo,          // GM 61
    MuteHiConga,       // GM 62
    OpenHiConga,       // GM 63
    LowConga,          // GM 64
    HighTimbale,       // GM 65
    LowTimbale,        // GM 66
    HighAgogo,         // GM 67
    LowAgogo,          // GM 68
    Cabasa,            // GM 69
    Maracas,           // GM 70
    ShortWhistle,      // GM 71
    LongWhistle,       // GM 72
    ShortGuiro,        // GM 73
    LongGuiro,         // GM 74
    Claves,            // GM 75
    HighWoodBlock,     // GM 76
    LowWoodBlock,      // GM 77
    MuteCuica,         // GM 78
    OpenCuica,         // GM 79
    MuteTriangle,      // GM 80
    OpenTriangle       // GM 81
}

public enum KitArticulation
{
    // Cymbals
    Crash,
    Ride,

    // Toms
    HighTom,
    MidTom,
    LowTom,

    //HiHat
    OpenHiHat,
    ClosedHiHat,

    // Drums
    Snare,
    Kick,

    // Control
    PedalHiHat,

    // Rest
    OtherPercussion
}

public static class Articulation
{

    public static KitArticulation GetKitArticulation(int gmNoteNumber)
    {
        if (!GmNoteToArticulation.TryGetValue(gmNoteNumber, out var drumArticulation))
        {
            return KitArticulation.OtherPercussion;
        }

        return ArticulationToKitArticulation[drumArticulation];
    }


    public static readonly IReadOnlyDictionary<DrumArticulation, string> ArticulationToName = new Dictionary<DrumArticulation, string>
    {
        // --- Kick/Bass Drum ---
        { DrumArticulation.Kick, "Kick Drum" },
        { DrumArticulation.AcousticBassDrum, "Acoustic Kick" },
        { DrumArticulation.ElectricKick, "Electric Kick" },

        // --- Snare ---
        { DrumArticulation.SnareHead, "Snare Head" },
        { DrumArticulation.SnareRimshot, "Snare Rimshot" },
        { DrumArticulation.SnareCrossStick, "Snare Cross Stick" },
        { DrumArticulation.AcousticSnare, "Acoustic Snare" },
        { DrumArticulation.ElectricSnare, "Electric Snare" },
        { DrumArticulation.HandClap, "Hand Clap" },

        // --- Toms ---
        { DrumArticulation.HighTom, "High Tom" },
        { DrumArticulation.HiMidTom, "High Mid Tom" },
        { DrumArticulation.LowMidTom, "Low Mid Tom" },
        { DrumArticulation.LowTom, "Low Tom" },
        { DrumArticulation.HighFloorTom, "High Floor Tom" },
        { DrumArticulation.LowFloorTom, "Low Floor Tom" },

        // --- Hi-Hat ---
        { DrumArticulation.HiHatClosedGeneric, "Closed Hi-Hat" },
        { DrumArticulation.HiHatOpenGeneric, "Open Hi-Hat" },
        { DrumArticulation.HiHatPedal, "Hi-Hat Pedal" },
        { DrumArticulation.HiHatClosedBow, "Closed Hi-Hat Bow" },
        { DrumArticulation.HiHatClosedEdge, "Closed Hi-Hat Edge" },
        { DrumArticulation.HiHatOpenBow, "Open Hi-Hat Bow" },
        { DrumArticulation.HiHatOpenEdge, "Open Hi-Hat Edge" },
        { DrumArticulation.HiHatFootSplash, "Hi-Hat Foot Splash" },

        // --- Cymbals ---
        { DrumArticulation.CrashCymbal1, "Crash Cymbal 1" },
        { DrumArticulation.Crash1Bow, "Crash 1 Bow" },
        { DrumArticulation.Crash1Edge, "Crash 1 Edge" },
        { DrumArticulation.CrashCymbal2, "Crash Cymbal 2" },
        { DrumArticulation.Crash2Bow, "Crash 2 Bow" },
        { DrumArticulation.Crash2Edge, "Crash 2 Edge" },
        { DrumArticulation.RideCymbal1, "Ride Cymbal 1" },
        { DrumArticulation.RideBow, "Ride Bow" },
        { DrumArticulation.RideBell, "Ride Bell" },
        { DrumArticulation.RideCymbal2, "Ride Cymbal 2" },
        { DrumArticulation.RideEdge, "Ride Edge" },
        { DrumArticulation.SplashCymbal, "Splash Cymbal" },
        { DrumArticulation.ChinaCymbal, "China Cymbal" },

        // --- Auxiliary Percussion ---
        { DrumArticulation.Tambourine, "Tambourine" },
        { DrumArticulation.Cowbell, "Cowbell" },
        { DrumArticulation.Vibraslap, "Vibraslap" },
        { DrumArticulation.HiBongo, "High Bongo" },
        { DrumArticulation.LowBongo, "Low Bongo" },
        { DrumArticulation.MuteHiConga, "Muted High Conga" },
        { DrumArticulation.OpenHiConga, "Open High Conga" },
        { DrumArticulation.LowConga, "Low Conga" },
        { DrumArticulation.HighTimbale, "High Timbale" },
        { DrumArticulation.LowTimbale, "Low Timbale" },
        { DrumArticulation.HighAgogo, "High Agogo" },
        { DrumArticulation.LowAgogo, "Low Agogo" },
        { DrumArticulation.Cabasa, "Cabasa" },
        { DrumArticulation.Maracas, "Maracas" },
        { DrumArticulation.ShortWhistle, "Short Whistle" },
        { DrumArticulation.LongWhistle, "Long Whistle" },
        { DrumArticulation.ShortGuiro, "Short Guiro" },
        { DrumArticulation.LongGuiro, "Long Guiro" },
        { DrumArticulation.Claves, "Claves" },
        { DrumArticulation.HighWoodBlock, "High Wood Block" },
        { DrumArticulation.LowWoodBlock, "Low Wood Block" },
        { DrumArticulation.MuteCuica, "Muted Cuica" },
        { DrumArticulation.OpenCuica, "Open Cuica" },
        { DrumArticulation.MuteTriangle, "Muted Triangle" },
        { DrumArticulation.OpenTriangle, "Open Triangle" }
    };

    public static readonly IReadOnlyDictionary<KitArticulation, string> KitArticulationToName = new Dictionary<KitArticulation, string>
    {
        // Cymbals
        { KitArticulation.Crash, "Crash Cymbal" },
        { KitArticulation.Ride, "Ride Cymbal" },
        { KitArticulation.OpenHiHat, "Open Hi-Hat" },
        { KitArticulation.ClosedHiHat, "Closed Hi-Hat" },

        // Drums
        { KitArticulation.HighTom, "High Tom" },
        { KitArticulation.MidTom, "Mid Tom" },
        { KitArticulation.LowTom, "Low Tom (Floor)" },
        { KitArticulation.Snare, "Snare Drum" },
        { KitArticulation.Kick, "Kick Drum" },

        // Control / Effects
        { KitArticulation.PedalHiHat, "Hi-Hat Pedal" },
        
        // Auxiliary / Unmapped
        { KitArticulation.OtherPercussion, "Other Percussion/FX" }
    };


    // The mapping of Roland TD-07DMK's specific MIDI note numbers to the unified enum.
    // https://support.roland.com/hc/en-us/articles/38775662946715-TD-07DMK-Default-MIDI-Note-Map?utm_source=chatgpt.com
    public static readonly IReadOnlyDictionary<int, DrumArticulation> Td07NoteToArticulation = new Dictionary<int, DrumArticulation>
    {
        // Kick
        { 36, DrumArticulation.Kick }, 

        // Snare
        { 38, DrumArticulation.SnareHead },
        { 40, DrumArticulation.SnareRimshot },
        { 37, DrumArticulation.SnareCrossStick },

        // Toms (TD maps to the High-Mid-Low Tom structure)
        { 48, DrumArticulation.HighTom },     // TOM 1
        { 45, DrumArticulation.LowTom },      // TOM 2
        { 43, DrumArticulation.LowFloorTom }, // TOM 3

        // Hi-Hat (TD uses high-detail articulation specific notes)
        { 42, DrumArticulation.HiHatClosedBow },
        { 22, DrumArticulation.HiHatClosedEdge },
        { 46, DrumArticulation.HiHatOpenBow },
        { 26, DrumArticulation.HiHatOpenEdge },
        { 44, DrumArticulation.HiHatPedal },

        // Crash Cymbals (TD uses bow/edge specific notes)
        { 49, DrumArticulation.Crash1Bow },
        { 55, DrumArticulation.Crash1Edge },
        { 57, DrumArticulation.Crash2Bow },
        { 52, DrumArticulation.Crash2Edge }, // Note: TD uses 52 for Crash 2 Edge, GM uses 52 for China Cymbal.

        // Ride Cymbals (TD uses high-detail articulation specific notes)
        { 51, DrumArticulation.RideBow },
        { 59, DrumArticulation.RideEdge },
        { 53, DrumArticulation.RideBell }
    };
    public static readonly IReadOnlyDictionary<DrumArticulation, int> Td07ArticulationToNote = Td07NoteToArticulation.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    // The mapping of General MIDI (GM) standard note numbers (Percussion Key Map 35-81)
    // to the unified enum, favoring generic/timbral distinctions where available.
    // https://musescore.org/sites/musescore.org/files/General%20MIDI%20Standard%20Percussion%20Set%20Key%20Map.pdf
    public static readonly IReadOnlyDictionary<int, DrumArticulation> GmNoteToArticulation = new Dictionary<int, DrumArticulation>
    {
        // --- Kick/Bass Drum (Notes 35-36) ---
        { 35, DrumArticulation.AcousticBassDrum }, // Acoustic Bass Drum
        { 36, DrumArticulation.Kick },             // Bass Drum 1 (Standard Kick)

        // --- Snare (Notes 37-40) ---
        { 37, DrumArticulation.SnareCrossStick },  // Side Stick (Cross Stick)
        { 38, DrumArticulation.AcousticSnare },    // Acoustic Snare
        { 39, DrumArticulation.HandClap },         // Hand Clap
        { 40, DrumArticulation.ElectricSnare },    // Electric Snare

        // --- Toms (Notes 41, 43, 45, 47, 48, 50) ---
        { 41, DrumArticulation.LowFloorTom },      // Low Floor Tom
        { 43, DrumArticulation.HighFloorTom },     // High Floor Tom
        { 45, DrumArticulation.LowTom },           // Low Tom
        { 47, DrumArticulation.LowMidTom },        // Low-Mid Tom
        { 48, DrumArticulation.HiMidTom },         // Hi-Mid Tom
        { 50, DrumArticulation.HighTom },          // High Tom

        // --- Hi-Hat (Notes 42, 44, 46) ---
        { 42, DrumArticulation.HiHatClosedGeneric }, // Closed Hi-Hat
        { 44, DrumArticulation.HiHatPedal },         // Pedal Hi-Hat
        { 46, DrumArticulation.HiHatOpenGeneric },   // Open Hi-Hat

        // --- Cymbals (Notes 49, 51-53, 55, 57, 59) ---
        { 49, DrumArticulation.CrashCymbal1 },     // Crash Cymbal 1
        { 51, DrumArticulation.RideCymbal1 },      // Ride Cymbal 1
        { 52, DrumArticulation.ChinaCymbal },      // Chinese Cymbal
        { 53, DrumArticulation.RideBell },         // Ride Bell
        { 55, DrumArticulation.SplashCymbal },     // Splash Cymbal
        { 57, DrumArticulation.CrashCymbal2 },     // Crash Cymbal 2
        { 59, DrumArticulation.RideCymbal2 },      // Ride Cymbal 2

        // --- Auxiliary Percussion (Notes 54, 56, 58, 60-81) ---
        { 54, DrumArticulation.Tambourine },
        { 56, DrumArticulation.Cowbell },
        { 58, DrumArticulation.Vibraslap },
        { 60, DrumArticulation.HiBongo },
        { 61, DrumArticulation.LowBongo },
        { 62, DrumArticulation.MuteHiConga },
        { 63, DrumArticulation.OpenHiConga },
        { 64, DrumArticulation.LowConga },
        { 65, DrumArticulation.HighTimbale },
        { 66, DrumArticulation.LowTimbale },
        { 67, DrumArticulation.HighAgogo },
        { 68, DrumArticulation.LowAgogo },
        { 69, DrumArticulation.Cabasa },
        { 70, DrumArticulation.Maracas },
        { 71, DrumArticulation.ShortWhistle },
        { 72, DrumArticulation.LongWhistle },
        { 73, DrumArticulation.ShortGuiro },
        { 74, DrumArticulation.LongGuiro },
        { 75, DrumArticulation.Claves },
        { 76, DrumArticulation.HighWoodBlock },
        { 77, DrumArticulation.LowWoodBlock },
        { 78, DrumArticulation.MuteCuica },
        { 79, DrumArticulation.OpenCuica },
        { 80, DrumArticulation.MuteTriangle },
        { 81, DrumArticulation.OpenTriangle }
    };
    public static readonly IReadOnlyDictionary<DrumArticulation, int> GmArticulationToNote = GmNoteToArticulation.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    private static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public static readonly IReadOnlyDictionary<DrumArticulation, KitArticulation> ArticulationToKitArticulation = new Dictionary<DrumArticulation, KitArticulation>
{
    // --- KICK (All kicks map to the main Kick lane) ---
    { DrumArticulation.Kick, KitArticulation.Kick },
    { DrumArticulation.AcousticBassDrum, KitArticulation.Kick },
    { DrumArticulation.ElectricKick, KitArticulation.Kick },

    // --- SNARE (All snare hits map to the main Snare lane) ---
    { DrumArticulation.SnareHead, KitArticulation.Snare },
    { DrumArticulation.SnareRimshot, KitArticulation.Snare },
    { DrumArticulation.SnareCrossStick, KitArticulation.Snare },
    { DrumArticulation.AcousticSnare, KitArticulation.Snare },
    { DrumArticulation.ElectricSnare, KitArticulation.Snare },
    { DrumArticulation.HandClap, KitArticulation.OtherPercussion }, // Hand Clap is auxiliary

    // --- TOMS (Consolidating 6 GM Toms into 3 Kit Lanes) ---
    { DrumArticulation.HighTom, KitArticulation.HighTom },       // GM 50
    { DrumArticulation.HiMidTom, KitArticulation.HighTom },      // GM 48 -> HighTom
    { DrumArticulation.LowMidTom, KitArticulation.MidTom },      // GM 47 -> MidTom
    { DrumArticulation.LowTom, KitArticulation.LowTom },         // GM 45
    { DrumArticulation.HighFloorTom, KitArticulation.LowTom },   // GM 43 -> LowTom
    { DrumArticulation.LowFloorTom, KitArticulation.LowTom },    // GM 41 -> LowTom

    // --- HI-HAT (Mapping detailed articulations to generic states) ---
    { DrumArticulation.HiHatClosedGeneric, KitArticulation.ClosedHiHat },
    { DrumArticulation.HiHatClosedBow, KitArticulation.ClosedHiHat },
    { DrumArticulation.HiHatClosedEdge, KitArticulation.ClosedHiHat },
    { DrumArticulation.HiHatOpenGeneric, KitArticulation.OpenHiHat },
    { DrumArticulation.HiHatOpenBow, KitArticulation.OpenHiHat },
    { DrumArticulation.HiHatOpenEdge, KitArticulation.OpenHiHat },
    { DrumArticulation.HiHatPedal, KitArticulation.PedalHiHat },
    { DrumArticulation.HiHatFootSplash, KitArticulation.PedalHiHat }, // Splash is close to pedal action

    // --- CYMBALS (Combining multiple strike zones into simple Crash/Ride) ---
    { DrumArticulation.CrashCymbal1, KitArticulation.Crash },
    { DrumArticulation.Crash1Bow, KitArticulation.Crash },
    { DrumArticulation.Crash1Edge, KitArticulation.Crash },
    { DrumArticulation.CrashCymbal2, KitArticulation.Crash },
    { DrumArticulation.Crash2Bow, KitArticulation.Crash },
    { DrumArticulation.Crash2Edge, KitArticulation.Crash },
    { DrumArticulation.SplashCymbal, KitArticulation.Crash }, // Splash is a small crash

    { DrumArticulation.RideCymbal1, KitArticulation.Ride },
    { DrumArticulation.RideBow, KitArticulation.Ride },
    { DrumArticulation.RideEdge, KitArticulation.Ride },
    { DrumArticulation.RideCymbal2, KitArticulation.Ride },
    
    // --- Specific Hits Mapped to OtherPercussion ---
    { DrumArticulation.ChinaCymbal, KitArticulation.OtherPercussion }, // China is often a specialized effect
    { DrumArticulation.RideBell, KitArticulation.OtherPercussion },    // Bell is a specific effect, not main lane
    
    // --- AUXILIARY PERCUSSION (All mapped to the special OtherPercussion lane) ---
    { DrumArticulation.Tambourine, KitArticulation.OtherPercussion },
    { DrumArticulation.Cowbell, KitArticulation.OtherPercussion },
    { DrumArticulation.Vibraslap, KitArticulation.OtherPercussion },
    { DrumArticulation.HiBongo, KitArticulation.OtherPercussion },
    { DrumArticulation.LowBongo, KitArticulation.OtherPercussion },
    { DrumArticulation.MuteHiConga, KitArticulation.OtherPercussion },
    { DrumArticulation.OpenHiConga, KitArticulation.OtherPercussion },
    { DrumArticulation.LowConga, KitArticulation.OtherPercussion },
    { DrumArticulation.HighTimbale, KitArticulation.OtherPercussion },
    { DrumArticulation.LowTimbale, KitArticulation.OtherPercussion },
    { DrumArticulation.HighAgogo, KitArticulation.OtherPercussion },
    { DrumArticulation.LowAgogo, KitArticulation.OtherPercussion },
    { DrumArticulation.Cabasa, KitArticulation.OtherPercussion },
    { DrumArticulation.Maracas, KitArticulation.OtherPercussion },
    { DrumArticulation.ShortWhistle, KitArticulation.OtherPercussion },
    { DrumArticulation.LongWhistle, KitArticulation.OtherPercussion },
    { DrumArticulation.ShortGuiro, KitArticulation.OtherPercussion },
    { DrumArticulation.LongGuiro, KitArticulation.OtherPercussion },
    { DrumArticulation.Claves, KitArticulation.OtherPercussion },
    { DrumArticulation.HighWoodBlock, KitArticulation.OtherPercussion },
    { DrumArticulation.LowWoodBlock, KitArticulation.OtherPercussion },
    { DrumArticulation.MuteCuica, KitArticulation.OtherPercussion },
    { DrumArticulation.OpenCuica, KitArticulation.OtherPercussion },
    { DrumArticulation.MuteTriangle, KitArticulation.OtherPercussion },
    { DrumArticulation.OpenTriangle, KitArticulation.OtherPercussion }
};

    public static string GetGmNoteName(int gmNoteNumber, int channelId)
    {
        // If it's the drum channel, use the drum map
        if (channelId == 9)
        {
            var drumArticulation = GmNoteToArticulation[gmNoteNumber];
            return ArticulationToName[drumArticulation];
        }

        // Otherwise, just use the standard note name
        return $"{NoteNames[gmNoteNumber % 12]}{gmNoteNumber / 12 - 1}";
    }
}

