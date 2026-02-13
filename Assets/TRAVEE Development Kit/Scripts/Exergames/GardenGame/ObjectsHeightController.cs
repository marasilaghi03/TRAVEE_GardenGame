using System.Collections;
using UnityEngine;

namespace GardenGame
{
    /// <summary>
    /// Put this on the "Objects" parent GameObject.
    /// It moves the entire container up/down based on Input.TableHeightOffsetCm.
    ///
    /// Works even if Garden.Input is assigned later (after SessionContainer OnInit).
    /// </summary>
    public class ObjectsHeightController : MonoBehaviour
    {
        public GardenController Garden;   
        public bool ApplyEveryFrame = true;

        [Header("Debug")]
        public bool LogWhenApplied = false;

        private Vector3 _baseLocalPos;
        private bool _baseCached;

        private int _lastOffsetCm = int.MinValue;

        void Awake()
        {
            CacheBase();
        }

        void OnEnable()
        {

            if (!_baseCached) CacheBase();
            StartCoroutine(InitRoutine());
        }

        private void CacheBase()
        {
            _baseLocalPos = transform.localPosition;
            _baseCached = true;
        }

        private IEnumerator InitRoutine()
        {
            float timeout = 5f;
            float t = 0f;

            while (t < timeout)
            {
                if (Garden == null)
                    Garden = FindObjectOfType<GardenController>(true);

                if (Garden != null && Garden.Input != null)
                    break;

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            ApplyNow();

            if (!ApplyEveryFrame)
                yield break;

            while (enabled && gameObject.activeInHierarchy)
            {
                ApplyNow();
                yield return null;
            }
        }

        private void ApplyNow()
        {
            if (Garden == null || Garden.Input == null) return;

            int offsetCm = Garden.Input.TableHeightOffsetCm;

            if (offsetCm == _lastOffsetCm) return;
            _lastOffsetCm = offsetCm;

            float offsetM = offsetCm * 0.01f;

            Vector3 p = _baseLocalPos;
            p.y = _baseLocalPos.y + offsetM;
            transform.localPosition = p;

            if (LogWhenApplied)
                Debug.Log($"[ObjectsHeightController] Applied TableHeightOffsetCm={offsetCm} (m={offsetM}) to '{name}'");
        }

        public void RecalibrateBase()
        {
            CacheBase();
            _lastOffsetCm = int.MinValue;
            ApplyNow();
        }
    }
}
