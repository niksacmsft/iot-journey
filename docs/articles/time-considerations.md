# Time Considerations

The vast majority of IoT projects involve collecting and forwarding events.
One usually needs to know *when* these events occurred in order to do anything
meaningful with the data.  In other words, a *temporal context* implicitly
applies to the data.

This document will cover many of the things you need to take into consideration
when working with time in an IoT project.

## Clock Sources

Events need to associated with a timestamp, but the reliability of that
timestamp can vary significantly depending on where it originates.  Should
you use the server's time, or the device's time, or perhaps some other source of
time?  We'll explore each option, taking into account the following aspects:

- **Latency** - How much delay occurs between the actual event and the measuring
  of its timestamp?
- **Accuracy** - How correct is the clock's time with relation to the world's
  timekeeping systems?
- **Precision** - What is the smallest degree of measure that we can use with
  certainty?

See also [Accuracy vs Precision][accuracy-precision].

[accuracy-precision]: https://en.wikipedia.org/wiki/Accuracy_and_precision

### Using the Cloud Gateway's Clock

#### Latency

When you use the cloud gateway's clock, you are recording the time that the
message containing the event information was received by the server.  In some
cases this is acceptable, but it's usually discouraged.  While simple to use,
it doesn't account for the time it takes to deliver the message from the device
to the cloud.

The latency can be detrimental, especially when you consider batch processing,
offline, and ocassionally-connected scenarios.  In these cases, event messages
are typically stored for later transmittal.  If the event time isn't captured
until the message is received, it could very well be incorrect.

There are some scenarios where this is not as important.  For example, if you
only care about *reasonably current* events, then you may just decide to discard
data when a device is offline.  You would still have the latency of transmittal,
but this is usually less than a few seconds.

#### Accuracy

A cloud gateway is typically synchronized over the Internet, via servers that
implement the Network Time Protocol (NTP).  The accuracy of a clock synchronized
by NTP is called the [stratum][clock-strata].  Stratum 0 is the most accurate,
and each hop away is incremented by one.  The larger the stratum, the further
away the original timing source, and the less accurate the time.

- Stratum 0 can only be measured directly from an independent timing source.
- Stratum 1 would refer to a machine that is *directly connected* to an
  independent timing source.  This is usually not achievable in the cloud,
  due to the physical hardware requirement.
- Stratum 2 is usually found on NTP host servers, such as `time.windows.com`.
- Stratum 3 or 4 is often found on domain controllers in a network.

Thus depending on how it is synchronized, your cloud gateway may have a clock
stratum of 3, 4, or higher.  Each level of stratum could add anywhere from a few
nanoseconds up to a few hundred milliseconds of error to your clock.  That means
your timestamps could be a couple of seconds off from what you expect them to
be.  This may or may not be acceptable for your IoT scenario, depending on
exactly what the scenario entails.

For our IoT Journey project, we only expect to receive an event from a single
device once per minute, and it really doesn't matter if we are off by a few
seconds in either direction.  However, other IoT scenarios may have higher
accuracy requirements.

[clock-strata]: https://en.wikipedia.org/wiki/Network_Time_Protocol#Clock_strata

#### Precision

A cloud gateway consists of software and services that run on top of virtual
machines.  The clock of the underlying physical servers is precise to about 10
milliseconds.  However, the virtual machine infrastructure can lead to
additional precision errors.  In general, if you are timing events that occur
10 or more times per second, the cloud gateway is not precise enough to generate
a sufficient timestamp.

### Using a Field Gateway's Clock

In IoT scenarios where many devices are in a single location, it may be desired
to incorporate a field gateway.  This is a computer that receives event messages
from the devices, then forwards them on to the cloud.

#### Latency

If messages are timestamped by the field gateway, and the gateway is designed
for high availability (fault-tolerant, always-on), then the latency issues
discussed in the previous section are greatly reduced.  There is still *some*
latency, but it is now limited by the local area network connection between the
devices and the field gateway, rather than the Internet.

#### Accuracy

When configured correctly, a field gateway will synchronize its clock via NTP,
either to a public stratum 2 NTP server, or to a local stratum 3 domain server.
This means that a field gateway will typically have stratum 3 or 4 clock,
similar to that of a cloud gateway.

Again, for most scenarios, this is perfectly acceptable, leading to only a few seconds or a few milliseconds of discrepancy.  However, if you are coordinating event times from multiple sites (using multiple field gateways), you may find this to be a limiting factor.

Since a field gateway is located on premises, it is possible to achieve higher
accuracy by attaching dedicated timing hardware to the server.  See the section
"Using Dedicated Timing Hardware" below.

#### Precision

Similar to a cloud gateway, the onboard clock of most field gateway servers is
precise to about 10 milliseconds.  However, a field gateway has the ability to
be installed directly on a physical server, so it is not necessarily impacted
by timing errors that can occur on a virtual machine.

In a perfect world, that means you should be able to generate distinct
timestamps for up to 100 events per second.  However, it practice it may be
slightly less than that.

Additionally, if dedicated timing hardware is attached to the field gateway,
it can achieve higher much higher precision levels.

### Using the Device's Real-time Clock (RTC)

IoT devices may have similar precision and accuracy characteristics as computer
hardware, or they may be significantly better or worse.  Some devices may not
have any clock hardware at all, relying on network synchronization during their
startup sequence, and CPU timing to keep the system clock ticking forward.

#### Latency

--- TODO

#### Accuracy

--- TODO

#### Precision

--- TODO

### Using the Device's GPS as a Clock

Many IoT scenarios use GPS signals for gathering latitude and longitude
coordinates to include in the event data.   If you're already using GPS for
location, you should consider using it for timing also.  GPS signals are highly
accurate, and this will relieve your device from having to synchronize its
clock over a network connection.   However, it's important to realize that
*precision* is usually still a function of the device's onboard hardware.

Additionally, it's important to understand that the raw GPS signal gives a
timestamp that is not fully aligned to Coordinated Universal Time (UTC).
In 1980, near the introduction of the GPS system, the "GPS time" was set to
align with UTC, but it has not been adjusted for leap seconds that have occurred
since then.  As of July 2015, GPS time is 17 seconds ahead of UTC, but that
will change as more leap seconds are added in future years.

There are two approaches to convert GPS time to UTC time.  Traditionally, a
table of leap seconds would need to be maintained on the device.  However, many
modern GPS receivers can read an additional field sent in GPS data stream that
sends the GPS-UTC offset, and apply it directly.   It's important to know which
approach your device's GPS receiver uses.  If it automatically adjusts for
leap seconds, then you have nothing more to do.  But if it doesn't, you may
need to adjust the timestamp manually before using it in your application.

### Using Dedicated Timing Hardware

If you need both high accuracy and high precision, you might consider a
dedicated hardware clock.  There are [several manufacturers][clock-makers] of
high-precision timing hardware that use signals from other sources, such as GPS
and radio.  These devices can be attached to a field gateway, to your network,
or in some cases, directly to your IoT devices.  These timing source provide the
highest precision and accuracy levels available, but come at a cost.

[clock-makers]: http://www.nist.gov/pml/div688/grp40/receiverlist.cfm


### Summary

The following tables highlight the pros and cons of various clock sources.

Clock Source                          | Latency            | Accuracy           | Precision
--------------------------------------|--------------------|--------------------|--------------------
Cloud Gateway                         | :-1:               | :star::star:       | :star:
Field Gateway                         | :star::star:       | :star::star:       | :star::star:
Field Gateway with Dedicated Hardware | :star::star:       | :star::star::star: | :star::star::star:
Device without RTC                    | :star::star::star: | :star:             | :star:
Device with RTC                       | :star::star::star: | :star::star:       | :star:
Device with Traditional GPS           | :star::star::star: | :star::star::star: | :star::star:
Device with Dedicated Hardware        | :star::star::star: | :star::star::star: | :star::star::star:

Clock Source | Pros | Cons
-------------|------|------
Cloud Gateway | <ul><li>Simple</li><li>Already synchronized</li><li>Acceptable for some always-online scenarios</li></ul> | <ul><li>High latency impacts data</li><li>Risk of bad data or dropped events when devices are offline</li><li>VMs may impact precision</li><li>No ability to attach dedicated clock hardware</li></ul>
Field Gateway | <ul><li>Single source of time across devices</li><li>Can attach dedicated clock hardware for improved accuracy and precision</li></ul> | <ul><li>F.G. not always incorporated in the solution</li><li>Not practical for ocassionally-connected devices</li></ul>
Device | <ul><li>Works when device is offline</li><li>May have a GPS clock already</li><li>May be possible to attach dedicated clock hardware</li></ul> | <ul><li>Synchronization is more difficult</li><li>Device may not have an RTC </li><li>Device precision may be lower than a typical computer</li></ul>


## Event Timestamps

  - UTC vs DateTimeOffset vs DateTime

  - Time Zones

  - Querying / Aggregation

## Time Formats

There are many different date and time formats used in programming.  However,
there are only two that you should consider when sending events:

- ISO 8601
  - utc, or offset

- Unix Timestamps
  - precision



- Time-based load leveling

- Time-based aggregation and windowing

    - ASA's `TIMESTAMP BY` keyword
