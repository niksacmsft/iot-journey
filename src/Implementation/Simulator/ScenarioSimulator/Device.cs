﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.IoTJourney.Logging;

using System.IO;
using System.Text;

namespace Microsoft.Practices.IoTJourney.ScenarioSimulator
{
    public class Device
    {
        private static readonly TimeSpan LoopFrequency = TimeSpan.FromSeconds(0.33);

        private readonly string _deviceId;

        private readonly IEnumerable<EventEntry> _eventEntries;

        //LAB Debug added string, int
        private readonly Func<object, Task<bool>> _sendEventAsync;

        public ISubject<int> ObservableEventCount { get; private set; }

        public float? CurrentTemperature { get; set; }

        public string Id { get {return _deviceId;} }

        public Device(
            string deviceId,
            IEnumerable<EventEntry> eventEntries,
            Func<object, Task<bool>> sendEventAsync)
        {
            _deviceId = deviceId;
            _sendEventAsync = sendEventAsync;
            _eventEntries = eventEntries;

            ObservableEventCount = new Subject<int>();
        }

        public async Task RunSimulationAsync(CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();

            ScenarioSimulatorEventSource.Log.DeviceStarting(_deviceId);


            // LAB debug added count
            int event_count = 0;
            double timesincelastmsg = 0;

            StringBuilder sb = new StringBuilder();


            try
            {
                while (!token.IsCancellationRequested)
                {
                    var elaspedTime = stopwatch.Elapsed;
                    //ScenarioSimulatorEventSource.Log.Logging(_deviceId, event_count, "Start of Loop - stopwatch.elaspedTime ="+elaspedTime);
                    stopwatch.Restart();

                    foreach (var entry in _eventEntries)
                    {
                        entry.UpdateElapsedTime(elaspedTime);
                        //ScenarioSimulatorEventSource.Log.Logging(_deviceId, event_count, "Updated - Entry.elaspedTime =" + entry.ElapsedTime);
                        if (!entry.ShouldSendEvent())
                        {
                            //ScenarioSimulatorEventSource.Log.Logging(_deviceId, event_count,"Not enough time - Entry.elaspedTime ="+entry.ElapsedTime);
                            continue;
                        }

                        timesincelastmsg = entry.ElapsedTime.TotalMilliseconds;
                        entry.ResetElapsedTime();

                        var evt = entry.CreateNewEvent(this);
                        var wasEventSent = await _sendEventAsync(evt);

                        

                        if (wasEventSent)
                        {
                            ObservableEventCount.OnNext(1);
                            //debug

                            ScenarioSimulatorEventSource.Log.EventSentMeasurement(_deviceId, timesincelastmsg,  event_count);
                            sb.AppendLine(timesincelastmsg.ToString());

                            event_count++;
                        }
                        else
                        {
                            // If the event was not sent, it is likely that Event Hub
                            // is throttling our requests. So we will cause the simulation
                            // for this particular device to delay and reduce the load.
                            // Note that in some cases you will want resend the event,
                            // however we are merely pausing before trying to send
                            // the next one.
                            try
                            {
                                ScenarioSimulatorEventSource.Log.Logging(_deviceId, event_count, "awaiting Simulation delay for 10 Seconds");
                                await Task.Delay(TimeSpan.FromSeconds(10), token);
                            }
                            catch (TaskCanceledException) { /* cancelling Task.Delay will throw */ }
                        }
                    }

                    try
                    {
                        //ScenarioSimulatorEventSource.Log.Logging(_deviceId, event_count, "awaiting Delay with LoopFreq = " + LoopFrequency);
                        await Task.Yield();//Task.Delay(LoopFrequency, token);
                    }
                    catch (TaskCanceledException) { /* cancelling Task.Delay will throw */ }


                   // ScenarioSimulatorEventSource.Log.Logging(_deviceId, event_count, "End of Loop - StopWatch.ElaspedTime =" + stopwatch.Elapsed);

                }
            }
            catch (Exception e)
            {
                ObservableEventCount.OnError(e);
                ScenarioSimulatorEventSource.Log.DeviceUnexpectedFailure(e, _deviceId);
                return;
            }

            using (StreamWriter outfile = new StreamWriter(@"c:\users\lbrader\appdata\data.txt"))
            {
                outfile.Write(sb.ToString());
            }

            ObservableEventCount.OnCompleted();

            ScenarioSimulatorEventSource.Log.DeviceStopping(_deviceId);
        }
    }
}