﻿using UnityEditor;
using UnityEngine;

namespace Pancake.Editor
{
    [DecoratorTarget(typeof(ObjectPreviewAttribute))]
    sealed class ObjectPreviewDecorator : FieldDecorator, ITypeValidationCallback
    {
        private ObjectPreviewAttribute attribute;
        private SerializedProperty serializedProperty;
        private Object previousObject;
        private UnityEditor.Editor editor;

        /// <summary>
        /// Called when element decorator becomes initialized.
        /// </summary>
        /// <param name="element">Serialized element reference with current decorator attribute.</param>
        /// <param name="decoratorAttribute">Reference of serialized property decorator attribute.</param>
        /// <param name="label">Display label of serialized property.</param>
        public override void Initialize(SerializedField element, DecoratorAttribute decoratorAttribute, GUIContent label)
        {
            attribute = decoratorAttribute as ObjectPreviewAttribute;
            serializedProperty = element.serializedProperty;
        }

        /// <summary>
        /// Called for rendering and handling decorator GUI.
        /// </summary>
        /// <param name="position">Calculated position for drawing decorator.</param>
        public override void OnGUI(Rect position)
        {
            Object currentObject = serializedProperty.objectReferenceValue;
            if (currentObject != null)
            {
                if (editor == null || previousObject != currentObject)
                {
                    editor = UnityEditor.Editor.CreateEditor(currentObject);
                }

                editor.DrawPreview(position);
            }

            previousObject = currentObject;
        }

        /// <summary>
        /// Get the height of the decorator, which required to display it.
        /// Calculate only the size of the current decorator, not the entire property.
        /// The decorator height will be added to the total size of the property with other decorator.
        /// </summary>
        public override float GetHeight() { return serializedProperty.objectReferenceValue ? attribute.Height : 0; }

        /// <summary>
        /// Return true if this property valid the using with this attribute.
        /// If return false, this property attribute will be ignored.
        /// </summary>
        /// <param name="property">Reference of serialized property.</param>
        /// <param name="label">Display label of serialized property.</param>
        public bool IsValidProperty(SerializedProperty property) { return property.propertyType == SerializedPropertyType.ObjectReference; }
    }
}