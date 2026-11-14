using UnityEngine;

namespace LighthouseGame
{
    public partial class BlockoutPlayer
    {
        private Vector3 _leviathanEuler, _leviathanOffset, _appliedLeviathanOffset;
        private Quaternion _appliedLeviathanRotation = Quaternion.identity;
        private bool _hasLeviathanShake;

        // Applied before interaction/photo evaluation, so the scored view is the rendered view.
        // The next update removes only this additive offset; mouse pitch/yaw never accumulate noise.
        public void SetLeviathanShake(Vector3 euler, Vector3 offset)
        {
            _leviathanEuler = euler;
            _leviathanOffset = offset;
            if (euler == Vector3.zero && offset == Vector3.zero) RemoveLeviathanShake();
        }

        private void ApplyLeviathanShake()
        {
            if (!View || (_leviathanEuler == Vector3.zero && _leviathanOffset == Vector3.zero)) return;
            _appliedLeviathanRotation = Quaternion.Euler(_leviathanEuler);
            _appliedLeviathanOffset = _leviathanOffset;
            View.transform.localRotation *= _appliedLeviathanRotation;
            View.transform.localPosition += _appliedLeviathanOffset;
            _hasLeviathanShake = true;
        }

        private void RemoveLeviathanShake()
        {
            if (!_hasLeviathanShake || !View) return;
            View.transform.localRotation *= Quaternion.Inverse(_appliedLeviathanRotation);
            View.transform.localPosition -= _appliedLeviathanOffset;
            _hasLeviathanShake = false;
        }
    }
}
