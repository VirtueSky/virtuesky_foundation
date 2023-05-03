﻿using UnityEngine;
using UnityEngine.Events;

namespace Pancake.Scriptable
{
    [AddComponentMenu("Scriptable/EventListeners/EventListenerFloat")]
    [EditorIcon("scriptable_event_listener")]
    public class EventListenerFloat : EventListenerGeneric<float>
    {
        [SerializeField] private EventResponse[] m_eventResponses = null;
        protected override EventResponse<float>[] EventResponses => m_eventResponses;

        [System.Serializable]
        public class EventResponse : EventResponse<float>
        {
            [SerializeField] private ScriptableEventFloat mScriptableEvent = null;
            public override ScriptableEvent<float> ScriptableEvent => mScriptableEvent;

            [SerializeField] private FloatUnityEvent m_response = null;
            public override UnityEvent<float> Response => m_response;
        }

        [System.Serializable]
        public class FloatUnityEvent : UnityEvent<float>
        {
        }
    }
}