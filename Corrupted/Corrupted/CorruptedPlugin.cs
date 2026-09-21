using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine.AI;
using UnityEngine.UI;

namespace Corrupted;

[BepInPlugin("denyscrasav4ik.thedumbfactory.corrupted", "Corrupted", "1.1.0")]
public class CorruptedPlugin : BaseUnityPlugin
{
    public static ConfigEntry<float> CorruptionInterval, MinMultiplier, MaxMultiplier, ZeroReplacementMin, ZeroReplacementMax, MeshVertexCorruption, MaterialReplacementChance, SpriteReplacementChance;
    public static ConfigEntry<bool> AllowNegativeValues, CorruptMeshFilters, CorruptSprites, CorruptMaterials;

    private void Awake()
    {
        CorruptionInterval = Config.Bind("Settings", "CorruptionInterval", 1f, "Time in seconds between corruption cycles.");
        MinMultiplier = Config.Bind("Settings", "MinMultiplier", 0f, "Minimum random intensity multiplier.");
        MaxMultiplier = Config.Bind("Settings", "MaxMultiplier", 2f, "Maximum random intensity multiplier.");
        AllowNegativeValues = Config.Bind("Settings", "AllowNegativeValues", false, "Whether negative multipliers are allowed.");

        ZeroReplacementMin = Config.Bind("Zero Value Corruption", "ZeroReplacementMin", 0.1f, "Minimum absolute value used when corrupting a value that is currently 0.");
        ZeroReplacementMax = Config.Bind("Zero Value Corruption", "ZeroReplacementMax", 2f, "Maximum absolute value used when corrupting a value that is currently 0.");

        CorruptMeshFilters = Config.Bind("Mesh Corruption", "CorruptMeshFilters", true, "Whether MeshFilters can have their mesh vertices corrupted.");
        MeshVertexCorruption = Config.Bind("Mesh Corruption", "MeshVertexCorruption", 0.5f, "How strongly mesh vertices can be moved.");

        CorruptSprites = Config.Bind("Asset Corruption", "CorruptSprites", true, "Whether sprites can randomly be replaced.");
        CorruptMaterials = Config.Bind("Asset Corruption", "CorruptMaterials", true, "Whether materials can randomly be replaced.");
        MaterialReplacementChance = Config.Bind("Asset Corruption", "MaterialReplacementChance", 0.5f, "Chance for a SpriteRenderer/UI Image material to be replaced.");
        SpriteReplacementChance = Config.Bind("Asset Corruption", "SpriteReplacementChance", 0.5f, "Chance for a sprite to be replaced.");

        DontDestroyOnLoad(new GameObject("Corrupted_Instance", typeof(SceneCorruptor)));
    }
}

public class SceneCorruptor : MonoBehaviour
{
    public float minMultiplier => CorruptedPlugin.AllowNegativeValues.Value ? -CorruptedPlugin.MaxMultiplier.Value : CorruptedPlugin.MinMultiplier.Value;
    public float maxMultiplier => CorruptedPlugin.MaxMultiplier.Value;

    private void Start() => StartCoroutine(CorruptionLoop());

    private IEnumerator CorruptionLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(CorruptedPlugin.CorruptionInterval.Value);
            var source = FindObjectsOfType<GameObject>().Where(obj => obj != gameObject).ToArray();
            if (source.Length > 0) CorruptObject(source[UnityEngine.Random.Range(0, source.Length)]);
        }
    }

    private void CorruptObject(GameObject obj)
    {
        CorruptTransform(obj.transform);
        if (obj.TryGetComponent(out Rigidbody rb)) CorruptRigidbody(rb);
        if (obj.TryGetComponent(out AudioSource audio)) CorruptAudioSource(audio);
        if (obj.TryGetComponent(out NavMeshAgent agent)) CorruptNavMeshAgent(agent);
        if (obj.TryGetComponent(out Renderer rend)) CorruptRenderer(rend);
        if (obj.TryGetComponent(out SpriteRenderer spr)) CorruptSpriteRenderer(spr);
        if (obj.TryGetComponent(out MeshFilter mf) && CorruptedPlugin.CorruptMeshFilters.Value) CorruptMeshFilter(mf);
        if (obj.TryGetComponent(out Collider col)) CorruptCollider(col);
        if (obj.TryGetComponent(out Collider2D col2D)) CorruptCollider2D(col2D);
        if (obj.TryGetComponent(out TMP_Text tmp)) CorruptTMP(tmp);
        if (obj.TryGetComponent(out Image img)) CorruptImage(img);
        if (obj.TryGetComponent(out RawImage rImg)) CorruptRawImage(rImg);
        if (obj.TryGetComponent(out Slider sld)) CorruptSlider(sld);
        if (obj.TryGetComponent(out Scrollbar scr)) { scr.value = Mathf.Clamp01(CorruptFloat(scr.value)); scr.size = Mathf.Clamp01(CorruptFloat(scr.size)); }
        if (obj.TryGetComponent(out Toggle tog) && UnityEngine.Random.value < 0.5f) tog.isOn = !tog.isOn;
        if (obj.TryGetComponent(out CanvasGroup cg)) { cg.alpha = Mathf.Clamp01(CorruptFloat(cg.alpha)); cg.interactable = cg.blocksRaycasts = UnityEngine.Random.value > 0.3f; }
        if (obj.TryGetComponent(out RectTransform rt)) CorruptRectTransform(rt);

        foreach (var mb in obj.GetComponents<MonoBehaviour>().Where(m => m != null && m != this))
            CorruptScript(mb);
    }

    private float RandomMultiplier() => UnityEngine.Random.Range(minMultiplier, maxMultiplier);
    private float CorruptFloat(float v) => Mathf.Approximately(v, 0f) ? UnityEngine.Random.Range(CorruptedPlugin.ZeroReplacementMin.Value, CorruptedPlugin.ZeroReplacementMax.Value) : v * RandomMultiplier();
    private double CorruptDouble(double v) => Math.Abs(v) < double.Epsilon ? UnityEngine.Random.Range(CorruptedPlugin.ZeroReplacementMin.Value, CorruptedPlugin.ZeroReplacementMax.Value) : v * (double)RandomMultiplier();
    private int CorruptInt(int v) => v == 0 ? UnityEngine.Random.Range(Mathf.Max(1, Mathf.RoundToInt(CorruptedPlugin.ZeroReplacementMin.Value)), Mathf.Max(1, Mathf.RoundToInt(CorruptedPlugin.ZeroReplacementMax.Value)) + 1) : Mathf.RoundToInt((float)v * RandomMultiplier());
    private long CorruptLong(long v) => v == 0L ? (long)UnityEngine.Random.Range(Math.Max(1L, (long)CorruptedPlugin.ZeroReplacementMin.Value), Math.Max(1L, (long)CorruptedPlugin.ZeroReplacementMax.Value)) : (long)((float)v * RandomMultiplier());

    private Vector2 CorruptVector2(Vector2 v) => new Vector2(CorruptFloat(v.x), CorruptFloat(v.y));
    private Vector3 CorruptVector3(Vector3 v) => new Vector3(CorruptFloat(v.x), CorruptFloat(v.y), CorruptFloat(v.z));
    private Vector4 CorruptVector4(Vector4 v) => new Vector4(CorruptFloat(v.x), CorruptFloat(v.y), CorruptFloat(v.z), CorruptFloat(v.w));
    private Color CorruptColor(Color c) => new Color(CorruptFloat(c.r), CorruptFloat(c.g), CorruptFloat(c.b), CorruptFloat(c.a));

    private void CorruptTransform(Transform t)
    {
        t.localPosition = CorruptVector3(t.localPosition);
        t.localScale = CorruptVector3(t.localScale);
        t.localEulerAngles = CorruptVector3(t.localEulerAngles);
    }

    private void CorruptRigidbody(Rigidbody rb)
    {
        rb.mass = CorruptFloat(rb.mass);
        rb.drag = CorruptFloat(rb.drag);
        rb.angularDrag = CorruptFloat(rb.angularDrag);
        rb.velocity = CorruptVector3(rb.velocity);
        rb.angularVelocity = CorruptVector3(rb.angularVelocity);
        rb.maxAngularVelocity = CorruptFloat(rb.maxAngularVelocity);
    }

    private void CorruptAudioSource(AudioSource a)
    {
        a.volume = Mathf.Clamp01(CorruptFloat(a.volume));
        a.pitch = Mathf.Clamp(CorruptFloat(a.pitch), -3f, 3f);
        a.spatialBlend = Mathf.Clamp01(CorruptFloat(a.spatialBlend));
        a.minDistance = Mathf.Max(0f, CorruptFloat(a.minDistance));
        a.maxDistance = Mathf.Max(0f, CorruptFloat(a.maxDistance));
        a.dopplerLevel = CorruptFloat(a.dopplerLevel);
        a.spread = Mathf.Clamp(CorruptFloat(a.spread), 0f, 360f);
        if (a.clip != null && UnityEngine.Random.value < 0.5f) a.clip = GetRandomAsset(a.clip);
    }

    private void CorruptNavMeshAgent(NavMeshAgent agent)
    {
        agent.speed = CorruptFloat(agent.speed);
        agent.angularSpeed = CorruptFloat(agent.angularSpeed);
        agent.acceleration = CorruptFloat(agent.acceleration);
        agent.stoppingDistance = Mathf.Max(0f, CorruptFloat(agent.stoppingDistance));
        agent.radius = Mathf.Max(0.01f, CorruptFloat(agent.radius));
        agent.height = Mathf.Max(0.01f, CorruptFloat(agent.height));
    }

    private void CorruptRenderer(Renderer rend)
    {
        Material[] mats = rend.materials;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null) continue;
            if (CorruptedPlugin.CorruptMaterials.Value && UnityEngine.Random.value < CorruptedPlugin.MaterialReplacementChance.Value)
            {
                Material newMat = GetRandomMaterial(mats[i]);
                if (newMat != null) { mats[i] = newMat; continue; }
            }

            for (int j = 0; j < mats[i].shader.GetPropertyCount(); j++)
            {
                if (mats[i].shader.GetPropertyType(j) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;
                string prop = mats[i].shader.GetPropertyName(j);
                if (prop.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0)
                    mats[i].SetColor(prop, CorruptColor(mats[i].GetColor(prop)));
            }
        }
        rend.materials = mats;
    }

    private void CorruptSpriteRenderer(SpriteRenderer sr)
    {
        if (CorruptedPlugin.CorruptSprites.Value && sr.sprite != null && UnityEngine.Random.value < CorruptedPlugin.SpriteReplacementChance.Value) sr.sprite = GetRandomAsset(sr.sprite);
        sr.color = CorruptColor(sr.color);
        if (CorruptedPlugin.CorruptMaterials.Value && sr.sharedMaterial != null && UnityEngine.Random.value < CorruptedPlugin.MaterialReplacementChance.Value) sr.sharedMaterial = GetRandomMaterial(sr.sharedMaterial);
    }

    private void CorruptMeshFilter(MeshFilter mf)
    {
        if (mf.sharedMesh == null || mf.mesh.vertexCount == 0) return;
        Vector3[] verts = mf.mesh.vertices;
        float limit = CorruptedPlugin.MeshVertexCorruption.Value;
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] += new Vector3(UnityEngine.Random.Range(-limit, limit), UnityEngine.Random.Range(-limit, limit), UnityEngine.Random.Range(-limit, limit));
        }
        mf.mesh.vertices = verts;
        mf.mesh.RecalculateBounds();
        mf.mesh.RecalculateNormals();
    }

    private void CorruptCollider(Collider c)
    {
        if (c is BoxCollider bc) { bc.center = CorruptVector3(bc.center); bc.size = CorruptVector3(bc.size); }
        else if (c is SphereCollider sc) { sc.center = CorruptVector3(sc.center); sc.radius = Mathf.Max(0.001f, CorruptFloat(sc.radius)); }
        else if (c is CapsuleCollider cc) { cc.center = CorruptVector3(cc.center); cc.radius = Mathf.Max(0.001f, CorruptFloat(cc.radius)); cc.height = Mathf.Max(0.001f, CorruptFloat(cc.height)); }
    }

    private void CorruptCollider2D(Collider2D c)
    {
        if (c is BoxCollider2D bc) { bc.offset = CorruptVector2(bc.offset); bc.size = CorruptVector2(bc.size); }
        else if (c is CircleCollider2D cc) { cc.offset = CorruptVector2(cc.offset); cc.radius = Mathf.Max(0.001f, CorruptFloat(cc.radius)); }
        else if (c is CapsuleCollider2D cap) { cap.offset = CorruptVector2(cap.offset); cap.size = CorruptVector2(cap.size); }
    }

    private void CorruptTMP(TMP_Text text)
    {
        text.fontSize = CorruptFloat(text.fontSize);
        text.characterSpacing = CorruptFloat(text.characterSpacing);
        text.wordSpacing = CorruptFloat(text.wordSpacing);
        text.lineSpacing = CorruptFloat(text.lineSpacing);
        text.margin = CorruptVector4(text.margin);
        text.color = CorruptColor(text.color);
        if (UnityEngine.Random.value < 0.35f) text.text = CorruptString(text.text);
    }

    private string CorruptString(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        char[] arr = input.ToCharArray();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        for (int i = 0, limit = UnityEngine.Random.Range(1, Mathf.Max(2, arr.Length / 3)); i < limit; i++)
            arr[UnityEngine.Random.Range(0, arr.Length)] = UnityEngine.Random.value < 0.5f ? chars[UnityEngine.Random.Range(0, chars.Length)] : ' ';
        return new string(arr);
    }

    private void CorruptImage(Image img)
    {
        img.fillAmount = Mathf.Clamp01(CorruptFloat(img.fillAmount));
        if (CorruptedPlugin.CorruptSprites.Value && img.sprite != null && UnityEngine.Random.value < CorruptedPlugin.SpriteReplacementChance.Value) img.sprite = GetRandomAsset(img.sprite);
        img.color = CorruptColor(img.color);
        if (CorruptedPlugin.CorruptMaterials.Value && img.material != null && UnityEngine.Random.value < CorruptedPlugin.MaterialReplacementChance.Value) img.material = GetRandomMaterial(img.material, true);
    }

    private void CorruptRawImage(RawImage img)
    {
        img.uvRect = new Rect(CorruptVector2(img.uvRect.position), CorruptVector2(img.uvRect.size));
        img.color = CorruptColor(img.color);
        if (CorruptedPlugin.CorruptMaterials.Value && img.material != null && UnityEngine.Random.value < CorruptedPlugin.MaterialReplacementChance.Value) img.material = GetRandomMaterial(img.material, true);
    }

    private void CorruptSlider(Slider sld)
    {
        sld.minValue = CorruptFloat(sld.minValue);
        sld.maxValue = CorruptFloat(sld.maxValue);
        if (sld.minValue > sld.maxValue) (sld.minValue, sld.maxValue) = (sld.maxValue, sld.minValue);
        sld.value = Mathf.Clamp(CorruptFloat(sld.value), sld.minValue, sld.maxValue);
    }

    private void CorruptRectTransform(RectTransform rect)
    {
        rect.anchoredPosition = CorruptVector2(rect.anchoredPosition);
        rect.sizeDelta = CorruptVector2(rect.sizeDelta);
        rect.pivot = new Vector2(Mathf.Clamp01(CorruptFloat(rect.pivot.x)), Mathf.Clamp01(CorruptFloat(rect.pivot.y)));
    }

    private void CorruptScript(MonoBehaviour script)
    {
        foreach (FieldInfo field in script.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(f => !f.IsInitOnly && !f.IsLiteral))
        {
            try
            {
                object obj = CorruptValue(field.GetValue(script), field.FieldType);
                if (obj != null) field.SetValue(script, obj);
            }
            catch (Exception ex) { Debug.LogWarning($"Couldn't corrupt {script.GetType().Name}.{field.Name}: {ex.Message}"); }
        }
    }

    private object CorruptValue(object val, Type type)
    {
        if (val == null) return null;
        if (type == typeof(float)) return CorruptFloat((float)val);
        if (type == typeof(double)) return CorruptDouble((double)val);
        if (type == typeof(int)) return CorruptInt((int)val);
        if (type == typeof(long)) return CorruptLong((long)val);
        if (type == typeof(short)) return (short)Mathf.Clamp(CorruptInt((short)val), -32768, 32767);
        if (type == typeof(byte)) return (byte)Mathf.Clamp(CorruptInt((byte)val), 0, 255);
        if (type == typeof(Vector2)) return CorruptVector2((Vector2)val);
        if (type == typeof(Vector3)) return CorruptVector3((Vector3)val);
        if (type == typeof(Vector4)) return CorruptVector4((Vector4)val);
        if (type == typeof(Quaternion)) return Quaternion.Euler(CorruptVector3(((Quaternion)val).eulerAngles));
        if (type == typeof(Color)) return CorruptColor((Color)val);
        if (type == typeof(AudioClip)) return GetRandomAsset((AudioClip)val);
        if (type == typeof(Sprite)) return CorruptedPlugin.CorruptSprites.Value ? GetRandomAsset((Sprite)val) : val;
        if (type == typeof(Material)) return CorruptedPlugin.CorruptMaterials.Value ? GetRandomMaterial((Material)val) : val;

        if (type.IsArray && (type.GetElementType() == typeof(Sprite) || type.GetElementType() == typeof(Material) || type.GetElementType() == typeof(AudioClip)))
        {
            Array arr = (Array)val;
            Array newArr = Array.CreateInstance(type.GetElementType(), arr.Length);
            for (int i = 0; i < arr.Length; i++) newArr.SetValue(CorruptValue(arr.GetValue(i), type.GetElementType()), i);
            return newArr;
        }
        return val;
    }

    private T GetRandomAsset<T>(T current = null) where T : UnityEngine.Object
    {
        var assets = Resources.FindObjectsOfTypeAll<T>().Where(a => a != null && a != current).ToArray();
        return assets.Length > 0 ? assets[UnityEngine.Random.Range(0, assets.Length)] : current;
    }

    private Material GetRandomMaterial(Material current = null, bool uiMaterial = false)
    {
        Texture2D tex = GetRandomAsset<Texture2D>();
        if (tex == null) return current;

        Shader shader = Shader.Find(uiMaterial ? "UI/Default" : "Legacy Shaders/Transparent/Diffuse");
        if (shader == null) return current;

        Texture2D newTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false) { name = tex.name + "_CorruptedCopy", filterMode = FilterMode.Point, anisoLevel = 0 };
        try { newTex.SetPixels(tex.GetPixels()); newTex.Apply(false, false); }
        catch { UnityEngine.Object.Destroy(newTex); return current; }

        return new Material(shader) { name = "Corrupted_" + tex.name, mainTexture = newTex };
    }
}
