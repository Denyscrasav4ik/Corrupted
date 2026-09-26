using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Corrupted;

[BepInPlugin("denyscrasav4ik.thedumbfactory.corrupted", "Corrupted", "1.2.0")]
public class CorruptedPlugin : BaseUnityPlugin
{
    public static ConfigEntry<float> CorruptionInterval, MinMultiplier, MaxMultiplier, ZeroReplacementMin, ZeroReplacementMax, MeshVertexCorruption, MaterialReplacementChance, SpriteReplacementChance, AnimatorCorruptionChance, RandomMethodChance;
    public static ConfigEntry<int> CorruptionAmount;
    public static ConfigEntry<bool> AllowNegativeValues, CorruptMeshFilters, CorruptSprites, CorruptMaterials, IgnoreUnityMethods, ExcludeBuiltInShaderProperties;
    public static ConfigEntry<string> ExcludedNamesConfig;
    public static HashSet<string> ExcludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        CorruptionInterval = Config.Bind("Settings", "CorruptionInterval", 1f, "Time in seconds between corruption cycles.");
        CorruptionAmount = Config.Bind("Settings", "CorruptionAmount", 1, "Amount of objects to corrupt per cycle.");
        MinMultiplier = Config.Bind("Settings", "MinMultiplier", 0f, "Minimum random intensity multiplier.");
        MaxMultiplier = Config.Bind("Settings", "MaxMultiplier", 2f, "Maximum random intensity multiplier.");
        AllowNegativeValues = Config.Bind("Settings", "AllowNegativeValues", false, "Whether negative multipliers are allowed.");
        RandomMethodChance = Config.Bind("Settings", "RandomMethodChance", 0.5f, "Chance for a random method to be invoked on a script.");
        IgnoreUnityMethods = Config.Bind("Settings", "IgnoreUnityMethods", true, "Whether methods inherited from UnityEngine.Object/MonoBehaviour should be ignored.");
        ExcludeBuiltInShaderProperties = Config.Bind("Settings", "ExcludeBuiltInShaderProperties", true, "Whether built-in Unity shader properties (starting with 'unity_') should be excluded from corruption.");
        ExcludedNamesConfig = Config.Bind("Settings", "ExcludedNames", "InputManager,Steam,UniversalAdditionalCameraData,UniversalAdditionalLightData", "Comma-separated list of script or method names to exclude from corruption.");
        UpdateExcludedNames();
        ExcludedNamesConfig.SettingChanged += (sender, args) => UpdateExcludedNames();

        ZeroReplacementMin = Config.Bind("Zero Value Corruption", "ZeroReplacementMin", 0.1f, "Minimum absolute value used when corrupting a value that is currently 0.");
        ZeroReplacementMax = Config.Bind("Zero Value Corruption", "ZeroReplacementMax", 2f, "Maximum absolute value used when corrupting a value that is currently 0.");

        CorruptMeshFilters = Config.Bind("Mesh Corruption", "CorruptMeshFilters", true, "Whether MeshFilters can have their mesh vertices corrupted.");
        MeshVertexCorruption = Config.Bind("Mesh Corruption", "MeshVertexCorruption", 0.5f, "How strongly mesh vertices can be moved.");

        CorruptSprites = Config.Bind("Asset Corruption", "CorruptSprites", true, "Whether sprites can randomly be replaced.");
        CorruptMaterials = Config.Bind("Asset Corruption", "CorruptMaterials", true, "Whether materials can randomly be replaced.");
        MaterialReplacementChance = Config.Bind("Asset Corruption", "MaterialReplacementChance", 0.5f, "Chance for a material to be replaced.");
        SpriteReplacementChance = Config.Bind("Asset Corruption", "SpriteReplacementChance", 0.5f, "Chance for a sprite to be replaced.");
        AnimatorCorruptionChance = Config.Bind("Asset Corruption", "AnimatorCorruptionChance", 0.5f, "Chance for an Animator to be corrupted.");

        DontDestroyOnLoad(new GameObject("Corrupted_Instance", typeof(SceneCorruptor)));
    }

    private void UpdateExcludedNames()
    {
        ExcludedNames.Clear();
        foreach (var name in ExcludedNamesConfig.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            ExcludedNames.Add(name.Trim());
        }
    }
}

public class SceneCorruptor : MonoBehaviour
{
    private float minMultiplier => CorruptedPlugin.AllowNegativeValues.Value ? -CorruptedPlugin.MaxMultiplier.Value : CorruptedPlugin.MinMultiplier.Value;
    private float maxMultiplier => CorruptedPlugin.MaxMultiplier.Value;

    private void Start() => StartCoroutine(CorruptionLoop());

    private IEnumerator CorruptionLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(CorruptedPlugin.CorruptionInterval.Value);
            var targets = FindObjectsOfType<GameObject>()
                .Where(obj => obj != null && obj != gameObject && obj.scene.IsValid() && obj.scene.isLoaded)
                .ToArray();

            if (targets.Length > 0)
            {
                for (int i = 0; i < CorruptedPlugin.CorruptionAmount.Value; i++)
                    CorruptObject(targets[UnityEngine.Random.Range(0, targets.Length)]);
            }
        }
    }

    private void CorruptObject(GameObject obj)
    {
        CorruptTransform(obj.transform);
        if (obj.TryGetComponent(out Rigidbody rb)) CorruptRigidbody(rb);
        if (obj.TryGetComponent(out AudioSource audio)) CorruptAudioSource(audio);
        if (obj.TryGetComponent(out NavMeshAgent agent)) CorruptNavMeshAgent(agent);
        if (obj.TryGetComponent(out Renderer rend)) CorruptRenderer(rend);
        if (obj.TryGetComponent(out Animator anim) && UnityEngine.Random.value < CorruptedPlugin.AnimatorCorruptionChance.Value) CorruptAnimator(anim);
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
            CorruptMaterialProperties(mats[i]);
        }
        rend.materials = mats;
    }

    private void CorruptMaterialProperties(Material mat)
    {
        if (mat == null || mat.shader == null) return;
        Shader shader = mat.shader;
        for (int i = 0; i < shader.GetPropertyCount(); i++)
        {
            string propName = shader.GetPropertyName(i);
            if (CorruptedPlugin.ExcludeBuiltInShaderProperties.Value && propName.StartsWith("unity_")) continue;

            int id = shader.GetPropertyNameId(i);
            switch (shader.GetPropertyType(i))
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    mat.SetColor(id, CorruptColor(mat.GetColor(id)));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    mat.SetVector(id, CorruptVector4(mat.GetVector(id)));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                case UnityEngine.Rendering.ShaderPropertyType.Range:
                    mat.SetFloat(id, CorruptFloat(mat.GetFloat(id)));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    if (UnityEngine.Random.value < 0.5f)
                    {
                        Texture tex = GetRandomAsset<Texture2D>();
                        if (tex != null)
                        {
                            mat.SetTexture(id, tex);
                            mat.SetTextureScale(id, CorruptVector2(mat.GetTextureScale(id)));
                            mat.SetTextureOffset(id, CorruptVector2(mat.GetTextureOffset(id)));
                        }
                    }
                    break;
            }
        }
    }

    private void CorruptSpriteRenderer(SpriteRenderer sr)
    {
        if (CorruptedPlugin.CorruptSprites.Value && sr.sprite != null && UnityEngine.Random.value < CorruptedPlugin.SpriteReplacementChance.Value) sr.sprite = GetRandomAsset(sr.sprite);
        sr.color = CorruptColor(sr.color);
        if (CorruptedPlugin.CorruptMaterials.Value && sr.sharedMaterial != null)
        {
            if (UnityEngine.Random.value < CorruptedPlugin.MaterialReplacementChance.Value)
                sr.sharedMaterial = GetRandomMaterial(sr.sharedMaterial);
            else
                CorruptMaterialProperties(sr.sharedMaterial);
        }
    }

    private void CorruptMeshFilter(MeshFilter mf)
    {
        if (mf.sharedMesh == null || mf.mesh.vertexCount == 0) return;
        Vector3[] verts = mf.mesh.vertices;
        float limit = CorruptedPlugin.MeshVertexCorruption.Value;
        for (int i = 0; i < verts.Length; i++)
            verts[i] += new Vector3(UnityEngine.Random.Range(-limit, limit), UnityEngine.Random.Range(-limit, limit), UnityEngine.Random.Range(-limit, limit));
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
        if (CorruptedPlugin.CorruptMaterials.Value && img.material != null)
        {
            if (UnityEngine.Random.value < CorruptedPlugin.MaterialReplacementChance.Value)
                img.material = GetRandomMaterial(img.material, true);
            else
                CorruptMaterialProperties(img.material);
        }
    }

    private void CorruptRawImage(RawImage img)
    {
        img.uvRect = new Rect(CorruptVector2(img.uvRect.position), CorruptVector2(img.uvRect.size));
        img.color = CorruptColor(img.color);
        if (CorruptedPlugin.CorruptMaterials.Value && img.material != null)
        {
            if (UnityEngine.Random.value < CorruptedPlugin.MaterialReplacementChance.Value)
                img.material = GetRandomMaterial(img.material, true);
            else
                CorruptMaterialProperties(img.material);
        }
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

    private void CorruptAnimator(Animator anim)
    {
        if (UnityEngine.Random.value < 0.5f)
        {
            Animator other = GetRandomAnimator(anim);
            if (other != null && other.runtimeAnimatorController != null)
            {
                anim.runtimeAnimatorController = other.runtimeAnimatorController;
                return;
            }
        }
        anim.speed = Mathf.Clamp(CorruptFloat(anim.speed), -5f, 5f);
    }

    private void CorruptScript(MonoBehaviour script)
    {
        if (script is EventSystem || script is StandaloneInputModule) return;
        Type type = script.GetType();
        if (CorruptedPlugin.ExcludedNames.Contains(type.Name)) return;
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(f => !f.IsInitOnly && !f.IsLiteral))
        {
            try
            {
                if (field.FieldType == typeof(string) && field.Name.IndexOf("scene", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                object val = field.GetValue(script);
                object corrupted = CorruptValue(val, field.FieldType);
                if (corrupted != null) field.SetValue(script, corrupted);
            }
            catch (Exception ex) { Debug.LogWarning($"Couldn't corrupt {type.Name}.{field.Name}: {ex.Message}"); }
        }
        if (UnityEngine.Random.value < CorruptedPlugin.RandomMethodChance.Value) InvokeRandomMethod(script);
    }

    private void InvokeRandomMethod(MonoBehaviour script)
    {
        Type type = script.GetType();
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m != null && !m.IsSpecialName && (!CorruptedPlugin.IgnoreUnityMethods.Value || m.DeclaringType == type) && !m.IsGenericMethodDefinition && !m.IsAbstract)
            .Where(m => !CorruptedPlugin.ExcludedNames.Contains(m.Name))
            .Where(m => m.GetParameters().All(p => !p.ParameterType.IsByRef && CanGenerateRandomValue(p.ParameterType)))
            .ToArray();

        if (methods.Length == 0) return;
        MethodInfo method = methods[UnityEngine.Random.Range(0, methods.Length)];
        object[] args = method.GetParameters().Select(p => GenerateRandomValue(p.ParameterType)).ToArray();

        try { method.Invoke(script, args); }
        catch (Exception ex) { Debug.LogWarning($"[SceneCorruptor] Couldn't invoke {type.Name}.{method.Name}: {ex.Message}"); }
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
        if (type == typeof(uint)) return (uint)Mathf.Max(0, Mathf.RoundToInt((float)(uint)val * RandomMultiplier()));
        if (type == typeof(bool)) return UnityEngine.Random.value < 0.5f ? !(bool)val : val;
        if (type == typeof(string)) return CorruptString((string)val);
        if (type == typeof(Vector2)) return CorruptVector2((Vector2)val);
        if (type == typeof(Vector3)) return CorruptVector3((Vector3)val);
        if (type == typeof(Vector4)) return CorruptVector4((Vector4)val);
        if (type == typeof(Quaternion)) return Quaternion.Euler(CorruptVector3(((Quaternion)val).eulerAngles));
        if (type == typeof(Color)) return CorruptColor((Color)val);
        if (type == typeof(AudioClip)) return GetRandomAsset((AudioClip)val);
        if (type == typeof(Sprite)) return CorruptedPlugin.CorruptSprites.Value ? GetRandomAsset((Sprite)val) : val;
        if (type == typeof(Material)) return CorruptedPlugin.CorruptMaterials.Value ? GetRandomMaterial((Material)val) : val;

        if (type.IsArray)
        {
            Array arr = (Array)val;
            Type elemType = type.GetElementType();
            Array newArr = Array.CreateInstance(elemType, arr.Length);
            for (int i = 0; i < arr.Length; i++) newArr.SetValue(CorruptValue(arr.GetValue(i), elemType), i);
            return newArr;
        }
        return val;
    }

    private bool CanGenerateRandomValue(Type t) =>
        t != typeof(void) && !t.IsByRef && (
            t == typeof(bool) || t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort) ||
            t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong) || t == typeof(float) ||
            t == typeof(double) || t == typeof(decimal) || t == typeof(char) || t == typeof(string) || t.IsEnum ||
            t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4) || t == typeof(Vector2Int) ||
            t == typeof(Vector3Int) || t == typeof(Quaternion) || t == typeof(Color) || t == typeof(Color32) ||
            t == typeof(Rect) || typeof(UnityEngine.Object).IsAssignableFrom(t) || (t.IsArray && CanGenerateRandomValue(t.GetElementType()))
        );

    private object GenerateRandomValue(Type t)
    {
        if (t == typeof(bool)) return UnityEngine.Random.value < 0.5f;
        if (t == typeof(byte)) return (byte)UnityEngine.Random.Range(0, 256);
        if (t == typeof(sbyte)) return (sbyte)UnityEngine.Random.Range(-128, 128);
        if (t == typeof(short)) return (short)UnityEngine.Random.Range(-32768, 32767);
        if (t == typeof(ushort)) return (ushort)UnityEngine.Random.Range(0, 65535);
        if (t == typeof(int)) return UnityEngine.Random.Range(-100000, 100001);
        if (t == typeof(uint)) return (uint)UnityEngine.Random.Range(0, int.MaxValue);
        if (t == typeof(long)) return (long)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        if (t == typeof(ulong)) return (ulong)UnityEngine.Random.Range(0, int.MaxValue);
        if (t == typeof(float)) return UnityEngine.Random.Range(-1000f, 1000f);
        if (t == typeof(double)) return (double)UnityEngine.Random.Range(-1000f, 1000f);
        if (t == typeof(decimal)) return (decimal)UnityEngine.Random.Range(-1000f, 1000f);
        if (t == typeof(char)) return (char)UnityEngine.Random.Range(32, 127);
        if (t == typeof(string)) return CorruptString("SampleStringText");
        if (t.IsEnum) { Array v = Enum.GetValues(t); return v.Length > 0 ? v.GetValue(UnityEngine.Random.Range(0, v.Length)) : Activator.CreateInstance(t); }
        if (t == typeof(Vector2)) return new Vector2(UnityEngine.Random.Range(-100f, 100f), UnityEngine.Random.Range(-100f, 100f));
        if (t == typeof(Vector3)) return new Vector3(UnityEngine.Random.Range(-100f, 100f), UnityEngine.Random.Range(-100f, 100f), UnityEngine.Random.Range(-100f, 100f));
        if (t == typeof(Vector4)) return new Vector4(UnityEngine.Random.Range(-100f, 100f), UnityEngine.Random.Range(-100f, 100f), UnityEngine.Random.Range(-100f, 100f), UnityEngine.Random.Range(-100f, 100f));
        if (t == typeof(Vector2Int)) return new Vector2Int(UnityEngine.Random.Range(-100, 101), UnityEngine.Random.Range(-100, 101));
        if (t == typeof(Vector3Int)) return new Vector3Int(UnityEngine.Random.Range(-100, 101), UnityEngine.Random.Range(-100, 101), UnityEngine.Random.Range(-100, 101));
        if (t == typeof(Quaternion)) return Quaternion.Euler(UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f));
        if (t == typeof(Color)) return new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
        if (t == typeof(Color32)) return new Color32((byte)UnityEngine.Random.Range(0, 256), (byte)UnityEngine.Random.Range(0, 256), (byte)UnityEngine.Random.Range(0, 256), (byte)UnityEngine.Random.Range(0, 256));
        if (t == typeof(Rect)) return new Rect(UnityEngine.Random.Range(-100f, 100f), UnityEngine.Random.Range(-100f, 100f), UnityEngine.Random.Range(-100f, 100f), UnityEngine.Random.Range(-100f, 100f));
        if (typeof(UnityEngine.Object).IsAssignableFrom(t))
        {
            var assets = Resources.FindObjectsOfTypeAll(t).Where(a => a != null).ToArray();
            return assets.Length > 0 ? assets[UnityEngine.Random.Range(0, assets.Length)] : null;
        }
        if (t.IsArray)
        {
            Type elem = t.GetElementType();
            int len = UnityEngine.Random.Range(0, 6);
            Array arr = Array.CreateInstance(elem, len);
            for (int i = 0; i < len; i++) arr.SetValue(GenerateRandomValue(elem), i);
            return arr;
        }
        return null;
    }

    private T GetRandomAsset<T>(T current = null) where T : UnityEngine.Object
    {
        var assets = Resources.FindObjectsOfTypeAll<T>().Where(a => a != null && a != current).ToArray();
        return assets.Length > 0 ? assets[UnityEngine.Random.Range(0, assets.Length)] : current;
    }

    private Animator GetRandomAnimator(Animator current)
    {
        var animators = Resources.FindObjectsOfTypeAll<Animator>()
            .Where(a => a != null && a != current && a.gameObject.scene.IsValid() && a.gameObject.scene.isLoaded && a.runtimeAnimatorController != null && a.gameObject != gameObject)
            .ToArray();
        return animators.Length > 0 ? animators[UnityEngine.Random.Range(0, animators.Length)] : null;
    }

    private Material GetRandomMaterial(Material current = null, bool uiMaterial = false)
    {
        Shader[] shaders = Resources.FindObjectsOfTypeAll<Shader>()
            .Where(s => s != null && !s.name.StartsWith("Hidden/") && (!uiMaterial || s.name.ToLower().Contains("ui"))).ToArray();
        if (shaders.Length == 0) return current;

        Shader shader = shaders[UnityEngine.Random.Range(0, shaders.Length)];
        try
        {
            Material mat = new Material(shader) { name = "Corrupted_" + shader.name };
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string propName = shader.GetPropertyName(i);
                if (CorruptedPlugin.ExcludeBuiltInShaderProperties.Value && propName.StartsWith("unity_")) continue;

                int id = shader.GetPropertyNameId(i);
                switch (shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        mat.SetColor(id, new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        mat.SetVector(id, new Vector4(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f)));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        mat.SetFloat(id, UnityEngine.Random.Range(-5f, 5f));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        Texture tex = GetRandomAsset<Texture2D>();
                        if (tex != null)
                        {
                            mat.SetTexture(id, tex);
                            mat.SetTextureScale(id, new Vector2(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f)));
                            mat.SetTextureOffset(id, new Vector2(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f)));
                        }
                        break;
                }
            }
            mat.renderQueue = UnityEngine.Random.Range(0, 5000);
            mat.doubleSidedGI = UnityEngine.Random.value < 0.5f;
            mat.enableInstancing = UnityEngine.Random.value < 0.5f;

            foreach (string kw in mat.shaderKeywords)
            {
                if (UnityEngine.Random.value < 0.5f)
                {
                    if (mat.IsKeywordEnabled(kw)) mat.DisableKeyword(kw);
                    else mat.EnableKeyword(kw);
                }
            }
            return mat;
        }
        catch { return current; }
    }
}
