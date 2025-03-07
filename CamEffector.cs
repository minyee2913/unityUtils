using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace minyee2913.Utils {
    [RequireComponent(typeof(CinemachineCamera))]
    [RequireComponent(typeof(CinemachineCameraOffset))]
    [RequireComponent(typeof(CinemachineBasicMultiChannelPerlin))]
    public class CamEffector : MonoBehaviour
    {
        public CinemachineCamera cam;
        static List<CamEffector> effectors = new();
        public static CamEffector current {
            get {
                if (effectors.Count <= 0) {
                    return null;
                } else {
                    return effectors[effectors.Count - 1];
                }
            }
        }
        CinemachineCameraOffset offset;
        CinemachineBasicMultiChannelPerlin noise;
        const float frame = 30;
        float orSize_d;
        float dutch_d;
        IEnumerator dutchRoutine = null;
        IEnumerator offRoutine = null;

        void Awake()
        {
            cam = GetComponent<CinemachineCamera>();
            offset = GetComponent<CinemachineCameraOffset>();
            noise = GetComponent<CinemachineBasicMultiChannelPerlin>();

            noise.NoiseProfile = Resources.Load<NoiseSettings>("noise_profile/6D Wobble");
            noise.AmplitudeGain = 0;
            noise.FrequencyGain = 0;
        }

        void Start() {
            effectors.Add(this);
        }

        void OnDisable()
        {
            effectors.Remove(this);
        }

        void ClearRoutine(IEnumerator routine) {
            if (routine != null) {
                StopCoroutine(routine);

                routine = null;
            }
        }

        public void CloseUp(float orSize, float dutch, float dur = 0) {
            if (orSize < 0) {
                orSize += dutch_d;
            }
            ClearRoutine(dutchRoutine);
            dutchRoutine = _closeUp(orSize, dutch, dur);

            StartCoroutine(dutchRoutine);
        }
        public void CloseOut(float dur = 0) {
            ClearRoutine(dutchRoutine);
            dutchRoutine = _closeOut(dur);

            StartCoroutine(dutchRoutine);
        }
        public void Offset(Vector2 off, float dur = 0) {
            ClearRoutine(offRoutine);

            offRoutine = _offset(off, dur);

            StartCoroutine(offRoutine);
        }

        public void Shake(float strength = 1, float dur = 0.05f)
        {
            StartCoroutine(_shake(strength, dur));
        }

        IEnumerator _closeUp(float orSize, float dutch, float dur) {
            if (dur > 0) {
                float dSize = cam.Lens.OrthographicSize, dDutch = cam.Lens.Dutch;

                for (int i = 1; i <= frame; i++) {
                    cam.Lens.OrthographicSize = dSize - (dSize - orSize) / frame * i;
                    cam.Lens.Dutch = dDutch - (dDutch - dutch) / frame * i;

                    yield return new WaitForSeconds(dur / frame);
                }
            }

            cam.Lens.OrthographicSize = orSize;
            cam.Lens.Dutch = dutch;

            dutchRoutine = null;
        }

        IEnumerator _closeOut(float dur) {
            if (dur > 0) {
                float dSize = cam.Lens.OrthographicSize, dDutch = cam.Lens.Dutch;

                for (int i = 1; i <= frame; i++) {
                    cam.Lens.OrthographicSize = dSize + (orSize_d - dSize) / frame * i;
                    cam.Lens.Dutch = dDutch + (dutch_d - dDutch) / frame * i;

                    yield return new WaitForSeconds(dur / frame);
                }
            }
            
            cam.Lens.OrthographicSize = orSize_d;
            cam.Lens.Dutch = dutch_d;

            dutchRoutine = null;
        }

        IEnumerator _offset(Vector3 off, float dur = 0) {
            if (dur > 0) {
                Vector2 beforeOff = offset.Offset;

                for (int i = 1; i <= frame; i++) {
                    offset.Offset = new Vector3(
                        beforeOff.x - (beforeOff.x - off.x) / frame * i,
                        beforeOff.y - (beforeOff.y - off.y) / frame * i
                    );

                    yield return new WaitForSeconds(dur / frame);
                }
            }

            offset.Offset = off;

            offRoutine = null;
        }

        IEnumerator _shake(float strength, float dur)
        {
            noise.AmplitudeGain = strength;
            noise.FrequencyGain = strength;

            yield return new WaitForSeconds(dur);

            noise.AmplitudeGain = 0;
            noise.FrequencyGain = 0;
        }
    }

}