using System.Collections.Generic;
using UnityEngine;

public abstract class UIBasePanel : MonoBehaviour
{
    static List<UIBasePanel> opened = new();
    public bool IsUppestLayer {
        get {
            if (IsOpened) {
                if (opened.IndexOf(this) == opened.Count - 1) {
                    return true;
                }
            }
            return false;
        }
    }
    public bool IsOpened => opened.Contains(this);
    [SerializeField]
    bool openByScale;
    Vector2 scale;

    void Awake()
    {
        scale = transform.localScale;
    }

    public static void CloseAll() {
        foreach (UIBasePanel panel in opened) {
            panel.Close();
        }
    }

    public virtual void Close() {
        
    }

    void OnDestroy()
    {
        if (opened.Contains(this)) {
            opened.Remove(this);
        }
    }
}
