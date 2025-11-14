using CommunityToolkit.Mvvm.ComponentModel;
using Danidrum.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace Danidrum.ViewModels;

public partial class ChannelGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private int _channelId;

    // This is a bool? (nullable bool) to support three states:
    // true = All children are muted
    // false = No children are muted
    // null = Some children are muted (indeterminate)
    [ObservableProperty]
    private bool? _isMuted = false;

    public ObservableCollection<TrackInfo> Tracks { get; } = new();

    private bool _isUpdatingFromParent = false;

    // This fires when the user clicks the PARENT (Channel) mute button
    partial void OnIsMutedChanged(bool? value)
    {
        // Only act if the user clicked (value is true or false)
        if (value.HasValue && !_isUpdatingFromParent)
        {
            _isUpdatingFromParent = true;
            // Set all children to match the new state
            foreach (var track in Tracks)
            {
                track.IsMuted = value.Value;
            }
            _isUpdatingFromParent = false;
        }
    }

    // This is called by a CHILD track when its IsMuted changes
    public void UpdateMuteStateFromChildren()
    {
        if (_isUpdatingFromParent) return; // We're in a parent update, stop

        if (Tracks.All(t => t.IsMuted))
        {
            SetProperty(ref _isMuted, true);
        }
        else if (Tracks.All(t => !t.IsMuted))
        {
            SetProperty(ref _isMuted, false);
        }
        else
        {
            SetProperty(ref _isMuted, null); // Indeterminate
        }
    }
}