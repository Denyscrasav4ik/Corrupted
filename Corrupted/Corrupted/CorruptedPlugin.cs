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

[BepInPlugin("denyscrasav4ik.thedumbfactory.corrupted", "Corrupted", "1.0.0")]
public class CorruptedPlugin : BaseUnityPlugin
{
    public static ConfigEntry<float> CorruptionInterval { get; private set; } = null!;
    public static ConfigEntry<float> MinMultiplier { get; private set; } = null!;
    public static ConfigEntry<float> MaxMultiplier { get; private set; } = null!;

    private void Awake()
    {
        CorruptionInterval = Config.Bind("Settings", "CorruptionInterval", 1f, "Time in seconds between corruption cycles.");
        MinMultiplier = Config.Bind("Settings", "MinMultiplier", 1f, "Minimum random intensity multiplier.");
        MaxMultiplier = Config.Bind("Settings", "MaxMultiplier", 2f, "Maximum random intensity multiplier.");

        var corruptorObject = new GameObject("Corrupted_Instance");
        corruptorObject.AddComponent<SceneCorruptor>();
        DontDestroyOnLoad(corruptorObject);
    }
}

public class SceneCorruptor : MonoBehaviour
{
    public float corruptionInterval => CorruptedPlugin.CorruptionInterval.Value;
    public float minMultiplier => CorruptedPlugin.MinMultiplier.Value;
    public float maxMultiplier => CorruptedPlugin.MaxMultiplier.Value;

    private void Start() => StartCoroutine(CorruptionLoop());

    private IEnumerator CorruptionLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(corruptionInterval);
            CorruptRandomObject();
        }
    }

    private void CorruptRandomObject()
    {
        var source = FindObjectsOfType<GameObject>().Where(obj => obj != gameObject).ToArray();
        if (source.Length > 0)
            CorruptObject(source[UnityEngine.Random.Range(0, source.Length)]);
    }

    private float RandMult() => UnityEngine.Random.Range(minMultiplier, maxMultiplier);

    private Color RandColor(Color c) => new(c.r * RandMult(), c.g * RandMult(), c.b * RandMult(), c.a * RandMult());

    private void CorruptObject(GameObject obj)
    {
        CorruptTransform(obj.transform);

        if (obj.TryGetComponent(out Rigidbody rb)) CorruptRigidbody(rb);
        if (obj.TryGetComponent(out AudioSource audio)) CorruptAudioSource(audio);
        if (obj.TryGetComponent(out NavMeshAgent agent)) CorruptNavMeshAgent(agent);
        if (obj.TryGetComponent(out Renderer rend)) CorruptRenderer(rend);
        if (obj.TryGetComponent(out Collider col)) CorruptCollider(col);
        if (obj.TryGetComponent(out Collider2D col2D)) CorruptCollider2D(col2D);
        if (obj.TryGetComponent(out TMP_Text tmp)) CorruptTMP(tmp);
        if (obj.TryGetComponent(out Image img)) CorruptImage(img);
        if (obj.TryGetComponent(out RawImage rawImg)) CorruptRawImage(rawImg);
        if (obj.TryGetComponent(out Slider slider)) CorruptSlider(slider);
        if (obj.TryGetComponent(out Scrollbar scroll)) CorruptScrollbar(scroll);
        if (obj.TryGetComponent(out Toggle toggle)) CorruptToggle(toggle);
        if (obj.TryGetComponent(out CanvasGroup cg)) CorruptCanvasGroup(cg);
        if (obj.TryGetComponent(out RectTransform rt)) CorruptRectTransform(rt);

        foreach (var mb in obj.GetComponents<MonoBehaviour>().Where(m => m != null && m != this))
            CorruptScript(mb);
    }

    private void CorruptTransform(Transform t)
    {
        t.localPosition *= RandMult();
        t.localScale *= RandMult();
        t.localEulerAngles *= RandMult();
    }

    private void CorruptRigidbody(Rigidbody rb)
    {
        rb.mass *= RandMult();
        rb.drag *= RandMult();
        rb.angularDrag *= RandMult();
        rb.velocity *= RandMult();
        rb.angularVelocity *= RandMult();
        rb.maxAngularVelocity *= RandMult();
    }

    private void CorruptAudioSource(AudioSource a)
    {
        a.volume = Mathf.Clamp01(a.volume * RandMult());
        a.pitch = Mathf.Clamp(a.pitch * RandMult(), -3f, 3f);
        a.spatialBlend = Mathf.Clamp01(a.spatialBlend * RandMult());
        a.minDistance *= RandMult();
        a.maxDistance *= RandMult();
        a.dopplerLevel *= RandMult();
        a.spread *= RandMult();
    }

    private void CorruptNavMeshAgent(NavMeshAgent a)
    {
        a.speed *= RandMult();
        a.angularSpeed *= RandMult();
        a.acceleration *= RandMult();
        a.stoppingDistance *= RandMult();
        a.radius *= RandMult();
        a.height *= RandMult();
    }

    private void CorruptRenderer(Renderer r)
    {
        foreach (var mat in r.materials.Where(m => m != null && m.HasProperty("_TextureColor")))
        {
            Color current = mat.GetColor("_TextureColor");
            mat.SetColor("_TextureColor", RandColor(current));
        }
    }

    private void CorruptCollider(Collider col)
    {
        if (col is BoxCollider bc) { bc.center *= RandMult(); bc.size *= RandMult(); }
        else if (col is SphereCollider sc) { sc.center *= RandMult(); sc.radius *= RandMult(); }
        else if (col is CapsuleCollider cc) { cc.center *= RandMult(); cc.radius *= RandMult(); cc.height *= RandMult(); }
    }

    private void CorruptCollider2D(Collider2D col)
    {
        if (col is BoxCollider2D bc) { bc.offset *= RandMult(); bc.size *= RandMult(); }
        else if (col is CircleCollider2D cc) { cc.offset *= RandMult(); cc.radius *= RandMult(); }
        else if (col is CapsuleCollider2D cap) { cap.offset *= RandMult(); cap.size *= RandMult(); }
    }

    private void CorruptScript(MonoBehaviour script)
    {
        var fields = script.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            try
            {
                var val = CorruptValue(field.GetValue(script), field.FieldType);
                if (val != null) field.SetValue(script, val);
            }
            catch (Exception ex) { Debug.LogWarning($"Couldn't corrupt {script.GetType().Name}.{field.Name}: {ex.Message}"); }
        }
    }

    private object CorruptValue(object value, Type type)
    {
        if (value == null) return null!;
        float n = RandMult();

        if (type == typeof(float)) return (float)value * n;
        if (type == typeof(double)) return (double)value * n;
        if (type == typeof(int)) return Mathf.RoundToInt((int)value * n);
        if (type == typeof(long)) return (long)((long)value * n);
        if (type == typeof(short)) return (short)((short)value * n);
        if (type == typeof(byte)) return (byte)Mathf.Clamp(Mathf.RoundToInt((byte)value * n), 0, 255);
        if (type == typeof(Vector2)) return (Vector2)value * n;
        if (type == typeof(Vector3)) return (Vector3)value * n;
        if (type == typeof(Vector4)) return (Vector4)value * n;
        if (type == typeof(Quaternion)) return Quaternion.Euler(((Quaternion)value).eulerAngles * n);
        if (type == typeof(Color)) return RandColor((Color)value);
        return value;
    }

    private void CorruptTMP(TMP_Text text)
    {
        float n = RandMult();
        text.fontSize *= n;
        text.characterSpacing *= n;
        text.wordSpacing *= n;
        text.lineSpacing *= n;
        text.margin *= n;
        text.color = RandColor(text.color);
        if (UnityEngine.Random.value < 0.35f) text.text = CorruptString(text.text);
    }

    private string CorruptString(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        char[] arr = input.ToCharArray();
        int count = UnityEngine.Random.Range(1, Mathf.Max(2, arr.Length / 3));
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";

        for (int i = 0; i < count; i++)
        {
            int idx = UnityEngine.Random.Range(0, arr.Length);
            arr[idx] = UnityEngine.Random.value < 0.5f ? chars[UnityEngine.Random.Range(0, chars.Length)] : ' ';
        }
        return new string(arr);
    }

    private void CorruptImage(Image img)
    {
        img.fillAmount *= RandMult();
        img.color = RandColor(img.color);
    }

    private void CorruptRawImage(RawImage img)
    {
        img.uvRect = new Rect(img.uvRect.position * RandMult(), img.uvRect.size * RandMult());
        img.color = RandColor(img.color);
    }

    private void CorruptSlider(Slider slider)
    {
        slider.minValue *= RandMult();
        slider.maxValue *= RandMult();
        slider.value *= RandMult();

        if (slider.minValue > slider.maxValue)
            (slider.minValue, slider.maxValue) = (slider.maxValue, slider.minValue);

        slider.value = Mathf.Clamp(slider.value, slider.minValue, slider.maxValue);
    }

    private void CorruptScrollbar(Scrollbar scrollbar)
    {
        scrollbar.value = Mathf.Clamp01(scrollbar.value * RandMult());
        scrollbar.size = Mathf.Clamp01(scrollbar.size * RandMult());
    }

    private void CorruptToggle(Toggle toggle)
    {
        if (UnityEngine.Random.value < 0.5f) toggle.isOn = !toggle.isOn;
    }

    private void CorruptCanvasGroup(CanvasGroup group)
    {
        group.alpha *= RandMult();
        group.interactable = UnityEngine.Random.value > 0.3f;
        group.blocksRaycasts = UnityEngine.Random.value > 0.3f;
    }

    private void CorruptRectTransform(RectTransform rect)
    {
        rect.anchoredPosition *= RandMult();
        rect.sizeDelta *= RandMult();
        rect.pivot = new Vector2(Mathf.Clamp01(rect.pivot.x * RandMult()), Mathf.Clamp01(rect.pivot.y * RandMult()));
    }
}
