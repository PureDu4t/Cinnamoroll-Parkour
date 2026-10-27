using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Delay physical interaction notifications, keeping all physical colliders active.</summary>
public static class LoadingInteractionGate
{
    private static readonly HashSet<(int owner, int other)> pending = new HashSet<(int, int)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        pending.Clear();
        AkEventLoadingGate.Defer = (owner, other) =>
            TryDefer(owner, other, () => owner.HandleEvent(other));
    }

    public static bool TryDefer(MonoBehaviour owner, GameObject other, Action replay)
    {
        if (!GameSpeechSettings.BlocksGameInput) return false;
        if (owner == null || other == null || !owner.isActiveAndEnabled) return true;
        var key = (owner.GetInstanceID(), other.GetInstanceID());
        if (pending.Add(key)) owner.StartCoroutine(Wait(owner, other, replay, key));
        return true;
    }

    private static IEnumerator Wait(MonoBehaviour owner, GameObject other, Action replay,
        (int owner, int other) key)
    {
        try
        {
            while (GameSpeechSettings.BlocksGameInput) yield return null;
            if (owner != null && owner.isActiveAndEnabled && other != null && other.activeInHierarchy
                && Overlaps(owner.gameObject, other))
                replay();
        }
        finally { pending.Remove(key); }
    }

    private static bool Overlaps(GameObject owner, GameObject other)
    {
        foreach (var a in owner.GetComponents<Collider2D>())
            foreach (var b in other.GetComponentsInChildren<Collider2D>())
                if (a.enabled && b.enabled && b.gameObject.activeInHierarchy)
                {
                    var distance = a.Distance(b);
                    if (distance.isValid && distance.isOverlapped) return true;
                }
        foreach (var a in owner.GetComponents<Collider>())
            foreach (var b in other.GetComponentsInChildren<Collider>())
                if (a.enabled && b.enabled && b.gameObject.activeInHierarchy
                    && Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                        b, b.transform.position, b.transform.rotation, out _, out _)) return true;
        return false;
    }
}
