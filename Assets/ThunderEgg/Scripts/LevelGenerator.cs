using HoloToolkit.Unity;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LevelGenerator : Singleton<LevelGenerator> {

    [Tooltip("Material to use when rendering Spatial Mapping meshes while the observer is running.")]
    public Material defaultMaterial;

    [Tooltip("Optional Material to use when rendering Spatial Mapping meshes after the observer has been stopped.")]
    public Material secondaryMaterial;

    [Tooltip("Minimum number of floor planes required in order to exit scanning/processing mode.")]
    public uint minimumFloors = 1;

    [Tooltip("Minimum number of wall planes required in order to exit scanning/processing mode.")]
    public uint minimumWalls = 1;

    public GameObject gridObjectPrefab;
    public Vector3 gridSpacing;

    /// <summary>
    /// Used for loading or saving spatial mapping data to disk.
    /// </summary>
    private ObjectSurfaceObserver objectSurfaceObserver;

    /// <summary>
    /// Indicates if processing of the surface meshes is complete.
    /// </summary>
    private bool meshesProcessed = false;

    /// <summary>
    /// Empty game object used to contain all planes created by the SurfaceToPlanes class.
    /// </summary>
    private GameObject gridParent;

    [SerializeField]
    private GameObject surfacePlanes;
    private int numCeilings;
    private int numCeilingsBottom;
    private int numFloors;
    private int numFloorsTop;
    private int numTables;
    private int numTablesTop;
    private int numWalls;
    private int numUnknowns;
    private static bool buildingGrid = false;
    private List<GameObject> horizontalPlanes;
    private List<GridPoint> gridObjects;
    GameObject ceiling;
    GameObject floor;
    GameObject wallNorth;
    GameObject wallSouth;
    GameObject wallEast;
    GameObject wallWest;

    /// <summary>
    /// GameObject initialization.
    /// </summary>
    private void Start()
    {
        // Update surfaceObserver and storedMeshes to use the same material during scanning.
        SpatialMappingManager.Instance.SetSurfaceMaterial(defaultMaterial);

        // Register for the MakePlanesComplete event.
        SurfaceMeshesToPlanes.Instance.MakePlanesComplete += SurfaceMeshesToPlanes_MakePlanesComplete;

        horizontalPlanes = new List<GameObject>();
        gridObjects = new List<GridPoint>();
        gridParent = new GameObject("GridPoints");
        gridParent.transform.position = Vector3.zero;
        gridParent.transform.rotation = Quaternion.identity;

        StartObserver();
    }

    /// <summary>
    /// Called once per frame.
    /// </summary>
    //void Update ()
    //{
    //}

    /// <summary>
    /// 
    /// </summary>
    void StartObserver()
    {
#if UNITY_EDITOR
        objectSurfaceObserver = GetComponent<ObjectSurfaceObserver>();

        if (objectSurfaceObserver == null || objectSurfaceObserver.roomModel == null)
        {
            SpatialMappingManager.Instance.StartObserver();
        }
        else
        {
            // In the Unity editor, try loading a saved mesh.
            objectSurfaceObserver.Load(objectSurfaceObserver.roomModel);

            if (objectSurfaceObserver.GetMeshFilters().Count > 0)
            {
                SpatialMappingManager.Instance.SetSpatialMappingSource(objectSurfaceObserver);
            }
        }
#else
        SpatialMappingManager.Instance.StartObserver();
#endif
    }

    /// <summary>
    /// 
    /// </summary>
    void TurnOnScanner()
    {
        if (!SpatialMappingManager.Instance.IsObserverRunning())
            StartObserver();
    }

    /// <summary>
    /// 
    /// </summary>
    void TurnOffScanner()
    {
        if (SpatialMappingManager.Instance.IsObserverRunning())
            SpatialMappingManager.Instance.StopObserver();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool RoomExists()
    {
        return meshesProcessed;
    }

    /// <summary>
    /// 
    /// </summary>
    public void FindRoom()
    {
        //Debug.Log("Find Room");
        TurnOffScanner();
        CreatePlanes();
        if (surfacePlanes == null)
            surfacePlanes = GameObject.Find("SurfacePlanes");
    }

    /// <summary>
    /// 
    /// </summary>
    public void RemoveRoom()
    {
        //Debug.Log("Remove Room");
        meshesProcessed = false;
        LevelGenerator.buildingGrid = false;

        ClearGrid();

        // Remove any previously existing planes, as they may no longer be valid.
        horizontalPlanes = SurfaceMeshesToPlanes.Instance.ActivePlanes;
        for (int index = 0; index < horizontalPlanes.Count; index++)
        {
            try { Destroy(horizontalPlanes[index]); } catch { }
        }
        horizontalPlanes.Clear();
        SurfaceMeshesToPlanes.Instance.ActivePlanes.Clear();

        SpatialMappingManager.Instance.SetSurfaceMaterial(defaultMaterial);

        TurnOnScanner();
    }

    /// <summary>
    /// 
    /// </summary>
    public bool ToggleRoom()
    {
        if (RoomExists())
            RemoveRoom();
        else
            FindRoom();
        return RoomExists();
    }

    /// <summary>
    /// Creates planes from the spatial mapping surfaces.
    /// </summary>
    private void CreatePlanes()
    {
        // Generate planes based on the spatial map.
        SurfaceMeshesToPlanes surfaceToPlanes = SurfaceMeshesToPlanes.Instance;
        if (surfaceToPlanes != null && surfaceToPlanes.enabled)
        {
            meshesProcessed = false;
            surfaceToPlanes.MakePlanes();
        }
    }

    /// <summary>
    /// Handler for the SurfaceMeshesToPlanes MakePlanesComplete event.
    /// </summary>
    /// <param name="source">Source of the event.</param>
    /// <param name="args">Args for the event.</param>
    private void SurfaceMeshesToPlanes_MakePlanesComplete(object source, System.EventArgs args)
    {
        // Collection of floor and table planes that we can use to set horizontal items on.
        List<GameObject> horizontal = new List<GameObject>();

        // Collection of wall planes that we can use to set vertical items on.
        List<GameObject> vertical = new List<GameObject>();

        // 3.a: Get all floor and table planes by calling
        // SurfaceMeshesToPlanes.Instance.GetActivePlanes().
        // Assign the result to the 'horizontal' list.
        horizontal = SurfaceMeshesToPlanes.Instance.GetActivePlanes(PlaneTypes.Floor | PlaneTypes.Table);

        // 3.a: Get all wall planes by calling
        // SurfaceMeshesToPlanes.Instance.GetActivePlanes().
        // Assign the result to the 'vertical' list.
        vertical = SurfaceMeshesToPlanes.Instance.GetActivePlanes(PlaneTypes.Wall);

        // Check to see if we have enough horizontal planes (minimumFloors)
        // and vertical planes (minimumWalls), to set holograms on in the world.
        if (horizontal.Count >= minimumFloors && vertical.Count >= minimumWalls)
        {
            // We have enough floors and walls to place our holograms on...

            // 3.a: Let's reduce our triangle count by removing triangles
            // from SpatialMapping meshes that intersect with our active planes.
            // Call RemoveVertices().
            // Pass in all activePlanes found by SurfaceMeshesToPlanes.Instance.
            RemoveVertices(SurfaceMeshesToPlanes.Instance.ActivePlanes);

            // 3.a: We can indicate to the user that scanning is over by
            // changing the material applied to the Spatial Mapping meshes.
            // Call SpatialMappingManager.Instance.SetSurfaceMaterial().
            // Pass in the secondaryMaterial.
            SpatialMappingManager.Instance.SetSurfaceMaterial(secondaryMaterial);

            // 3.a: We are all done processing the mesh, so we can now
            // initialize a collection of Placeable holograms in the world
            // and use horizontal/vertical planes to set their starting positions.
            // Call SpaceCollectionManager.Instance.GenerateItemsInWorld().
            // Pass in the lists of horizontal and vertical planes that we found earlier.
            try
            {
                SpaceCollectionManager.Instance.GenerateItemsInWorld(horizontal, vertical);
            }
            catch { }

            horizontalPlanes = SurfaceMeshesToPlanes.Instance.GetActivePlanes(PlaneTypes.Floor | PlaneTypes.Table | PlaneTypes.Ceiling);
            SearchForRoom(surfacePlanes);
            StartCoroutine(CreateGrid());

            meshesProcessed = true;
        }
        else
        {
            // We do not have enough floors/walls to place our holograms on...

            // 3.a: Re-enter scanning mode so the user can find more surfaces by 
            // calling StartObserver() on the SpatialMappingManager.Instance.
            StartObserver();

            // 3.a: Re-process spatial data after scanning completes by
            // re-setting meshesProcessed to false.
            meshesProcessed = false;
        }
    }

    /// <summary>
    /// Removes triangles from the spatial mapping surfaces.
    /// </summary>
    /// <param name="boundingObjects"></param>
    private void RemoveVertices(IEnumerable<GameObject> boundingObjects)
    {
        RemoveSurfaceVertices removeVerts = RemoveSurfaceVertices.Instance;
        if (removeVerts != null && removeVerts.enabled)
        {
            removeVerts.RemoveSurfaceVerticesWithinBounds(boundingObjects);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="surfacePlanes"></param>
    void SearchForRoom(GameObject surfacePlanes)
    {
        if (surfacePlanes == null)
            return;

        Transform currentChild;
        SurfacePlane currentSurface;
        int children = surfacePlanes.transform.childCount;
        bool validSurface = false;
        numCeilings = 0;
        numCeilingsBottom = 0;
        numFloors = 0;
        numFloorsTop = 0;
        numTables = 0;
        numTablesTop = 0;
        numWalls = 0;
        numUnknowns = 0;

        ceiling = GetLargestCeiling();
        floor = GetLargestFloor();
        float roomHeight = ceiling.transform.position.y - floor.transform.position.y;
        Collider planeCollider;
        Vector3 planeNormal;
        RaycastHit hitInfo;

        for (int i = 0; i < children; ++i)
        {
            validSurface = false;
            currentChild = surfacePlanes.transform.GetChild(i);
            currentSurface = currentChild.GetComponent<SurfacePlane>();
            //currentSurface.GetComponent<MeshRenderer>().enabled = false;

            // remove plane that the camera cannot see
            planeNormal = -currentChild.forward;
            if (!Physics.Raycast(Camera.main.transform.position, planeNormal, out hitInfo,
                Mathf.Infinity, ~(1 << SpatialMappingManager.Instance.LayerMask)))
                currentChild.gameObject.SetActive(false);

            if (currentSurface != null)
            {
                //Debug.Log("For loop: " + currentChild + " " + currentSurface.PlaneType);
                //Debug.DrawRay(currentChild.position, currentChild.forward);
                planeCollider = currentChild.GetComponent<Collider>();
                switch (currentSurface.PlaneType)
                {
                    case PlaneTypes.Ceiling:
                        numCeilings++;
                        currentChild.name = currentChild.name + "(Ceiling)";
                        if (Vector3.Dot(currentChild.forward, Vector3.down) < 0)
                        {
                            validSurface = true;
                            Debug.DrawRay(currentChild.position, currentChild.forward, Color.cyan);
                            numCeilingsBottom++;
                        }
                        // remove ceilings above the main ceiling
                        if (planeCollider.transform.position.y > ceiling.transform.position.y)
                            currentChild.gameObject.SetActive(false);
                        break;

                    case PlaneTypes.Floor:
                        numFloors++;
                        currentChild.name = currentChild.name + "(Floor)";
                        if (Vector3.Dot(currentChild.forward, Vector3.down) > 0)
                        {
                            validSurface = true;
                            Debug.DrawRay(currentChild.position, currentChild.forward, Color.green);
                            numFloorsTop++;
                        }
                        // remove floors below the main floor
                        if (planeCollider.transform.position.y < floor.transform.position.y)
                            currentChild.gameObject.SetActive(false);
                        break;

                    case PlaneTypes.Table:
                        numTables++;
                        currentChild.name = currentChild.name + "(Table)";
                        if (Vector3.Dot(currentChild.forward, Vector3.down) > 0)
                        {
                            validSurface = true;
                            Debug.DrawRay(currentChild.position, currentChild.forward, Color.red);
                            numTablesTop++;
                        }
                        // remove tables that are too tall or below the floor
                        if (planeCollider.transform.position.y > floor.transform.position.y + roomHeight*0.75f ||
                            planeCollider.transform.position.y < floor.transform.position.y)
                            currentChild.gameObject.SetActive(false);
                        break;

                    case PlaneTypes.Wall:
                        numWalls++;
                        currentChild.name = currentChild.name + "(Wall)";
                        validSurface = true;
                        // remove walls that are not room height
                        if (planeCollider.bounds.extents.y*2.0f < roomHeight)
                            currentChild.gameObject.SetActive(false);
                        else
                        {
                            if (Mathf.Abs(planeNormal.z) >= Mathf.Abs(planeNormal.x))
                            {
                                if (planeNormal.z <= 0.0f)
                                {
                                    wallNorth = currentChild.gameObject;
                                    currentChild.name = currentChild.name + "(North)";
                                }
                                else
                                {
                                    wallSouth = currentChild.gameObject;
                                    currentChild.name = currentChild.name + "(South)";
                                }
                            }
                            else
                            {
                                if (planeNormal.x <= 0.0f)
                                {
                                    wallEast = currentChild.gameObject;
                                    currentChild.name = currentChild.name + "(East)";
                                }
                                else
                                {
                                    wallWest = currentChild.gameObject;
                                    currentChild.name = currentChild.name + "(West)";
                                }
                            }
                        }
                        break;

                    case PlaneTypes.Unknown:
                        numUnknowns++;
                        currentChild.name = currentChild.name + "(Unknown)";
                        break;

                    default:
                        Debug.Log("For loop: " + currentChild + " Invalid Surface PlaneType");
                        break;
                }

                //if (currentChild.gameObject.activeSelf != validSurface)
                //    currentChild.gameObject.SetActive(validSurface);
            }
            else
            {
                Debug.Log("For loop: " + currentChild + " Missing Surface Info");
            }
        }

        ceiling.name = ceiling.name + " (Largest Ceiling Found)";
        floor.name = floor.name + " (Largest Floor Found)";
    }

    /// <summary>
    /// 
    /// </summary>
    private GameObject GetLargestObjectYZ(List<GameObject> objectsToCheck)
    {
        GameObject largestObjectXZ = objectsToCheck[0];
        Collider currentCollider = objectsToCheck[0].GetComponent<Collider>();
        float largestArea = (currentCollider.bounds.extents.y * 2) * (currentCollider.bounds.extents.z * 2);

        float newArea;
        for (int i = 1; i < objectsToCheck.Count; i++)
        {
            currentCollider = objectsToCheck[0].GetComponent<Collider>();
            newArea = (currentCollider.bounds.extents.y * 2) * (currentCollider.bounds.extents.z * 2);
            if (newArea > largestArea)
            {
                largestArea = newArea;
                largestObjectXZ = objectsToCheck[i];
            }
        }

        return largestObjectXZ;
    }

    /// <summary>
    /// 
    /// </summary>
    private GameObject GetLargestFloor()
    {
        return GetLargestObjectYZ(SurfaceMeshesToPlanes.Instance.GetActivePlanes(PlaneTypes.Floor));
    }

    /// <summary>
    /// 
    /// </summary>
    private GameObject GetLargestCeiling()
    {
        return GetLargestObjectYZ(SurfaceMeshesToPlanes.Instance.GetActivePlanes(PlaneTypes.Ceiling));
    }

    /// <summary>
    /// 
    /// </summary>
    public List<GridPoint> GetGrid()
    {
        return gridObjects;
    }

    /// <summary>
    /// 
    /// </summary>
    public Vector3 GetClosestGridPosition(Vector3 position)
    {
        Vector3 closestPoint = position;
        float closestDistance = Mathf.Infinity;
        Vector3 currentPath;
        float currentDistance;
        RaycastHit hitInfo;
        for (int i = 0; i < gridObjects.Count; i++)
        {
            currentPath = ((GridPoint)gridObjects[i]).transform.position - position;
            currentDistance = currentPath.sqrMagnitude;
            if ( !Physics.Raycast(position, currentPath, out hitInfo, Mathf.Sqrt(currentDistance)) )
                continue;
            if (hitInfo.collider.gameObject.layer != GameManager.Instance.GridPhysicsLayer)
                continue;

            if (currentDistance < closestDistance)
            {
                closestDistance = currentDistance;
                closestPoint = ((GridPoint)gridObjects[i]).transform.position;
            }
        }
        return closestPoint;
    }

    /// <summary>
    /// 
    /// </summary>
    private void ClearGrid()
    {
        for (int i = 0; i < gridObjects.Count; i++)
            Destroy(((GridPoint)gridObjects[i]).gameObject);
        gridObjects.Clear();
    }

    /// <summary>
    /// 
    /// </summary>
    private IEnumerator CreateGrid()
    {
        //Debug.Log("CreateGrid");
        LevelGenerator.buildingGrid = true;

        if (gridObjects.Count > 0)
            ClearGrid();

        GameObject newGridObject;

        //bool buildingGrid = true;
        //Collider floorCollider = floor.GetComponent<Collider>();

        //Vector3 startLocation = floorCollider.bounds.min;
        //startLocation.x += gridSpacing / 2.0f;
        //startLocation.y += gridSpacing / 2.0f;
        //startLocation.z += gridSpacing / 2.0f;

        //Vector3 endLocation = floorCollider.bounds.max;

        //Vector3 gridLocation = startLocation;
        //while (buildingGrid)
        //{
        //    newGridObject = Instantiate(gridObjectPrefab);
        //    newGridObject.transform.position = gridLocation;
        //    newGridObject.transform.parent = gridParent.transform;
        //    gridObjects.Add(newGridObject);

        //    gridLocation.x += gridSpacing;

        //    if (gridLocation.x >= endLocation.x)
        //    {
        //        gridLocation.z += gridSpacing;
        //        if (gridLocation.z < endLocation.z)
        //            gridLocation.x = startLocation.x;
        //        else
        //            buildingGrid = false;
        //    }
        //}

        //Vector3[] corners = GetColliderVertexPositions(floor);
        Vector3[] corners = GetColliderVertexPositions(floor.GetComponent<BoxCollider>(),
            gridSpacing.x / 2.0f, gridSpacing.y / 2.0f, gridSpacing.z / 2.0f);
        Vector3[] cornersCeiling = GetColliderVertexPositions(ceiling.GetComponent<BoxCollider>(),
            gridSpacing.x / 2.0f, gridSpacing.y / 2.0f, gridSpacing.z / 2.0f);

        //Debug.Log("CreateGrid: corners: " + corners.Length + corners[5]);
        //Debug.Log("CreateGrid: cornersCeiling: " + cornersCeiling.Length + cornersCeiling[5]);

        Vector3 xPath = corners[4] - corners[5];
        Vector3 yPath = (ceiling.transform.position.y - floor.transform.position.y) * Vector3.up;
        Vector3 zPath = corners[6] - corners[5];
        int numberSectionsX = (int)Mathf.Floor(xPath.magnitude / gridSpacing.x);
        int numberSectionsY = (int)Mathf.Floor(yPath.magnitude / gridSpacing.y);
        int numberSectionsZ = (int)Mathf.Floor(zPath.magnitude / gridSpacing.z);

        //Debug.Log("CreateGrid: X Path: " + xPath.magnitude + ' ' + numberSectionsX);
        //Debug.Log("CreateGrid: Y Path: " + yPath.magnitude + ' ' + numberSectionsY);
        //Debug.Log("CreateGrid: Z Path: " + zPath.magnitude + ' ' + numberSectionsZ);

        xPath = xPath.normalized;
        yPath = yPath.normalized;
        zPath = zPath.normalized;

        Vector3 startLocation = corners[5];
        //Vector3 endLocation = corners[7];
        Vector3 gridLocation = startLocation;
        int loops = 0;
        int row = 0;
        int column = 0;
        int height = 0;
        bool buildingGrid = true;
        List<Vector3> verticesGrid = new List<Vector3>();
        RaycastHit hitInfo;
        bool createGridPoint = true;
        float fcw = 4.0f;
        //Bounds gridPointBounds = new Bounds();
        //MeshCollider[] roomMeshes = LevelGenerator.Instance.GetComponentsInChildren<MeshCollider>();
        Vector3 pathToGridPoint;
        while (buildingGrid)
        {
            if (!LevelGenerator.buildingGrid)
                break;

            createGridPoint = true;
            gridLocation = startLocation
                    + zPath * gridSpacing.z * row
                    + xPath * gridSpacing.x * column
                    + yPath * gridSpacing.y * height;

            // do not create a grid point that is too close to the surface mesh
            if (Physics.Raycast(gridLocation, xPath, out hitInfo, gridSpacing.x / fcw, SpatialMappingManager.Instance.LayerMask) ||
                Physics.Raycast(gridLocation, -xPath, out hitInfo, gridSpacing.x / fcw, SpatialMappingManager.Instance.LayerMask) ||
                Physics.Raycast(gridLocation, yPath, out hitInfo, gridSpacing.y / fcw, SpatialMappingManager.Instance.LayerMask) ||
                Physics.Raycast(gridLocation, -yPath, out hitInfo, gridSpacing.y / fcw, SpatialMappingManager.Instance.LayerMask) ||
                Physics.Raycast(gridLocation, zPath, out hitInfo, gridSpacing.z / fcw, SpatialMappingManager.Instance.LayerMask) ||
                Physics.Raycast(gridLocation, -zPath, out hitInfo, gridSpacing.z / fcw, SpatialMappingManager.Instance.LayerMask)
                )
            {
                createGridPoint = false;
            }

            // do not create a grid point based on a bounding box region around the point intersecting the surface mesh
            //if (createGridPoint)
            //{
            //    gridPointBounds = new Bounds(gridLocation, gridSpacing);
            //    for (int i = 0; i < roomMeshes.Length; i++)
            //    {
            //        if (roomMeshes[i].bounds.Intersects(gridPointBounds))
            //        {
            //            createGridPoint = false;
            //            break;
            //        }
            //    }
            //}

            // do not create a grid point that cannot be seen by the camera
            if (createGridPoint)
            {
                pathToGridPoint = gridLocation - Camera.main.transform.position;
                createGridPoint = !Physics.Raycast(Camera.main.transform.position, pathToGridPoint, out hitInfo, pathToGridPoint.magnitude);
            }

            if (createGridPoint)
                verticesGrid.Add(gridLocation);

            // move up z row
            row += 1;
            if (row > numberSectionsZ)
            {
                // move up x column
                row = 0;
                column += 1;
                if (column > numberSectionsX)
                {
                    // move up y height
                    column = 0;
                    height += 1;
                    if (height > numberSectionsY)
                    {
                        // out of bounds - quit building
                        buildingGrid = false;
                    }
                }
            }
            loops += 1;

            if (loops % 100 == 0)
                yield return null;
        }

        //for (int i = 0; i < corners.Length; i++)
        for (int i = 0; i < verticesGrid.Count; i++)
        {
            if (!LevelGenerator.buildingGrid)
                break;

            newGridObject = Instantiate(gridObjectPrefab);
            //newGridObject.transform.position = corners[i];
            newGridObject.transform.position = verticesGrid[i];
            newGridObject.transform.parent = gridParent.transform;
            newGridObject.transform.localEulerAngles = new Vector3(0.0f, floor.transform.localEulerAngles.y, 0.0f);
            newGridObject.name = newGridObject.name + i;
            GridPoint newGridPoint = newGridObject.GetComponent<GridPoint>();
            gridObjects.Add(newGridPoint);

            if (i % 100 == 0)
                yield return null;
        }

        LevelGenerator.buildingGrid = false;
    }

    //Vector3[] GetColliderVertexPositions(GameObject inputObject) {
    //    var vertices = new Vector3[8];
    //    var thisMatrix = inputObject.transform.localToWorldMatrix;
    //    var storedRotation = inputObject.transform.rotation;
    //    inputObject.transform.rotation = Quaternion.identity;
   
    //    var extents = inputObject.GetComponent<Collider>().bounds.extents;
    //    vertices[0] = thisMatrix.MultiplyPoint3x4(extents);
    //    vertices[1] = thisMatrix.MultiplyPoint3x4(new Vector3(-extents.x, extents.y, extents.z));
    //    vertices[2] = thisMatrix.MultiplyPoint3x4(new Vector3(extents.x, extents.y, -extents.z));
    //    vertices[3] = thisMatrix.MultiplyPoint3x4(new Vector3(-extents.x, extents.y, -extents.z));
    //    vertices[4] = thisMatrix.MultiplyPoint3x4(new Vector3(extents.x, -extents.y, extents.z));
    //    vertices[5] = thisMatrix.MultiplyPoint3x4(new Vector3(-extents.x, -extents.y, extents.z));
    //    vertices[6] = thisMatrix.MultiplyPoint3x4(new Vector3(extents.x, -extents.y, -extents.z));
    //    vertices[7] = thisMatrix.MultiplyPoint3x4(-extents);

    //    inputObject.transform.rotation = storedRotation;
    //    return vertices;
    //}

    Vector3[] GetColliderVertexPositions(BoxCollider b, 
        float offsetx = 0.0f, float offsety = 0.0f, float offsetz = 0.0f)
    {
        Vector3[] vertices = new Vector3[8];

        vertices[0] = b.center + new Vector3(b.size.x, -b.size.y, b.size.z) * 0.5f;
        vertices[1] = b.center + new Vector3(-b.size.x, -b.size.y, b.size.z) * 0.5f;
        vertices[2] = b.center + new Vector3(-b.size.x, b.size.y, b.size.z) * 0.5f;
        vertices[3] = b.center + new Vector3(b.size.x, b.size.y, b.size.z) * 0.5f;

        vertices[4] = b.center + new Vector3(b.size.x, -b.size.y, b.size.z) * 0.5f;
        vertices[5] = b.center + new Vector3(-b.size.x, -b.size.y, b.size.z) * 0.5f;
        vertices[6] = b.center + new Vector3(-b.size.x, b.size.y, b.size.z) * 0.5f;
        vertices[7] = b.center + new Vector3(b.size.x, b.size.y, b.size.z) * 0.5f;

        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = transform.TransformPoint(vertices[i]);

        vertices[4].x -= offsetx;
        vertices[4].y += offsety;
        vertices[4].z += offsetz;
        vertices[5].x += offsetx;
        vertices[5].y += offsety;
        vertices[5].z += offsetz;
        vertices[6].x += offsetx;
        vertices[6].y += offsety;
        vertices[6].z -= offsetz;
        vertices[7].x -= offsetx;
        vertices[7].y += offsety;
        vertices[7].z -= offsetz;

        return vertices;
    }

    /// <summary>
    /// Called when the GameObject is unloaded.
    /// </summary>
    private void OnDestroy()
    {
        if (SurfaceMeshesToPlanes.Instance != null)
        {
            SurfaceMeshesToPlanes.Instance.MakePlanesComplete -= SurfaceMeshesToPlanes_MakePlanesComplete;
        }
    }
}
