using HoloToolkit.Unity;
using UnityEngine;
using System;
using System.Collections;

public sealed class GameManager : Singleton<GameManager> {

    public GameObject GrabbedGameObject { get; set; }
    public float GrabbedGameObjectDistanceOffset { get; set; }
    public bool PlacingObject { get; set; }
    public int GridPhysicsLayer = 8;

    /// <summary>
    /// 
    /// </summary>
    void Start()
    {
        GrabbedGameObject = null;
        GrabbedGameObjectDistanceOffset = 0.0f;
        PlacingObject = false;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.TapPressAction += OnSelect;
            InputManager.Instance.TapReleaseAction += OnRelease;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    //void Update()
    //   {
    //   }

    public int LayerMask
    {
        get { return (1 << GridPhysicsLayer); }
    }

    /// <summary>
    /// Called by GazeGestureManager when the user performs a Select gesture
    /// </summary>
    public void OnSelect()
    {
        Debug.Log("GameManger.OnSelect()");
        if (!LevelGenerator.Instance.RoomExists())
            LevelGenerator.Instance.FindRoom();
        else if (GameManager.Instance.GrabbedGameObject != null)
            GameManager.Instance.GrabbedGameObject.SendMessage("OnSelect");

        InputManager.Instance.DrawGazeLine();
    }

    /// <summary>
    /// Called by GazeGestureManager when the user performs a Select gesture
    /// </summary>
    public void OnRelease()
    {
        if (GameManager.Instance.GrabbedGameObject != null)
            GameManager.Instance.GrabbedGameObject.SendMessage("OnSelect");
        GameManager.Instance.GrabbedGameObject = null;
    }

    /// <summary>
    /// Called by GazeGestureManager when the user performs a Select gesture
    /// </summary>
    public void OnClear()
    {
        LevelGenerator.Instance.RemoveRoom();
    }
}
