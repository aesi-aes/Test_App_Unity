#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Reflection;
using System;

public static class Helper
{
	private static Vector3[] directionArray = new Vector3[] { Vector3.right, Vector3.up, Vector3.forward };

	public static bool RandomBoolean => UnityEngine.Random.value > .5f;

    public static Vector2 GetXZ(this Vector3 vector) => new Vector2(vector.x, vector.z);

    public static Vector3 GetFlattenedXZ(this Vector3 vector) => new Vector3(vector.x, 0f, vector.z);

    public static Vector3 GetAsFlattenedXZ(this Vector2 vector) => new Vector3(vector.x, 0f, vector.y);

    public static bool IsInsideBox(Vector3 worldPos, BoxCollider box)
    {
        var localPos = box.transform.InverseTransformPoint(worldPos);
        var delta = localPos - box.center + box.size * .5f;
        return Vector3.Max(Vector3.zero, delta).Equals(Vector3.Min(delta, box.size));
    }

    public static bool PointInCameraView(Vector3 point, Camera camera)
    {
        var viewport = camera.WorldToViewportPoint(point);
        var inCameraFrustum = Is01(viewport.x) && Is01(viewport.y);
        var inFrontOfCamera = viewport.z > 0;

        return inCameraFrustum && inFrontOfCamera;
    }

    public static bool Is01(float a) => a > 0f && a < 1f;

	public static bool IsVisible_NoAlloc(Vector3 pos, Vector3 boundSize, ref Bounds bounds, ref Plane[] planes, Camera camera)
    {
        bounds.center = pos;
        bounds.size = boundSize;

        GeometryUtility.CalculateFrustumPlanes(camera, planes);

        return GeometryUtility.TestPlanesAABB(planes, bounds);
    }

    public static bool IsVisible(Vector3 pos, Vector3 boundSize, Camera camera)
    {
        var bounds = new Bounds(pos, boundSize);
        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
        return GeometryUtility.TestPlanesAABB(planes, bounds);
    }

    public static GameObject CreateCopy(GameObject gameObject, IEnumerable<Renderer> renderers = null, bool ignoreParticleSystems = false, bool onlyActive = false)
    {
        var copy = new GameObject(gameObject.name + " (Copy)");
        copy.transform.position = gameObject.transform.position;
        copy.transform.rotation = gameObject.transform.rotation;
        copy.transform.localScale = gameObject.transform.localScale;

        if (renderers == null)
        {
            renderers = gameObject.GetComponentsInChildren<Renderer>();
        }

        foreach (var r in renderers)
        {
			if (ignoreParticleSystems && r is ParticleSystemRenderer)
            {
				continue;
            }

			if (onlyActive && !r.gameObject.activeInHierarchy)
			{
				continue;
			}

			var copyRenderer = GameObject.Instantiate(r.gameObject, copy.transform);
			copyRenderer.transform.position = r.transform.position;
            copyRenderer.transform.rotation = r.transform.rotation;
            copyRenderer.transform.localScale = r.transform.localScale;

            var renderer = copyRenderer.GetComponent<Renderer>();
            renderer.enabled = true;

            TryReplaceSkinnedMeshRenderer(copyRenderer, r);
        }

        return copy;
    }

    public static Color ColorMoveTowards(Color color1, Color color2, float v) =>
        new Color(
            Mathf.MoveTowards(color1.r, color2.r, v),
            Mathf.MoveTowards(color1.g, color2.g, v),
            Mathf.MoveTowards(color1.b, color2.b, v),
            Mathf.MoveTowards(color1.a, color2.a, v));

    public static T AddComponentIfNotExists<T>(this GameObject gameObject) where T : MonoBehaviour
    {
        var c = gameObject.GetComponent<T>();
        if (c == null)
        {
            c = gameObject.AddComponent<T>();
        }
        return c;
    }

    public static GameObject GetNearest(List<GameObject> gameObjects, Vector3 pos, Vector3 dir, float cutAngle, float angleLimit = 170f)
    {
        if (gameObjects.Count == 0)
        {
            return null;
        }

        List<SortEntry> entries = new List<SortEntry>();

        //create a list with given values
        foreach (GameObject obj in gameObjects)
        {
            float dist = Vector3.Distance(pos, obj.transform.position);
            float angle = Vector3.Angle(obj.transform.position - pos, dir);

            entries.Add(new SortEntry(obj, angle, dist));
        }

        if (entries.Count == 0)
            return null;

        //sort by angle
        entries.Sort((a, b) => { return a.Angle.CompareTo(b.Angle); });

        cutAngle = 10;

        int index = 0;
        float currentAngle = cutAngle;

        do
        {
            index = entries.FindLastIndex(o => o.Angle < currentAngle);
            currentAngle += 10;

            if (currentAngle > angleLimit)
            {
                break;
            }
        }
        while (index < 0);

        if (currentAngle > angleLimit && index < 0)
        {
            return null;
        }

        entries = entries.GetRange(0, index + 1);

        //sort by distance
        entries.Sort((a, b) => { return a.Distance.CompareTo(b.Distance); });

        return entries[0].Obj;
    }

    public static Sprite LoadSprite(string filePath, float pixelsPerUnit = 100.0f)
    {
        Texture2D SpriteTexture = LoadTexture(filePath);
        return Sprite.Create(SpriteTexture, new Rect(0, 0, SpriteTexture.width, SpriteTexture.height), new Vector2(0, 0), pixelsPerUnit);
    }

    public static Texture2D LoadTexture(string filePath)
    {
        Texture2D tex2d;
        byte[] FileData;

        if (File.Exists(filePath))
        {
            FileData = File.ReadAllBytes(filePath);
            tex2d = new Texture2D(2, 2);  
            if (tex2d.LoadImage(FileData))
            {
                return tex2d;             
            }
        }
        return null;
    }

    public static bool CaptureScreenshot(Camera cam, string path)
    {
        var texture = CaptureScreenshot(cam, Screen.width, Screen.height);
        try
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());

            return true;
        }
        catch (Exception e)
        {
            Debug.unityLogger.Log("Helper", "Couldn't create snapshot because: " + e.Message);
            return false;
        }
    }

    public static Texture2D CaptureScreenshot(Camera camera)
    {
        return CaptureScreenshot(camera, Screen.width, Screen.height);
    }

    public static Texture2D CaptureScreenshot(Camera cam, int width, int height)
    {
        var rt = new RenderTexture(width, height, 32, RenderTextureFormat.ARGB32);
		rt.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.D32_SFloat_S8_UInt;

		cam.targetTexture = rt;
        cam.RenderDontRestore();
        RenderTexture.active = rt;

        var texture = new Texture2D(width, height, TextureFormat.ARGB32, false, true);
        var rect = new Rect(0, 0, width, height);
        texture.ReadPixels(rect, 0, 0);
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();

        cam.targetTexture = null;
        RenderTexture.active = null;
        GameObject.Destroy(rt);

        return texture;
    }

    public static Bounds GetBounds(this Transform transform, LayerMask? layerMask = null)
    {
        var bounds = new Bounds();
        bounds.size = Vector3.zero; // reset

        var colliders = transform.GetComponentsInChildren<Collider>();
        foreach (var c in colliders)
            if (layerMask == null || layerMask.Value.ContainsLayer(c.gameObject.layer))
                bounds.Encapsulate(c.bounds);

        return bounds;
    }

    public static bool ContainsLayer(this LayerMask mask, int layer) => mask == (mask | (1 << layer));

    public static void SetOrthoCameraToBounds(Camera camera, Transform transform, LayerMask? layerMask = null) =>
        SetOrthoCameraToBounds(camera, GetBounds(transform, layerMask));

    public static void SetOrthoCameraToBounds(Camera camera, Bounds bounds)
    {
        camera.orthographicSize = Mathf.Max(bounds.size.z / 2f, bounds.size.x / 2f);
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y + bounds.size.y / 2f, bounds.center.z);
    }

    class SortEntry
    {
        public SortEntry(GameObject obj, float angle, float distance)
        {
            Obj = obj;
            Angle = angle;
            Distance = distance;
        }

        public GameObject Obj { get; set; }
        public float Angle { get; set; }
        public float Distance { get; set; }
    }

#if UNITY_EDITOR
    /// <summary>
    /// http://wiki.unity3d.com/index.php/CreateScriptableObjectAsset
    /// </summary>
    public static T CreateAsset<T>(T original = null, string fileName = null, bool selectAfterCreation = false) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        if (original != null)
            EditorUtility.CopySerialized(original, asset);

        var path = AssetDatabase.GetAssetPath(original);
        if (path == "")
        {
            path = "Assets";
        }
        else if (Path.GetExtension(path) != "")
        {
            path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(original)), "");
        }
        var file = !string.IsNullOrEmpty(fileName) ? fileName : "New " + typeof(T).ToString();
        file += ".asset";
        var assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/" + file);

        AssetDatabase.CreateAsset(asset, assetPathAndName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (selectAfterCreation)
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = original;
        }

        return asset;
    }

#endif

	public static void DrawGizmoBox(Transform tx, Vector3 boundaryBox)
	{
		var pos1 = tx.position + tx.rotation * new Vector3(boundaryBox.x, boundaryBox.y, boundaryBox.z);
		var pos2 = tx.position + tx.rotation * new Vector3(boundaryBox.x, boundaryBox.y, -boundaryBox.z);
		var pos3 = tx.position + tx.rotation * new Vector3(boundaryBox.x, -boundaryBox.y, boundaryBox.z);
		var pos4 = tx.position + tx.rotation * new Vector3(boundaryBox.x, -boundaryBox.y, -boundaryBox.z);
		var pos5 = tx.position + tx.rotation * new Vector3(-boundaryBox.x, boundaryBox.y, -boundaryBox.z);
		var pos6 = tx.position + tx.rotation * new Vector3(-boundaryBox.x, boundaryBox.y, boundaryBox.z);
		var pos7 = tx.position + tx.rotation * new Vector3(-boundaryBox.x, -boundaryBox.y, -boundaryBox.z);
		var pos8 = tx.position + tx.rotation * new Vector3(-boundaryBox.x, -boundaryBox.y, boundaryBox.z);

		Gizmos.DrawLine(pos1, pos2);
		Gizmos.DrawLine(pos3, pos4);
		Gizmos.DrawLine(pos1, pos3);
		Gizmos.DrawLine(pos2, pos5);

		Gizmos.DrawLine(pos5, pos6);
		Gizmos.DrawLine(pos7, pos8);
		Gizmos.DrawLine(pos5, pos7);
		Gizmos.DrawLine(pos4, pos7);

		Gizmos.DrawLine(pos2, pos4);
		Gizmos.DrawLine(pos3, pos8);
		Gizmos.DrawLine(pos1, pos6);
		Gizmos.DrawLine(pos6, pos8);
	}

	public static float Remap(float value, float from1, float to1, float from2, float to2)
	{
		return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
	}

	public static Vector3 Vector3ToPlusMinus180(Vector3 deltaRotation) =>
        new Vector3(
            FloatToPlusMinus180(deltaRotation.x),
            FloatToPlusMinus180(deltaRotation.y),
            FloatToPlusMinus180(deltaRotation.z));

    public static float FloatToPlusMinus180(float x) => (x > 180f) ? x - 360f : ((x < -180f) ? x + 360f : x);

    public static Vector3 Center(params Vector3[] positions)
    {
        var pos = Vector3.zero;

        for (var idx = 0; idx < positions.Length; ++idx)
            pos += positions[idx];

        return pos / positions.Length;
    }

    /// <summary>
    /// https://answers.unity.com/questions/486626/how-can-i-shuffle-alist.html
    /// </summary>
    public static void Randomize<T>(this List<T> list)
    {
        for (var idx = 0; idx < list.Count; ++idx)
        {
            T temp = list[idx];
            int randomIndex = UnityEngine.Random.Range(idx, list.Count);
            list[idx] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

	/// <summary>
	/// Warning! Is expensive!
	/// 
	/// https://stackoverflow.com/questions/272633/add-spaces-before-capital-letters
	/// </summary>
	public static string AddSpacesToSentence(string text, bool preserveAcronyms)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        var newText = new StringBuilder(text.Length * 2);
        newText.Append(text[0]);
        for (int i = 1; i < text.Length; i++)
        {
            if (char.IsUpper(text[i]))
                if ((text[i - 1] != ' ' && !char.IsUpper(text[i - 1])) ||
                    (preserveAcronyms && char.IsUpper(text[i - 1]) &&
                     i < text.Length - 1 && !char.IsUpper(text[i + 1])))
                    newText.Append(' ');
            newText.Append(text[i]);
        }
        return newText.ToString();
    }

    public static Vector2 DegreeToUnitVector2(float degrees)
    {
        var radians = degrees * Mathf.Deg2Rad;
        return RadiansToUnitVector2(radians);
    }

    public static Vector2 RadiansToUnitVector2(float radians)
    {
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }

    public static Vector3 GetLocalDirection(Transform t, Vector3 dir)
    {
        return t.worldToLocalMatrix.MultiplyVector(dir);
    }

    public static IEnumerator FadeColor(Color source, Color destination, float time, System.Action<Color> OnUpdate)
    {
        float curTime = time;
        WaitForEndOfFrame wait = new WaitForEndOfFrame();

        while (curTime > 0)
        {
            curTime -= Time.deltaTime;
            OnUpdate(Color.Lerp(source, destination, Mathf.Clamp01(1 - (curTime / time))));
            yield return wait;
        }

        yield return null;
    }

    public static string DecryptString(string encrString)
    {
        byte[] b;
        string decrypted;
        try
        {
            b = System.Convert.FromBase64String(encrString);
            decrypted = System.Text.ASCIIEncoding.ASCII.GetString(b);
        }
        catch (System.FormatException fe)
        {
            Debug.unityLogger.LogWarning("Helper", "Could not decrypt string: " + fe.Message);
            decrypted = "";
        }
        return decrypted;
    }

    public static string EncryptString(string strEncrypted)
    {
        byte[] b = System.Text.ASCIIEncoding.ASCII.GetBytes(strEncrypted);
        string encrypted = System.Convert.ToBase64String(b);
        return encrypted;
    }

    public static void AddExplosionForce(this Rigidbody2D body, float explosionForce, Vector3 explosionPosition, float explosionRadius, float torque)
    {
        var dir = (body.transform.position - explosionPosition);
        float wearoff = 1 - (dir.magnitude / explosionRadius);

        var force = dir.normalized * explosionForce * wearoff;
        body.AddForce(force, ForceMode2D.Impulse);

      //  body.AddTorque(torque, ForceMode2D.Impulse);
    }

    public static object Clone(object original)
    {
        object clonedObject = null;
        BinaryFormatter formatter = new BinaryFormatter();
        using (Stream stream = new MemoryStream())
        {
            formatter.Serialize(stream, original);
            stream.Seek(0, SeekOrigin.Begin);
            clonedObject = formatter.Deserialize(stream);
        }

        return clonedObject;
    }

#if UNITY_EDITOR
    public static void DrawSeparator()
    {
        EditorGUILayout.TextArea("", GUI.skin.horizontalSlider);
    }
#endif

    public static System.Type[] GetAllSubTypes(System.Type aBaseClass, bool include = false)
    {
        var result = new System.Collections.Generic.List<System.Type>();
        System.Reflection.Assembly[] AS = System.AppDomain.CurrentDomain.GetAssemblies();
        foreach (var A in AS)
        {
            System.Type[] types = A.GetTypes();
            foreach (var T in types)
            {
                if (T.IsSubclassOf(aBaseClass))
                    result.Add(T);
            }
        }
        if (include)
            result.Add(aBaseClass);

        return result.ToArray();
    }

    public static float CalculateAngle(Vector2 direction)
    {
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    public static float CalculateAngle(Vector2 from, Vector2 to)
    {
        return Quaternion.FromToRotation(Vector2.up, to - from).eulerAngles.z;
    }

    public static float CalculateAngle(Vector3 from, Vector3 to)
    {
        return Quaternion.FromToRotation(Vector3.up, to - from).eulerAngles.z;
    }

    public static float CalculateAngle(Vector3 from, Vector3 to, Vector3 axis)
    {
        return Quaternion.FromToRotation(axis, to - from).eulerAngles.z;
    }

    public static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles)
    {
        Vector3 dir = point - pivot; // get point direction relative to pivot
        dir = Quaternion.Euler(angles) * dir; // rotate it
        point = dir + pivot; // calculate rotated point
        return point; // return it
    }

    public static float Acclerate(float val,ref float speed)
	{
		float result = val + speed;
		speed += speed;
		return result;
	}

	public static float DeAcclerate(float val,ref float speed)
	{
		float result = val - speed;
		speed += speed;
		return result;
	}

	public static string ToJson(object obj)
	{
		return JsonUtility.ToJson(obj);
	}

	public static T FromJson<T>(string json) where T:class
	{
		return JsonUtility.FromJson<T>(json);
	}

	public static Texture2D FlipTexture(Texture2D original, bool horizontal = true)
	{
		Texture2D flipped = new Texture2D(original.width,original.height);

		int xN = original.width;
		int yN = original.height;

		for(int x=0;x<xN;x++)
			for(int y=0;y<yN;y++)
			{
				if(horizontal)
					flipped.SetPixel(xN-x-1, y, original.GetPixel(x,y));
				else
					flipped.SetPixel(x, yN-y-1, original.GetPixel(x,y));
			}
		flipped.Apply();

		return flipped;
	}

	public static void WritePNGToFile(string path, Texture2D texture)
	{
		byte[] bytes = texture.EncodeToPNG();
		File.WriteAllBytes(path,bytes);
	}
#if UNITY_EDITOR
    public static Dictionary<Vector2,Color> ConvertTextureToMatrix(Texture2D texture)
	{
		Dictionary<Vector2,Color> result = new Dictionary<Vector2, Color> ();

		Helper.SetTextureIsReadable (texture,true);

		for (int x = 0; x < texture.width; x++)
			for (int y = 0; y < texture.height; y++)
				result.Add (new Vector2 (x, y), texture.GetPixel (x, y));
				
		return result;
	}


    public static void SetTextureIsReadable( Texture2D texture, bool isReadable)
	{
		if ( null == texture ) return;

		string assetPath = AssetDatabase.GetAssetPath( texture );
		var tImporter = AssetImporter.GetAtPath( assetPath ) as TextureImporter;
		if ( tImporter != null )
		{
			tImporter.textureType = TextureImporterType.Default;

			tImporter.isReadable = isReadable;

			AssetDatabase.ImportAsset( assetPath );
			AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
		}
	}

	public static List<string> Permutation(string alphabet, int count)
	{
		List<string> result = new List<string>();
		var q = alphabet.Select(x => x.ToString());
		int size = count;
		for (int i = 0; i < size - 1; i++)
			q = q.SelectMany(x => alphabet, (x, y) => x + y);

		result = q.ToList();
	
		return result;
	}

	public static T CreateAsset<T> (string folder, string name) where T : ScriptableObject
	{
		T asset = ScriptableObject.CreateInstance<T> ();

		string assetPathAndName =  ("Assets/" + folder + "/" + name + ".asset");

		AssetDatabase.CreateAsset (asset, assetPathAndName);

		AssetDatabase.SaveAssets ();
		AssetDatabase.Refresh();
		//EditorUtility.FocusProjectWindow ();
		//Selection.activeObject = asset;

		return asset;
	}

	public static T CreateAsset<T> () where T : ScriptableObject
	{
		T asset = ScriptableObject.CreateInstance<T> ();

		string path = GetAssetPath (Selection.activeObject);

		string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath (path + "/New " + typeof(T).ToString() + ".asset");

		AssetDatabase.CreateAsset (asset, assetPathAndName);

		AssetDatabase.SaveAssets ();
		AssetDatabase.Refresh();
		EditorUtility.FocusProjectWindow ();
		Selection.activeObject = asset;

		return asset;
	}

    public static string GetAssetPath(UnityEngine.Object asset)
	{

		string path = AssetDatabase.GetAssetPath (asset);
		if (path == "") 
		{
			path = "Assets";
		} 
		else if (Path.GetExtension (path) != "") 
		{
			path = path.Replace (Path.GetFileName (AssetDatabase.GetAssetPath (asset)), "");
		}

		return path;
	}
#endif

    public static float GetRandomSign()
	{
		return UnityEngine.Random.Range(0,2) == 1 ? 1 : -1;
	}

	public static Mesh CopyMesh(Mesh _mesh)
	{
		Mesh mesh = new Mesh();
		mesh.name = "MeshCopy";
		mesh.vertices = _mesh.vertices;
		mesh.uv = _mesh.uv;
		mesh.triangles = _mesh.triangles;
		mesh.normals = _mesh.normals;
		return mesh;
	}

    static void TryReplaceSkinnedMeshRenderer(GameObject copyRenderer, Renderer from)
    {
        var skinned = from as SkinnedMeshRenderer;
        if (skinned == null)
            return;

        var newCopyRenderer = new GameObject(copyRenderer.name);
        newCopyRenderer.transform.position = copyRenderer.transform.position;
        newCopyRenderer.transform.rotation = copyRenderer.transform.rotation;
        newCopyRenderer.transform.localScale = Vector3.one;
        newCopyRenderer.transform.SetParent(copyRenderer.transform.parent);

        var meshFilter = newCopyRenderer.AddComponent<MeshFilter>();
        newCopyRenderer.layer = copyRenderer.layer;

        var mesh = new Mesh();
        skinned.BakeMesh(mesh);
        meshFilter.mesh = mesh;

        var r = newCopyRenderer.AddComponent<MeshRenderer>();
        r.material = skinned.material;
        r.allowOcclusionWhenDynamic = skinned.allowOcclusionWhenDynamic;

        GameObject.Destroy(copyRenderer);
    }

    /// <summary>
    /// DEPRECATED! Not performant! Needs rework!
    /// </summary>
    public static Vector3 GetCamRayCastPosition()
	{
		Transform cam = Camera.main.transform;
		RaycastHit hit;

		if(Physics.Raycast(cam.position, cam.forward,out hit, 800f))
			return hit.point;

		return IdentifyPosition(new Vector2(Screen.width/2 ,Screen.height/2));
	}

	public static Vector3 RotateVector(Vector3 _vec, Vector3 _axis, float _angle)
	{
		Quaternion quat = Quaternion.AngleAxis(_angle, _axis);
		Vector3 vect = quat * _vec;
		return vect;
	}

    public static Vector2 RotateVector(Vector2 _vec, Vector2 _axis, float _angle)
    {
        Quaternion quat = Quaternion.AngleAxis(_angle, _axis);
        Vector3 vect = _vec;
        vect = quat * vect;
        return vect;
    }

    public static Vector2 RotateVector(this Vector2 v, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        float tx = v.x;
        float ty = v.y;

        return new Vector2(cos * tx - sin * ty, sin * tx + cos * ty);
    }

    public static T FindInRoot<T>(Transform trf) where T: class 
	{
		Transform root = GetRootTransform(trf);

		return root.GetComponent<T>();
	}

	public static T FindInCompleteObject<T>(Transform trf) where T: class
	{
		Transform root = GetRootTransform(trf);

		T cmp = root.GetComponent<T>();

		if (cmp == null)
			cmp = root.GetComponentInChildren<T>(false);

		return cmp;
	}

	public static Transform GetRootTransform(Transform _object)
	{
		Transform parent = _object;

		while(parent.parent != null)
			parent = parent.parent;

		return parent;
	}

    /// <summary>
    /// DEPRECATED! Not performant! Needs rework!
    /// </summary>
    public static Vector3 GetMousePosition2D()
    {
        Vector3 pz = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        pz.z = 0;
        return pz;
    }

    public static void SetLayerRecursively(this GameObject obj, int newLayer, params int[] onlyIfOnLayer)
    {
        if (null == obj)
        {
            return;
        }

        // #1 check for all given layers...
        for (var idx = 0; idx < onlyIfOnLayer.Length; ++idx)
        {
            if (onlyIfOnLayer[idx] == obj.layer)
            {
                obj.layer = newLayer;
                break;
            }
        }

        // #2 ... if no layers given, always set.
        if (onlyIfOnLayer.Length == 0)
            obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            if (null == child)
                continue;
            child.gameObject.SetLayerRecursively(newLayer, onlyIfOnLayer);
        }
    }

    /// <summary>
    /// DEPRECATED! Not performant! Needs rework!
    /// </summary>
    public static Vector3 GetMouseRayPosition(LayerMask mask)
	{
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		RaycastHit hit;

		// ray hit an object, return intersection point
		if (Physics.Raycast(ray, out hit,500,mask))
			return hit.point;

		return Vector3.zero;
	}

	public static Vector3 GetRayHit(Vector3 _center,Vector3 _dir, float _dist)
	{
		Ray ray = new Ray(_center,_dir);
		RaycastHit hit;

		if(Physics.Raycast(ray,out hit,_dist))
			return hit.point;
		return Vector3.zero;
	}

    /// <summary>
    /// DEPRECATED! Not performant! Needs rework!
    /// </summary>
	public static Vector3 IdentifyPosition(Vector2 _screenPos)
	{
		Ray ray = Camera.main.ScreenPointToRay(_screenPos);
		// the Y=0 plane (horizontal plane)
		float t = -(ray.origin.y) / ray.direction.y;
		return ray.GetPoint(t);
	}

    /// <summary>
    /// DEPRECATED! Not performant! Needs rework!
    /// </summary>
	public static Vector3 GetCameraLookAt()
	{
		Ray ray = Camera.main.ScreenPointToRay(new Vector2(Screen.width/2,Screen.height/2));
		RaycastHit hit;

		// ray hit an object, return intersection point
		if (Physics.Raycast(ray, out hit,1000))
			return hit.point;

		return Camera.main.transform.position;
	}

    /// <summary>
    /// DEPRECATED! Not performant! Needs rework!
    /// </summary>
    public static GameObject GetMouseRayObject()
	{
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		RaycastHit hit;

		// ray hit an object, return intersection point
		if (Physics.Raycast(ray, out hit,500))
		{
			GameObject other = hit.collider.gameObject;
			return other;
		}				

		return null;
	}	

	public static Vector2 GetWorldToScreen(Vector3 _position, Camera cam = null)
	{
        if (cam == null)
            cam = Camera.main;
		return cam.WorldToScreenPoint(_position);		
	}

	public static Vector3 GetTerrainPos(Vector3 _pos)
	{
		if(Terrain.activeTerrain != null)
			_pos.y = Terrain.activeTerrain.SampleHeight(_pos);
		else
			_pos.y = 0;
		return _pos;
	}	

	public static string VecToString(Vector2 vec)
	{
		return vec.x.ToString() + "," + vec.y.ToString();
	}

	public static Vector2 StringToVec(string vec)
	{
		string[] array = vec.Split(',');
		Vector2 result = new Vector2((float)System.Convert.ToDouble(array[0]),(float)System.Convert.ToDouble(array[1]));
		return result;
	}

	public static bool IsPointerOverUI()
	{
		if(EventSystem.current == null)
			return false;
		
		//mouse ID
		int fingerID = -1;

		if (Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began) 
			fingerID = Input.GetTouch (0).fingerId;
		
		return EventSystem.current.IsPointerOverGameObject(fingerID);

	}
    
	public static float Smooth(float from, float to, float speed, float deltaTime)
	{
		float result = from;

		if(from < to)
			result += speed * deltaTime;
		else
			result -= speed * deltaTime;

		return result;
	}

    // http://answers.unity3d.com/questions/131624/vector3-comparison.html
    /// <summary>
    /// Compares the vectors.
    /// <returns><c>true</c>, if vectors are "the same", <c>false</c> otherwise.</returns>
    /// </summary>
    public static bool CompareVector3(this Vector3 a, Vector3 b, float tolerance =.1f)
    {
		var dx = a.x - b.x;
        if (Mathf.Abs(dx) > tolerance)
        {
            return false;
        }

		var dy = a.y - b.y;
		if (Mathf.Abs(dy) > tolerance)
        {
			return false;
        }

		var dz = a.z - b.z;
		return Mathf.Abs(dz) < tolerance;
    }

    // http://answers.unity3d.com/questions/131624/vector3-comparison.html
    /// <summary>
    /// Compares the vectors.
    /// <returns><c>true</c>, if vectors are "the same", <c>false</c> otherwise.</returns>
    /// </summary>
    public static bool CompareVector2(this Vector2 a, Vector2 b, float tolerance = .1f)
    {
		var dx = a.x - b.x;
		if (Mathf.Abs(dx) > tolerance)
		{
			return false;
		}

		var dy = a.y - b.y;
		return Mathf.Abs(dy) < tolerance;
	}

    public static bool Approximately(Quaternion q1, Quaternion q2, float tolerance) => Mathf.Abs(Quaternion.Dot(q1, q2)) >= 1f - tolerance;

    public static bool Approximately(Vector3 me, Vector3 other, float tolerance)
    {
        var dx = me.x - other.x;
        if (Mathf.Abs(dx) > tolerance)
        {
            return false;
        }

        var dy = me.y - other.y;
        if (Mathf.Abs(dy) > tolerance)
        {
            return false;
        }

        var dz = me.z - other.z;
        return Mathf.Abs(dz) < tolerance;
    }
    
    public static Vector3 RoundByDegree(this Vector3 vector, float degrees)
    {
		var angle = Vector3.Angle(vector, Vector3.up);
		if (angle < degrees / 2.0f)          // Cannot do cross product 
        {
			return Vector3.up * vector.magnitude;  //   with angles 0 & 180
        }
		if (angle > 180.0f - degrees / 2.0f)
        {
			return Vector3.down * vector.magnitude;
        }

		var t = Mathf.Round(angle / degrees);

		var deltaAngle = (t * degrees) - angle;

		var axis = Vector3.Cross(Vector3.up, vector);
		var q = Quaternion.AngleAxis(deltaAngle, axis);
		return q * vector;
	}

#if UNITY_EDITOR
    public static object GetTargetObjectOfProperty(SerializedProperty prop)
    {
        if (prop == null) return null;

        var path = prop.propertyPath.Replace(".Array.data[", "[");
        object obj = prop.serializedObject.targetObject;
        var elements = path.Split('.');
        foreach (var element in elements)
        {
            if (element.Contains("["))
            {
                var elementName = element.Substring(0, element.IndexOf("["));
                var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                obj = GetValue_Imp(obj, elementName, index);
            }
            else
            {
                obj = GetValue_Imp(obj, element);
            }
        }
        return obj;
    }
#endif

    private static object GetValue_Imp(object source, string name)
    {
        if (source == null)
            return null;
        var type = source.GetType();

        while (type != null)
        {
            var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f != null)
                return f.GetValue(source);

            var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null)
                return p.GetValue(source, null);

            type = type.BaseType;
        }
        return null;
    }

    private static object GetValue_Imp(object source, string name, int index)
    {
        var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
        if (enumerable == null) return null;
        var enm = enumerable.GetEnumerator();
        //while (index-- >= 0)
        //    enm.MoveNext();
        //return enm.Current;

        for (int i = 0; i <= index; i++)
        {
            if (!enm.MoveNext()) return null;
        }
        return enm.Current;
    }

    public static int AddLayerToLayerMask(this int layerMask, int layer)
    {
        return layerMask | 1 << layer;
    }

    public static int RemoveLayerFromLayerMask(this int layerMask, int layer)
    {
        return layerMask & ~(1 << layer);
    }

	public static Vector2 ClampVector2(Vector2 targetPos, Vector2 leftTop, Vector2 rightBottom)
	{
		var factor = 1f;

		if (targetPos.x < leftTop.x)
		{
			factor = GetFactor(factor, targetPos.x, leftTop.x, leftTop.x, rightBottom.x);
		}
		else if (targetPos.x > rightBottom.x)
		{
			factor = GetFactor(factor, targetPos.x, rightBottom.x, leftTop.x, rightBottom.x);
		}
		
        if (targetPos.y < leftTop.y)
		{
			factor = GetFactor(factor, targetPos.y, leftTop.y, leftTop.y, rightBottom.y);
		}
		else if (targetPos.y > rightBottom.y)
		{
			factor = GetFactor(factor, targetPos.y, rightBottom.y, leftTop.y, rightBottom.y);
		}

		var half = (leftTop + rightBottom) / 2f;
		targetPos -= half;
		targetPos *= factor;
		targetPos += half;

		return targetPos;
	}

	private static float GetFactor(float currentFactor, float actualValue, float maxValue, float leftTop, float rightBottom)
	{
		var half = (leftTop + rightBottom) / 2f;
		var newFactor = (maxValue - half) / (actualValue - half);

        return Mathf.Min(currentFactor, newFactor);
	}

	public static Vector3 RotateVector90(Vector3 normalized, bool clockwise) =>
        clockwise ?
        new Vector3(-normalized.z, normalized.y, normalized.x) :
        new Vector3(normalized.z, normalized.y, -normalized.x);

    public static T GetComponentInThisOrDirectChildren<T>(this MonoBehaviour behaviour) where T : MonoBehaviour
    {
        var tx = behaviour.transform;
		var direct = tx.GetComponent<T>();
		if (direct != null)
			return direct;

		for (var idx = 0; idx < tx.childCount; ++idx)
        {
            var comp = tx.GetChild(idx).GetComponent<T>();
            if (comp != null)
                return comp;
        }                                            

        return null;
    }

	public static RaycastHit? SphereCastNonAlloc_GetNearest(Ray ray, float collisionRadius, RaycastHit[] hits, float distance, LayerMask layerMask, QueryTriggerInteraction queryTrigger = QueryTriggerInteraction.Ignore, HashSet<int> ignoreColliderHashes = null)
	{
        var hitCount = Physics.SphereCastNonAlloc(ray, collisionRadius, hits, distance, layerMask, queryTrigger);
        return GetNearestRaycastHit(hits, hitCount, ignoreColliderHashes);
	}

	public static RaycastHit? RaycastNonAlloc_GetNearest(Ray ray, RaycastHit[] hits, float range, LayerMask layerMask, QueryTriggerInteraction queryTrigger = QueryTriggerInteraction.Ignore, HashSet<int> ignoreColliderHashes = null)
	{
		var hitCount = Physics.RaycastNonAlloc(ray, hits, range, layerMask, queryTrigger);
        return GetNearestRaycastHit(hits, hitCount, ignoreColliderHashes);
	}

	public static RaycastHit? RaycastNonAlloc_GetNearest(Vector3 pos, Vector3 dir, RaycastHit[] hits, float range, LayerMask layerMask, QueryTriggerInteraction queryTrigger = QueryTriggerInteraction.Ignore, HashSet<int> ignoreColliderHashes = null)
	{
		var hitCount = Physics.RaycastNonAlloc(pos, dir, hits, range, layerMask, queryTrigger);
		return GetNearestRaycastHit(hits, hitCount, ignoreColliderHashes);
	}

	public static RaycastHit? GetNearestRaycastHit(RaycastHit[] hits, int hitCount, HashSet<int> ignoreColliderHashes = null)
	{
		var minDistance = float.MaxValue;
		var minIdx = -1;

		for (var idx = 0; idx < hitCount; ++idx)
		{
            if (ignoreColliderHashes != null && ignoreColliderHashes.Contains(hits[idx].collider.GetHashCode()))
            {
                continue;
            }

			if (hits[idx].distance < minDistance)
			{
				minDistance = hits[idx].distance;
				minIdx = idx;
			}
		}

		if (minIdx > -1)
		{
			return hits[minIdx];
		}

		return null;
	}

	public static RaycastHit? GetNearestAngleRaycastHit(Vector3 startPosition, Vector3 direction, RaycastHit[] hits, int hitCount, HashSet<int> ignoreColliderHashes = null)
	{
		var minAngle = float.MaxValue;
		var minIdx = -1;

		for (var idx = 0; idx < hitCount; ++idx)
		{
			if (ignoreColliderHashes != null && ignoreColliderHashes.Contains(hits[idx].collider.GetHashCode()))
			{
				continue;
			}

            var angle = Mathf.Abs(Vector3.Angle(direction, hits[idx].point - startPosition));
			if (angle < minAngle)
			{
				minAngle = angle;
				minIdx = idx;
			}
		}

		if (minIdx > -1)
		{
			return hits[minIdx];
		}

		return null;
	}

	public static bool IsParented(Transform tx, Transform parent)
	{
		while (tx != null)
        {
            if (tx == parent)
            {
                return true;
            }

            tx = tx.parent;
        }

        return false;
	}

	public static Vector3 GetDirectionWithinLimit(Vector3 initialDirection, Vector3 targetDirection, float maxAngle)
	{
		var angle = Vector3.Angle(initialDirection, targetDirection);
		var factor = maxAngle / angle;
		return Vector3.Slerp(initialDirection, targetDirection, factor);
	}

    /// <summary>
    /// If deactivated in hierarchy, get parent (or self) that deactivates it.
    /// </summary>
	public static Transform GetDeactivatingParent(this Transform transform)
	{
		if (transform.gameObject.activeInHierarchy)
		{
            return null;
		}

		if (!transform.gameObject.activeSelf)
		{
			return transform;
		}

		var parent = transform.parent;
		while (parent != null)
		{
			if (!parent.gameObject.activeSelf)
			{
				return parent;
			}

			parent = parent.parent;
		}

		return null;
	}

	public static Vector3 GetLocalBoundingBox(Collider collider)
	{
		if (collider is BoxCollider box)
        {
			return box.size * box.transform.lossyScale.x;
        }
		else if (collider is SphereCollider sphere)
		{
			var radius = sphere.radius * sphere.transform.lossyScale.x;
            var dia = radius * 2f;
			return new Vector3(dia, dia, dia);
		}
		else if (collider is CapsuleCollider capsule)
		{
            var scale = capsule.transform.lossyScale.x;
			var radius = capsule.radius * scale;
			var height = capsule.height * scale;
			var direction = capsule.direction;

			var result = new Vector3();
			for (int i = 0; i < 3; i++)
			{
				if (i == direction)
                {
					result += directionArray[i] * height;
                }
				else
                {
					result += directionArray[i] * radius * 2;
                }
			}
			return result;
		}
		else if (collider is MeshCollider mesh)
		{
			return mesh.sharedMesh.bounds.size * mesh.transform.lossyScale.x;
		}

        return Vector3.zero;
	}

	public static int? TryGetAnimatorStringToHash(string parameter)
	{
		if (!string.IsNullOrEmpty(parameter))
		{
			return Animator.StringToHash(parameter);
		}

        return null;
	}

	public static Vector2 CircleCoordsToSquare(Vector2 delta)
	{
		if (Mathf.Approximately(delta.x, 0f))
		{
			return delta;
		}

		if (Mathf.Approximately(delta.y, 0f))
		{
			return delta;
		}

		var absX = Mathf.Abs(delta.x);
        var absY = Mathf.Abs(delta.y);

        var multiplicator = (absX > absY) ? absX : absY;
        multiplicator = delta.magnitude / multiplicator;

        return delta * multiplicator;
    }

	public static float GetMinimumFromVector3(Vector3 anchor)
	{
        var min = float.MaxValue;
        if (anchor.x < min)
        {
            min = anchor.x;
        }
        if (anchor.y < min)
        {
            min = anchor.y;
        }
        if (anchor.z < min)
        {
            min = anchor.z;
        }
        return min;
	}
}
