// This tool enables painting prefabs in 3D like a tilemap within the Unity editor.
// Features:
// - Pencil placement and area fill by drag
// - Overlap avoidance by pushing new instances out of intersections
// - Works in edit mode (no play required)

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LevelPainterWindow : EditorWindow
{
    private enum PaintMode
    {
        Pencil,
        Fill,
        Eraser,
        Eyedropper
    }

    private void HandleEraserMode(Event e, Vector3 hitPoint)
    {
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            EraseAt(hitPoint);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0)
        {
            EraseAt(hitPoint);
            e.Use();
        }
    }

    private void HandleEyedropperMode(Event e, Vector3 hitPoint)
    {
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            PickAt(hitPoint);
            e.Use();
        }
    }

    private enum ConflictMode
    {
        Replace,
        Stack
    }

    private const float SceneHandleSize = 0.05f;

    [SerializeField] private List<GameObject> prefabPalette = new List<GameObject>();
    [SerializeField] private LevelPrefabPalette paletteAsset;
    [SerializeField] private List<LevelPrefabPalette> paletteSet = new List<LevelPrefabPalette>();
    [SerializeField] private int activePaletteSetIndex = -1;
	[SerializeField] private int selectedPrefabIndex = 0;
    [SerializeField] private List<Vector3> prefabSizes = new List<Vector3>();
    [SerializeField] private float brushRadius = 1.0f;
    [SerializeField] private float spacing = 1.0f;
    [SerializeField] private bool alignToSurfaceNormal = true;
    [SerializeField] private bool randomizeYaw = false;
    [SerializeField] private float randomYawMaxDegrees = 360f;
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private bool avoidOverlap = true;
    [SerializeField] private int maxPushIterations = 8;
    [SerializeField] private float maxPushDistance = 3.0f;
    [SerializeField] private bool autoAddMeshCollider = true;
    [SerializeField] private bool autoColliderConvex = true;
    [SerializeField] private bool snapToExistingArray = true;
    [SerializeField] private ConflictMode conflictMode = ConflictMode.Replace;

    private PaintMode paintMode = PaintMode.Pencil;
    private bool isActiveInScene = true;

    private bool isDraggingArea = false;
    private Vector3 dragStartWorld;
    private Vector3 dragCurrentWorld;
    private Vector3 previewStartWorld;
    private Vector3 previewCurrentWorld;
    private Vector3 dragPlaneNormal = Vector3.up;
    private Vector3 fillPlaneOrigin;
    private bool hasLastPaintPosition = false;
    private Vector3 lastPaintPosition;
    private float fillFixedY;
	[SerializeField] private Vector2 mainScroll;
	[SerializeField] private Vector2 paletteListScroll;
	[SerializeField] private Vector2 paletteGridScroll;
	[SerializeField] private float paletteGridHeight = 320f;
	[SerializeField] private int paletteGridColumns = 4;
	[SerializeField] private float paletteCellSize = 72f;

    [MenuItem("Tools/Level Painter")] 
    public static void ShowWindow()
    {
        var window = GetWindow<LevelPainterWindow>();
        window.titleContent = new GUIContent("Level Painter");
        window.Show();
    }

	private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

	private void OnInspectorUpdate()
    {
		// Ensure preview thumbnails refresh
		Repaint();
	}

	private void OnGUI()
	{
		// Top toolbar: tools and options
		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		int newTool = GUILayout.Toolbar((int)paintMode, new GUIContent[] {
			new GUIContent("Pencil"),
			new GUIContent("Fill"),
			new GUIContent("Eraser"),
			new GUIContent("Eyedropper"),
		}, EditorStyles.toolbarButton);
		if (newTool != (int)paintMode) paintMode = (PaintMode)newTool;
		GUILayout.FlexibleSpace();
		snapToExistingArray = GUILayout.Toggle(snapToExistingArray, new GUIContent("Snap"), EditorStyles.toolbarButton);
		string[] conflictOpts = new []{"Replace","Stack"};
		int conflictIdx = (int)conflictMode;
		conflictIdx = EditorGUILayout.Popup(conflictIdx, conflictOpts, EditorStyles.toolbarPopup, GUILayout.Width(90));
		conflictMode = (ConflictMode)conflictIdx;
		EditorGUILayout.EndHorizontal();

		mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

		EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            paletteAsset = (LevelPrefabPalette)EditorGUILayout.ObjectField("Palette Asset", paletteAsset, typeof(LevelPrefabPalette), false);
            if (GUILayout.Button("New", GUILayout.Width(60)))
            {
                CreateNewPaletteAsset();
            }
        }

        // Quick palette switcher (palette set)
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("+ Add Palette", EditorStyles.toolbarButton, GUILayout.Width(110)))
        {
            if (paletteAsset != null) paletteSet.Add(paletteAsset);
        }
        if (GUILayout.Button("- Remove", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            if (activePaletteSetIndex >= 0 && activePaletteSetIndex < paletteSet.Count)
            {
                paletteSet.RemoveAt(activePaletteSetIndex);
                activePaletteSetIndex = Mathf.Clamp(activePaletteSetIndex - 1, -1, paletteSet.Count - 1);
            }
        }
        GUILayout.FlexibleSpace();
        string[] names = new string[paletteSet.Count];
        for (int i = 0; i < paletteSet.Count; i++) names[i] = paletteSet[i] != null ? paletteSet[i].name : "(null)";
        int newActive = Mathf.Clamp(EditorGUILayout.Popup(activePaletteSetIndex, names, EditorStyles.toolbarPopup, GUILayout.Width(180)), -1, paletteSet.Count - 1);
        if (newActive != activePaletteSetIndex)
        {
            activePaletteSetIndex = newActive;
            if (activePaletteSetIndex >= 0)
            {
                paletteAsset = paletteSet[activePaletteSetIndex];
                LoadFromAsset();
            }
        }
        EditorGUILayout.EndHorizontal();

		// Palette toolbar
		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24)))
		{
			prefabPalette.Add(null);
		}
		if (GUILayout.Button("Remove Nulls", EditorStyles.toolbarButton))
		{
			for (int i = prefabPalette.Count - 1; i >= 0; i--)
			{
				if (prefabPalette[i] == null)
				{
					prefabPalette.RemoveAt(i);
				}
			}
			if (selectedPrefabIndex >= prefabPalette.Count) selectedPrefabIndex = Mathf.Max(0, prefabPalette.Count - 1);
		}
		if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
		{
			prefabPalette.Clear();
			selectedPrefabIndex = 0;
		}
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(60)))
		{
			LoadFromAsset();
		}
		if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
		{
			SaveToAsset();
		}
		EditorGUILayout.EndHorizontal();

		// Palette content styled like Tile Palette
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		EditorGUILayout.LabelField("Selected Item", EditorStyles.miniBoldLabel);
		paletteListScroll = EditorGUILayout.BeginScrollView(paletteListScroll, GUILayout.MaxHeight(160));
		// Show only the selected item for editing
		SyncSizeListWithPalette();
		if (prefabPalette.Count == 0)
		{
			EditorGUILayout.HelpBox("Palette is empty. Use + to add slots or Load a palette.", MessageType.Info);
		}
		else
		{
			int i = Mathf.Clamp(selectedPrefabIndex, 0, prefabPalette.Count - 1);
			EditorGUILayout.BeginHorizontal();
			prefabPalette[i] = (GameObject)EditorGUILayout.ObjectField($"Element {i}", prefabPalette[i], typeof(GameObject), false);
			bool remove = GUILayout.Button("X", GUILayout.Width(24));
			EditorGUILayout.EndHorizontal();

			if (remove)
			{
				prefabPalette.RemoveAt(i);
				prefabSizes.RemoveAt(i);
				if (selectedPrefabIndex >= prefabPalette.Count) selectedPrefabIndex = Mathf.Max(0, prefabPalette.Count - 1);
				EditorGUILayout.EndScrollView();
				EditorGUILayout.EndVertical();
				goto AFTER_SELECTED_ITEM_BLOCK;
			}

			if (i >= 0 && i < prefabSizes.Count)
			{
				if (prefabPalette[i] != null && prefabSizes[i] == Vector3.zero)
				{
					prefabSizes[i] = EstimatePrefabSize(prefabPalette[i]);
				}
				prefabSizes[i] = EditorGUILayout.Vector3Field("Size", prefabSizes[i]);
			}
		}
		EditorGUILayout.EndScrollView();
		EditorGUILayout.EndVertical();
AFTER_SELECTED_ITEM_BLOCK:

		EditorGUILayout.Space(6);
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		EditorGUILayout.LabelField("Palette Grid", EditorStyles.miniBoldLabel);
        paletteGridScroll = EditorGUILayout.BeginScrollView(paletteGridScroll, GUILayout.MinHeight(paletteGridHeight));
        DrawPaletteSelectionGrid();
		EditorGUILayout.EndScrollView();
		EditorGUILayout.EndVertical();

        var selectedPrefab = GetSelectedPrefab();
		EditorGUILayout.Space(6);
		EditorGUILayout.LabelField("Selected Preview", EditorStyles.boldLabel);
		DrawLargePreview(selectedPrefab);

		EditorGUILayout.Space(6);
		EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
        brushRadius = EditorGUILayout.Slider("Brush Radius", brushRadius, 0.1f, 10f);
        spacing = EditorGUILayout.Slider("Spacing", spacing, 0.1f, 10f);
        paletteGridHeight = EditorGUILayout.Slider("Palette Grid Height", paletteGridHeight, 120f, 800f);
        paletteGridColumns = EditorGUILayout.IntSlider("Palette Grid Columns", paletteGridColumns, 1, 10);
        paletteCellSize = EditorGUILayout.Slider("Palette Cell Size (Preview)", paletteCellSize, 16f, 160f);
        alignToSurfaceNormal = EditorGUILayout.Toggle("Align To Surface Normal", alignToSurfaceNormal);
        randomizeYaw = EditorGUILayout.Toggle("Randomize Yaw", randomizeYaw);
        if (randomizeYaw)
        {
            randomYawMaxDegrees = EditorGUILayout.Slider("Max Yaw Degrees", randomYawMaxDegrees, 0f, 360f);
        }

		EditorGUILayout.Space(6);
		EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
		// Redundant but explicit controls similar to Tile Palette inspector
		paintMode = (PaintMode)EditorGUILayout.EnumPopup("Tool Mode", paintMode);
		snapToExistingArray = EditorGUILayout.Toggle("Snap To Existing Array", snapToExistingArray);
		conflictMode = (ConflictMode)EditorGUILayout.EnumPopup("On Conflict", conflictMode);

		EditorGUILayout.Space(6);
		EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
		placementMask = LayerMaskField("Placement Mask", placementMask);
		isActiveInScene = EditorGUILayout.Toggle("Enable In Scene", isActiveInScene);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Overlap Avoidance", EditorStyles.boldLabel);
        avoidOverlap = EditorGUILayout.Toggle("Avoid Overlap", avoidOverlap);
        using (new EditorGUI.DisabledScope(!avoidOverlap))
        {
            maxPushIterations = EditorGUILayout.IntSlider("Max Push Iterations", maxPushIterations, 0, 32);
            maxPushDistance = EditorGUILayout.Slider("Max Push Distance", maxPushDistance, 0f, 10f);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Colliders", EditorStyles.boldLabel);
        autoAddMeshCollider = EditorGUILayout.Toggle("Auto Add MeshCollider", autoAddMeshCollider);
        using (new EditorGUI.DisabledScope(!autoAddMeshCollider))
        {
            autoColliderConvex = EditorGUILayout.Toggle("Convex MeshCollider", autoColliderConvex);
        }

		EditorGUILayout.HelpBox(
            "Scene controls:\n" +
            "- Left Click: place\n" +
            "- Drag in Fill mode: define area, release to fill\n" +
            "- Hold Shift while clicking to place multiple with drag (Pencil)\n" +
            "- Tool works in Edit Mode.", MessageType.Info);

		EditorGUILayout.EndScrollView();
    }

    private static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        var layers = new List<string>();
        var layerNumbers = new List<int>();

        for (int i = 0; i < 32; i++)
        {
            var layerName = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(layerName))
            {
                layers.Add(layerName);
                layerNumbers.Add(i);
            }
        }

        int maskWithoutEmpty = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if (((1 << layerNumbers[i]) & selected.value) > 0)
                maskWithoutEmpty |= (1 << i);
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());
        int mask = 0;
        for (int i = 0; i < layerNumbers.Count; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) > 0)
                mask |= (1 << layerNumbers[i]);
        }
        selected.value = mask;
        return selected;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        // Repaint scene while moving the mouse to keep brush feedback fresh
        if (e.type == EventType.MouseMove)
        {
            SceneView.RepaintAll();
        }

        if (!isActiveInScene)
        {
            DrawSceneOverlayLabel("Level Painter disabled (Enable In Scene is off)");
            return;
        }

        if (GetSelectedPrefab() == null)
        {
            DrawSceneOverlayLabel("Select a prefab in the Palette to paint");
            return;
        }

        // Do not intercept camera navigation (Alt key in SceneView)
        if (e.alt) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!TryRaycast(ray, out Vector3 hitPoint, out Vector3 hitNormal))
        {
            DrawCursorFallback(ray);
            return;
        }

        dragPlaneNormal = hitNormal.sqrMagnitude > 0.001f ? hitNormal : Vector3.up;

        DrawBrushGizmos(hitPoint, dragPlaneNormal);

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (paintMode == PaintMode.Pencil)
        {
            HandlePencilMode(e, hitPoint, hitNormal);
        }
        else if (paintMode == PaintMode.Fill)
        {
            HandleFillMode(e, hitPoint, hitNormal);
        }
        else if (paintMode == PaintMode.Eraser)
        {
            HandleEraserMode(e, hitPoint);
        }
        else if (paintMode == PaintMode.Eyedropper)
        {
            HandleEyedropperMode(e, hitPoint);
        }
    }

    private void DrawSceneOverlayLabel(string message)
    {
        Handles.BeginGUI();
        var rect = new Rect(10, 10, 380, 24);
        GUI.color = new Color(0, 0, 0, 0.5f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(rect, message, EditorStyles.whiteLabel);
        Handles.EndGUI();
    }

    private void HandlePencilMode(Event e, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (snapToExistingArray && TrySnapToExistingGrid(ref hitPoint))
            {
                // snapped
            }
            PlaceOne(hitPoint, hitNormal);
            hasLastPaintPosition = true;
            lastPaintPosition = hitPoint;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0)
        {
            if (snapToExistingArray) TrySnapToExistingGrid(ref hitPoint);
            if (TryPlaceWithSpacing(hitPoint, hitNormal))
            {
                e.Use();
            }
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            hasLastPaintPosition = false;
        }
    }

    private void HandleFillMode(Event e, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (e.type == EventType.MouseDown && e.button == 0 && !isDraggingArea)
        {
            isDraggingArea = true;
            dragStartWorld = previewStartWorld = hitPoint;
            dragCurrentWorld = previewCurrentWorld = hitPoint;
            fillPlaneOrigin = hitPoint;
            fillFixedY = hitPoint.y;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0 && isDraggingArea)
        {
            dragCurrentWorld = previewCurrentWorld = hitPoint;
            SceneView.RepaintAll();
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && isDraggingArea)
        {
            isDraggingArea = false;
            if (snapToExistingArray) SnapRectToExistingGrid(ref dragStartWorld, ref dragCurrentWorld);
            PerformAreaFill(dragStartWorld, dragCurrentWorld, dragPlaneNormal);
            e.Use();
        }

        if (isDraggingArea)
        {
            DrawDragRectGizmos(previewStartWorld, previewCurrentWorld, dragPlaneNormal);
        }
    }

    private bool TryRaycast(Ray ray, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        // Prefer hitting non-painted surfaces to avoid snapping onto just placed instances
        var hits = Physics.RaycastAll(ray, 10000f, placementMask, QueryTriggerInteraction.Ignore);
        float bestDist = float.PositiveInfinity;
        bool found = false;
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;
            if (h.distance >= bestDist) continue;
            var marker = h.collider.GetComponentInParent<LevelPaintMarker>();
            if (marker != null) continue; // skip our painted instances
            bestDist = h.distance;
            hitPoint = h.point;
            hitNormal = h.normal;
            found = true;
        }
        if (found) return true;

        // Fallback: accept nearest even if it's a painted item
        if (hits.Length > 0)
        {
            int bestIdx = 0;
            for (int i = 1; i < hits.Length; i++)
            {
                if (hits[i].distance < hits[bestIdx].distance) bestIdx = i;
            }
            hitPoint = hits[bestIdx].point;
            hitNormal = hits[bestIdx].normal;
            return true;
        }

        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float distance))
        {
            hitPoint = ray.origin + ray.direction * distance;
            hitNormal = Vector3.up;
            return true;
        }

        return false;
    }

    private void DrawCursorFallback(Ray ray)
    {
        Handles.color = Color.gray;
        Handles.DrawDottedLine(ray.origin, ray.origin + ray.direction * 10f, 2f);
    }

    private void EraseAt(Vector3 worldPoint)
    {
        var nearest = FindNearestMarker(worldPoint, out float dist);
        if (nearest != null)
        {
            Undo.DestroyObjectImmediate(nearest.gameObject);
        }
    }

    private void PickAt(Vector3 worldPoint)
    {
        var nearest = FindNearestMarker(worldPoint, out float dist);
        if (nearest != null && nearest.prefab != null)
        {
            int idx = prefabPalette.FindIndex(p => p == nearest.prefab);
            if (idx >= 0)
            {
                selectedPrefabIndex = idx;
            }
        }
    }

    private LevelPaintMarker FindNearestMarker(Vector3 worldPoint, out float minDist)
    {
        var markers = GameObject.FindObjectsOfType<LevelPaintMarker>();
        LevelPaintMarker nearest = null;
        minDist = float.PositiveInfinity;
        foreach (var m in markers)
        {
            if (m == null) continue;
            float d = Vector3.SqrMagnitude(m.transform.position - worldPoint);
            if (d < minDist)
            {
                minDist = d;
                nearest = m;
            }
        }
        minDist = Mathf.Sqrt(minDist);
        return nearest;
    }

    	private void ReplaceExistingAt(Vector3 worldPoint)
	{
		var markers = GameObject.FindObjectsOfType<LevelPaintMarker>();
		if (markers == null || markers.Length == 0) return;

		Vector3 planeNormal = dragPlaneNormal.sqrMagnitude > 0.0001f ? dragPlaneNormal.normalized : Vector3.up;
		Vector3 axisX = GetPreferredLateralAxis(planeNormal);
		Vector3 axisZ = Vector3.Cross(planeNormal, axisX).normalized;

		Vector3 selSize = GetSelectedPrefabSize();
		float cellX = Mathf.Max(0.01f, Mathf.Abs(selSize.x));
		float cellZ = Mathf.Max(0.01f, Mathf.Abs(selSize.z));
		float tolX = cellX * 0.3f; // Tighter tolerance for more precise replacement
		float tolZ = cellZ * 0.3f;

		LevelPaintMarker best = null;
		float bestSqr = float.PositiveInfinity;
		foreach (var m in markers)
		{
			if (m == null || !m.gameObject.activeInHierarchy) continue;
			if (m.paletteAsset != paletteAsset) continue;
			
			// compare by intended centers, not transforms
			Vector3 center = m.worldCenter != Vector3.zero ? m.worldCenter : m.transform.position;
			Vector3 toM = center - worldPoint;
			float dx = Vector3.Dot(toM, axisX);
			float dz = Vector3.Dot(toM, axisZ);
			
			if (Mathf.Abs(dx) <= tolX && Mathf.Abs(dz) <= tolZ)
			{
				float sqr = dx * dx + dz * dz;
				if (sqr < bestSqr)
				{
					bestSqr = sqr;
					best = m;
				}
			}
		}

		if (best != null)
		{
			Debug.Log($"[LevelPainter] Replacing object at {best.transform.position} (worldCenter: {best.worldCenter}) with new object at {worldPoint}");
			Undo.DestroyObjectImmediate(best.gameObject);
		}
		else
		{
			Debug.Log($"[LevelPainter] No object found to replace at {worldPoint} (tolerance: {tolX}x{tolZ})");
		}
	}

    private bool HasConflictAt(Vector3 worldPoint)
    {
        // In replace mode, there should be no conflicts since we clear the area
        if (conflictMode == ConflictMode.Replace) return false;
        
        var markers = GameObject.FindObjectsOfType<LevelPaintMarker>();
        if (markers == null || markers.Length == 0) return false;

        Vector3 planeNormal = dragPlaneNormal.sqrMagnitude > 0.0001f ? dragPlaneNormal.normalized : Vector3.up;
        Vector3 axisX = GetPreferredLateralAxis(planeNormal);
        Vector3 axisZ = Vector3.Cross(planeNormal, axisX).normalized;

        Vector3 selSize = GetSelectedPrefabSize();
        float cellX = Mathf.Max(0.01f, Mathf.Abs(selSize.x));
        float cellZ = Mathf.Max(0.01f, Mathf.Abs(selSize.z));
        float tolX = cellX * 0.5f;
        float tolZ = cellZ * 0.5f;

        foreach (var m in markers)
        {
            if (m == null || !m.gameObject.activeInHierarchy) continue;
            // Only consider markers from the same palette asset
            if (m.paletteAsset != paletteAsset) continue;
            Vector3 center = m.worldCenter != Vector3.zero ? m.worldCenter : m.transform.position;
            Vector3 toM = center - worldPoint;
            float dx = Vector3.Dot(toM, axisX);
            float dz = Vector3.Dot(toM, axisZ);
            if (Mathf.Abs(dx) <= tolX && Mathf.Abs(dz) <= tolZ)
            {
                return true;
            }
        }
        return false;
    }

    	private bool TrySnapToExistingGrid(ref Vector3 worldPoint)
	{
		var nearest = FindNearestMarker(worldPoint, out float dist);
		if (nearest == null) return false;
		
		Vector3 axisX = GetPreferredLateralAxis(dragPlaneNormal);
		Vector3 axisZ = Vector3.Cross(dragPlaneNormal, axisX).normalized;
		Vector3 origin = nearest.worldCenter != Vector3.zero ? nearest.worldCenter : nearest.transform.position;
		Vector3 size = nearest.size != Vector3.zero ? nearest.size : EstimateObjectBoundsSize(nearest.gameObject);
		
		// Use the actual spacing from the nearest object
		float stepX = Mathf.Max(0.1f, Mathf.Abs(size.x) + spacing);
		float stepZ = Mathf.Max(0.1f, Mathf.Abs(size.z) + spacing);
		
		float dx = Vector3.Dot(worldPoint - origin, axisX);
		float dz = Vector3.Dot(worldPoint - origin, axisZ);
		dx = Mathf.Round(dx / stepX) * stepX;
		dz = Mathf.Round(dz / stepZ) * stepZ;
		worldPoint = origin + axisX * dx + axisZ * dz;
		
		if (Vector3.Dot(dragPlaneNormal.normalized, Vector3.up) > 0.9f)
		{
			worldPoint.y = fillFixedY;
		}
		
		Debug.Log($"[LevelPainter] Snapped to grid: origin={origin}, stepX={stepX}, stepZ={stepZ}, result={worldPoint}");
		return true;
	}

    private void SnapRectToExistingGrid(ref Vector3 start, ref Vector3 end)
    {
        TrySnapToExistingGrid(ref start);
        TrySnapToExistingGrid(ref end);
    }

    private void DrawBrushGizmos(Vector3 center, Vector3 normal)
    {
        Handles.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Handles.DrawSolidDisc(center, normal, brushRadius * 0.02f);
        Handles.color = new Color(0.2f, 0.8f, 1f, 1f);
        Handles.DrawWireDisc(center, normal, brushRadius);
    }

    private void DrawDragRectGizmos(Vector3 start, Vector3 end, Vector3 planeNormal)
    {
        Vector3 right = Vector3.Cross(planeNormal, Vector3.up);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(planeNormal, Vector3.right);
        right.Normalize();
        Vector3 forward = Vector3.Cross(planeNormal, right).normalized;

        Vector3 local = WorldToLocalRect(end, start, right, forward);
        Vector3 p0 = start + right * local.x + forward * local.y;
        Vector3 p1 = start + right * local.x + forward * 0;
        Vector3 p2 = start + right * 0 + forward * local.y;
        Vector3 p3 = start;

        Handles.color = new Color(0.1f, 1f, 0.3f, 0.2f);
        Handles.DrawAAConvexPolygon(p0, p1, p3, p2);
        Handles.color = new Color(0.1f, 1f, 0.3f, 1f);
        Handles.DrawLine(p0, p1, SceneHandleSize);
        Handles.DrawLine(p1, p3, SceneHandleSize);
        Handles.DrawLine(p3, p2, SceneHandleSize);
        Handles.DrawLine(p2, p0, SceneHandleSize);
    }

    private static Vector3 WorldToLocalRect(Vector3 worldEnd, Vector3 worldStart, Vector3 right, Vector3 forward)
    {
        Vector3 delta = worldEnd - worldStart;
        float x = Vector3.Dot(delta, right);
        float y = Vector3.Dot(delta, forward);
        return new Vector3(x, y, 0f);
    }

    private void PlaceOne(Vector3 position, Vector3 normal)
    {
        // Conflict policy
        if (HasConflictAt(position))
        {
            if (conflictMode == ConflictMode.Stack)
            {
                return; // do not place over existing
            }
            else if (conflictMode == ConflictMode.Replace)
            {
                ReplaceExistingAt(position);
            }
        }

        var go = InstantiatePrefabAt(position, normal);
        if (go == null) return;

		EnsureCollidersForInstance(go);

        if (avoidOverlap)
        {
            PushOutToAvoidOverlap(go);
        }
    }

    private bool TryPlaceWithSpacing(Vector3 position, Vector3 normal)
    {
        float minStep = Mathf.Max(0.05f, spacing);
        if (!hasLastPaintPosition || Vector3.Distance(lastPaintPosition, position) >= minStep)
        {
            if (conflictMode == ConflictMode.Replace) 
            {
                ReplaceExistingAt(position);
                // In replace mode, don't call PlaceOne to avoid HasConflictAt check
                var go = InstantiatePrefabAt(position, normal);
                if (go != null)
                {
                    EnsureCollidersForInstance(go);
                    if (avoidOverlap)
                    {
                        PushOutToAvoidOverlap(go);
                    }
                }
            }
            else
            {
                PlaceOne(position, normal);
            }
            lastPaintPosition = position;
            hasLastPaintPosition = true;
            return true;
        }
        return false;
    }

    	private void PerformAreaFill(Vector3 start, Vector3 end, Vector3 planeNormal)
	{
		Vector3 right = Vector3.Cross(planeNormal, Vector3.up);
		if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(planeNormal, Vector3.right);
		right.Normalize();
		Vector3 forward = Vector3.Cross(planeNormal, right).normalized;

		// Enhanced snapping: find existing grid pattern and align to it
		Vector3 snappedStart = start;
		Vector3 snappedEnd = end;
		Vector2 existingGridStep = Vector2.zero;
		Vector3 existingGridOrigin = Vector3.zero;
		
		if (snapToExistingArray)
		{
			// Find existing grid pattern in the area
			if (FindExistingGridPattern(start, end, planeNormal, out existingGridStep, out existingGridOrigin))
			{
				Debug.Log($"[LevelPainter] Found existing grid: stepX={existingGridStep.x}, stepZ={existingGridStep.y}, origin={existingGridOrigin}");
				// Snap to the existing grid pattern
				SnapRectToExistingGridPattern(ref snappedStart, ref snappedEnd, existingGridStep, existingGridOrigin, right, forward);
			}
			else
			{
				Debug.Log("[LevelPainter] No existing grid pattern found, using fallback");
				// Fallback to old snapping method
				SnapRectToExistingGrid(ref snappedStart, ref snappedEnd);
			}
		}

		// In replace mode, save exact positions AFTER finding grid pattern but BEFORE clearing
		List<Vector3> savedPositions = new List<Vector3>();
		if (conflictMode == ConflictMode.Replace)
		{
			Debug.Log($"[LevelPainter] Replace mode - start: {start}, end: {end}");
			Debug.Log($"[LevelPainter] Replace mode - right: {right}, forward: {forward}");
			SaveExactPositionsBeforeReplace(start, end, planeNormal, right, forward, out savedPositions);
			// Now clear the area
			ClearAreaForReplace(start, end, planeNormal, right, forward);
		}

		Vector3 localSize = WorldToLocalRect(snappedEnd, snappedStart, right, forward);
		float width = Mathf.Abs(localSize.x);
		float height = Mathf.Abs(localSize.y);
		Vector3 baseCorner = snappedStart;
		if (localSize.x < 0) baseCorner += right * localSize.x;
		if (localSize.y < 0) baseCorner += forward * localSize.y;

		// In replace mode, just place at saved positions (simple approach)
		if (conflictMode == ConflictMode.Replace)
		{
			Debug.Log($"[LevelPainter] Replace mode: {savedPositions.Count} saved positions");
			
			int replaceUndoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("LevelPainter Fill Replace");
			
			if (savedPositions.Count > 0)
			{
				// Place objects at exact saved positions only
				foreach (Vector3 savedPos in savedPositions)
				{
					Vector3 pos = savedPos;
					Vector3 norm = planeNormal;
					
					// Maintain Y coordinate if on horizontal surface
					if (Vector3.Dot(planeNormal.normalized, Vector3.up) > 0.9f)
					{
						pos.y = fillFixedY;
					}
					
					var go = InstantiatePrefabAt(pos, norm);
					if (go == null) continue;
					EnsureCollidersForInstance(go);
				}
				Debug.Log($"[LevelPainter] Successfully placed {savedPositions.Count} objects at saved positions");
			}
			
			Undo.CollapseUndoOperations(replaceUndoGroup);
			return; // Exit early, we've handled replace mode
		}
		
		// Only proceed with normal fill logic if NOT in replace mode
		if (conflictMode != ConflictMode.Replace)
		{
			// Use existing grid step if available, otherwise use prefab size
			float stepX, stepZ;
			if (existingGridStep.x > 0.01f && existingGridStep.y > 0.01f)
			{
				// Use existing grid pattern (for stack mode)
				stepX = existingGridStep.x;
				stepZ = existingGridStep.y;
				Debug.Log($"[LevelPainter] Using existing grid pattern: stepX={stepX}, stepZ={stepZ}");
			}
			else
			{
				// Use user-defined prefab size for stable grid steps
				Vector3 selSizeForFill = GetSelectedPrefabSize();
				stepX = Mathf.Max(0.1f, Mathf.Max(spacing, Mathf.Abs(selSizeForFill.x)));
				stepZ = Mathf.Max(0.1f, Mathf.Max(spacing, Mathf.Abs(selSizeForFill.z)));
				Debug.Log($"[LevelPainter] Using prefab-based grid: stepX={stepX}, stepZ={stepZ}");
			}

			int undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("LevelPainter Fill");

			// Calculate grid counts based on the actual area size
			int countX = Mathf.Max(0, Mathf.FloorToInt(width / stepX));
			int countZ = Mathf.Max(0, Mathf.FloorToInt(height / stepZ));
			
			Debug.Log($"[LevelPainter] Fill area: {countX}x{countZ} cells, size: {width}x{height}");
			
			for (int ix = 0; ix < countX; ix++)
			{
				for (int iz = 0; iz < countZ; iz++)
				{
					float offX = ix * stepX + stepX * 0.5f;
					float offZ = iz * stepZ + stepZ * 0.5f;
					Vector3 target = baseCorner + right * offX + forward * offZ;
					Vector3 pos;
					Vector3 norm;
					
					// Project onto the painting plane to avoid edge raycast artifacts
					if (Vector3.Dot(planeNormal.normalized, Vector3.up) > 0.9f)
					{
						pos = target;
						pos.y = fillFixedY;
						norm = Vector3.up;
				}
					else
					{
						pos = ClosestPointOnPlane(target, fillPlaneOrigin, planeNormal);
						norm = planeNormal;
					}
					
					// Handle conflicts based on conflict mode
					if (conflictMode == ConflictMode.Stack && HasConflictAt(pos))
					{
						continue; // Skip this position in stack mode
					}
					
					var go = InstantiatePrefabAt(pos, norm);
					if (go == null) continue;
					EnsureCollidersForInstance(go);
					if (avoidOverlap)
					{
						PushOutToAvoidOverlap(go);
					}
				}
			}

			Undo.CollapseUndoOperations(undoGroup);
		}
	}

	private bool FindExistingGridPattern(Vector3 start, Vector3 end, Vector3 planeNormal, out Vector2 gridStep, out Vector3 gridOrigin)
	{
		gridStep = Vector2.zero;
		gridOrigin = Vector3.zero;
		
		// Find all existing markers in the area
		var markers = GameObject.FindObjectsOfType<LevelPaintMarker>();
		if (markers.Length < 2) return false; // Need at least 2 points to determine a pattern
		
		Vector3 axisX = GetPreferredLateralAxis(planeNormal);
		Vector3 axisZ = Vector3.Cross(planeNormal, axisX).normalized;
		
		// Find the most common spacing patterns
		var xSpacings = new List<float>();
		var zSpacings = new List<float>();
		var origins = new List<Vector3>();
		
		foreach (var marker in markers)
		{
			if (marker == null || !marker.gameObject.activeInHierarchy) continue;
			if (marker.paletteAsset != paletteAsset) continue;
			
			Vector3 center = marker.worldCenter != Vector3.zero ? marker.worldCenter : marker.transform.position;
			
			// Check if this marker is within our fill area
			if (IsPointInRect(center, start, end, axisX, axisZ))
			{
				origins.Add(center);
				
				// Find nearest neighbors to calculate spacing
				foreach (var other in markers)
				{
					if (other == null || other == marker || !other.gameObject.activeInHierarchy) continue;
					if (other.paletteAsset != paletteAsset) continue;
					
					Vector3 otherCenter = other.worldCenter != Vector3.zero ? other.worldCenter : other.transform.position;
					
					float dx = Vector3.Dot(otherCenter - center, axisX);
					float dz = Vector3.Dot(otherCenter - center, axisZ);
					
					if (Mathf.Abs(dx) > 0.01f && Mathf.Abs(dx) < 10f) xSpacings.Add(Mathf.Abs(dx));
					if (Mathf.Abs(dz) > 0.01f && Mathf.Abs(dz) < 10f) zSpacings.Add(Mathf.Abs(dz));
				}
			}
		}
		
		if (xSpacings.Count < 1 || zSpacings.Count < 1) return false;
		
		// Find the most common spacing values
		gridStep.x = FindMostCommonSpacing(xSpacings);
		gridStep.y = FindMostCommonSpacing(zSpacings);
		
		// Find the grid origin (leftmost, bottommost point)
		if (origins.Count > 0)
		{
			gridOrigin = origins[0];
			foreach (var origin in origins)
			{
				float dx = Vector3.Dot(origin - gridOrigin, axisX);
				float dz = Vector3.Dot(origin - gridOrigin, axisZ);
				if (dx < 0) gridOrigin += axisX * dx;
				if (dz < 0) gridOrigin += axisZ * dz;
			}
		}
		
		return gridStep.x > 0.01f && gridStep.y > 0.01f;
	}

	private float FindMostCommonSpacing(List<float> spacings)
	{
		if (spacings.Count == 0) return 0f;
		
		// Group spacings by tolerance
		var groups = new Dictionary<int, List<float>>();
		float tolerance = 0.1f;
		
		foreach (float spacing in spacings)
		{
			int groupKey = Mathf.RoundToInt(spacing / tolerance);
			if (!groups.ContainsKey(groupKey)) groups[groupKey] = new List<float>();
			groups[groupKey].Add(spacing);
		}
		
		// Find the group with most members
		int maxCount = 0;
		float bestSpacing = spacings[0];
		
		foreach (var group in groups)
		{
			if (group.Value.Count > maxCount)
			{
				maxCount = group.Value.Count;
				bestSpacing = group.Value[0];
			}
		}
		
		return bestSpacing;
	}

	private bool IsPointInRect(Vector3 point, Vector3 rectStart, Vector3 rectEnd, Vector3 axisX, Vector3 axisZ)
	{
		// Null safety checks
		if (axisX.sqrMagnitude < 0.001f || axisZ.sqrMagnitude < 0.001f)
		{
			Debug.LogWarning("[LevelPainter] Invalid axes in IsPointInRect");
			return false;
		}
		
		Vector3 delta = point - rectStart;
		float dx = Vector3.Dot(delta, axisX);
		float dz = Vector3.Dot(delta, axisZ);
		
		Vector3 rectSize = rectEnd - rectStart;
		float rectWidth = Mathf.Abs(Vector3.Dot(rectSize, axisX));
		float rectHeight = Mathf.Abs(Vector3.Dot(rectSize, axisZ));
		
		return dx >= 0 && dx <= rectWidth && dz >= 0 && dz <= rectHeight;
	}

	private void SnapRectToExistingGridPattern(ref Vector3 start, ref Vector3 end, Vector2 gridStep, Vector3 gridOrigin, Vector3 axisX, Vector3 axisZ)
	{
		// Snap start point to grid
		Vector3 startDelta = start - gridOrigin;
		float startDX = Vector3.Dot(startDelta, axisX);
		float startDZ = Vector3.Dot(startDelta, axisZ);
		
		startDX = Mathf.Round(startDX / gridStep.x) * gridStep.x;
		startDZ = Mathf.Round(startDZ / gridStep.y) * gridStep.y;
		
		start = gridOrigin + axisX * startDX + axisZ * startDZ;
		
		// Snap end point to grid
		Vector3 endDelta = end - gridOrigin;
		float endDX = Vector3.Dot(endDelta, axisX);
		float endDZ = Vector3.Dot(endDelta, axisZ);
		
		endDX = Mathf.Round(endDX / gridStep.x) * gridStep.x;
		endDZ = Mathf.Round(endDZ / gridStep.y) * gridStep.y;
		
		end = gridOrigin + axisX * endDX + axisZ * endDZ;
		
		// Maintain Y coordinate
		if (Vector3.Dot(dragPlaneNormal.normalized, Vector3.up) > 0.9f)
		{
			start.y = fillFixedY;
			end.y = fillFixedY;
		}
	}

	private bool FindGridPatternOutsideArea(Vector3 start, Vector3 end, Vector3 planeNormal, out Vector2 gridStep, out Vector3 gridOrigin)
	{
		gridStep = Vector2.zero;
		gridOrigin = Vector3.zero;
		
		// Find all existing markers outside the fill area
		var markers = GameObject.FindObjectsOfType<LevelPaintMarker>();
		if (markers.Length < 2) return false;
		
		Vector3 axisX = GetPreferredLateralAxis(planeNormal);
		Vector3 axisZ = Vector3.Cross(planeNormal, axisX).normalized;
		
		var xSpacings = new List<float>();
		var zSpacings = new List<float>();
		var origins = new List<Vector3>();
		
		foreach (var marker in markers)
		{
			if (marker == null || !marker.gameObject.activeInHierarchy) continue;
			if (marker.paletteAsset != paletteAsset) continue;
			
			Vector3 center = marker.worldCenter != Vector3.zero ? marker.worldCenter : marker.transform.position;
			
			// Check if this marker is OUTSIDE our fill area
			if (!IsPointInRect(center, start, end, axisX, axisZ))
			{
				origins.Add(center);
				
				// Find nearest neighbors to calculate spacing
				foreach (var other in markers)
				{
					if (other == null || other == marker || !other.gameObject.activeInHierarchy) continue;
					if (other.paletteAsset != paletteAsset) continue;
					
					Vector3 otherCenter = other.worldCenter != Vector3.zero ? other.worldCenter : other.transform.position;
					
					float dx = Vector3.Dot(otherCenter - center, axisX);
					float dz = Vector3.Dot(otherCenter - center, axisZ);
					
					if (Mathf.Abs(dx) > 0.01f && Mathf.Abs(dx) < 10f) xSpacings.Add(Mathf.Abs(dx));
					if (Mathf.Abs(dz) > 0.01f && Mathf.Abs(dz) < 10f) zSpacings.Add(Mathf.Abs(dz));
				}
			}
		}
		
		if (xSpacings.Count < 1 || zSpacings.Count < 1) return false;
		
		// Find the most common spacing values
		gridStep.x = FindMostCommonSpacing(xSpacings);
		gridStep.y = FindMostCommonSpacing(zSpacings);
		
		// Find the grid origin (leftmost, bottommost point)
		if (origins.Count > 0)
		{
			gridOrigin = origins[0];
			foreach (var origin in origins)
			{
				float dx = Vector3.Dot(origin - gridOrigin, axisX);
				float dz = Vector3.Dot(origin - gridOrigin, axisZ);
				if (dx < 0) gridOrigin += axisX * dx;
				if (dz < 0) gridOrigin += axisZ * dz;
			}
		}
		
		return gridStep.x > 0.01f && gridStep.y > 0.01f;
	}

	private bool TryAdjustToExistingGrid(Vector3 targetPos, Vector2 gridStep, Vector3 gridOrigin, Vector3 axisX, Vector3 axisZ, out Vector3 adjustedPos)
	{
		adjustedPos = targetPos;
		
		// If we don't have a valid grid pattern, can't adjust
		if (gridStep.x <= 0.01f || gridStep.y <= 0.01f) return false;
		
		// Find the nearest existing object to this position
		var markers = GameObject.FindObjectsOfType<LevelPaintMarker>();
		LevelPaintMarker nearest = null;
		float minDist = float.PositiveInfinity;
		
		foreach (var marker in markers)
		{
			if (marker == null || !marker.gameObject.activeInHierarchy) continue;
			if (marker.paletteAsset != paletteAsset) continue;
			
			Vector3 center = marker.worldCenter != Vector3.zero ? marker.worldCenter : marker.transform.position;
			float dist = Vector3.Distance(targetPos, center);
			if (dist < minDist)
			{
				minDist = dist;
				nearest = marker;
			}
		}
		
		if (nearest == null) return false;
		
		// Use the nearest object as a reference point
		Vector3 referencePos = nearest.worldCenter != Vector3.zero ? nearest.worldCenter : nearest.transform.position;
		
		// Calculate the grid offset from the reference
		Vector3 delta = targetPos - referencePos;
		float dx = Vector3.Dot(delta, axisX);
		float dz = Vector3.Dot(delta, axisZ);
		
		// Round to the nearest grid position
		float gridDX = Mathf.Round(dx / gridStep.x) * gridStep.x;
		float gridDZ = Mathf.Round(dz / gridStep.y) * gridStep.y;
		
		// Calculate the adjusted position
		adjustedPos = referencePos + axisX * gridDX + axisZ * gridDZ;
		
		// Maintain the Y coordinate
		adjustedPos.y = targetPos.y;
		
		Debug.Log($"[LevelPainter] Grid adjustment: target={targetPos}, reference={referencePos}, delta=({dx},{dz}), gridDelta=({gridDX},{gridDZ}), adjusted={adjustedPos}");
		
		return true;
	}

	private void SaveExactPositionsBeforeReplace(Vector3 start, Vector3 end, Vector3 planeNormal, Vector3 axisX, Vector3 axisZ, out List<Vector3> positions)
	{
		positions = new List<Vector3>();
		
		Debug.Log($"[LevelPainter] Saving exact positions before replace: {start} to {end}");
		
		var markers = GameObject.FindObjectsOfType<LevelPaintMarker>();
		
		// Collect all marker positions in the area (same palette only)
		foreach (var marker in markers)
		{
			if (marker == null || !marker.gameObject.activeInHierarchy) continue;
			if (marker.paletteAsset != paletteAsset) continue;
			
			Vector3 center = marker.worldCenter != Vector3.zero ? marker.worldCenter : marker.transform.position;
			
			// Check if this marker is within our fill area
			if (IsPointInRect(center, start, end, axisX, axisZ))
			{
				positions.Add(center);
			}
		}
		
		// Sort positions by X and Z coordinates for consistent ordering
		positions.Sort((a, b) => {
			float ax = Vector3.Dot(a, axisX);
			float az = Vector3.Dot(a, axisZ);
			float bx = Vector3.Dot(b, axisX);
			float bz = Vector3.Dot(b, axisZ);
			
			if (Mathf.Abs(ax - bx) < 0.01f)
			{
				return az.CompareTo(bz); // Sort by Z if X is similar
			}
			return ax.CompareTo(bx); // Sort by X first
		});
		
		Debug.Log($"[LevelPainter] Saved {positions.Count} exact positions for replace");
	}



	private void ClearAreaForReplace(Vector3 start, Vector3 end, Vector3 planeNormal, Vector3 axisX, Vector3 axisZ)
	{
		Debug.Log($"[LevelPainter] Clearing area for replace: {start} to {end}");
		
		var markers = GameObject.FindObjectsOfType<LevelPaintMarker>();
		var toRemove = new List<LevelPaintMarker>();
		
		foreach (var marker in markers)
		{
			if (marker == null || !marker.gameObject.activeInHierarchy) continue;
			if (marker.paletteAsset != paletteAsset) continue;
			
			Vector3 center = marker.worldCenter != Vector3.zero ? marker.worldCenter : marker.transform.position;
			
			// Check if this marker is within our fill area
			if (IsPointInRect(center, start, end, axisX, axisZ))
			{
				toRemove.Add(marker);
			}
		}
		
		Debug.Log($"[LevelPainter] Found {toRemove.Count} objects to remove in replace mode");
		
		// Remove all objects in the area
		foreach (var marker in toRemove)
		{
			if (marker != null && marker.gameObject != null)
			{
				Undo.DestroyObjectImmediate(marker.gameObject);
			}
		}
		
		// Force a small delay to ensure objects are properly removed
		if (Selection.activeGameObject != null)
		{
			EditorUtility.SetDirty(Selection.activeGameObject);
		}
	}

    private Vector3 ProjectPointToSurface(Vector3 point, Vector3 hintNormal, out Vector3 hitNormal)
    {
        Ray ray = new Ray(point + hintNormal * 10f, -hintNormal);
        var hits = Physics.RaycastAll(ray, 100f, placementMask, QueryTriggerInteraction.Ignore);
        float bestDist = float.PositiveInfinity;
        hitNormal = hintNormal;
        Vector3 bestPoint = point;
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;
            var marker = h.collider.GetComponentInParent<LevelPaintMarker>();
            if (marker != null) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                bestPoint = h.point;
                hitNormal = h.normal;
            }
        }
        if (bestDist < float.PositiveInfinity)
        {
            return bestPoint;
        }
        hitNormal = hintNormal;
        return point;
    }

private GameObject InstantiatePrefabAt(Vector3 targetCenterPosition, Vector3 normal)
    {
        var prefab = GetSelectedPrefab();
		if (prefab == null) 
		{
			Debug.LogWarning("[LevelPainter] GetSelectedPrefab returned null");
			return null;
		}
		Debug.Log($"[LevelPainter] Instantiating prefab: {prefab.name} at {targetCenterPosition}");
		GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (go == null) 
		{
			Debug.LogWarning("[LevelPainter] PrefabUtility.InstantiatePrefab returned null");
			return null;
		}

        Undo.RegisterCreatedObjectUndo(go, "LevelPainter Place");

        // Respect prefab's original rotation; align by rotating prefab's local up to the surface normal
        Quaternion prefabRotation = go.transform.rotation;
        Quaternion rotation = prefabRotation;
        if (alignToSurfaceNormal)
        {
            Vector3 prefabLocalUpInWorld = prefabRotation * Vector3.up;
            Quaternion align = Quaternion.FromToRotation(prefabLocalUpInWorld, normal);
            rotation = align * prefabRotation;
        }
        if (randomizeYaw)
        {
            float yaw = Random.Range(0f, randomYawMaxDegrees);
            Vector3 yawAxis = alignToSurfaceNormal ? normal : (rotation * Vector3.up);
            rotation = Quaternion.AngleAxis(yaw, yawAxis) * rotation;
        }
    go.transform.rotation = rotation;

    // Place so the prefab's bottom (along its local up) sits on the painting plane point
    Bounds localBounds = ComputePrefabLocalBounds(prefab);
    Vector3 localCenter = localBounds.center;
    Vector3 localBottom = new Vector3(localCenter.x, localCenter.y - localBounds.extents.y, localCenter.z);
    Vector3 worldBottomOffset = rotation * localBottom;
    go.transform.position = targetCenterPosition - worldBottomOffset;

    // Final snap to actual surface by raycasting along the surface normal
    SnapBottomToSurface(go, localBottom, targetCenterPosition, normal);
        // Tag instance to identify in raycast/overlap and store size
        var marker = Undo.AddComponent<LevelPaintMarker>(go);
        marker.size = GetSelectedPrefabSize();
    marker.prefab = prefab;
    marker.paletteIndex = selectedPrefabIndex;
    marker.worldCenter = targetCenterPosition;
        marker.paletteAsset = paletteAsset;
        EditorUtility.SetDirty(go);
        return go;
    }

	private void PushOutToAvoidOverlap(GameObject instance)
	{
		Vector3 size = GetSelectedPrefabSize();
		if (size == Vector3.zero) return;

		Vector3 planeNormal = dragPlaneNormal.sqrMagnitude > 0.0001f ? dragPlaneNormal.normalized : Vector3.up;
		Vector3 axisX = GetPreferredLateralAxis(planeNormal);
		Vector3 axisZ = Vector3.Cross(planeNormal, axisX).normalized;

		for (int iter = 0; iter < maxPushIterations; iter++)
		{
			Vector3 accumulated = Vector3.zero;
			var others = GameObject.FindObjectsOfType<LevelPaintMarker>();
			foreach (var other in others)
			{
				if (other == null) continue;
				var t = other.transform;
				if (t == instance.transform) continue;
				if (!other.gameObject.activeInHierarchy) continue;

				Vector3 otherSize = other.size != Vector3.zero ? other.size : EstimateObjectBoundsSize(other.gameObject);
				
				// Enhanced overlap detection: try AABB first, then collider if available
				bool hasOverlap = false;
				Vector3 pushVector = Vector3.zero;

				// 1. AABB-based overlap detection (fast, reliable)
				if (IsOverlappingAABB(instance.transform.position, size, t.position, otherSize, planeNormal))
				{
					hasOverlap = true;
					pushVector = CalculateAABBPushVector(instance.transform.position, size, t.position, otherSize, axisX, axisZ);
				}

				// 2. Collider-based overlap detection (more accurate, but only if colliders exist)
				if (!hasOverlap && HasColliderOverlap(instance, other.gameObject))
				{
					hasOverlap = true;
					pushVector = CalculateColliderPushVector(instance, other.gameObject, planeNormal);
				}

				if (hasOverlap && pushVector.sqrMagnitude > 0.001f)
				{
					accumulated += pushVector;
				}
			}

			if (accumulated.sqrMagnitude <= 1e-6f) break;
			if (accumulated.magnitude > maxPushDistance) accumulated = accumulated.normalized * maxPushDistance;
			
			instance.transform.position += accumulated;
		}

		EditorUtility.SetDirty(instance);
	}

	private Vector3 CalculateAABBPushVector(Vector3 aPos, Vector3 aSize, Vector3 bPos, Vector3 bSize, Vector3 axisX, Vector3 axisZ)
	{
		float aHalfX = aSize.x * 0.5f;
		float aHalfZ = aSize.z * 0.5f;
		float bHalfX = bSize.x * 0.5f;
		float bHalfZ = bSize.z * 0.5f;

		float aCenterX = Vector3.Dot(aPos, axisX);
		float bCenterX = Vector3.Dot(bPos, axisX);
		float aCenterZ = Vector3.Dot(aPos, axisZ);
		float bCenterZ = Vector3.Dot(bPos, axisZ);

		float overlapX = (aHalfX + bHalfX) - Mathf.Abs(aCenterX - bCenterX);
		float overlapZ = (aHalfZ + bHalfZ) - Mathf.Abs(aCenterZ - bCenterZ);

		if (overlapX <= 0f || overlapZ <= 0f) return Vector3.zero;

		// Choose the axis with smaller overlap for more natural separation
		if (overlapX < overlapZ)
		{
			float signX = Mathf.Sign(aCenterX - bCenterX);
			if (signX == 0f) signX = 1f;
			return axisX * (overlapX * signX * 1.05f); // Add 5% buffer
		}
		else
		{
			float signZ = Mathf.Sign(aCenterZ - bCenterZ);
			if (signZ == 0f) signZ = 1f;
			return axisZ * (overlapZ * signZ * 1.05f); // Add 5% buffer
		}
	}

	private bool HasColliderOverlap(GameObject a, GameObject b)
	{
		var collidersA = a.GetComponentsInChildren<Collider>();
		var collidersB = b.GetComponentsInChildren<Collider>();

		if (collidersA.Length == 0 || collidersB.Length == 0) return false;

		foreach (var colA in collidersA)
		{
			if (colA == null || !colA.enabled) continue;
			foreach (var colB in collidersB)
			{
				if (colB == null || !colB.enabled) continue;
				
				// Use Physics.ComputePenetration for accurate overlap detection
				if (Physics.ComputePenetration(colA, colA.transform.position, colA.transform.rotation,
					colB, colB.transform.position, colB.transform.rotation,
					out Vector3 direction, out float distance))
				{
					return true;
				}
			}
		}
		return false;
	}

	private Vector3 CalculateColliderPushVector(GameObject a, GameObject b, Vector3 planeNormal)
	{
		var collidersA = a.GetComponentsInChildren<Collider>();
		var collidersB = b.GetComponentsInChildren<Collider>();

		Vector3 totalPush = Vector3.zero;
		int overlapCount = 0;

		foreach (var colA in collidersA)
		{
			if (colA == null || !colA.enabled) continue;
			foreach (var colB in collidersB)
			{
				if (colB == null || !colB.enabled) continue;
				
				if (Physics.ComputePenetration(colA, colA.transform.position, colA.transform.rotation,
					colB, colB.transform.position, colB.transform.rotation,
					out Vector3 direction, out float distance))
				{
					// Project direction onto the painting plane
					Vector3 projectedDir = Vector3.ProjectOnPlane(direction, planeNormal).normalized;
					if (projectedDir.sqrMagnitude > 0.001f)
					{
						totalPush += projectedDir * distance * 1.1f; // Add 10% buffer
						overlapCount++;
					}
				}
			}
		}

		return overlapCount > 0 ? totalPush / overlapCount : Vector3.zero;
	}

    private Vector3 GetPreferredLateralAxis(Vector3 planeNormal)
    {
        Vector3 right = Vector3.Cross(planeNormal, Vector3.up);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(planeNormal, Vector3.right);
        return right.normalized;
    }

    private Bounds ComputePrefabLocalBounds(GameObject prefab)
    {
        bool hasAny = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);
        var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            var meshBounds = mf.sharedMesh.bounds;
            Matrix4x4 localToRoot = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Bounds transformed = TransformBounds(meshBounds, localToRoot);
            if (!hasAny)
            {
                result = transformed;
                hasAny = true;
            }
            else
            {
                result.Encapsulate(transformed.min);
                result.Encapsulate(transformed.max);
            }
        }

        if (!hasAny)
        {
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var b = r.bounds;
                Matrix4x4 worldToRoot = prefab.transform.worldToLocalMatrix;
                Bounds transformed = TransformBoundsWorld(b, worldToRoot);
                if (!hasAny)
                {
                    result = transformed;
                    hasAny = true;
                }
                else
                {
                    result.Encapsulate(transformed.min);
                    result.Encapsulate(transformed.max);
                }
            }
        }

        if (!hasAny)
        {
            result = new Bounds(Vector3.zero, Vector3.one);
        }
        return result;
    }

    // Compute width/height on the painting plane axes ignoring random yaw for consistent grid
    private Vector2 ComputeFootprintSizeOnPlane(GameObject prefab, Vector3 planeNormal)
    {
        if (prefab == null) return new Vector2(1f, 1f);
        Bounds local = ComputePrefabLocalBounds(prefab);
        Vector3 size = local.size;
        // Build an orientation that aligns up to plane normal and zero yaw relative to world right/forward of plane
        Vector3 axisX = GetPreferredLateralAxis(planeNormal);
        Vector3 axisZ = Vector3.Cross(planeNormal, axisX).normalized;
        // Use extents projected onto axes after considering prefab's default rotation only (no yaw/random)
        // Approximate: assume local axes map to world axes through this alignment
        Vector3 ex = size.x * 0.5f * axisX;
        Vector3 ez = size.z * 0.5f * axisZ;
        float width = (Mathf.Abs(ex.x) + Mathf.Abs(ex.y) + Mathf.Abs(ex.z)) * 2f; // L1 norm proxy
        float depth = (Mathf.Abs(ez.x) + Mathf.Abs(ez.y) + Mathf.Abs(ez.z)) * 2f;
        // Fallback to size.x/z if proxy fails
        if (width <= 0.0001f) width = Mathf.Abs(size.x);
        if (depth <= 0.0001f) depth = Mathf.Abs(size.z);
        return new Vector2(width, depth);
    }

    private static Bounds TransformBounds(Bounds b, Matrix4x4 matrix)
    {
        Vector3[] localCorners = new Vector3[8]
        {
            b.center + new Vector3( b.extents.x,  b.extents.y,  b.extents.z),
            b.center + new Vector3( b.extents.x,  b.extents.y, -b.extents.z),
            b.center + new Vector3( b.extents.x, -b.extents.y,  b.extents.z),
            b.center + new Vector3( b.extents.x, -b.extents.y, -b.extents.z),
            b.center + new Vector3(-b.extents.x,  b.extents.y,  b.extents.z),
            b.center + new Vector3(-b.extents.x,  b.extents.y, -b.extents.z),
            b.center + new Vector3(-b.extents.x, -b.extents.y,  b.extents.z),
            b.center + new Vector3(-b.extents.x, -b.extents.y, -b.extents.z)
        };
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            Vector3 world = matrix.MultiplyPoint3x4(localCorners[i]);
            min = Vector3.Min(min, world);
            max = Vector3.Max(max, world);
        }
        return new Bounds((min + max) * 0.5f, max - min);
    }

    private static Bounds TransformBoundsWorld(Bounds worldBounds, Matrix4x4 worldToLocal)
    {
        Vector3[] worldCorners = new Vector3[8]
        {
            worldBounds.center + new Vector3( worldBounds.extents.x,  worldBounds.extents.y,  worldBounds.extents.z),
            worldBounds.center + new Vector3( worldBounds.extents.x,  worldBounds.extents.y, -worldBounds.extents.z),
            worldBounds.center + new Vector3( worldBounds.extents.x, -worldBounds.extents.y,  worldBounds.extents.z),
            worldBounds.center + new Vector3( worldBounds.extents.x, -worldBounds.extents.y, -worldBounds.extents.z),
            worldBounds.center + new Vector3(-worldBounds.extents.x,  worldBounds.extents.y,  worldBounds.extents.z),
            worldBounds.center + new Vector3(-worldBounds.extents.x,  worldBounds.extents.y, -worldBounds.extents.z),
            worldBounds.center + new Vector3(-worldBounds.extents.x, -worldBounds.extents.y,  worldBounds.extents.z),
            worldBounds.center + new Vector3(-worldBounds.extents.x, -worldBounds.extents.y, -worldBounds.extents.z)
        };
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            Vector3 local = worldToLocal.MultiplyPoint3x4(worldCorners[i]);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }
        return new Bounds((min + max) * 0.5f, max - min);
    }

    private static Vector3 ClosestPointOnPlane(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
    {
        planeNormal = planeNormal.normalized;
        float distance = Vector3.Dot(planeNormal, point - planePoint);
        return point - planeNormal * distance;
    }

    private void SnapBottomToSurface(GameObject instance, Vector3 localBottom, Vector3 targetPointOnPlane, Vector3 planeNormal)
    {
        Vector3 castOrigin = targetPointOnPlane + planeNormal * 5f;
        var hits = Physics.RaycastAll(castOrigin, -planeNormal, 5000f, placementMask, QueryTriggerInteraction.Ignore);
        float bestDist = float.PositiveInfinity;
        Vector3 bestPoint = targetPointOnPlane;
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(instance.transform)) continue; // skip self
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                bestPoint = h.point;
            }
        }
        Vector3 bottomWorld = instance.transform.TransformPoint(localBottom);
        float along = Vector3.Dot(bottomWorld - bestPoint, planeNormal);
        if (Mathf.Abs(along) > 1e-4f)
        {
            instance.transform.position -= planeNormal * along;
        }
    }

	private void EnsureCollidersForInstance(GameObject instance)
	{
        if (!autoAddMeshCollider) return;

        var meshFilters = instance.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.GetComponent<Collider>() != null) continue;

            var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = autoColliderConvex;
            EditorUtility.SetDirty(mc);
        }
	}

	private GameObject GetSelectedPrefab()
	{
		if (prefabPalette == null || prefabPalette.Count == 0) return null;
		if (selectedPrefabIndex < 0 || selectedPrefabIndex >= prefabPalette.Count) return null;
		return prefabPalette[selectedPrefabIndex];
	}

	private void DrawPaletteSelectionGrid()
	{
		int count = prefabPalette != null ? prefabPalette.Count : 0;
		if (count == 0)
		{
			EditorGUILayout.HelpBox("Add prefabs to the palette to start painting.", MessageType.None);
			return;
		}

        int columnCount = Mathf.Max(1, paletteGridColumns);
        GUIContent[] contents = new GUIContent[count];
		for (int i = 0; i < count; i++)
		{
			Texture2D preview = null;
            string label = "Empty";
			var p = prefabPalette[i];
			if (p != null)
			{
				preview = AssetPreview.GetAssetPreview(p);
				if (preview == null)
				{
					preview = AssetPreview.GetMiniThumbnail(p);
				}
				label = p.name;
			}
            // Show name under preview (use text field in content; Unity shows it as tooltip by default).
            // We'll draw labels manually below grid for readability.
            contents[i] = new GUIContent(preview, label);
		}

        // Compute rows from available height and target cell size
        float cellH = Mathf.Clamp(paletteCellSize, 40f, 160f);
        int rowsVisible = Mathf.Max(1, Mathf.FloorToInt((paletteGridHeight - 40f) / cellH));
        int newIndex = GUILayout.SelectionGrid(selectedPrefabIndex, contents, columnCount, GUILayout.MinHeight(rowsVisible * cellH));
		if (newIndex != selectedPrefabIndex)
		{
			selectedPrefabIndex = newIndex;
		}

        // Draw names under the previews in a grid-aligned fashion with wrapping labels
        int rows = Mathf.CeilToInt((float)count / columnCount);
        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < columnCount; c++)
            {
                if (idx < count)
                {
                    string name = prefabPalette[idx] != null ? prefabPalette[idx].name : "Empty";
                    GUILayout.Label(name, EditorStyles.miniLabel, GUILayout.Height(14));
                    idx++;
                }
                else
                {
                    GUILayout.Label("");
                }
            }
            EditorGUILayout.EndHorizontal();
        }
	}

	private void DrawLargePreview(GameObject prefab)
	{
		Rect rect = GUILayoutUtility.GetRect(100, 120, GUILayout.ExpandWidth(true));
		GUI.Box(rect, GUIContent.none);
		if (prefab == null)
		{
			GUI.Label(rect, "No prefab selected", EditorStyles.centeredGreyMiniLabel);
			return;
		}
		Texture2D preview = AssetPreview.GetAssetPreview(prefab);
		if (preview == null)
		{
			preview = AssetPreview.GetMiniThumbnail(prefab);
		}
		if (preview != null)
		{
			GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
		}
		else
		{
			GUI.Label(rect, prefab.name, EditorStyles.centeredGreyMiniLabel);
		}
	}

    private void CreateNewPaletteAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create Palette", "LevelPrefabPalette", "asset", "Select save location for the palette asset");
        if (string.IsNullOrEmpty(path)) return;
        var asset = ScriptableObject.CreateInstance<LevelPrefabPalette>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        paletteAsset = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private void LoadFromAsset()
    {
        if (paletteAsset == null)
        {
            EditorUtility.DisplayDialog("Load Palette", "Assign a Palette Asset first.", "OK");
            return;
        }
        prefabPalette = new List<GameObject>();
        prefabSizes = new List<Vector3>();
        if (paletteAsset.items != null && paletteAsset.items.Count > 0)
        {
            foreach (var it in paletteAsset.items)
            {
                prefabPalette.Add(it.prefab);
                prefabSizes.Add(it.size);
            }
        }
        else
        {
            // Fallback legacy list
            prefabPalette = new List<GameObject>(paletteAsset.prefabs);
            foreach (var p in prefabPalette)
            {
                prefabSizes.Add(EstimatePrefabSize(p));
            }
        }
        if (selectedPrefabIndex >= prefabPalette.Count)
            selectedPrefabIndex = Mathf.Max(0, prefabPalette.Count - 1);
        Repaint();
    }

    private void SaveToAsset()
    {
        if (paletteAsset == null)
        {
            if (EditorUtility.DisplayDialog("Save Palette", "No Palette Asset assigned. Create one now?", "Create", "Cancel"))
            {
                CreateNewPaletteAsset();
            }
            if (paletteAsset == null) return;
        }
        // Save both legacy and structured lists
        paletteAsset.prefabs = new List<GameObject>(prefabPalette);
        if (paletteAsset.items == null) paletteAsset.items = new List<LevelPrefabPalette.PrefabItem>();
        paletteAsset.items.Clear();
        for (int i = 0; i < prefabPalette.Count; i++)
        {
            var item = new LevelPrefabPalette.PrefabItem
            {
                prefab = prefabPalette[i],
                size = i < prefabSizes.Count ? prefabSizes[i] : Vector3.zero
            };
            paletteAsset.items.Add(item);
        }
        EditorUtility.SetDirty(paletteAsset);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(paletteAsset);
    }

    private static void ComputeWorldBox(Collider col, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
    {
        if (col is BoxCollider box)
        {
            rotation = col.transform.rotation;
            center = box.transform.TransformPoint(box.center);
            Vector3 size = Vector3.Scale(box.size, Abs(col.transform.lossyScale));
            halfExtents = size * 0.5f;
            return;
        }

        Bounds b = col.bounds;
        center = b.center;
        halfExtents = b.extents;
        rotation = Quaternion.identity;
    }

    private static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private void SyncSizeListWithPalette()
    {
        while (prefabSizes.Count < prefabPalette.Count) prefabSizes.Add(Vector3.zero);
        while (prefabSizes.Count > prefabPalette.Count) prefabSizes.RemoveAt(prefabSizes.Count - 1);
    }

    private Vector3 GetSelectedPrefabSize()
    {
        int idx = selectedPrefabIndex;
        if (idx < 0 || idx >= prefabSizes.Count) return Vector3.zero;
        if (prefabSizes[idx] == Vector3.zero && idx < prefabPalette.Count && prefabPalette[idx] != null)
        {
            prefabSizes[idx] = EstimatePrefabSize(prefabPalette[idx]);
        }
        return prefabSizes[idx];
    }

    private Vector3 EstimatePrefabSize(GameObject prefab)
    {
        if (prefab == null) return Vector3.one;
        var renderers = prefab.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = new Bounds(renderers[0].bounds.center, Vector3.zero);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b.size;
        }
        var mfs = prefab.GetComponentsInChildren<MeshFilter>();
        if (mfs != null && mfs.Length > 0)
        {
            Bounds b = new Bounds(mfs[0].sharedMesh.bounds.center, Vector3.zero);
            foreach (var mf in mfs) b.Encapsulate(mf.sharedMesh.bounds);
            return Vector3.Scale(b.size, prefab.transform.lossyScale);
        }
        return Vector3.one;
    }

    private Vector3 EstimateObjectBoundsSize(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = new Bounds(renderers[0].bounds.center, Vector3.zero);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b.size;
        }
        return Vector3.one;
    }

    private Vector3 EstimatePrefabSizeFromPaletteByName(string name)
    {
        for (int i = 0; i < prefabPalette.Count; i++)
        {
            var p = prefabPalette[i];
            if (p == null) continue;
            if (name.StartsWith(p.name))
            {
                if (i < prefabSizes.Count && prefabSizes[i] != Vector3.zero) return prefabSizes[i];
                return EstimatePrefabSize(p);
            }
        }
        return Vector3.zero;
    }

    private bool IsOverlappingAABB(Vector3 aPos, Vector3 aSize, Vector3 bPos, Vector3 bSize, Vector3 planeNormal)
    {
        // Project onto lateral plane axes
        Vector3 axisX = GetPreferredLateralAxis(planeNormal);
        Vector3 axisZ = Vector3.Cross(planeNormal, axisX).normalized;

        float aHalfX = aSize.x * 0.5f;
        float aHalfZ = aSize.z * 0.5f;
        float bHalfX = bSize.x * 0.5f;
        float bHalfZ = bSize.z * 0.5f;

        float aMinX = Vector3.Dot(aPos, axisX) - aHalfX;
        float aMaxX = Vector3.Dot(aPos, axisX) + aHalfX;
        float bMinX = Vector3.Dot(bPos, axisX) - bHalfX;
        float bMaxX = Vector3.Dot(bPos, axisX) + bHalfX;

        float aMinZ = Vector3.Dot(aPos, axisZ) - aHalfZ;
        float aMaxZ = Vector3.Dot(aPos, axisZ) + aHalfZ;
        float bMinZ = Vector3.Dot(bPos, axisZ) - bHalfZ;
        float bMaxZ = Vector3.Dot(bPos, axisZ) + bHalfZ;

        bool overlapX = aMinX <= bMaxX && aMaxX >= bMinX;
        bool overlapZ = aMinZ <= bMaxZ && aMaxZ >= bMinZ;
        return overlapX && overlapZ;
    }

    private float OverlapAmountAlongAxis(Vector3 aPos, Vector3 aSize, Vector3 bPos, Vector3 bSize, Vector3 preferredAxis, Vector3 planeNormal)
    {
        Vector3 axisX = GetPreferredLateralAxis(planeNormal);
        float aHalf = Vector3.Dot(aSize * 0.5f, new Vector3(Mathf.Abs(axisX.x), Mathf.Abs(axisX.y), Mathf.Abs(axisX.z)));
        float bHalf = Vector3.Dot(bSize * 0.5f, new Vector3(Mathf.Abs(axisX.x), Mathf.Abs(axisX.y), Mathf.Abs(axisX.z)));
        float aCenter = Vector3.Dot(aPos, axisX);
        float bCenter = Vector3.Dot(bPos, axisX);
        float dist = (aHalf + bHalf) - Mathf.Abs(aCenter - bCenter);
        if (dist <= 0f) return 0f;
        float sign = Mathf.Sign(aCenter - bCenter);
        if (sign == 0f) sign = 1f;
        return dist * sign;
    }
}
#endif


