// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

namespace NodaTime.Text
{
    /// <summary>
    /// Describes the state of a single interval endpoint (<see cref="Interval.Start"/> or <see cref="Interval.End"/>)
    /// while it is being exchanged as text, before it is resolved to the nullable <see cref="Instant"/> representation
    /// used by <see cref="Interval"/> itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An endpoint is in one of three distinct states during an exchange:
    /// <list type="bullet">
    ///   <item><description><see cref="Missing"/> — no value at all was provided for the endpoint (an empty segment).
    ///   This is deliberately not the same as an explicit infinite endpoint, and is never filled in with the
    ///   current time automatically. It is handled according to the caller-specified <see cref="MissingEndpointPolicy"/>.</description></item>
    ///   <item><description><see cref="Infinite"/> — the endpoint was explicitly stated to be unbounded in its direction
    ///   ("StartOfTime" for a start, "EndOfTime" for an end). This corresponds to a null <see cref="Instant"/> when
    ///   constructing an <see cref="Interval"/>.</description></item>
    ///   <item><description><see cref="Instant"/> — a concrete, finite <see cref="Instant"/> was provided.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Once a missing endpoint has been resolved (or an interval has been constructed), the missing state no longer
    /// exists: an <see cref="Interval"/> only distinguishes finite endpoints from infinite ones via
    /// <see cref="Interval.HasStart"/> and <see cref="Interval.HasEnd"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="IntervalEndpoint"/>
    /// <seealso cref="IntervalEndpoints"/>
    public enum IntervalEndpointState
    {
        /// <summary>
        /// The endpoint was omitted from the exchange entirely. It is resolved using the caller-specified
        /// <see cref="MissingEndpointPolicy"/>, never by reading a clock.
        /// </summary>
        Missing = 0,

        /// <summary>
        /// The endpoint was explicitly stated to be unbounded ("StartOfTime" or "EndOfTime"), and becomes a null
        /// <see cref="Instant"/> on the constructed <see cref="Interval"/>.
        /// </summary>
        Infinite = 1,

        /// <summary>
        /// The endpoint is a concrete finite <see cref="Instant"/>.
        /// </summary>
        Instant = 2,
    }
}
