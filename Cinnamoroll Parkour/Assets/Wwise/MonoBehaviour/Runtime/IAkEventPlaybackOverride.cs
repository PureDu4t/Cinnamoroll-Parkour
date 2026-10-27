/// <summary>Optional additional playback provider. A true result does not suppress Wwise playback.</summary>
public interface IAkEventPlaybackOverride
{
    bool TryPlay(AkEvent trigger);
    bool TryStop(AkEvent trigger);
}

/// <summary>Project hook without a dependency from Wwise onto Assembly-CSharp.</summary>
public static class AkEventLoadingGate
{
    public static System.Func<AkEvent, UnityEngine.GameObject, bool> Defer;
}
