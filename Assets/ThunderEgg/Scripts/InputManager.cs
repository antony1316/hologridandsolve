using HoloToolkit.Unity;
using UnityEngine;
using UnityEngine.VR.WSA.Input;
using System;
using System.Collections;

public sealed class InputManager : Singleton<InputManager> {
    
    public event Action TapPressAction;
    public event Action TapReleaseAction;

    private GestureRecognizer gestureRecognizer;
    private bool eventsAreRegistered = false;

    public float gazeBeamDistanceSqrdMax = 1.0f;
    public float gazeBeamDistanceSqrdSpeed = 10.0f;

    private bool gazeBeamActive = false;
    private GameObject gazeBeamObject;
    private LineRenderer gazeBeamLineRend;

    /// <summary>
    /// 
    /// </summary>
    void Start()
    {
        // Keyboard
        if (KeyboardInput.Instance)
        {
            KeyboardInput.Instance.RegisterKeyEvent(new KeyboardInput.KeyCodeEventPair(KeyCode.Space, KeyboardInput.KeyEvent.KeyReleased), FakeTapKeyboardHandler);
            KeyboardInput.Instance.RegisterKeyEvent(new KeyboardInput.KeyCodeEventPair(KeyCode.Backspace, KeyboardInput.KeyEvent.KeyReleased), FakeBackKeyboardHandler);
            KeyboardInput.Instance.RegisterKeyEvent(new KeyboardInput.KeyCodeEventPair(KeyCode.I, KeyboardInput.KeyEvent.KeyDown), FakeHandsCloserKeyboardHandler);
            KeyboardInput.Instance.RegisterKeyEvent(new KeyboardInput.KeyCodeEventPair(KeyCode.O, KeyboardInput.KeyEvent.KeyDown), FakeHandsFartherKeyboardHandler);
        }

        // Gesture
        gestureRecognizer = new GestureRecognizer();
        gestureRecognizer.SetRecognizableGestures(GestureSettings.Hold | GestureSettings.Tap |
                                                  GestureSettings.NavigationY | GestureSettings.NavigationX);

        gestureRecognizer.StartCapturingGestures();

        TryToRegisterEvents();

        // Gaze Beam
        gazeBeamObject = new GameObject("lineTap");
        gazeBeamLineRend = gazeBeamObject.AddComponent<LineRenderer>();
        gazeBeamLineRend.SetVertexCount(2);
        gazeBeamLineRend.SetWidth(0.1f, 0.1f);
        ResetGazeBeam();
    }

    /// <summary>
    /// 
    /// </summary>
    void Update()
    {
        // Gaze Beam
        if (gazeBeamActive)
            RedrawGazeLine();
    }

    /// <summary>
    /// 
    /// </summary>
    public interface IInputHandler
    {
        bool OnTapped(InteractionSourceKind source, int tapCount, Ray ray);
    }

    /// <summary>
    /// 
    /// </summary>
    private void TryToRegisterEvents()
    {
        if (!eventsAreRegistered && gestureRecognizer != null)
        {
            gestureRecognizer.TappedEvent += OnTapped;
            eventsAreRegistered = true;
        }
    }
    private void TryToUnregisterEvents()
    {
        if (eventsAreRegistered && gestureRecognizer != null)
        {
            gestureRecognizer.TappedEvent -= OnTapped;
            eventsAreRegistered = false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public void TriggerTapPress()
    {
        if (TapPressAction != null)
        {
            Debug.Log("InputManager.TapPressAction");
            TapPressAction();
        }
    }
    public void TriggerTapRelease()
    {
        if (TapReleaseAction != null)
        {
            Debug.Log("InputManager.TapReleaseAction");
            TapReleaseAction();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    private void FakeTapKeyboardHandler(KeyboardInput.KeyCodeEventPair keyCodeEvent)
    {
        SendFakeTap();
    }
    private void FakeBackKeyboardHandler(KeyboardInput.KeyCodeEventPair keyCodeEvent)
    {
        OnBackTapped(InteractionSourceKind.Other, 0, new Ray(Vector3.zero, Vector3.forward));
    }
    private void FakeHandsFartherKeyboardHandler(KeyboardInput.KeyCodeEventPair keyCodeEvent)
    {
        MoveGrabbedObject(0.1f);
    }
    private void FakeHandsCloserKeyboardHandler(KeyboardInput.KeyCodeEventPair keyCodeEvent)
    {
        MoveGrabbedObject(-0.1f);
    }

    /// <summary>
    /// 
    /// </summary>
    private void SendFakeTap()
    {
        OnTapped(InteractionSourceKind.Other, 0, new Ray(Vector3.zero, Vector3.forward));
    }
    public void OnTapped(InteractionSourceKind source, int tapCount, Ray ray)
    {
        Debug.Log("InputManager.OnTapped");
        GameManager.Instance.OnSelect();
    }
    public void OnBackTapped(InteractionSourceKind source, int tapCount, Ray ray)
    {
        Debug.Log("InputManager.OnBackTapped");
        GameManager.Instance.OnClear();
    }
    public void MoveGrabbedObject(float offsetDistance)
    {
        if (GameManager.Instance.GrabbedGameObject != null)
        {
            Debug.Log("InputManager.MoveGrabbedObject");
            GameManager.Instance.GrabbedGameObjectDistanceOffset += offsetDistance;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    void ResetGazeBeam()
    {
        gazeBeamActive = false;
        if (gazeBeamLineRend != null)
            gazeBeamLineRend.enabled = false;
        gazeBeamObject.SetActive(false);
    }

    /// <summary>
    /// 
    /// </summary>
    Vector3 gazeStart;
    Vector3 gazeEnd;
    bool endReachedEnd;
    bool startReachEnd;
    float distanceSqrd;
    Vector3 direction;
    Vector3 lineStart;
    Vector3 lineEnd;
    float gazeBeamDistance;
    public void DrawGazeLine()
    {
        //Debug.Log("Create new lineTap.");
        gazeStart = Camera.main.transform.position + Vector3.down * 0.5f;
        gazeEnd = GazeManager.Instance.Position;

        endReachedEnd = false;
        startReachEnd = false;
        distanceSqrd = (gazeEnd - gazeStart).sqrMagnitude;
        direction = (gazeEnd - gazeStart).normalized;
        lineStart = gazeStart;
        lineEnd = lineStart;
        //gazeBeamDistance = gazeBeamDistanceSqrdSpeed * Time.deltaTime;
        //lineEnd = gazeStart + gazeBeamDistance * direction;

        gazeBeamLineRend.SetPosition(0, lineStart);
        gazeBeamLineRend.SetPosition(1, lineEnd);
        gazeBeamLineRend.enabled = true;
        gazeBeamObject.SetActive(true);

        gazeBeamActive = true;
    }

    /// <summary>
    /// 
    /// </summary>
    private void RedrawGazeLine()
    {
        gazeBeamDistance = gazeBeamDistanceSqrdSpeed * Time.deltaTime;

        if (!endReachedEnd)
        {
            distanceSqrd = (gazeEnd - lineEnd).sqrMagnitude;
            // if the end can step, it should
            // if it can't step, it should end 
            if (distanceSqrd >= gazeBeamDistance)
                lineEnd += gazeBeamDistance * direction;
            else
            {
                lineEnd = gazeEnd;
                endReachedEnd = true;
            }

            distanceSqrd = (lineEnd - lineStart).sqrMagnitude;
            // the start should stay within distance of the end 
            if (distanceSqrd >= gazeBeamDistanceSqrdMax)
                lineStart = lineEnd - gazeBeamDistanceSqrdMax * direction;
        }
        else
        {
            distanceSqrd = (lineEnd - lineStart).sqrMagnitude;
            // if the start can step, it should
            // if it can't step, it should end 
            if (distanceSqrd >= gazeBeamDistance)
                lineStart += gazeBeamDistance * direction;
            else
            {
                lineStart = lineEnd;
                startReachEnd = true;
            }
        }

        gazeBeamLineRend.SetPosition(0, lineStart);
        gazeBeamLineRend.SetPosition(1, lineEnd);

        if (startReachEnd)
            ResetGazeBeam();
    }
}
