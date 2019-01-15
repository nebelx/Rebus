﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Logging;

namespace Rebus.Threading.TaskParallelLibrary
{
    /// <summary>
    /// <see cref="Task"/>-based background timer thingie, that will periodically call an async <see cref="Func&lt;Task&gt;"/>
    ///  </summary>
    public class TplAsyncTask : IAsyncTask
    {
        /// <summary>
        /// This is the default interval between invocations if the periodic action, unless the <see cref="Interval"/> property is set to something else
        /// </summary>
        public static TimeSpan DefaultInterval = TimeSpan.FromSeconds(10);

        readonly string _description;
        readonly Func<Task> _action;
        readonly bool _prettyInsignificant;
        readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        readonly ManualResetEvent _finished = new ManualResetEvent(false);
        readonly ILog _log;

        Task _task;

        bool _disposed;
        TimeSpan _interval;

        /// <summary>
        /// Constructs the periodic background task with the given <paramref name="description"/>, periodically executing the given <paramref name="action"/>,
        /// waiting <see cref="Interval"/> between invocations.
        /// </summary>
        public TplAsyncTask(string description, Func<Task> action, IRebusLoggerFactory rebusLoggerFactory, bool prettyInsignificant)
        {
            _description = description;
            _action = action;
            _prettyInsignificant = prettyInsignificant;
            _log = rebusLoggerFactory.GetLogger<TplAsyncTask>();
            Interval = DefaultInterval;
        }

        /// <summary>
        /// Configures the interval between invocations. The default value is <see cref="DefaultInterval"/>
        /// </summary>
        public TimeSpan Interval
        {
            get => _interval;
            set => _interval = value < TimeSpan.FromMilliseconds(100)
                ? TimeSpan.FromMilliseconds(100)
                : value;
        }

        /// <summary>
        /// Starts the task
        /// </summary>
        public void Start()
        {
            if (_disposed)
            {
                throw new InvalidOperationException($"Cannot start periodic task '{_description}' because it has been disposed!");
            }

            LogStartStop("Starting periodic task {taskDescription} with interval {timerInterval}", _description, Interval);

            var token = _tokenSource.Token;

            // don't pass cancellation token to this one, as it might cancel prematurely, thus not setting our reset event as required
            // ReSharper disable once MethodSupportsCancellation
            _task = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var intervalAboveZero = Interval;

                        await Task.Delay(intervalAboveZero, token);

                        try
                        {
                            await _action();
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested)
                        {
                            // it's fine, we're shutting down
                        }
                        catch (Exception exception)
                        {
                            _log.Warn("Exception in periodic task {taskDescription}: {exception}", _description, exception);
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // it's fine, we're shutting down
                }
                finally
                {
                    _finished.Set();
                }
            });
        }

        /// <summary>
        /// Stops the background task
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // if it was never started, we don't do anything
                if (_task == null) return;

                LogStartStop("Stopping periodic task {taskDescription}", _description);

                _tokenSource.Cancel();

                if (!_finished.WaitOne(TimeSpan.FromSeconds(5)))
                {
                    _log.Warn("Periodic task {taskDescription} did not finish within 5 second timeout!", _description);
                }
            }
            finally
            {
                _disposed = true;
            }
        }

        void LogStartStop(string message, params object[] objs)
        {
            if (_prettyInsignificant)
            {
                _log.Debug(message, objs);
            }
            else
            {
                _log.Info(message, objs);
            }
        }
    }
}