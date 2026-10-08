// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Utility;
using System;
using static System.FormattableString;

namespace NodaTime.Text
{
    /// <summary>
    /// Represents a single interval endpoint (a start or an end) in its three-state exchange form:
    /// missing, explicitly infinite, or a concrete finite <see cref="Instant"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use <see cref="Missing"/> and <see cref="Infinite"/> for the two non-finite states, or construct an
    /// endpoint from a concrete <see cref="Instant"/>. <see cref="State"/> reports which of the three states
    /// the endpoint is in, and <see cref="Instant"/> returns the finite value.
    /// </para>
    /// <para>
    /// The difference between <see cref="Missing"/> and <see cref="Infinite"/> matters when exchanging an
    /// interval as text: a missing endpoint is an empty segment and must be handled with an explicit
    /// <see cref="MissingEndpointPolicy"/>, whereas an infinite endpoint is an explicit "StartOfTime"/"EndOfTime"
    /// token. Neither state ever consults a clock.
    /// </para>
    /// </remarks>
    /// <threadsafety>This type is an immutable value type.</threadsafety>
    /// <seealso cref="IntervalEndpointState"/>
    /// <seealso cref="IntervalEndpoints"/>
    public readonly struct IntervalEndpoint : IEquatable<IntervalEndpoint>
    {
        /// <summary>
        /// An endpoint which was omitted from the exchange entirely. It is resolved with an explicit
        /// <see cref="MissingEndpointPolicy"/>; it is never treated as "now" automatically.
        /// </summary>
        public static IntervalEndpoint Missing { get; } = new IntervalEndpoint(IntervalEndpointState.Missing, NodaConstants.UnixEpoch);

        /// <summary>
        /// An endpoint explicitly stated to be unbounded in its direction ("StartOfTime" for a start,
        /// "EndOfTime" for an end). This becomes a null <see cref="Instant"/> on the constructed interval.
        /// </summary>
        public static IntervalEndpoint Infinite { get; } = new IntervalEndpoint(IntervalEndpointState.Infinite, NodaConstants.UnixEpoch);

        private readonly Instant instant;

        private IntervalEndpoint(IntervalEndpointState state, Instant instant)
        {
            State = state;
            this.instant = instant;
        }

        /// <summary>
        /// Creates an endpoint representing the given concrete finite <see cref="Instant"/>.
        /// </summary>
        /// <param name="instant">The instant at the endpoint.</param>
        public IntervalEndpoint(Instant instant) : this(IntervalEndpointState.Instant, instant)
        {
        }

        /// <summary>
        /// Gets which of the three states (<see cref="IntervalEndpointState.Missing"/>,
        /// <see cref="IntervalEndpointState.Infinite"/> or <see cref="IntervalEndpointState.Instant"/>)
        /// this endpoint is in.
        /// </summary>
        public IntervalEndpointState State { get; }

        /// <summary>
        /// Gets the concrete finite <see cref="Instant"/> of the endpoint.
        /// </summary>
        /// <exception cref="InvalidOperationException">The endpoint is <see cref="Missing"/> or
        /// <see cref="Infinite"/>, rather than a concrete instant.</exception>
        /// <seealso cref="IsInstant"/>
        public Instant Instant
        {
            get
            {
                if (State != IntervalEndpointState.Instant)
                {
                    throw new InvalidOperationException(Invariant($"Endpoint is {State}, not a concrete Instant."));
                }
                return instant;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the endpoint was omitted from the exchange.
        /// </summary>
        public bool IsMissing => State == IntervalEndpointState.Missing;

        /// <summary>
        /// Returns <c>true</c> if the endpoint was explicitly stated to be unbounded.
        /// </summary>
        public bool IsInfinite => State == IntervalEndpointState.Infinite;

        /// <summary>
        /// Returns <c>true</c> if the endpoint is a concrete finite <see cref="Instant"/>.
        /// </summary>
        public bool IsInstant => State == IntervalEndpointState.Instant;

        /// <summary>
        /// Converts a concrete <see cref="Instant"/> into an endpoint with state
        /// <see cref="IntervalEndpointState.Instant"/>.
        /// </summary>
        /// <param name="instant">The instant at the endpoint.</param>
        /// <returns>An endpoint representing <paramref name="instant"/>.</returns>
        public static implicit operator IntervalEndpoint(Instant instant) => new IntervalEndpoint(instant);

        /// <summary>
        /// Renders the endpoint for diagnostics: an empty string for a missing endpoint, "Infinite" for an
        /// explicitly unbounded endpoint, and the extended ISO-8601 text for a concrete instant. Endpoint position
        /// (rather than this method) determines the "StartOfTime"/"EndOfTime" token used by
        /// <see cref="IntervalPattern"/> when formatting a whole interval.
        /// </summary>
        /// <returns>The diagnostic text for this endpoint.</returns>
        public override string ToString() => State switch
        {
            IntervalEndpointState.Missing => "",
            IntervalEndpointState.Infinite => "Infinite",
            _ => InstantPattern.ExtendedIso.Format(instant),
        };

        /// <summary>
        /// Determines whether the specified endpoint is equal to this one. Two endpoints are equal if they have
        /// the same state and, when the state is <see cref="IntervalEndpointState.Instant"/>, the same instant.
        /// </summary>
        /// <param name="other">The endpoint to compare with this one.</param>
        /// <returns><c>true</c> if the endpoints are equal; <c>false</c> otherwise.</returns>
        public bool Equals(IntervalEndpoint other) => State == other.State &&
            (State != IntervalEndpointState.Instant || instant == other.instant);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is IntervalEndpoint other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCodeHelper.Hash(State, instant);

        /// <summary>
        /// Implements the equality operator.
        /// </summary>
        /// <param name="left">The left endpoint.</param>
        /// <param name="right">The right endpoint.</param>
        /// <returns><c>true</c> if the endpoints are equal; <c>false</c> otherwise.</returns>
        public static bool operator ==(IntervalEndpoint left, IntervalEndpoint right) => left.Equals(right);

        /// <summary>
        /// Implements the inequality operator.
        /// </summary>
        /// <param name="left">The left endpoint.</param>
        /// <param name="right">The right endpoint.</param>
        /// <returns><c>false</c> if the endpoints are equal; <c>true</c> otherwise.</returns>
        public static bool operator !=(IntervalEndpoint left, IntervalEndpoint right) => !(left == right);
    }
}
