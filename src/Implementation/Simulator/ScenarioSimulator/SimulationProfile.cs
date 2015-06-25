﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.IoTJourney.Logging;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.Practices.IoTJourney.ScenarioSimulator
{
    public class SimulationProfile
    {
        private readonly SimulatorConfiguration _simulatorConfiguration;

        private readonly string _hostName;

        private readonly int _devicesPerInstance;

        private readonly ISubject<int> _observableTotalCount = new Subject<int>();

        public SimulationProfile(
            string hostName,
            int instanceCount,
            SimulatorConfiguration simulatorConfiguration)
        {
            _hostName = hostName;
            _simulatorConfiguration = simulatorConfiguration;


            // The instance Count is used when scaling out the simulator
            _devicesPerInstance = simulatorConfiguration.NumberOfDevices / instanceCount;
        }

        public async Task RunSimulationAsync(string scenario, CancellationToken token)
        {
            ScenarioSimulatorEventSource.Log.SimulationStarted(_hostName, scenario);

            var produceEventsForScenario = SimulationScenarios.GetScenarioByName(scenario);

            var simulationTasks = new List<Task>();

            var warmup = _simulatorConfiguration.WarmupDuration;
            var warmupPerDevice = warmup.Ticks / _devicesPerInstance;

            var messagingFactories =
                Enumerable.Range(0, _simulatorConfiguration.SenderCountPerInstance)
                    .Select(i => MessagingFactory.CreateFromConnectionString(_simulatorConfiguration.EventHubConnectionString))
                    .ToArray();

            _observableTotalCount
                .Sum()
                .Subscribe(total => ScenarioSimulatorEventSource.Log.FinalEventCountForAllDevices(total));

            _observableTotalCount
                .Buffer(TimeSpan.FromSeconds(10))
                .Scan(0, (total, next) => total + next.Sum())
                .Subscribe(total => ScenarioSimulatorEventSource.Log.CurrentEventCountForAllDevices(total));

            try
            {
                for (int i = 0; i < _devicesPerInstance; i++)
                {
                    // Use the short form of the host or instance name to generate the vehicle ID
                    var deviceId = String.Format("{0}-{1}", ConfigurationHelper.InstanceName, i);


                    Console.WriteLine("device # {0}  message factory # {1} ", i, (i % messagingFactories.Length));
                    var eventSender = new EventSender(
                        messagingFactory: messagingFactories[i % messagingFactories.Length],
                        config: _simulatorConfiguration,
                        serializer: Serializer.ToJsonUTF8
                    );

                    var deviceTask = SimulateDeviceAsync(
                        deviceId: deviceId,
                        produceEventsForScenario: produceEventsForScenario,
                        sendEventsAsync: eventSender.SendAsync,
                        waitBeforeStarting: TimeSpan.FromTicks(warmupPerDevice * i),
                        totalCount: _observableTotalCount,
                        token: token
                    );

                    simulationTasks.Add(deviceTask);
                }

                await Task.WhenAll(simulationTasks.ToArray());

                _observableTotalCount.OnCompleted();
            }
            finally
            {
                // cannot await on a finally block to do CloseAsync
                foreach (var factory in messagingFactories)
                {
                    factory.Close();
                }
            }

            ScenarioSimulatorEventSource.Log.SimulationEnded(_hostName);
        }

        private static async Task SimulateDeviceAsync(
            string deviceId,
            Func<EventEntry[]> produceEventsForScenario,
            Func<object, string, int, Task<bool>> sendEventsAsync,
            TimeSpan waitBeforeStarting,
            IObserver<int> totalCount,
            CancellationToken token)
        {
            ScenarioSimulatorEventSource.Log.WarmingUpFor(deviceId, waitBeforeStarting.Ticks);

            try
            {
                await Task.Delay(waitBeforeStarting, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            var messagingEntries = produceEventsForScenario();
            var device = new Device(deviceId, messagingEntries, sendEventsAsync);

            device.ObservableEventCount
                .Sum()
                .Subscribe(total => ScenarioSimulatorEventSource.Log.FinalEventCount(deviceId, total));

            device.ObservableEventCount
                .Subscribe(totalCount.OnNext);

            await device.RunSimulationAsync(token);
        }
    }
}