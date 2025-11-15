using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Danidrum.Services;
using Danidrum.ViewModels;
using Melanchall.DryWetMidi.Interaction;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Tools;
using DryWetMidiFile = Melanchall.DryWetMidi.Core.MidiFile;

namespace Danidrum;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly TrackListService _trackListService;
    private readonly PlaybackService _playbackService;

    [ObservableProperty] private string _currentMidiFile;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private IReadOnlyCollection<TrackInfo> _tracks;
    [ObservableProperty] private TrackInfo _selectedTrack;
    [ObservableProperty] private double _totalSongDurationMs;
    [ObservableProperty] private double _currentSongPositionMs;
    [ObservableProperty] private double _pixelsPerSecond = 200.0;
    [ObservableProperty] private double _totalSongWidth;
    [ObservableProperty] private bool _isPlaying;

    private bool _isUpdatingMutes = false;
    private bool _isProcessingMute = false;

    [ObservableProperty] private IReadOnlyCollection<NoteLaneViewModel> _noteLanes = new List<NoteLaneViewModel>();
    [ObservableProperty] private IReadOnlyCollection<BarViewModel> _bars = new List<BarViewModel>();
    [ObservableProperty] private IReadOnlyCollection<SubdivisionViewModel> _subdivisions = new List<SubdivisionViewModel>();

    private DryWetMidiFile _dryWetMidiFile;
    private bool _isUserSeeking = false;

    public MainWindowViewModel(TrackListService trackListService, PlaybackService playbackService)
    {
        _trackListService = trackListService;
        _playbackService = playbackService;
        _playbackService.PositionChanged += OnPlaybackPositionChanged;
        _playbackService.PlaybackStateChanged += isPlaying => IsPlaying = isPlaying;
    }

    private void OnPlaybackPositionChanged(long currentTimeMs)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            if (!_isUserSeeking)
            {
                CurrentSongPositionMs = currentTimeMs;
            }
        });
    }

    private void SubscribeToMuteChanges()
    {
        if (Tracks == null) return;
        foreach (var track in Tracks)
        {
            track.PropertyChanged += OnTrackPropertyChanged;
        }
    }

    private void UnsubscribeFromMuteChanges()
    {
        if (Tracks == null) return;
        foreach (var track in Tracks)
        {
            track.PropertyChanged -= OnTrackPropertyChanged;
        }
    }

    partial void OnSelectedTrackChanged(TrackInfo value)
    {
            ProcessSelectedTrack();
    }

    private async void OnTrackPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (_isProcessingMute || _isUpdatingMutes) return;

        if (e.PropertyName == nameof(TrackInfo.IsMuted))
        {
            _isProcessingMute = true;
            try
            {
                _isUpdatingMutes = true;

                var changedTrack = (TrackInfo)sender;
                int channelId = changedTrack.ChannelId;
                bool newState = changedTrack.IsMuted;

                foreach (var track in Tracks.Where(t => t.ChannelId == channelId))
                {
                    track.IsMuted = newState;
                }

                _isUpdatingMutes = false;

                var mutedChannels = Tracks.Where(e => e.IsMuted).Select(e => e.ChannelId).ToHashSet();
                _playbackService.UpdateMuteList(mutedChannels);
            }
            finally
            {
                _isProcessingMute = false;
            }
        }
    }


    [RelayCommand]
    private async Task Loaded()
    {
        IsLoading = true;
        await LoadMidiFileAsync(@"c:\Users\koczu\Downloads\Nirvana-Come As You Are-11-11-2025.mid");
        IsLoading = false;
    }


    private void ProcessSelectedTrack()
    {
        if (SelectedTrack == null)
            return;

        var newLanes = new List<NoteLaneViewModel>();
        var tempoMap = _dryWetMidiFile.GetTempoMap();

        var notesInTrack = SelectedTrack.Chunk.GetNotes();
        var groupedNotes = notesInTrack
            .GroupBy(n => n.NoteNumber)
            .OrderBy(g => g.Key); // Order by pitch

        double pxPerMs = PixelsPerSecond / 1000.0;

        foreach (var noteGroup in groupedNotes)
        {
            var noteNumber = noteGroup.Key;
            var lane = new NoteLaneViewModel
            {
                NoteNumber = noteNumber,
                LaneName = MidiNoteConverter.GetNoteName(noteNumber, SelectedTrack.ChannelId)
            };

            foreach (var note in noteGroup)
            {
                var startTime = note.TimeAs<MetricTimeSpan>(tempoMap);
                var duration = note.LengthAs<MetricTimeSpan>(tempoMap);

                double startMs = startTime.TotalMilliseconds;
                double durMs = duration.TotalMilliseconds;

                var noteViewModel = new NoteViewModel
                {
                    CanvasLeft = startMs * pxPerMs,
                    CanvasWidth = Math.Max(1.0, durMs * pxPerMs)
                };

                lane.Notes.Add(noteViewModel);
            }

            if (lane.Notes.Count > 0)
                newLanes.Add(lane);
        }

        NoteLanes = new ObservableCollection<NoteLaneViewModel>(newLanes);


        var firstNote = notesInTrack.OrderBy(n => n.Time).First();
        Console.WriteLine(firstNote.Time); // in ticks
        var firstNoteMs = firstNote.TimeAs<MetricTimeSpan>(tempoMap).TotalMilliseconds;
    }

    [RelayCommand]
    private async Task LoadMidiFileAsync(string filePath)
    {
        IsLoading = true;
        await StopPlayback();

        _dryWetMidiFile = await Task.Run(() => DryWetMidiFile.Read(filePath));

        Tracks = _trackListService.GetAllTrackInfo(_dryWetMidiFile);
        SubscribeToMuteChanges();

        var lastNoteEvent = _dryWetMidiFile
            .GetTimedEvents()
            .LastOrDefault(e => e.Event is NoteEvent);

        var tempoMap = _dryWetMidiFile.GetTempoMap();
        var bars = MeasureExtractor.Extract(_dryWetMidiFile);

        MetricTimeSpan duration;
        if (lastNoteEvent != null)
        {
            duration = lastNoteEvent.TimeAs<MetricTimeSpan>(tempoMap);
        }
        else
        {
            duration = new MetricTimeSpan(0);
        }

        TotalSongDurationMs = duration.TotalMilliseconds;

        // unified conversion
        double pxPerMs = PixelsPerSecond /1000.0;
        TotalSongWidth = TotalSongDurationMs * pxPerMs;

        // Bars use same pxPerMs
        Bars = bars.Select(bar => new BarViewModel()
        {
            X = bar.StartTimeMs * pxPerMs,
            TimeSignature = $"{bar.TimeSignature.Numerator}/{bar.TimeSignature.Denominator}",
            MeasureIndex = bar.MeasureIndex,
            DisplayText = $"{bar.MeasureIndex} ({bar.TimeSignature.Numerator}/{bar.TimeSignature.Denominator})"
        }).ToList();

        // Subdivisions (beat lines) across all bars
        var subs = new List<SubdivisionViewModel>();
        foreach (var bar in bars)
        {
            int beats = bar.TimeSignature.Numerator;
            if (beats <=1) continue;

            long barLengthTicks = bar.EndTick - bar.StartTick;
            // divide evenly in ticks to avoid ms rounding issues
            for (int b =1; b < beats; b++)
            {
                long subTick = bar.StartTick + (barLengthTicks * b) / beats;
                // convert tick -> ms using tempoMap to be accurate
                var subMetric = TimeConverter.ConvertTo<MetricTimeSpan>(subTick, tempoMap);
                double subMs = subMetric.TotalMicroseconds /1000.0;
                double subX = subMs * pxPerMs;
                subs.Add(new SubdivisionViewModel
                {
                    X = subX,
                    MeasureIndex = bar.MeasureIndex,
                    BeatIndex = b
                });
            }
        }

        Subdivisions = subs;

        SelectedTrack = Tracks.FirstOrDefault(t => t.IsLikelyDrumTrack) ?? Tracks.FirstOrDefault();

        _playbackService.InitializePlaybackEngine(_dryWetMidiFile, Tracks);
        _playbackService.Start();

        IsLoading = false;
    }



    [RelayCommand]
    private void StartPlayback()
    {
        _playbackService.Start();
    }


    [RelayCommand]
    private async Task PausePlayback()
    {
        await _playbackService.PauseAsync();
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (IsPlaying)
        {
            PausePlaybackCommand.Execute(null);
        }
        else
        {
            StartPlaybackCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void SetSelectedTrack(TrackInfo track)
    {
        SelectedTrack = track;
    }

    [RelayCommand] // This now creates an IAsyncRelayCommand
    private async Task StopPlayback()
    {
        await _playbackService.StopPlayback();
    }

    [RelayCommand]
    private void StartSeeking() => _isUserSeeking = true;

    [RelayCommand]
    private async Task StopSeeking()
    {
        // Convert UI time to playback time by adding visual latency compensation.
        var targetMs = (long)CurrentSongPositionMs + _playbackService.VisualLatencyMs;
        _playbackService.SeekTo(targetMs);
        _isUserSeeking = false;
    }
}