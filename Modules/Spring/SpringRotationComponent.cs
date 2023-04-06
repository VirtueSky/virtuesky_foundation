﻿using System.Collections;
using Pancake.Attribute;
using UnityEngine;

namespace Pancake.Spring
{
    [EditorIcon("script_spring")]
    public class SpringRotationComponent : BaseSpringComponent, ISpringTo<Vector3>, ISpringTo<Quaternion>, INudgeable<Vector3>, INudgeable<Quaternion>
    {
        private SpringVector3 _spring;
        private CoroutineHandle _handle;
        private readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();

        private void Awake()
        {
            var rotation = transform.rotation;
            _spring = new SpringVector3() {StartValue = rotation.eulerAngles, EndValue = rotation.eulerAngles, Damping = damping, Stiffness = stiffness};
        }

        public void SpringTo(Vector3 target) { SpringTo(Quaternion.Euler(target)); }

        public void SpringTo(Quaternion target)
        {
            if (_handle is {IsDone: false}) StopCoroutine(_handle);

            CheckInspectorChanges();
            _handle = this.RunCoroutine(IeSpringToTarget(target));
        }

        public void Nudge(Vector3 value)
        {
            CheckInspectorChanges();
            if (Math.Approximately(_spring.CurrentVelocity.sqrMagnitude, 0))
            {
                if (_handle is {IsDone: false}) StopCoroutine(_handle);
                _handle = this.RunCoroutine(IeHandleNudge(value));
            }
            else
            {
                _spring.UpdateEndValue(_spring.EndValue, _spring.CurrentVelocity + value);
            }
        }

        public void Nudge(Quaternion value) { Nudge(value.eulerAngles); }

        private IEnumerator IeSpringToTarget(Quaternion target)
        {
            if (Math.Approximately(_spring.CurrentVelocity.sqrMagnitude, 0))
            {
                _spring.Reset();
                _spring.StartValue = transform.eulerAngles;
                _spring.EndValue = target.eulerAngles;
            }
            else
            {
                _spring.UpdateEndValue(target.eulerAngles, _spring.CurrentVelocity);
            }

            while (!Math.Approximately(0, 1f - Quaternion.Dot(transform.rotation, target)))
            {
                transform.rotation = Quaternion.Euler(_spring.Evaluate(Time.deltaTime));
                yield return _waitForEndOfFrame;
            }

            _spring.Reset();
        }

        private IEnumerator IeHandleNudge(Vector3 value)
        {
            _spring.Reset();
            var rotation = transform.rotation;
            _spring.StartValue = rotation.eulerAngles;
            _spring.EndValue = rotation.eulerAngles;
            _spring.InitialVelocity = value;
            transform.rotation = Quaternion.Euler(_spring.Evaluate(Time.deltaTime));

            while (!Math.Approximately(0, 1f - Quaternion.Dot(rotation, transform.rotation)))
            {
                transform.rotation = Quaternion.Euler(_spring.Evaluate(Time.deltaTime));

                yield return _waitForEndOfFrame;
            }

            _spring.Reset();
        }

        private void CheckInspectorChanges()
        {
            _spring.Damping = damping;
            _spring.Stiffness = stiffness;
        }
    }
}