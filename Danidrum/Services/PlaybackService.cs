using Danidrum.ViewModels;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using System.Diagnostics;
using DryWetMidiFile = Melanchall.DryWetMidi.Core.MidiFile;

namespace Danidrum.Services;

public class PlaybackService
{
    private Task? _playbackTask;
    private CancellationTokenSource? _playbackCts;
    private readonly Stopwatch _stopwatch = new();
    private IOutputDevice _outputDevice;
    private long _seekOffsetMs;
    private List<TimedEvent> _backingTrackEvents;
    private DryWetMidiFile _dryWetMidiFile;
    private int _nextEventIndex;
    private TempoMap _tempoMap;
    private HashSet<int> _mutedChannels;
    public event Action<long> PositionChanged;
    public event Action<bool>? PlaybackStateChanged;

    private long _totalSongDurationInMs;

    // Compensation for output device latency (e.g., Microsoft GS Wavetable Synth).
    // Positive values delay the UI relative to the actual scheduled MIDI time so visuals match what you hear.
    public int VisualLatencyMs { get; set; } = 240;

    public bool IsPlaying { get; private set; }

    public PlaybackService()
    {
        _outputDevice = OutputDevice.GetByName("Microsoft GS Wavetable Synth");
    }

    public void InitializePlaybackEngine(DryWetMidiFile dryWetMidiFile, IReadOnlyCollection<TrackInfo> tracks)
    {
        _dryWetMidiFile = dryWetMidiFile;
        _tempoMap = dryWetMidiFile.GetTempoMap();
        _totalSongDurationInMs = (long)((MetricTimeSpan)_dryWetMidiFile.GetDuration(TimeSpanType.Metric)).TotalMilliseconds;


        //var mutedChannels = tracks
        //    .Where(t => t.IsMuted)
        //    .Select(t => t.ChannelId)
        //    .ToHashSet();

        _backingTrackEvents = _dryWetMidiFile.GetTimedEvents()
            //.Where(e =>
            //{
            //    if (e.Event is ChannelEvent channelEvent)
            //    {
            //        return !mutedChannels.Contains(channelEvent.Channel);
            //    }
            //    return true;
            //})
            .OrderBy(e => e.Time)
            .ToList();

        _nextEventIndex = 0;
        _seekOffsetMs = 0;
    }

    public void UpdateMuteList(HashSet<int> mutedChannels)
    {
        _mutedChannels = mutedChannels;
    }

    public void Start()
    {
        _playbackCts = new CancellationTokenSource();
        // Start fresh so elapsed time aligns with new seek offset.
        _stopwatch.Restart();
        IsPlaying = true;
        PlaybackStateChanged?.Invoke(IsPlaying);
        _playbackTask = Task.Run(() => PlaybackLoop(_playbackCts.Token));
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
            // 123 stands for All Notes Off command
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
        _seekOffsetMs = seekOffsetMs;
        _nextEventIndex = _backingTrackEvents.FindIndex(e => e.TimeAs<MetricTimeSpan>(_tempoMap).TotalMilliseconds >= _seekOffsetMs);
        if (_nextEventIndex == -1)
        {
            _nextEventIndex = _backingTrackEvents.Count;
        }
        // Sync the running clock with the new position so currentTime = seek + elapsedSinceSeek
        _stopwatch.Restart();
    }

    public async Task PlaybackLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var currentTimeMs = _stopwatch.ElapsedMilliseconds + _seekOffsetMs;
            if (currentTimeMs >= _totalSongDurationInMs)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(StopPlayback);
                break;
            }

            // Report UI time compensated by output latency so visuals align with what is heard.
            var uiTimeMs = Math.Max(0, currentTimeMs - VisualLatencyMs);
            PositionChanged?.Invoke(uiTimeMs);

            while (_nextEventIndex < _backingTrackEvents.Count)
            {
                var timedEvent = _backingTrackEvents[_nextEventIndex];
               
                var eventTimeMs = timedEvent.TimeAs<MetricTimeSpan>(_tempoMap).TotalMilliseconds;

                if (eventTimeMs <= currentTimeMs)
                {

                    if (timedEvent.Event is ChannelEvent channelEvent)
                    {
                        if (_mutedChannels == null || !_mutedChannels.Contains(channelEvent.Channel))
                        {
                            _outputDevice.SendEvent(timedEvent.Event);
                        }
                    }
                    
                    _nextEventIndex++;
                }
                else break;
            }

            await Task.Delay(1, token);
        }
    }
}