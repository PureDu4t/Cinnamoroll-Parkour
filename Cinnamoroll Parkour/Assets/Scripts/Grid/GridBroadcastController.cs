using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(AkGameObj))]
public class GridBroadcastController : MonoBehaviour
{
    private enum BroadcastStartMode
    {
        FullMap,
        FromPlayer
    }

    private enum BroadcastDirection
    {
        LeftToRight,
        RightToLeft
    }

    [System.Serializable]
    private class CellTypeEventMapping
    {
        public CellType cellType;
        public AK.Wwise.Event eventToPlay;
    }

    [System.Serializable]
    private class CellTypeSwitchMapping
    {
        public CellType cellType;
        public AK.Wwise.Switch switchValue;
    }

    [System.Serializable]
    private class BroadcastRowEventMapping
    {
        public int gridY;
        public AK.Wwise.Event eventToPlay;
    }

    private class BroadcastStep
    {
        public int x;
        public readonly List<GridCell> cells = new List<GridCell>();
    }

    private class ActiveBroadcastVoice
    {
        public AK.Wwise.Event eventToPlay;
        public uint playingId;
        public GameObject playbackGameObject;
        public bool finished;
    }

    [Header("Grid References")]
    [SerializeField] private GridDatabase gridDatabase;
    [SerializeField] private PlayerPositionTracker playerPositionTracker;
    [SerializeField] private Transform broadcastEmitter;
    [SerializeField] private Transform listenerReference;

    [Header("Keys")]
    [SerializeField] private KeyCode fullLeftToRightKey = KeyCode.S;
    [SerializeField] private KeyCode fullRightToLeftKey = KeyCode.W;
    [SerializeField] private KeyCode fromPlayerRightKey = KeyCode.D;
    [SerializeField] private KeyCode fromPlayerLeftKey = KeyCode.A;
    [SerializeField] private KeyCode stopBroadcastKey = KeyCode.Q;
    [SerializeField] private KeyCode layerModifierKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode alternateLayerModifierKey = KeyCode.RightShift;

    [Header("Query")]
    [Tooltip("If false, Empty cells are skipped even when query bounds include them.")]
    [SerializeField] private bool includeEmptyCells;

    [Tooltip("Use the inclusive X/Y range below instead of scanning the whole GridDatabase.")]
    [SerializeField] private bool useManualBroadcastBounds = true;

    [SerializeField] private int explicitMinimumX;
    [SerializeField] private int explicitMaximumX = 15;
    [SerializeField] private int explicitMinimumY;
    [SerializeField] private int explicitMaximumY = 9;

    [Header("Pan")]
    [Tooltip("Move each broadcast voice left/right before playing its cell sound.")]
    [SerializeField] private bool useSpatializedEmitterPanning = true;

    [Tooltip("How far left/right the broadcast voices move from the listener.")]
    [SerializeField] private float panWorldWidth = 30f;

    [Tooltip("Values above 1 make panning gentler near the center.")]
    [SerializeField] private float panResponseCurve = 1.25f;

    [Tooltip("Keeps each broadcast voice in front of the listener for Wwise spatialization.")]
    [SerializeField] private float emitterForwardDistance = 5f;

    [Header("Playback")]
    [Tooltip("Used when no Cell Type Event Mapping exists for a cell.")]
    [SerializeField] private AK.Wwise.Event defaultBroadcastEvent;

    [FormerlySerializedAs("stepTickEvent")]
    [Tooltip("Optional looping tick Event started with the broadcast and stopped when the broadcast ends.")]
    [SerializeField] private AK.Wwise.Event broadcastLoopTickEvent;

    [Tooltip("Optional: one Wwise Event per CellType. Leave empty if all cells use Default Broadcast Event.")]
    [SerializeField] private List<CellTypeEventMapping> cellTypeEvents =
        new List<CellTypeEventMapping>();

    [Tooltip("Preferred broadcast path: exact Grid Y to one row-specific Wwise Event. Each row Event can play a fixed-pitch Switch Container.")]
    [SerializeField] private List<BroadcastRowEventMapping> rowEvents =
        new List<BroadcastRowEventMapping>();

    [Tooltip("Set the Wwise cell-type Switch before playing the Event.")]
    [SerializeField] private List<CellTypeSwitchMapping> cellTypeSwitches =
        new List<CellTypeSwitchMapping>();

    [Tooltip("Prefer setting cell-type Switches by Wwise names. This avoids stale Unity Wwise Picker assets after Wwise edits.")]
    [SerializeField] private bool useCellTypeSwitchNames = true;

    [Tooltip("Wwise Switch Group used by the top-level broadcast Switch Container.")]
    [SerializeField] private string cellTypeSwitchGroupName =
        "Broadcast_Cell_Type";

    [Header("Pitch Step Switches")]
    [Tooltip("Optional: Wwise pitch Switches by Y row. Element 0 is the lowest broadcast Y, element 1 is the next row, and so on.")]
    [SerializeField] private List<AK.Wwise.Switch> pitchStepSwitches =
        new List<AK.Wwise.Switch>();

    [Tooltip("Prefer setting pitch Switches by Wwise names. This avoids stale Unity Wwise Picker assets after Wwise edits.")]
    [SerializeField] private bool usePitchSwitchNames = true;

    [Tooltip("Wwise Switch Group used by each nested pitch Switch Container.")]
    [SerializeField] private string pitchSwitchGroupName =
        "Broadcast_Cell_PitchStep";

    [Tooltip("Pitch Switch names from lowest row upward. Element 0 is C, element 1 is D, and so on.")]
    [SerializeField] private string[] pitchSwitchNames =
    {
        "P1_C",
        "P2_D",
        "P3_E",
        "P4_F",
        "P5_G",
        "P6_A",
        "P7_B",
        "P8_C2",
        "P9_D2",
        "P10_E2"
    };

    [Tooltip("Use the current broadcast range's lowest Y as pitch step 0. Turn off to use Minimum Grid Y below.")]
    [SerializeField] private bool useBroadcastMinimumYForPitch = true;

    [Tooltip("Design Y for pitch step 0. Used only when Use Broadcast Minimum Y For Pitch is off.")]
    [SerializeField] private int minimumGridY = 1;

    [Tooltip("Broadcast tempo. 120 BPM means one grid step every 0.5 seconds.")]
    [SerializeField] private float beatsPerMinute = 120f;

    [FormerlySerializedAs("beatsPerCell")]
    [Tooltip("How many beats each X-column step lasts.")]
    [SerializeField] private float beatsPerStep = 1f;
    [Tooltip("Length of the looping drum source in seconds. MUS_Broadcast_Drums.wav is 32 seconds.")]
    [SerializeField] private float drumLoopLengthSeconds = 32f;

    [FormerlySerializedAs("maxCellsPerBroadcast")]
    [Tooltip("0 means no limit. A positive number stops after this many X columns.")]
    [SerializeField] private int maxColumnsPerBroadcast;

    [Tooltip("Fade time, in milliseconds, when Q stops the current Event.")]
    [SerializeField] private int stopFadeMilliseconds = 50;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private Coroutine broadcastRoutine;
    private readonly List<ActiveBroadcastVoice> activeVoices =
        new List<ActiveBroadcastVoice>();
    private readonly List<GameObject> voiceGameObjects =
        new List<GameObject>();
    private readonly HashSet<GameObject> registeredVoiceGameObjects =
        new HashSet<GameObject>();
    private int currentBroadcastMinimumX;
    private int currentBroadcastMaximumX = 1;
    private int currentBroadcastMinimumY;
    private AK.Wwise.Event activeLoopTickEvent;
    private GameObject activeLoopTickGameObject;
    private uint activeLoopTickPlayingId = AkSoundEngine.AK_INVALID_PLAYING_ID;

    public bool IsBroadcasting
    {
        get { return broadcastRoutine != null; }
    }

    private void Awake()
    {
        AutoAssignMissingReferences();
    }

    private void Reset()
    {
        AutoAssignMissingReferences();
    }

    private void Update()
    {
        if (GameSpeechSettings.BlocksGameInput) return;
        if (Input.GetKeyDown(stopBroadcastKey))
        {
            StopBroadcast();
            return;
        }

        bool layerOnly =
            Input.GetKey(layerModifierKey)
            || Input.GetKey(alternateLayerModifierKey);

        if (Input.GetKeyDown(fullLeftToRightKey))
        {
            StartBroadcast(
                BroadcastStartMode.FullMap,
                BroadcastDirection.LeftToRight,
                layerOnly
            );
        }
        else if (Input.GetKeyDown(fullRightToLeftKey))
        {
            StartBroadcast(
                BroadcastStartMode.FullMap,
                BroadcastDirection.RightToLeft,
                layerOnly
            );
        }
        else if (Input.GetKeyDown(fromPlayerRightKey))
        {
            StartBroadcast(
                BroadcastStartMode.FromPlayer,
                BroadcastDirection.LeftToRight,
                layerOnly
            );
        }
        else if (Input.GetKeyDown(fromPlayerLeftKey))
        {
            StartBroadcast(
                BroadcastStartMode.FromPlayer,
                BroadcastDirection.RightToLeft,
                layerOnly
            );
        }
    }

    public void StopBroadcast()
    {
        bool hadActiveBroadcast =
            broadcastRoutine != null
            || activeVoices.Count > 0
            || activeLoopTickPlayingId != AkSoundEngine.AK_INVALID_PLAYING_ID;

        if (broadcastRoutine != null)
        {
            StopCoroutine(broadcastRoutine);
            broadcastRoutine = null;
        }

        StopBroadcastLoopTick();
        StopCurrentEvents();

        if (debugLogging && hadActiveBroadcast)
        {
            Debug.Log("Grid broadcast stopped.");
        }
    }

    private void StartBroadcast(
        BroadcastStartMode startMode,
        BroadcastDirection direction,
        bool layerOnly
    )
    {
        if (!ValidateConfiguration())
        {
            return;
        }

        // The drum has a one-instance limit. Finish the old source immediately before restarting.
        StopBroadcastLoopTick(true);
        StopBroadcast();

        List<BroadcastStep> steps = BuildBroadcastSteps(
            startMode,
            direction,
            layerOnly
        );
        ApplyMaxStepLimit(steps);

        if (steps.Count == 0)
        {
            Debug.Log("Grid broadcast found no columns to announce.");
            return;
        }

        if (debugLogging)
        {
            Debug.Log(
                "Grid broadcast started. Columns: " + steps.Count
                + ", Mode: " + startMode
                + ", Direction: " + direction
                + ", Layer Only: " + layerOnly
                + ", BPM: " + beatsPerMinute
            );
        }

        // Allocate/register before the drum starts; first-column allocation must not delay its onset.
        int requiredVoices = 0;
        foreach (var step in steps) requiredVoices = Mathf.Max(requiredVoices, step.cells.Count);
        for (int i = 0; i < requiredVoices; i++) EnsureVoiceGameObjectRegistered(GetVoiceGameObject(i));
        StartBroadcastLoopTick();
        broadcastRoutine = StartCoroutine(PlayBroadcast(steps));
    }

    private List<BroadcastStep> BuildBroadcastSteps(
        BroadcastStartMode startMode,
        BroadcastDirection direction,
        bool layerOnly
    )
    {
        List<GridCell> databaseCells = gridDatabase.GetAllCells();
        List<BroadcastStep> result = new List<BroadcastStep>();

        if (databaseCells.Count == 0)
        {
            return result;
        }

        int minimumX;
        int maximumX;
        int minimumY;
        int maximumY;
        GetQueryBounds(
            databaseCells,
            out minimumX,
            out maximumX,
            out minimumY,
            out maximumY
        );
        currentBroadcastMinimumX = minimumX;
        currentBroadcastMaximumX = maximumX;
        currentBroadcastMinimumY = minimumY;

        Vector2Int playerPosition =
            playerPositionTracker.CurrentGridPosition;

        if (layerOnly
            && (playerPosition.y < minimumY || playerPosition.y > maximumY))
        {
            return result;
        }

        int xStart;
        int xEnd;

        if (direction == BroadcastDirection.LeftToRight)
        {
            xStart = startMode == BroadcastStartMode.FromPlayer
                ? Mathf.Clamp(playerPosition.x, minimumX, maximumX)
                : minimumX;
            xEnd = maximumX;
        }
        else
        {
            xStart = startMode == BroadcastStartMode.FromPlayer
                ? Mathf.Clamp(playerPosition.x, minimumX, maximumX)
                : maximumX;
            xEnd = minimumX;
        }

        int yStart = layerOnly ? playerPosition.y : minimumY;
        int yEnd = layerOnly ? playerPosition.y : maximumY;
        int xStep = direction == BroadcastDirection.LeftToRight ? 1 : -1;

        for (int x = xStart; ShouldContinueX(x, xEnd, xStep); x += xStep)
        {
            BroadcastStep step = new BroadcastStep();
            step.x = x;

            for (int y = yStart; y <= yEnd; y++)
            {
                GridCell cell = gridDatabase.GetCell(new Vector2Int(x, y));

                if (ShouldIncludeCell(cell))
                {
                    step.cells.Add(cell);
                }
            }

            result.Add(step);
        }

        return result;
    }

    private void ApplyMaxStepLimit(List<BroadcastStep> steps)
    {
        if (maxColumnsPerBroadcast <= 0
            || steps.Count <= maxColumnsPerBroadcast)
        {
            return;
        }

        steps.RemoveRange(
            maxColumnsPerBroadcast,
            steps.Count - maxColumnsPerBroadcast
        );
    }

    private void GetQueryBounds(
        List<GridCell> databaseCells,
        out int minimumX,
        out int maximumX,
        out int minimumY,
        out int maximumY
    )
    {
        if (useManualBroadcastBounds)
        {
            minimumX = Mathf.Min(explicitMinimumX, explicitMaximumX);
            maximumX = Mathf.Max(explicitMinimumX, explicitMaximumX);
            minimumY = Mathf.Min(explicitMinimumY, explicitMaximumY);
            maximumY = Mathf.Max(explicitMinimumY, explicitMaximumY);
            return;
        }

        minimumX = databaseCells[0].position.x;
        maximumX = databaseCells[0].position.x;
        minimumY = databaseCells[0].position.y;
        maximumY = databaseCells[0].position.y;

        for (int i = 1; i < databaseCells.Count; i++)
        {
            Vector2Int position = databaseCells[i].position;
            minimumX = Mathf.Min(minimumX, position.x);
            maximumX = Mathf.Max(maximumX, position.x);
            minimumY = Mathf.Min(minimumY, position.y);
            maximumY = Mathf.Max(maximumY, position.y);
        }
    }

    private static bool ShouldContinueX(int current, int end, int step)
    {
        if (step > 0)
        {
            return current <= end;
        }

        return current >= end;
    }

    private bool ShouldIncludeCell(GridCell cell)
    {
        if (cell == null)
        {
            return false;
        }

        if (cell.type == CellType.AirWall)
        {
            return false;
        }

        return includeEmptyCells || cell.type != CellType.Empty;
    }

    private IEnumerator PlayBroadcast(List<BroadcastStep> steps)
    {
        double secondsPerStep = GetSecondsPerStep();
        double fallbackOrigin = Time.realtimeSinceStartupAsDouble;
        var clock = new BroadcastAudioClock(drumLoopLengthSeconds);
        bool sourceClockSeen = false;
        bool clockWarningReported = false;
        // Submit the first column in the same Unity frame/audio command batch as the drum.
        PlayStep(steps[0]);
        int next = 1;
        while (true)
        {
            double elapsed;
            int position;
            if (activeLoopTickPlayingId != AkSoundEngine.AK_INVALID_PLAYING_ID
                && AkUnitySoundEngine.GetSourcePlayPosition(activeLoopTickPlayingId, out position, true) == AKRESULT.AK_Success)
            {
                elapsed = clock.Observe(position / 1000.0);
                sourceClockSeen = true;
                fallbackOrigin = Time.realtimeSinceStartupAsDouble - elapsed;
            }
            else
            {
                elapsed = Time.realtimeSinceStartupAsDouble - fallbackOrigin;
                if (!clockWarningReported && elapsed > .25 && activeLoopTickPlayingId != AkSoundEngine.AK_INVALID_PLAYING_ID)
                {
                    Debug.LogWarning("Broadcast drum playback clock unavailable; using absolute realtime clock. Check Wwise source playback.", this);
                    clockWarningReported = true;
                }
                // A pending drum source must not advance columns before it begins playback.
                if (!sourceClockSeen && activeLoopTickPlayingId != AkSoundEngine.AK_INVALID_PLAYING_ID && elapsed < 2)
                { yield return null; continue; }
            }
            if (next < steps.Count && elapsed >= next * secondsPerStep)
            {
                PlayStep(steps[next]);
                next++;
            }
            // Let the final column keep its drum beat instead of immediately stopping the loop.
            if (next == steps.Count && elapsed >= steps.Count * secondsPerStep) break;
            yield return null;
        }

        StopBroadcastLoopTick();
        broadcastRoutine = null;

        if (debugLogging)
        {
            Debug.Log("Grid broadcast finished.");
        }
    }

    private float GetSecondsPerStep()
    {
        float safeBpm = Mathf.Max(1f, beatsPerMinute);
        float safeBeatsPerStep = Mathf.Max(0.01f, beatsPerStep);

        return 60f / safeBpm * safeBeatsPerStep;
    }

    private void StartBroadcastLoopTick()
    {
        if (broadcastLoopTickEvent == null
            || !broadcastLoopTickEvent.IsValid())
        {
            return;
        }

        StopBroadcastLoopTick();

        activeLoopTickEvent = broadcastLoopTickEvent;
        activeLoopTickGameObject = GetPlaybackGameObject();
        TtsText.TryPlay(gameObject);
        activeLoopTickPlayingId = activeLoopTickEvent.Post(
            activeLoopTickGameObject
        );

        if (activeLoopTickPlayingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogError("Wwise could not play broadcast loop tick.");
            activeLoopTickEvent = null;
            activeLoopTickGameObject = null;
        }
    }

    private void StopBroadcastLoopTick(bool immediately = false)
    {
        var text = GetComponent<TtsText>();
        if (text != null) text.Stop();
        if (activeLoopTickPlayingId == AkSoundEngine.AK_INVALID_PLAYING_ID
            || activeLoopTickEvent == null
            || !activeLoopTickEvent.IsValid())
        {
            activeLoopTickPlayingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
            activeLoopTickEvent = null;
            activeLoopTickGameObject = null;
            return;
        }

        AkSoundEngine.ExecuteActionOnEvent(
            activeLoopTickEvent.Id,
            AkActionOnEventType.AkActionOnEventType_Stop,
            activeLoopTickGameObject != null
                ? activeLoopTickGameObject
                : GetPlaybackGameObject(),
            immediately ? 0 : stopFadeMilliseconds,
            AkCurveInterpolation.AkCurveInterpolation_Linear,
            activeLoopTickPlayingId
        );

        activeLoopTickPlayingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
        activeLoopTickEvent = null;
        activeLoopTickGameObject = null;
    }

    private void PlayStep(BroadcastStep step)
    {
        if (debugLogging)
        {
            Debug.Log(
                "Broadcast X column "
                + step.x
                + ". Cells in column: "
                + step.cells.Count
            );
        }

        for (int i = 0; i < step.cells.Count; i++)
        {
            PlayCell(step.cells[i], i);
        }
    }

    private ActiveBroadcastVoice PlayCell(GridCell cell, int voiceIndex)
    {
        bool usesRowEvent;
        AK.Wwise.Event eventToPlay = GetEventForCell(cell, out usesRowEvent);

        if (eventToPlay == null || !eventToPlay.IsValid())
        {
            Debug.LogWarning(
                "No Wwise Event configured for grid cell type: "
                + cell.type
                + " at Y "
                + cell.position.y
            );

            return null;
        }

        GameObject playbackGameObject = GetVoiceGameObject(voiceIndex);
        EnsureVoiceGameObjectRegistered(playbackGameObject);
        ApplyCellAudioParameters(cell, playbackGameObject, usesRowEvent);

        ActiveBroadcastVoice voice = new ActiveBroadcastVoice();
        voice.eventToPlay = eventToPlay;
        voice.playbackGameObject = playbackGameObject;
        TtsText.TryPlay(gameObject);
        voice.playingId = eventToPlay.Post(
            playbackGameObject,
            (uint)AkCallbackType.AK_EndOfEvent,
            OnBroadcastEventCallback,
            voice
        );

        if (voice.playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            Debug.LogError(
                "Wwise could not play broadcast Event for cell "
                + cell.position
                + " / "
                + cell.type
            );

            return null;
        }

        activeVoices.Add(voice);

        if (debugLogging)
        {
            Debug.Log(
                "Broadcast cell "
                + cell.position
                + " / "
                + cell.type
                + " using "
                + (usesRowEvent ? "row Event" : "legacy Event")
                + " on "
                + playbackGameObject.name
                + ". Playing ID: "
                + voice.playingId
            );
        }

        return voice;
    }

    private void ApplyCellAudioParameters(
        GridCell cell,
        GameObject playbackGameObject,
        bool usesRowEvent
    )
    {
        AK.Wwise.Switch cellTypeSwitch = GetSwitchForCell(cell.type);
        int pitchStep = GetPitchStepForCell(cell);
        AK.Wwise.Switch pitchSwitch = GetPitchSwitchForStep(pitchStep);
        string cellTypeSwitchName = GetCellTypeSwitchName(cell.type);
        string pitchSwitchName = GetPitchSwitchName(pitchStep);
        float signedPan = GridAudioParameterUtility.GetSignedPanFromRange(
            cell.position.x,
            currentBroadcastMinimumX,
            currentBroadcastMaximumX,
            panResponseCurve
        );

        if (useCellTypeSwitchNames
            && !string.IsNullOrEmpty(cellTypeSwitchGroupName)
            && !string.IsNullOrEmpty(cellTypeSwitchName))
        {
            AKRESULT result = AkSoundEngine.SetSwitch(
                cellTypeSwitchGroupName,
                cellTypeSwitchName,
                playbackGameObject
            );

            if (debugLogging)
            {
                Debug.Log(
                    "Broadcast cell-type Switch by name on "
                    + playbackGameObject.name
                    + ": "
                    + cellTypeSwitchGroupName
                    + " / "
                    + cellTypeSwitchName
                    + ", result "
                    + result
                );
            }
        }
        else if (cellTypeSwitch != null && cellTypeSwitch.IsValid())
        {
            cellTypeSwitch.SetValue(playbackGameObject);
        }
        else if (debugLogging)
        {
            Debug.LogWarning(
                "No valid cell-type Switch configured for "
                + cell.type
                + "."
            );
        }

        if (usesRowEvent)
        {
            if (debugLogging)
            {
                Debug.Log(
                    "Broadcast row Event handles pitch for Y "
                    + cell.position.y
                    + ". Skipping pitch Switch."
                );
            }
        }
        else if (usePitchSwitchNames
            && !string.IsNullOrEmpty(pitchSwitchGroupName)
            && !string.IsNullOrEmpty(pitchSwitchName))
        {
            AKRESULT result = AkSoundEngine.SetSwitch(
                pitchSwitchGroupName,
                pitchSwitchName,
                playbackGameObject
            );

            if (debugLogging)
            {
                Debug.Log(
                    "Broadcast pitch Switch by name on "
                    + playbackGameObject.name
                    + ": "
                    + pitchSwitchGroupName
                    + " / "
                    + pitchSwitchName
                    + ", result "
                    + result
                );
            }
        }
        else if (pitchSwitch != null && pitchSwitch.IsValid())
        {
            pitchSwitch.SetValue(playbackGameObject);
        }
        else if (debugLogging)
        {
            Debug.LogWarning(
                "No valid pitch Switch configured for Y step "
                + pitchStep
                + "."
            );
        }

        if (useSpatializedEmitterPanning)
        {
            MovePlaybackObjectForPan(playbackGameObject, signedPan);
        }

        if (debugLogging)
        {
            Debug.Log(
                "Broadcast audio parameters: cell "
                + cell.position
                + ", type switch "
                + GetDebugSwitchName(cellTypeSwitch)
                + ", row Event "
                + usesRowEvent
                + ", pitch step "
                + pitchStep
                + ", pitch switch "
                + GetDebugPitchSwitchLabel(pitchSwitch, pitchSwitchName)
                + ", voice "
                + playbackGameObject.name
                + ", pan "
                + signedPan.ToString("0.00")
            );
        }
    }

    private AK.Wwise.Event GetEventForCell(
        GridCell cell,
        out bool usesRowEvent
    )
    {
        usesRowEvent = false;

        AK.Wwise.Event rowEvent = GetRowEventForY(cell.position.y);

        if (rowEvent != null && rowEvent.IsValid())
        {
            usesRowEvent = true;
            return rowEvent;
        }

        return GetLegacyEventForCellType(cell.type);
    }

    private AK.Wwise.Event GetRowEventForY(int gridY)
    {
        for (int i = 0; i < rowEvents.Count; i++)
        {
            BroadcastRowEventMapping mapping = rowEvents[i];

            if (mapping.gridY == gridY
                && mapping.eventToPlay != null
                && mapping.eventToPlay.IsValid())
            {
                return mapping.eventToPlay;
            }
        }

        return null;
    }

    private AK.Wwise.Event GetLegacyEventForCellType(CellType cellType)
    {
        for (int i = 0; i < cellTypeEvents.Count; i++)
        {
            CellTypeEventMapping mapping = cellTypeEvents[i];

            if (mapping.cellType == cellType
                && mapping.eventToPlay != null
                && mapping.eventToPlay.IsValid())
            {
                return mapping.eventToPlay;
            }
        }

        return defaultBroadcastEvent;
    }

    private AK.Wwise.Switch GetSwitchForCell(CellType cellType)
    {
        for (int i = 0; i < cellTypeSwitches.Count; i++)
        {
            CellTypeSwitchMapping mapping = cellTypeSwitches[i];

            if (mapping.cellType == cellType
                && mapping.switchValue != null
                && mapping.switchValue.IsValid())
            {
                return mapping.switchValue;
            }
        }

        return null;
    }

    private string GetCellTypeSwitchName(CellType cellType)
    {
        switch (cellType)
        {
            case CellType.Ground:
                return "Ground";
            case CellType.Wall:
            case CellType.CrackedWall:
                return "Wall";
            case CellType.Door:
                return "MoveableWall";
            case CellType.Lever:
                return "Lever";
            case CellType.Cannon:
                return "Fan";
            case CellType.Battery:
                return "Battery";
            case CellType.Goal:
                return "Win";
            case CellType.Lore:
                return "Lore";
            case CellType.Background:
                return "Background";
            case CellType.Death:
                return "Death";
            default:
                return string.Empty;
        }
    }

    private int GetPitchStepForCell(GridCell cell)
    {
        int pitchMinimumY = useBroadcastMinimumYForPitch
            ? currentBroadcastMinimumY
            : minimumGridY;

        return Mathf.Max(0, cell.position.y - pitchMinimumY);
    }

    private string GetPitchSwitchName(int pitchStep)
    {
        if (pitchSwitchNames == null || pitchSwitchNames.Length == 0)
        {
            return string.Empty;
        }

        if (pitchStep < 0 || pitchStep >= pitchSwitchNames.Length)
        {
            if (debugLogging)
            {
                Debug.LogWarning(
                    "No broadcast pitch Switch name configured for Y step "
                    + pitchStep
                    + ". Add more Pitch Switch Names on "
                    + name
                    + " if this row should change pitch."
                );
            }

            return string.Empty;
        }

        return pitchSwitchNames[pitchStep];
    }

    private AK.Wwise.Switch GetPitchSwitchForStep(int pitchStep)
    {
        if (pitchStepSwitches == null || pitchStepSwitches.Count == 0)
        {
            return null;
        }

        if (pitchStep < 0 || pitchStep >= pitchStepSwitches.Count)
        {
            if (debugLogging)
            {
                Debug.LogWarning(
                    "No broadcast pitch Switch configured for Y step "
                    + pitchStep
                    + ". Add more Pitch Step Switches on "
                    + name
                    + " if this row should change pitch."
                );
            }

            return null;
        }

        return pitchStepSwitches[pitchStep];
    }


    private static string GetDebugPitchSwitchLabel(
        AK.Wwise.Switch switchValue,
        string switchName
    )
    {
        if (!string.IsNullOrEmpty(switchName))
        {
            return switchName;
        }

        return GetDebugSwitchName(switchValue);
    }

    private static string GetDebugSwitchName(AK.Wwise.Switch switchValue)
    {
        if (switchValue == null || !switchValue.IsValid())
        {
            return "None";
        }

        return switchValue.Name;
    }

    private void StopCurrentEvents()
    {
        for (int i = activeVoices.Count - 1; i >= 0; i--)
        {
            StopVoice(activeVoices[i]);
        }

        activeVoices.Clear();
    }

    private void StopVoice(ActiveBroadcastVoice voice)
    {
        var text = GetComponent<TtsText>();
        if (text != null && voice != null && text.isActiveAndEnabled) text.Stop();
        if (voice == null
            || voice.eventToPlay == null
            || !voice.eventToPlay.IsValid()
            || voice.playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
        {
            return;
        }

        AkSoundEngine.ExecuteActionOnEvent(
            voice.eventToPlay.Id,
            AkActionOnEventType.AkActionOnEventType_Stop,
            voice.playbackGameObject != null
                ? voice.playbackGameObject
                : GetPlaybackGameObject(),
            stopFadeMilliseconds,
            AkCurveInterpolation.AkCurveInterpolation_Linear,
            voice.playingId
        );

        voice.finished = true;
        voice.playingId = AkSoundEngine.AK_INVALID_PLAYING_ID;
    }

    private void MovePlaybackObjectForPan(
        GameObject playbackGameObject,
        float signedPan
    )
    {
        if (playbackGameObject == null || listenerReference == null)
        {
            return;
        }

        float normalizedX = (signedPan + 1f) * 0.5f;
        float localPanX = Mathf.Lerp(
            -panWorldWidth * 0.5f,
            panWorldWidth * 0.5f,
            normalizedX
        );

        Vector3 listenerPosition = listenerReference.position;
        Vector3 forwardOffset =
            listenerReference.forward * emitterForwardDistance;

        playbackGameObject.transform.position = new Vector3(
            listenerPosition.x + localPanX,
            listenerPosition.y,
            listenerPosition.z
        ) + forwardOffset;

        AkSoundEngine.SetObjectPosition(
            playbackGameObject,
            playbackGameObject.transform.position,
            playbackGameObject.transform.forward,
            playbackGameObject.transform.up
        );
    }

    private GameObject GetVoiceGameObject(int voiceIndex)
    {
        while (voiceGameObjects.Count <= voiceIndex)
        {
            GameObject voiceGameObject = new GameObject(
                "BroadcastVoice_" + voiceGameObjects.Count
            );
            Transform parent =
                broadcastEmitter != null ? broadcastEmitter : transform;
            voiceGameObject.transform.SetParent(parent, false);
            voiceGameObjects.Add(voiceGameObject);
        }

        return voiceGameObjects[voiceIndex];
    }

    private void EnsureVoiceGameObjectRegistered(GameObject voiceGameObject)
    {
        if (voiceGameObject == null
            || registeredVoiceGameObjects.Contains(voiceGameObject))
        {
            return;
        }

        AKRESULT result = AkSoundEngine.RegisterGameObj(
            voiceGameObject,
            voiceGameObject.name
        );

        registeredVoiceGameObjects.Add(voiceGameObject);

        if (debugLogging)
        {
            Debug.Log(
                "Registered broadcast voice GameObject "
                + voiceGameObject.name
                + ". Result: "
                + result
            );
        }
    }

    private void UnregisterVoiceGameObjects()
    {
        foreach (GameObject voiceGameObject in registeredVoiceGameObjects)
        {
            if (voiceGameObject != null)
            {
                AkSoundEngine.UnregisterGameObj(voiceGameObject);
            }
        }

        registeredVoiceGameObjects.Clear();
    }

    private GameObject GetPlaybackGameObject()
    {
        if (broadcastEmitter != null)
        {
            return broadcastEmitter.gameObject;
        }

        return gameObject;
    }

    private void OnBroadcastEventCallback(
        object cookie,
        AkCallbackType callbackType,
        AkCallbackInfo callbackInfo
    )
    {
        if (callbackType != AkCallbackType.AK_EndOfEvent)
        {
            return;
        }

        ActiveBroadcastVoice voice = cookie as ActiveBroadcastVoice;

        if (voice == null)
        {
            return;
        }

        AkEventCallbackInfo eventInfo = callbackInfo as AkEventCallbackInfo;

        if (eventInfo != null
            && voice.playingId != AkSoundEngine.AK_INVALID_PLAYING_ID
            && eventInfo.playingID != voice.playingId)
        {
            return;
        }

        voice.finished = true;
        activeVoices.Remove(voice);
    }

    private void AutoAssignMissingReferences()
    {
        if (gridDatabase == null)
        {
            gridDatabase = GridDatabase.Instance;
        }

        if (gridDatabase == null)
        {
            gridDatabase = FindObjectOfType<GridDatabase>();
        }

        if (playerPositionTracker == null)
        {
            playerPositionTracker =
                GetComponent<PlayerPositionTracker>();
        }

        if (playerPositionTracker == null)
        {
            playerPositionTracker =
                FindObjectOfType<PlayerPositionTracker>();
        }

        if (listenerReference == null && Camera.main != null)
        {
            listenerReference = Camera.main.transform;
        }
    }

    private bool ValidateConfiguration()
    {
        AutoAssignMissingReferences();

        if (gridDatabase == null)
        {
            Debug.LogError("GridBroadcastController needs a GridDatabase.");
            return false;
        }

        if (playerPositionTracker == null)
        {
            Debug.LogError(
                "GridBroadcastController needs a PlayerPositionTracker."
            );
            return false;
        }

        if (defaultBroadcastEvent == null
            && cellTypeEvents.Count == 0
            && rowEvents.Count == 0)
        {
            Debug.LogError(
                "GridBroadcastController needs Row Events, a default "
                + "Event, or Cell Type Event mappings."
            );
            return false;
        }

        if (beatsPerMinute <= 0f)
        {
            Debug.LogError(
                "GridBroadcastController Beats Per Minute must be above 0."
            );
            return false;
        }

        if (beatsPerStep <= 0f)
        {
            Debug.LogError(
                "GridBroadcastController Beats Per Step must be above 0."
            );
            return false;
        }

        if (useSpatializedEmitterPanning && broadcastEmitter == null)
        {
            Debug.LogError(
                "GridBroadcastController needs a Broadcast Emitter "
                + "when spatialized emitter panning is enabled."
            );
            return false;
        }

        if (useSpatializedEmitterPanning
            && broadcastEmitter.GetComponent<AkGameObj>() == null)
        {
            Debug.LogError(
                "The Broadcast Emitter needs an AkGameObj component."
            );
            return false;
        }

        if (useSpatializedEmitterPanning && listenerReference == null)
        {
            Debug.LogError(
                "GridBroadcastController needs a Listener Reference "
                + "when spatialized emitter panning is enabled."
            );
            return false;
        }

        return true;
    }

    private void OnDisable()
    {
        StopBroadcast();
        UnregisterVoiceGameObjects();
    }
}
