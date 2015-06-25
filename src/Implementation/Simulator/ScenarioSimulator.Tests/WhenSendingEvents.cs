﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Practices.IoTJourney.Devices.Events;
using Xunit;

namespace Microsoft.Practices.IoTJourney.ScenarioSimulator.Tests
{
    public class WhenSendingEvents
    {
        [Fact]
        [Trait("Running time", "Short")]
        public void EventTypeWillBeShortTypeName()
        {
            // This assumes that the event consumers are 
            // making the same choice.
            var evt = new UpdateTemperatureEvent();
            var expected = evt.GetType().Name;
            var actual = EventSender.DetermineTypeFromEvent(evt);

            Assert.Equal(expected, actual);
        }


        [Fact]
        public void EventTypeTest()
        {
            var evt = new[] {new EventEntry(EventFactory.TemperatureEventFactory, TimeSpan.FromSeconds(1), 0.1)};
            
            
        }
    }
}