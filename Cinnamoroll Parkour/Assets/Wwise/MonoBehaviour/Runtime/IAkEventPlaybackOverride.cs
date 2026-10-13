/// <summary>Optional additional playback provider. A true result does not suppress Wwise playback.</summary>
public interface IAkEventPlaybackOverride
{
    bool TryPlay(AkEvent trigger);
    bool TryStop(AkEvent trigger);
}
