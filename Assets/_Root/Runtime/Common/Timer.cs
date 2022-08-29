using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable InconsistentNaming
namespace Pancake
{
    /// <summary>
    /// Allows you to run events on a delay without the use of <see cref="Coroutine"/>s
    /// or <see cref="MonoBehaviour"/>s.
    ///
    /// To create and start a Timer, use the <see cref="Register"/> method.
    /// </summary>
    public class Timer
    {
        #region Public Properties/Fields

        /// <summary>
        /// How long the timer takes to complete from start to finish.
        /// </summary>
        public float duration { get; private set; }

        /// <summary>
        /// Whether the timer will run again after completion.
        /// </summary>
        public bool isLooped { get; set; }

        /// <summary>
        /// Whether or not the timer completed running. This is false if the timer was cancelled.
        /// </summary>
        public bool isCompleted { get; private set; }

        /// <summary>
        /// Whether the timer uses real-time or game-time. Real time is unaffected by changes to the timescale
        /// of the game(e.g. pausing, slow-mo), while game time is affected.
        /// </summary>
        public bool usesRealTime { get; private set; }

        /// <summary>
        /// Whether the timer is currently paused.
        /// </summary>
        public bool isPaused { get { return _timeElapsedBeforePause.HasValue; } }

        /// <summary>
        /// Whether or not the timer was cancelled.
        /// </summary>
        public bool isCancelled { get { return _timeElapsedBeforeCancel.HasValue; } }

        /// <summary>
        /// Get whether or not the timer has finished running for any reason.
        /// </summary>
        public bool isDone { get { return isCompleted || isCancelled || isOwnerDestroyed; } }

        #endregion

        #region Public Static Methods

        /// <summary>
        /// Register a new timer that should fire an event after a certain amount of time
        /// has elapsed.
        ///
        /// Registered timers are destroyed when the scene changes.
        /// </summary>
        /// <param name="duration">The time to wait before the timer should fire, in seconds.</param>
        /// <param name="onComplete">An action to fire when the timer completes.</param>
        /// <param name="onUpdate">An action that should fire each time the timer is updated. Takes the amount
        /// of time passed in seconds since the start of the timer's current loop.</param>
        /// <param name="isLooped">Whether the timer should repeat after executing.</param>
        /// <param name="useRealTime">Whether the timer uses real-time(i.e. not affected by pauses,
        /// slow/fast motion) or game-time(will be affected by pauses and slow/fast-motion).</param>
        /// <param name="autoDestroyOwner">An object to attach this timer to. After the object is destroyed,
        /// the timer will expire and not execute. This allows you to avoid annoying <see cref="System.NullReferenceException"/>s
        /// by preventing the timer from running and accessessing its parents' components
        /// after the parent has been destroyed.</param>
        /// <returns>A timer object that allows you to examine stats and stop/resume progress.</returns>
        public static Timer Register(
            float duration,
            Action onComplete,
            Action<float> onUpdate = null,
            bool isLooped = false,
            bool useRealTime = false,
            MonoBehaviour autoDestroyOwner = null)
        {
            // create a manager object to update all the timers if one does not already exist.
            if (_manager == null)
            {
                TimerManager managerInScene = Object.FindObjectOfType<TimerManager>();
                if (managerInScene != null)
                {
                    _manager = managerInScene;
                }
                else
                {
                    GameObject managerObject = new GameObject {name = "TimerManager"};
                    _manager = managerObject.AddComponent<TimerManager>();
                }
            }

            Timer timer = new Timer(duration,
                onComplete,
                onUpdate,
                isLooped,
                useRealTime,
                autoDestroyOwner);
            _manager.RegisterTimer(timer);
            return timer;
        }

        /// <summary>
        /// Cancels a timer. The main benefit of this over the method on the instance is that you will not get
        /// a <see cref="NullReferenceException"/> if the timer is null.
        /// </summary>
        /// <param name="timer">The timer to cancel.</param>
        public static void Cancel(Timer timer)
        {
            if (timer != null)
            {
                timer.Cancel();
            }
        }

        /// <summary>
        /// Pause a timer. The main benefit of this over the method on the instance is that you will not get
        /// a <see cref="NullReferenceException"/> if the timer is null.
        /// </summary>
        /// <param name="timer">The timer to pause.</param>
        public static void Pause(Timer timer)
        {
            if (timer != null)
            {
                timer.Pause();
            }
        }

        /// <summary>
        /// Resume a timer. The main benefit of this over the method on the instance is that you will not get
        /// a <see cref="NullReferenceException"/> if the timer is null.
        /// </summary>
        /// <param name="timer">The timer to resume.</param>
        public static void Resume(Timer timer)
        {
            if (timer != null)
            {
                timer.Resume();
            }
        }

        public static void CancelAllRegisteredTimers()
        {
            if (_manager != null)
            {
                _manager.CancelAllTimers();
            }

            // if the manager doesn't exist, we don't have any registered timers yet, so don't
            // need to do anything in this case
        }

        public static void PauseAllRegisteredTimers()
        {
            if (_manager != null)
            {
                _manager.PauseAllTimers();
            }

            // if the manager doesn't exist, we don't have any registered timers yet, so don't
            // need to do anything in this case
        }

        public static void ResumeAllRegisteredTimers()
        {
            if (_manager != null)
            {
                _manager.ResumeAllTimers();
            }

            // if the manager doesn't exist, we don't have any registered timers yet, so don't
            // need to do anything in this case
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Stop a timer that is in-progress or paused. The timer's on completion callback will not be called.
        /// </summary>
        public void Cancel()
        {
            if (isDone)
            {
                return;
            }

            _timeElapsedBeforeCancel = GetTimeElapsed();
            _timeElapsedBeforePause = null;
        }

        /// <summary>
        /// Pause a running timer. A paused timer can be resumed from the same point it was paused.
        /// </summary>
        public void Pause()
        {
            if (isPaused || isDone)
            {
                return;
            }

            _timeElapsedBeforePause = GetTimeElapsed();
        }

        /// <summary>
        /// Continue a paused timer. Does nothing if the timer has not been paused.
        /// </summary>
        public void Resume()
        {
            if (!isPaused || isDone)
            {
                return;
            }

            _timeElapsedBeforePause = null;
        }

        /// <summary>
        /// Get how many seconds have elapsed since the start of this timer's current cycle.
        /// </summary>
        /// <returns>The number of seconds that have elapsed since the start of this timer's current cycle, i.e.
        /// the current loop if the timer is looped, or the start if it isn't.
        ///
        /// If the timer has finished running, this is equal to the duration.
        ///
        /// If the timer was cancelled/paused, this is equal to the number of seconds that passed between the timer
        /// starting and when it was cancelled/paused.</returns>
        public float GetTimeElapsed()
        {
            if (isCompleted || GetWorldTime() >= GetFireTime())
            {
                return duration;
            }

            return _timeElapsedBeforeCancel ?? _timeElapsedBeforePause ?? GetWorldTime() - _startTime;
        }

        /// <summary>
        /// Get how many seconds remain before the timer completes.
        /// </summary>
        /// <returns>The number of seconds that remain to be elapsed until the timer is completed. A timer
        /// is only elapsing time if it is not paused, cancelled, or completed. This will be equal to zero
        /// if the timer completed.</returns>
        public float GetTimeRemaining() { return duration - GetTimeElapsed(); }

        /// <summary>
        /// Get how much progress the timer has made from start to finish as a ratio.
        /// </summary>
        /// <returns>A value from 0 to 1 indicating how much of the timer's duration has been elapsed.</returns>
        public float GetRatioComplete() { return GetTimeElapsed() / duration; }

        /// <summary>
        /// Get how much progress the timer has left to make as a ratio.
        /// </summary>
        /// <returns>A value from 0 to 1 indicating how much of the timer's duration remains to be elapsed.</returns>
        public float GetRatioRemaining() { return GetTimeRemaining() / duration; }

        #endregion

        #region Private Static Properties/Fields

        // responsible for updating all registered timers
        private static TimerManager _manager;

        #endregion

        #region Private Properties/Fields

        private bool isOwnerDestroyed { get { return _hasAutoDestroyOwner && _autoDestroyOwner == null; } }

        private readonly Action _onComplete;
        private readonly Action<float> _onUpdate;
        private float _startTime;
        private float _lastUpdateTime;

        // for pausing, we push the start time forward by the amount of time that has passed.
        // this will mess with the amount of time that elapsed when we're cancelled or paused if we just
        // check the start time versus the current world time, so we need to cache the time that was elapsed
        // before we paused/cancelled
        private float? _timeElapsedBeforeCancel;
        private float? _timeElapsedBeforePause;

        // after the auto destroy owner is destroyed, the timer will expire
        // this way you don't run into any annoying bugs with timers running and accessing objects
        // after they have been destroyed
        private readonly MonoBehaviour _autoDestroyOwner;
        private readonly bool _hasAutoDestroyOwner;

        #endregion

        #region Private Constructor (use static Register method to create new timer)

        private Timer(float duration, Action onComplete, Action<float> onUpdate, bool isLooped, bool usesRealTime, MonoBehaviour autoDestroyOwner)
        {
            this.duration = duration;
            _onComplete = onComplete;
            _onUpdate = onUpdate;

            this.isLooped = isLooped;
            this.usesRealTime = usesRealTime;

            _autoDestroyOwner = autoDestroyOwner;
            _hasAutoDestroyOwner = autoDestroyOwner != null;

            _startTime = GetWorldTime();
            _lastUpdateTime = _startTime;
        }

        #endregion

        #region Private Methods

        private float GetWorldTime() { return usesRealTime ? Time.realtimeSinceStartup : Time.time; }

        private float GetFireTime() { return _startTime + duration; }

        private float GetTimeDelta() { return GetWorldTime() - _lastUpdateTime; }

        private void Update()
        {
            if (isDone)
            {
                return;
            }

            if (isPaused)
            {
                _startTime += GetTimeDelta();
                _lastUpdateTime = GetWorldTime();
                return;
            }

            _lastUpdateTime = GetWorldTime();

            if (_onUpdate != null)
            {
                _onUpdate(GetTimeElapsed());
            }

            if (GetWorldTime() >= GetFireTime())
            {
                if (_onComplete != null)
                {
                    _onComplete();
                }

                if (isLooped)
                {
                    _startTime = GetWorldTime();
                }
                else
                {
                    isCompleted = true;
                }
            }
        }

        #endregion

        #region Manager Class (implementation detail, spawned automatically and updates all registered timers)

        /// <summary>
        /// Manages updating all the <see cref="Timer"/>s that are running in the application.
        /// This will be instantiated the first time you create a timer -- you do not need to add it into the
        /// scene manually.
        /// </summary>
        private class TimerManager : MonoBehaviour
        {
            private List<Timer> _timers = new List<Timer>();

            // buffer adding timers so we don't edit a collection during iteration
            private List<Timer> _timersToAdd = new List<Timer>();

            public void RegisterTimer(Timer timer) { _timersToAdd.Add(timer); }

            public void CancelAllTimers()
            {
                foreach (Timer timer in _timers)
                {
                    timer.Cancel();
                }

                _timers = new List<Timer>();
                _timersToAdd = new List<Timer>();
            }

            public void PauseAllTimers()
            {
                foreach (Timer timer in _timers)
                {
                    timer.Pause();
                }
            }

            public void ResumeAllTimers()
            {
                foreach (Timer timer in _timers)
                {
                    timer.Resume();
                }
            }

            // update all the registered timers on every frame
            private void Update() { UpdateAllTimers(); }

            private void UpdateAllTimers()
            {
                if (_timersToAdd.Count > 0)
                {
                    _timers.AddRange(_timersToAdd);
                    _timersToAdd.Clear();
                }

                foreach (Timer timer in _timers)
                {
                    timer.Update();
                }

                _timers.RemoveAll(t => t.isDone);
            }
        }

        #endregion
    }

    /// <summary>
    /// Contains extension methods related to <see cref="Timer"/>s.
    /// </summary>
    public static partial class C
    {
        /// <summary>
        /// Attach a timer on to the behaviour. If the behaviour is destroyed before the timer is completed,
        /// e.g. through a scene change, the timer callback will not execute.
        /// </summary>
        /// <param name="behaviour">The behaviour to attach this timer to.</param>
        /// <param name="duration">The duration to wait before the timer fires.</param>
        /// <param name="onComplete">The action to run when the timer elapses.</param>
        /// <param name="onUpdate">A function to call each tick of the timer. Takes the number of seconds elapsed since
        /// the start of the current cycle.</param>
        /// <param name="isLooped">Whether the timer should restart after executing.</param>
        /// <param name="useRealTime">Whether the timer uses real-time(not affected by slow-mo or pausing) or
        /// game-time(affected by time scale changes).</param>
        public static Timer AttachTimer(
            this MonoBehaviour behaviour,
            float duration,
            Action onComplete,
            Action<float> onUpdate = null,
            bool isLooped = false,
            bool useRealTime = false)
        {
            return Timer.Register(duration,
                onComplete,
                onUpdate,
                isLooped,
                useRealTime,
                behaviour);
        }
    }
}