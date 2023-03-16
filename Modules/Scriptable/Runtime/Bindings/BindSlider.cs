﻿using Pancake.Attribute;
using UnityEngine;
using UnityEngine.UI;

namespace Pancake.Scriptable
{
    [EditorIcon("scriptable_bind")]
    [RequireComponent(typeof(Slider))]
    public class BindSlider : RefPassenger<Slider>
    {
        [SerializeField] private FloatVariable floatVariable;

        protected override void Awake()
        {
            base.Awake();
            OnValueChanged(floatVariable);
            component.onValueChanged.AddListener(SetBoundVariable);
            floatVariable.OnValueChanged += OnValueChanged;
        }

        private void OnDestroy()
        {
            component.onValueChanged.RemoveListener(SetBoundVariable);
            floatVariable.OnValueChanged -= OnValueChanged;
        }

        private void OnValueChanged(float value) { component.value = value; }

        private void SetBoundVariable(float value) { floatVariable.Value = value; }
    }
}