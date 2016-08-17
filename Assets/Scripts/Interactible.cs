using HoloToolkit.Unity;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// The Interactible class flags a Game Object as being "Interactible".
/// Determines what happens when an Interactible is being gazed at.
/// </summary>
public class Interactible : MonoBehaviour
{
    public static float OBJECT_DISTANCE_MIN = 0.85f * 1.5f;
    public static float OBJECT_DISTANCE_MAX = 0.85f * 3.0f;

    [Tooltip("Audio clip to play when interacting with this hologram.")]
    public AudioClip TargetFeedbackSound;
    public float enlargeRatio = 4.0f;
    public bool placeOnSpatialMap = false;
    public bool facePlayer = false;
    public float snapTime = 1.0f;

    public float GrabDistance { set; get; }

    private AudioSource audioSource;
    private Vector3 startScale;
    private Collider thisCollider;
    private Rigidbody thisRigidbody;
    private Vector3 startColliderSize;
    private Material[] defaultMaterials;
    private bool placing = false;
    private Vector3 nextPosition;
    private float snapTimeCurrent = 0.0f;
    private Vector3 snapPositionSource;
    private Vector3 snapPositionDestination;

    void Start()
    {
        GrabDistance = 3.0f;
        startScale = transform.localScale;

        defaultMaterials = GetComponent<Renderer>().materials;

        // Add a BoxCollider if the interactible does not contain one.
        thisCollider = GetComponentInChildren<Collider>();
        if (thisCollider == null)
        {
            thisCollider = gameObject.AddComponent<BoxCollider>();
        }
        thisRigidbody = GetComponentInChildren<Rigidbody>();
        if (thisRigidbody == null)
        {
            thisRigidbody = gameObject.AddComponent<Rigidbody>();
        }
        thisRigidbody.useGravity = false;
        thisRigidbody.isKinematic = false;
        thisRigidbody.constraints = RigidbodyConstraints.FreezeAll;

        startColliderSize = ((BoxCollider)thisCollider).size;
        EnableAudioHapticFeedback();
    }

    // Update is called once per frame.
    void Update()
    {
        // If the user is in placing mode,
        // update the placement to match the user's gaze.
        if (placing)
        {
            // Do a raycast into the world that will only hit the Spatial Mapping mesh.
            var headPosition = Camera.main.transform.position;
            var gazeDirection = Camera.main.transform.forward;

            RaycastHit hitInfo;
            //if (Physics.Raycast(headPosition, gazeDirection, out hitInfo,
            //    grabDistance, GameManager.Instance.GridPhysicsLayer))
            //{
            //    // Move this object to where the raycast
            //    // hit the Spatial Mapping mesh.
            //    // Here is where you might consider adding intelligence
            //    // to how the object is placed.  For example, consider
            //    // placing based on the bottom of the object's
            //    // collider so it sits properly on surfaces.
            //    this.transform.position = hitInfo.point;
            //}
            //else 
            if (Physics.Raycast(headPosition, gazeDirection, out hitInfo,
                GrabDistance + GameManager.Instance.GrabbedGameObjectDistanceOffset, SpatialMappingManager.Instance.LayerMask))
            {
                // Move this object to where the raycast
                // hit the Spatial Mapping mesh.
                // Here is where you might consider adding intelligence
                // to how the object is placed.  For example, consider
                // placing based on the bottom of the object's
                // collider so it sits properly on surfaces.
                Debug.DrawLine(hitInfo.point, hitInfo.point + hitInfo.normal);
                nextPosition = hitInfo.point;
                if (hitInfo.normal.x != 0)
                    nextPosition.x += hitInfo.normal.x * thisCollider.bounds.extents.x;
                if (hitInfo.normal.y != 0)
                    nextPosition.y += hitInfo.normal.y * thisCollider.bounds.extents.y;
                if (hitInfo.normal.z != 0)
                    nextPosition.z += hitInfo.normal.z * thisCollider.bounds.extents.z;
            }
            else
            {
                nextPosition = headPosition + gazeDirection * (GrabDistance + GameManager.Instance.GrabbedGameObjectDistanceOffset);
            }
            //this.transform.position = nextPosition;
            thisRigidbody.MovePosition(nextPosition);

            // Rotate this object to face the user.
            if (facePlayer)
            {
                Quaternion toQuat = Camera.main.transform.localRotation;
                toQuat.x = 0;
                toQuat.z = 0;
                this.transform.rotation = toQuat;
            }
        }
    }

    private void EnableAudioHapticFeedback()
    {
        // If this hologram has an audio clip, add an AudioSource with this clip.
        if (TargetFeedbackSound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.clip = TargetFeedbackSound;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1;
            audioSource.dopplerLevel = 0;
        }
    }

    /* TODO: DEVELOPER CODING EXERCISE 2.d */

    void GazeEntered()
    {
        if (placing && !GameManager.Instance.PlacingObject)
            return;

        //Debug.Log("GazeEntered");
        ((BoxCollider)thisCollider).size = Vector3.one;
        transform.localScale = transform.localScale * enlargeRatio;
        for (int i = 0; i < defaultMaterials.Length; i++)
        {
            // 2.d: Uncomment the below line to highlight the material when gaze enters.
            defaultMaterials[i].SetFloat("_Highlight", .25f);
        }
    }

    void GazeExited()
    {
        if (placing && !GameManager.Instance.PlacingObject)
            return;

        //Debug.Log("GazeExited");
        transform.localScale = startScale;
        ((BoxCollider)thisCollider).size = startColliderSize;
        for (int i = 0; i < defaultMaterials.Length; i++)
        {
            // 2.d: Uncomment the below line to remove highlight on material when gaze exits.
            defaultMaterials[i].SetFloat("_Highlight", 0f);
        }
    }

    void OnSelect()
    {
        Debug.Log("Interactable.OnSelect");

        if (placing)
        {
            placing = false;
            GameManager.Instance.GrabbedGameObject = null;
            //SnapToGrid();
            StartCoroutine(SnapToGrid());
            thisRigidbody.isKinematic = false;
            thisRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }
        else if (GameManager.Instance.GrabbedGameObject == null)
        {
            placing = true;
            GameManager.Instance.GrabbedGameObjectDistanceOffset = 0.0f;
            GameManager.Instance.GrabbedGameObject = GestureManager.Instance.FocusedObject;
            thisRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            thisRigidbody.isKinematic = true;

            if (placeOnSpatialMap)
                GrabDistance = 30.0f;
            else
                GrabDistance = (GameManager.Instance.GrabbedGameObject.transform.position - Camera.main.transform.position).magnitude;
            if (GrabDistance < OBJECT_DISTANCE_MIN)
                GrabDistance = OBJECT_DISTANCE_MIN;
            else if (GrabDistance > OBJECT_DISTANCE_MAX)
                GrabDistance = OBJECT_DISTANCE_MAX;
            transform.localScale = startScale;
            for (int i = 0; i < defaultMaterials.Length; i++)
            {
                defaultMaterials[i].SetFloat("_Highlight", .5f);
            }

            // Play the audioSource feedback when we gaze and select a hologram.
            if (audioSource != null && !audioSource.isPlaying)
            {
                audioSource.Play();
            }

            /* TODO: DEVELOPER CODING EXERCISE 6.a */
            // 6.a: Handle the OnSelect by sending a PerformTagAlong message.
        }

    }

    IEnumerator SnapToGrid()
    {
        //transform.position = LevelGenerator.Instance.GetClosestGridPosition(transform.position);
        snapTimeCurrent = 0.0f;
        snapPositionSource = transform.position;
        snapPositionDestination = LevelGenerator.Instance.GetClosestGridPosition(transform.position);
        while (snapTimeCurrent < snapTime)
        {
            if (placing)
                break;
            thisRigidbody.MovePosition( Vector3.Lerp(snapPositionSource, snapPositionDestination, snapTimeCurrent / snapTime) );
            snapTimeCurrent += Time.deltaTime;
            //yield return new WaitForEndOfFrame();
            yield return null;
        }
        snapTimeCurrent = 0.0f;
        yield return null;
    }
}