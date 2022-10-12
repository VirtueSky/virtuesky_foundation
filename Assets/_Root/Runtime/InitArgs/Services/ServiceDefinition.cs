﻿using System;
using UnityEngine;
using JetBrains.Annotations;
using Pancake.Init.Serialization;
using Object = UnityEngine.Object;

namespace Pancake.Init.Internal
{
    /// <summary>
    /// Defines a single service that derives from <see cref="UnityEngine.Object"/> as well
    /// as the defining type of the services which its clients can use to retrieving the service instance.
    /// </summary>
    [Serializable]
    public sealed class ServiceDefinition
    {
        #pragma warning disable CS0649
        public Object service;

        public _Type definingType;
        #pragma warning restore CS0649

        public ServiceDefinition(Object service, _Type definingType)
        {
            this.service = service;    
            this.definingType = definingType;    
        }

        public ServiceDefinition(Object service, Type definingType)
        {
            this.service = service;
            this.definingType = new _Type(definingType);
        }

        #if UNITY_EDITOR
        public static void OnValidate(Object obj, ref ServiceDefinition definition)
        {
            if(definition.service == null)
            {
                return;
            }

            if(definition.definingType.Value == null)
            {
                UnityEditor.Undo.RecordObject(obj, "Set Service Defining Type");
                definition.definingType = new _Type(definition.service.GetType());
            }
            else if(!IsAssignableFrom(definition.definingType.Value, definition.service))
            {
                #if DEV_MODE
                Debug.Log($"Service {TypeUtility.ToString(definition.service.GetType())} can not be cast to defining type {TypeUtility.ToString(definition.definingType.Value)}. Setting to null.", obj);
                #endif

                definition.definingType.Value = null;
            }
        }

        private static bool IsAssignableFrom([JetBrains.Annotations.NotNull] Type definingType, [JetBrains.Annotations.NotNull] Object service)
        {
            var serviceType = service.GetType();
            if(definingType.IsAssignableFrom(serviceType))
            {
                return true;
            }

            foreach(var @interface in serviceType.GetInterfaces())
            {
                if(@interface.IsGenericType && !@interface.IsGenericTypeDefinition && @interface.GetGenericTypeDefinition() == typeof(IValueProvider<>))
                {
                    var providedValueType = @interface.GetGenericArguments()[0];
                    if(definingType.IsAssignableFrom(providedValueType))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        #endif
    }
}