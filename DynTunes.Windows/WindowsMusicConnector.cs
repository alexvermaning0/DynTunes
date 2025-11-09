using Windows.Media.Control;
using DynTunes;
using DynTunes.Connectors;

#if !DEBUG
using Elements.Core;
using FrooxEngine;
#endif

namespace DynTunes.Windows.Connectors;

public class WindowsMusicConnector : IMusicConnector
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private volatile MediaPlayerState _state = new();
    
    public WindowsMusicConnector()
    {
        Task.Factory.StartNew(ConnectAndRunAsync, TaskCreationOptions.LongRunning);
    }

    private async Task ConnectAndRunAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
            
            // Get initial session
            _currentSession = _sessionManager.GetCurrentSession();
            if (_currentSession != null)
            {
                AttachSessionHandlers();
            }
        }
        catch (Exception e)
        {
            #if !DEBUG
            UniLog.Warning($"Failed to initialize Windows Media: {e}");
            #else
            Console.WriteLine($"Failed to initialize Windows Media: {e}");
            #endif
            return;
        }

        #if !DEBUG
        while (!Engine.Current.ShutdownRequested)
        #else
        while (true)
        #endif
        {
            try
            {
                await UpdateStatusAsync();
            }
            catch (Exception e)
            {
                #if !DEBUG
                UniLog.Warning($"Failed to update Windows Media status: {e}");
                #else
                Console.WriteLine($"Failed to update Windows Media status: {e}");
                #endif
            }
            
            await Task.Delay(_state.IsPlaying ? 500 : 2000);
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        if (_currentSession != null)
        {
            DetachSessionHandlers();
        }

        _currentSession = _sessionManager?.GetCurrentSession();
        
        if (_currentSession != null)
        {
            AttachSessionHandlers();
        }
        else
        {
            _state = new MediaPlayerState();
        }
    }

    private void AttachSessionHandlers()
    {
        if (_currentSession == null) return;
        
        _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
        _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
        _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
    }

    private void DetachSessionHandlers()
    {
        if (_currentSession == null) return;
        
        _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = UpdateStatusAsync();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = UpdateStatusAsync();
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        _ = UpdateStatusAsync();
    }

    private async Task UpdateStatusAsync()
    {
        if (_currentSession == null)
        {
            _state = new MediaPlayerState();
            return;
        }

        try
        {
            var properties = await _currentSession.TryGetMediaPropertiesAsync();
            var playbackInfo = _currentSession.GetPlaybackInfo();
            var timeline = _currentSession.GetTimelineProperties();

            _state.Title = properties?.Title;
            _state.Artist = properties?.Artist;
            _state.Album = properties?.AlbumTitle;
            _state.AlbumArtUrl = properties?.Thumbnail?.ToString();
            
            _state.IsPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            
            _state.PositionSeconds = (float)timeline.Position.TotalSeconds;
            _state.LengthSeconds = (float)timeline.EndTime.TotalSeconds;
        }
        catch
        {
            _state = new MediaPlayerState();
        }
    }
    
    public MediaPlayerState GetState()
    {
        return _state;
    }
}
