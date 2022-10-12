﻿using UnityEngine;
using UnityEditor;

namespace Pancake.Editor
{
    /// <summary>
    /// Decorator for serialized element.
    /// </summary>
    public abstract class FieldDecorator : IDecoratorInitialization, IDecoratorGUI, IDecoratorHeight
    {
        /// <summary>
        /// Called when element decorator becomes initialized.
        /// </summary>
        /// <param name="element">Serialized element reference with current decorator attribute.</param>
        /// <param name="decoratorAttribute">Reference of serialized property decorator attribute.</param>
        /// <param name="label">Display label of serialized property.</param>
        public virtual void Initialize(SerializedField element, DecoratorAttribute decoratorAttribute, GUIContent label) { }

        /// <summary>
        /// Called for rendering and handling decorator GUI.
        /// </summary>
        /// <param name="position">Calculated position for drawing decorator.</param>
        public abstract void OnGUI(Rect position);

        /// <summary>
        /// Get the height of the decorator, which required to display it.
        /// Calculate only the size of the current decorator, not the entire property.
        /// The decorator height will be added to the total size of the property with other decorator.
        /// </summary>
        public abstract float GetHeight();

        /// <summary>
        /// On which side should the space be reserved?
        /// </summary>
        public virtual DecoratorSide GetSide() { return DecoratorSide.Bottom; }
    }
}