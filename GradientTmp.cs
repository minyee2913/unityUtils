using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GradientTmp : MonoBehaviour
{
    TMP_Text image;
    [SerializeField]
    List<Color> colors = new();
    [SerializeField]
    float delay;
    [SerializeField]
    bool useRealtime;
    int index;
    float time;
    void Start()
    {
        image = GetComponent<TMP_Text>();
    }

    // Update is called once per frame
    void Update()
    {
        if (useRealtime)
            time += Time.unscaledDeltaTime;
        else
        {
            time += Time.deltaTime;
        }

        if (colors.Count > 0)
        {
            image.color = Color.Lerp(image.color, colors[index], time / delay);

            if (time >= delay)
            {
                index++;
                time = 0;

                if (index >= colors.Count)
                {
                    index = 0;
                }
            }
        }
    }
}
