using UnityEngine;

namespace Pancake.Serialization
{
    /// <summary>
    /// Represents base class for all serialized Queue(T) collections.
    /// </summary>
    public abstract class SerializableQueueBase : ISerializationCallbackReceiver
    {
        /// <summary>
        /// Called after engine deserializes this object.
        /// 
        /// Implement this method to receive a callback after engine deserializes this object.
        /// </summary>
        public abstract void OnAfterDeserialize();

        /// <summary>
        /// Called before engine serializes this object.
        /// 
        /// Implement this method to receive a callback before engine serializes this object.
        /// </summary>
        public abstract void OnBeforeSerialize();
    }
}
