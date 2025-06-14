using System.Collections;
using System.Text;
using UnityEngine;

namespace minyee2913.Utils
{
    public class Kits
    {
        public static IEnumerator MoveInParabola(Transform objTransform, float duration, Vector3 target, float parabolaHeight)
        {
            Vector3 start = objTransform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                Vector3 horizontal = Vector3.Lerp(start, target, t);

                float arc = Mathf.Sin(Mathf.PI * t) * parabolaHeight;

                float height = Mathf.Lerp(start.y, target.y, t) + arc;
                objTransform.position = new Vector3(horizontal.x, height, horizontal.z);

                yield return null;
            }

            objTransform.position = target;
        }

        public static string InsertLineBreaks(string input, int maxCharsPerLine)
        {
            StringBuilder sb = new StringBuilder();
            int count = 0;

            foreach (char c in input)
            {
                sb.Append(c);
                count++;

                if (count >= maxCharsPerLine && char.IsWhiteSpace(c))
                {
                    sb.Append('\n');
                    count = 0;
                }
            }

            return sb.ToString();
        }
    }
}
