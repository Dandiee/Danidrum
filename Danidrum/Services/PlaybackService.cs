using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using System.Diagnostics;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;

namespace Danidrum.Services;

public class PlaybackService
{
    private Task? _playbackTask;
    private CancellationTokenSource? _playbackCts;
    private readonly Stopwatch _stopwatch = new();
    private IOutputDevice _outputDevice;
    private long _seekOffsetMs;
    private int _nextEventIndex;
    private HashSet<int> _mutedChannels;
    public event Action<long> PositionChanged;
    public event Action<bool>? PlaybackStateChanged;

    private Playback _playback;

    private SongContext _song;

    public bool IsPlaying { get; private set; }

    public PlaybackService()
    {
        _outputDevice = OutputDevice.GetByName("Microsoft GS Wavetable Synth");
    }

    private static MusicalTimeSpan NoteLength = MusicalTimeSpan.Quarter;
    private static MusicalTimeSpan GapStepLength = MusicalTimeSpan.ThirtySecond;


    public void InitializePlaybackEngine(SongContext song)
    {
        _playback = song.Midi.GetPlayback(OutputDevice.GetByName("Microsoft GS Wavetable Synth"));
        
    }

    public void UpdateMuteList(HashSet<int> mutedChannels)
    {
        _mutedChannels = mutedChannels;
    }

    public void Start()
    {
        _playback.Start();
        //_playback.Start();

        //_playbackCts = new CancellationTokenSource();
        //// Start fresh so elapsed time aligns with new seek offset.
        //_stopwatch.Restart();
        //IsPlaying = true;
        //PlaybackStateChanged?.Invoke(IsPlaying);
        //_playbackTask = Task.Run(() => PlaybackLoop(_playbackCts.Token));
    }

    public async Task StopPlayback()
    {
        if (_playbackCts == null || _playbackTask == null)
        {
            return;
        }
        
        await _playbackCts.CancelAsync();

        try
        {
            await _playbackTask;
        }
        catch (OperationCanceledException) { }
        finally
        {
            _playbackTask = null;
            _playbackCts.Dispose();
            _playbackCts = null;
        }

        _stopwatch.Stop();

        for (var i = 0; i < 16; i++)
        {
            // 123 stands for All TimedNotes Off command
            var allNotesOff = new ControlChangeEvent((SevenBitNumber)123, (SevenBitNumber)0)
            {
                Channel = (FourBitNumber)i
            };
            _outputDevice?.SendEvent(allNotesOff);
        }

        if (IsPlaying)
        {
            IsPlaying = false;
            PlaybackStateChanged?.Invoke(IsPlaying);
        }
    }

    public async Task PauseAsync()
    {
        if (_playbackCts == null || _playbackTask == null)
        {
            return;
        }

        // Capture current time into seek offset to resume from the same spot.
        _seekOffsetMs += _stopwatch.ElapsedMilliseconds;

        await _playbackCts.CancelAsync();

        try
        {
            await _playbackTask;
        }
        catch (OperationCanceledException) { }
        finally
        {
            _playbackTask = null;
            _playbackCts.Dispose();
            _playbackCts = null;
        }

        _stopwatch.Stop();

        for (var i = 0; i < 16; i++)
        {
            var allNotesOff = new ControlChangeEvent((SevenBitNumber)123, (SevenBitNumber)0)
            {
                Channel = (FourBitNumber)i
            };
            _outputDevice?.SendEvent(allNotesOff);
        }

        if (IsPlaying)
        {
            IsPlaying = false;
            PlaybackStateChanged?.Invoke(IsPlaying);
        }
    }

    public void SeekTo(long seekOffsetMs)
    {
        //_seekOffsetMs = seekOffsetMs;
        //_nextEventIndex = _song.TimedNotes.FindIndex(e => e.StartTimeMs >= _seekOffsetMs);
        //if (_nextEventIndex == -1)
        //{
        //    _nextEventIndex = _song.TimedNotes.Count;
        //}
        
        //_stopwatch.Restart();
    }

    public async Task PlaybackLoop(CancellationToken token)
    {
        //while (!token.IsCancellationRequested)
        //{
        //    var currentTimeMs = _stopwatch.ElapsedMilliseconds + _seekOffsetMs;
        //    if (currentTimeMs >= _song.LengthMs)
        //    {
        //        await System.Windows.Application.Current.Dispatcher.InvokeAsync(StopPlayback);
        //        break;
        //    }

        //    var uiTimeMs = Math.Max(0, currentTimeMs);
        //    PositionChanged?.Invoke(uiTimeMs);

        //    while (_nextEventIndex < _song.TimedNotes.Count)
        //    {
        //        var note = _song.TimedNotes[_nextEventIndex];
        //        if (note.StartTimeMs <= currentTimeMs)
        //        {
        //            if (note.TimedEvent.Event is ChannelEvent channelEvent)
        //            {
        //                if (_mutedChannels == null || !_mutedChannels.Contains(channelEvent.Channel))
        //                {
        //                    _outputDevice.SendEvent(note.TimedEvent.Event);
        //                    note.Lane.StateChanged?.Invoke(this, EventArgs.Empty);
        //                }
        //            }
                    
        //            _nextEventIndex++;
        //        }
        //        else break;
        //    }

        //    await Task.Delay(1, token);
        //}
    }
}