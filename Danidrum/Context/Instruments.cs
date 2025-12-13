
namespace Danidrum.Context;
using System.Collections.Generic;

public class Instrument
{
    public int Id { get; }
    public string Category { get; }
    public string Name { get; }

    private Instrument(int id, string category, string name)
    {
        Id = id;
        Category = category;
        Name = name;
    }

    public override string ToString() => Name;

    public static readonly IReadOnlyList<Instrument> All = new List<Instrument>
        {
            // PIANO
            new(1, "Piano", "Acoustic Grand Piano"),
            new(2, "Piano", "Bright Acoustic Piano"),
            new(3, "Piano", "Electric Grand Piano"),
            new(4, "Piano", "Honky-Tonk Piano"),
            new(5, "Piano", "Electric Piano 1"),
            new(6, "Piano", "Electric Piano 2"),
            new(7, "Piano", "Harpsichord"),
            new(8, "Piano", "Clavinet"),

            // CHROMATIC PERCUSSION
            new(9, "Chromatic Percussion", "Celesta"),
            new(10, "Chromatic Percussion", "Glockenspiel"),
            new(11, "Chromatic Percussion", "Music Box"),
            new(12, "Chromatic Percussion", "Vibraphone"),
            new(13, "Chromatic Percussion", "Marimba"),
            new(14, "Chromatic Percussion", "Xylophone"),
            new(15, "Chromatic Percussion", "Tubular Bells"),
            new(16, "Chromatic Percussion", "Dulcimer"),

            // ORGAN
            new(17, "Organ", "Drawbar Organ"),
            new(18, "Organ", "Percussive Organ"),
            new(19, "Organ", "Rock Organ"),
            new(20, "Organ", "Church Organ"),
            new(21, "Organ", "Reed Organ"),
            new(22, "Organ", "Accordion"),
            new(23, "Organ", "Harmonica"),
            new(24, "Organ", "Tango Accordion"),

            // GUITAR
            new(25, "Guitar", "Nylon String Guitar"),
            new(26, "Guitar", "Steel String Guitar"),
            new(27, "Guitar", "Electric Jazz Guitar"),
            new(28, "Guitar", "Electric Clean Guitar"),
            new(29, "Guitar", "Electric Muted Guitar"),
            new(30, "Guitar", "Overdriven Guitar"),
            new(31, "Guitar", "Distortion Guitar"),
            new(32, "Guitar", "Guitar Harmonics"),

            // BASS
            new(33, "Bass", "Acoustic Bass"),
            new(34, "Bass", "Electric Bass (finger)"),
            new(35, "Bass", "Electric Bass (pick)"),
            new(36, "Bass", "Fretless Bass"),
            new(37, "Bass", "Slap Bass 1"),
            new(38, "Bass", "Slap Bass 2"),
            new(39, "Bass", "Synth Bass 1"),
            new(40, "Bass", "Synth Bass 2"),

            // SOLO STRINGS
            new(41, "Solo Strings", "Violin"),
            new(42, "Solo Strings", "Viola"),
            new(43, "Solo Strings", "Cello"),
            new(44, "Solo Strings", "Contrabass"),
            new(45, "Solo Strings", "Tremolo Strings"),
            new(46, "Solo Strings", "Pizzicato Strings"),
            new(47, "Solo Strings", "Orchestral Strings"),
            new(48, "Solo Strings", "Timpani"),

            // ENSEMBLE
            new(49, "Ensemble", "String Ensemble 1"),
            new(50, "Ensemble", "String Ensemble 2"),
            new(51, "Ensemble", "SynthStrings 1"),
            new(52, "Ensemble", "SynthStrings 2"),
            new(53, "Ensemble", "Choir Aahs"),
            new(54, "Ensemble", "Voice Oohs"),
            new(55, "Ensemble", "Synth Voice"),
            new(56, "Ensemble", "Orchestra Hit"),

            // BRASS
            new(57, "Brass", "Trumpet"),
            new(58, "Brass", "Trombone"),
            new(59, "Brass", "Tuba"),
            new(60, "Brass", "Muted Trumpet"),
            new(61, "Brass", "French Horn"),
            new(62, "Brass", "Brass Section"),
            new(63, "Brass", "SynthBrass 1"),
            new(64, "Brass", "SynthBrass 2"),

            // REED
            new(65, "Reed", "Soprano Sax"),
            new(66, "Reed", "Alto Sax"),
            new(67, "Reed", "Tenor Sax"),
            new(68, "Reed", "Baritone Sax"),
            new(69, "Reed", "Oboe"),
            new(70, "Reed", "English Horn"),
            new(71, "Reed", "Bassoon"),
            new(72, "Reed", "Clarinet"),

            // PIPE
            new(73, "Pipe", "Piccolo"),
            new(74, "Pipe", "Flute"),
            new(75, "Pipe", "Recorder"),
            new(76, "Pipe", "Pan Flute"),
            new(77, "Pipe", "Blown Bottle"),
            new(78, "Pipe", "Shakuhachi"),
            new(79, "Pipe", "Whistle"),
            new(80, "Pipe", "Ocarina"),

            // SYNTH LEAD
            new(81, "Synth Lead", "Square Wave"),
            new(82, "Synth Lead", "Saw Wave"),
            new(83, "Synth Lead", "Calliope"),
            new(84, "Synth Lead", "Chiffer"),
            new(85, "Synth Lead", "Charang"),
            new(86, "Synth Lead", "Voice"),
            new(87, "Synth Lead", "Fifths"),
            new(88, "Synth Lead", "Bass + Lead"),

            // SYNTH PAD
            new(89, "Synth Pad", "New Age"),
            new(90, "Synth Pad", "Warm"),
            new(91, "Synth Pad", "Polysynth"),
            new(92, "Synth Pad", "Choir"),
            new(93, "Synth Pad", "Bowed"),
            new(94, "Synth Pad", "Metallic"),
            new(95, "Synth Pad", "Halo"),
            new(96, "Synth Pad", "Sweep"),

            // SYNTH EFFECTS
            new(97, "Synth Effects", "Rain"),
            new(98, "Synth Effects", "Soundtrack"),
            new(99, "Synth Effects", "Crystal"),
            new(100, "Synth Effects", "Atmosphere"),
            new(101, "Synth Effects", "Brightness"),
            new(102, "Synth Effects", "Goblins"),
            new(103, "Synth Effects", "Echoes"),
            new(104, "Synth Effects", "Sci-Fi"),

            // ETHNIC
            new(105, "Ethnic", "Sitar"),
            new(106, "Ethnic", "Banjo"),
            new(107, "Ethnic", "Shamisen"),
            new(108, "Ethnic", "Koto"),
            new(109, "Ethnic", "Kalimba"),
            new(110, "Ethnic", "Bagpipe"),
            new(111, "Ethnic", "Fiddle"),
            new(112, "Ethnic", "Shanai"),

            // PERCUSSIVE
            new(0, "Percussive", "Drum"),
            new(113, "Percussive", "Tinkle Bell"),
            new(114, "Percussive", "Agogo"),
            new(115, "Percussive", "Steel Drums"),
            new(116, "Percussive", "Woodblock"),
            new(117, "Percussive", "Taiko Drum"),
            new(118, "Percussive", "Melodic Tom"),
            new(119, "Percussive", "Synth Drum"),
            new(120, "Percussive", "Reverse Cymbal"),

            // SOUND EFFECTS
            new(121, "Sound Effects", "Guitar Fret Noise"),
            new(122, "Sound Effects", "Breath Noise"),
            new(123, "Sound Effects", "Seashore"),
            new(124, "Sound Effects", "Bird Tweet"),
            new(125, "Sound Effects", "Telephone Ring"),
            new(126, "Sound Effects", "Helicopter"),
            new(127, "Sound Effects", "Applause"),
            new(128, "Sound Effects", "Gunshot"),


            
        };

    public static readonly IReadOnlyDictionary<int, Instrument> Mapping = All.ToDictionary(e => e.Id);
}