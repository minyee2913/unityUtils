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
        bool inCloseUp, inViewUp;
        float orSize_d;
        float view_d;
        float dutch_d;
        float dutch_view_d;
        IEnumerator dutchRoutine = null;
        IEnumerator offRoutine = null;
        Camera camera_;

        void Awake()
        {
            cam = GetComponent<CinemachineCamera>();
            offset = GetComponent<CinemachineCameraOffset>();
            noise = GetComponent<CinemachineBasicMultiChannelPerlin>();
            camera_ = GetComponent<Camera>();

            noise.NoiseProfile = Resources.Load<NoiseSettings>("noise_profile/6D Wobble");
            noise.AmplitudeGain = 0;
            noise.FrequencyGain = 0;

            orSize_d = cam.Lens.OrthographicSize;
            dutch_d = dutch_view_d = cam.Lens.Dutch;
            view_d = cam.Lens.FieldOfView;
        }

        void Start() {
            effectors.Add(this);
            noise.AmplitudeGain = 0;
            noise.FrequencyGain = 0;
        }

        void OnDisable()
        {
            effectors.Remove(this);
            
            noise.AmplitudeGain = 0;
            noise.FrequencyGain = 0;
        }

        void ClearRoutine(ref IEnumerator routine) {
            if (routine != null) {
                StopCoroutine(routine);

                routine = null;
            }
        }

        public void CloseUp(float orSize, float dutch, float dur = 0) {
            if (orSize < 0) {
                orSize += orSize_d;
            }
            ClearRoutine(ref dutchRoutine);
            dutchRoutine = _closeUp(orSize, dutch, dur);

            StartCoroutine(dutchRoutine);
        }
        public void ViewUp(float fov, float dutch, float dur = 0) {
            if (fov < 0) {
                fov += view_d;
            }
            ClearRoutine(ref dutchRoutine);
            dutchRoutine = _viewUp(fov, dutch, dur);

            StartCoroutine(dutchRoutine);
        }
        public void ViewOut(float dur = 0) {
            ClearRoutine(ref dutchRoutine);
            dutchRoutine = _viewOut(dur);

            StartCoroutine(dutchRoutine);
        }
        public void CloseOut(float dur = 0) {
            ClearRoutine(ref dutchRoutine);
            dutchRoutine = _closeOut(dur);

            StartCoroutine(dutchRoutine);
        }
        public void Offset(Vector2 off, float dur = 0) {
            ClearRoutine(ref offRoutine);

            offRoutine = _offset(off, dur);

            StartCoroutine(offRoutine);
        }

        public void Shake(float strength = 1, float dur = 0.05f)
        {
            StartCoroutine(_shake(strength, dur));
        }

        IEnumerator _closeUp(float orSize, float dutch, float dur) {
            if (!inCloseUp) {
                orSize_d = cam.Lens.OrthographicSize;
                dutch_d = cam.Lens.Dutch;

                inCloseUp = true;
            }

            if (dur > 0) {
                float dSize = cam.Lens.OrthographicSize;
                float dDutch = cam.Lens.Dutch;

                float elapsedTime = 0f;

                while (elapsedTime < dur) {
                    float t = elapsedTime / dur;
                    cam.Lens.OrthographicSize = Mathf.Lerp(dSize, orSize, t);
                    cam.Lens.Dutch = Mathf.Lerp(dDutch, dutch, t);

                    elapsedTime += Time.deltaTime;
                    yield return null;
                }

                
            }

            cam.Lens.OrthographicSize = orSize;
            cam.Lens.Dutch = dutch;

            dutchRoutine = null;
        }

        IEnumerator _viewUp(float fov, float dutch, float dur) {
            if (!inViewUp) {
                view_d = cam.Lens.FieldOfView;
                dutch_view_d = cam.Lens.Dutch;

                inViewUp = true;
            }

            if (dur > 0) {
                float dSize = cam.Lens.FieldOfView;
                float dDutch = cam.Lens.Dutch;

                float elapsedTime = 0f;

                while (elapsedTime < dur) {
                    float t = elapsedTime / dur;
                    cam.Lens.FieldOfView = Mathf.Lerp(dSize, fov, t);
                    cam.Lens.Dutch = Mathf.Lerp(dDutch, dutch, t);

                    elapsedTime += Time.deltaTime;

                    yield return null;
                }
            }

            cam.Lens.FieldOfView = fov;
            cam.Lens.Dutch = dutch;

            dutchRoutine = null;
        }

        IEnumerator _viewOut(float dur) {
            if (dur > 0) {
                float dSize = cam.Lens.FieldOfView;
                float dDutch = cam.Lens.Dutch;

                float elapsedTime = 0f;

                while (elapsedTime < dur) {
                    float t = elapsedTime / dur;
                    cam.Lens.FieldOfView = Mathf.Lerp(dSize, view_d, t);
                    cam.Lens.Dutch = Mathf.Lerp(dDutch, dutch_view_d, t);

                    elapsedTime += Time.deltaTime;

                    yield return null;
                }
            }

            cam.Lens.FieldOfView = view_d;
            cam.Lens.Dutch = dutch_view_d;

            inViewUp = false;
            dutchRoutine = null;
        }

        IEnumerator _closeOut(float dur) {
            if (dur > 0) {
                float dSize = cam.Lens.OrthographicSize;
                float dDutch = cam.Lens.Dutch;

                float elapsedTime = 0f;

                while (elapsedTime < dur) {
                    float t = elapsedTime / dur;
                    cam.Lens.OrthographicSize = Mathf.Lerp(dSize, orSize_d, t);
                    cam.Lens.Dutch = Mathf.Lerp(dDutch, dutch_d, t);

                    elapsedTime += Time.deltaTime;

                    yield return null;
                }
            }
            
            cam.Lens.OrthographicSize = orSize_d;
            cam.Lens.Dutch = dutch_d;

            inCloseUp = false;

            dutchRoutine = null;
        }

        IEnumerator _offset(Vector3 off, float dur = 0) {
            if (dur > 0) {
                Vector3 beforeOff = offset.Offset;

                float elapsedTime = 0f;

                while (elapsedTime < dur) {
                    float t = elapsedTime / dur;
                    offset.Offset = Vector3.Lerp(beforeOff, off, t);

                    elapsedTime += Time.deltaTime;
                    yield return null;
                }

                offset.Offset = off;
            }

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