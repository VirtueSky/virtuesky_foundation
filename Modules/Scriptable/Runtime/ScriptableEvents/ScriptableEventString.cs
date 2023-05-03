﻿using UnityEngine;

namespace Pancake.Scriptable
{
    [CreateAssetMenu(fileName = "scriptable_event_string.asset", menuName = "Pancake/Scriptable/ScriptableEvents/string")]
    [EditorIcon("scriptable_event")]
    public class ScriptableEventString : ScriptableEvent<string>
    {
    }
}