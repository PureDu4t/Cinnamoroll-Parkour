using System;
using UnityEngine;

public class GridWorldManager : MonoBehaviour
{
    public static GridWorldManager Instance { get; private set; }

    [SerializeField] private TilemapImporter tilemapImporter;
    [SerializeField] private GridPhysicsBuilder gridPhysicsBuilder;

    public event Action OnGridReady;

    public bool IsGridReady { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GridWorldManager instances found. Keeping the first one.");
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        InitializeGrid();
    }

    public void InitializeGrid()
    {
        if (tilemapImporter == null)
        {
            Debug.LogError("GridWorldManager missing tilemapImporter.");
            return;
        }

        tilemapImporter.Import();

        if (gridPhysicsBuilder != null)
        {
            gridPhysicsBuilder.Rebuild();
        }

        IsGridReady = true;

        if (OnGridReady != null)
        {
            OnGridReady.Invoke();
        }

        Debug.Log("Grid World initialized.");
    }
}