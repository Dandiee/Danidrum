using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Danidrum.Services;
using System.Windows;

namespace Danidrum;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly PlaybackService _playbackService;

    [ObservableProperty] private string _currentMidiFile;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ChunkContext _selectedChunk;
    [ObservableProperty] private double _currentSongPositionMs;
    [ObservableProperty] private bool _isPlaying;


    [ObservableProperty] private SongContext _song;

    private bool _isUserSeeking = false;

    public MainWindowViewModel(PlaybackService playbackService)
    {
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

    [RelayCommand]
    private async Task Loaded()
    {
        IsLoading = true;
        //await LoadMidiFileAsync(@"c:\Users\koczu\Downloads\Nirvana-Come As You Are-11-11-2025.mid");
        await LoadMidiFileAsync(@"c:\Users\koczu\Downloads\Blink - 182-Whats my age again-05-03-2025.mid");
        IsLoading = false;
    }


   
    [RelayCommand]
    private async Task LoadMidiFileAsync(string filePath)
    {
        IsLoading = true;
        await StopPlayback();

        Song = new SongContext(filePath);
        SelectedChunk = Song.Chunks.FirstOrDefault(t => t.IsLikelyDrumTrack) ?? Song.Chunks.FirstOrDefault();

        _playbackService.InitializePlaybackEngine(Song);
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
        _playbackService.SeekTo((long)CurrentSongPositionMs);
        _isUserSeeking = false;
    }
}