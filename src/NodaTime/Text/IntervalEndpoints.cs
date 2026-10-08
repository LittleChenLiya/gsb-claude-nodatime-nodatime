// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using JetBrains.Annotations;
using NodaTime.Utility;
using System;
using static System.FormattableString;

namespace NodaTime.Text
{
    /// <summary>
    /// The two endpoints of an interval in their three-state exchange form, as parsed by
    /// <see cref="IntervalPattern.ParseEndpoints(string)"/> or built explicitly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="Interval"/>, this type retains the distinction between a missing endpoint
    /// (<see cref="IntervalEndpointState.Missing"/>) and an explicitly infinite one
    /// (<see cref="IntervalEndpointState.Infinite"/>). Missing endpoints can therefore be round-tripped and are
    /// only collapsed when <see cref="ToInterval(MissingEndpointPolicy, MissingEndpointPolicy)"/> is called after
    /// each has been resolved with an explicit <see cref="MissingEndpointPolicy"/>.
    /// </para>
    /// <para>
    /// This type does not order-check its endpoints: an end before the start is rejected later, by
    /// <see cref="IntervalPattern"/>, at the position of the end endpoint in the original text, so that callers
    /// parsing text keep the original location of the failure.
    /// </para>
    /// </remarks>
    /// <threadsafety>This type is an immutable value type.</threadsafety>
    /// <seealso cref="IntervalEndpoint"/>
    /// <seealso cref="IntervalPattern"/>
    public readonly struct IntervalEndpoints : IEquatable<IntervalEndpoints>
    {
        /// <summary>
        /// Creates a new set of interval endpoints.
        /// </summary>
        /// <param name="start">The start endpoint (inclusive lower bound).</param>
        /// <param name="end">The end endpoint (exclusive upper bound).</param>
        public IntervalEndpoints(IntervalEndpoint start, IntervalEndpoint end)
        {
            Start = start;
            End = end;
        }

        /// <summary>
        /// Gets the start endpoint (inclusive lower bound).
        /// </summary>
        public IntervalEndpoint Start { get; }

        /// <summary>
        /// Gets the end endpoint (exclusive upper bound).
        /// </summary>
        public IntervalEndpoint End { get; }

        /// <summary>
        /// Returns <c>true</c> if at least one endpoint is missing.
        /// </summary>
        public bool HasMissingEndpoints => Start.IsMissing || End.IsMissing;

        /// <summary>
        /// Converts these endpoints into an <see cref="Interval"/>, using the given policies to resolve any
        /// missing endpoints. Explicitly infinite endpoints become null instants; concrete endpoints are used
        /// directly. No clock is consulted: policies never default to "now".
        /// </summary>
        /// <param name="startPolicy">The policy used to resolve a missing start endpoint.</param>
        /// <param name="endPolicy">The policy used to resolve a missing end endpoint.</param>
        /// <returns>The constructed interval.</returns>
        /// <exception cref="UnparsableValueException">A missing endpoint could not be resolved (the policy was
        /// <see cref="MissingEndpointPolicy.Reject"/>).</exception>
        /// <exception cref="ArgumentOutOfRangeException">The resolved end is earlier than the resolved start.</exception>
        [Pure]
        public Interval ToInterval(MissingEndpointPolicy startPolicy, MissingEndpointPolicy endPolicy)
        {
            Preconditions.CheckNotNull(startPolicy, nameof(startPolicy));
            Preconditions.CheckNotNull(endPolicy, nameof(endPolicy));
            Instant? start = Resolve(Start, startPolicy, isStart: true);
            Instant? end = Resolve(End, endPolicy, isStart: false);
            return new Interval(start, end);
        }

        /// <summary>
        /// Converts these endpoints into an <see cref="Interval"/>. This is equivalent to calling
        /// <see cref="ToInterval(MissingEndpointPolicy, MissingEndpointPolicy)"/> with
        /// <see cref="MissingEndpointPolicy.Reject"/> for both endpoints, so any missing endpoint causes the
        /// conversion to fail.
        /// </summary>
        /// <returns>The constructed interval.</returns>
        /// <exception cref="UnparsableValueException">One of the endpoints is missing.</exception>
        [Pure]
        public Interval ToInterval() => ToInterval(MissingEndpointPolicy.Reject, MissingEndpointPolicy.Reject);

        private static Instant? Resolve(IntervalEndpoint endpoint, MissingEndpointPolicy policy, bool isStart)
        {
            switch (endpoint.State)
            {
                case IntervalEndpointState.Infinite:
                    return null;
                case IntervalEndpointState.Instant:
                    return endpoint.Instant;
                default:
                    // Missing: the caller's policy decides. The library never reads a clock here.
                    return policy.Resolve(isStart);
            }
        }

        /// <inheritdoc />
        public bool Equals(IntervalEndpoints other) => Start.Equals(other.Start) && End.Equals(other.End);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is IntervalEndpoints other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCodeHelper.Hash(Start, End);

        /// <summary>
        /// Deconstructs this value into its two endpoints.
        /// </summary>
        /// <param name="start">The start endpoint.</param>
        /// <param name="end">The end endpoint.</param>
        [Pure]
        public void Deconstruct(out IntervalEndpoint start, out IntervalEndpoint end)
        {
            start = Start;
            end = End;
        }

        /// <summary>
        /// Renders the endpoints as diagnostic text in "start/end" form, with an empty segment for a missing
        /// endpoint and "Infinite" for an explicitly unbounded one.
        /// </summary>
        /// <returns>The diagnostic text.</returns>
        public override string ToString() => Invariant($"{Start}/{End}");

        /// <summary>Equality operator.</summary>
        public static bool operator ==(IntervalEndpoints left, IntervalEndpoints right) => left.Equals(right);

        /// <summary>Inequality operator.</summary>
        public static bool operator !=(IntervalEndpoints left, IntervalEndpoints right) => !(left == right);
    }
}
