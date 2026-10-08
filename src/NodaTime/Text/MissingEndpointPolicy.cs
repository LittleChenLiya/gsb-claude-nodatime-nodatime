// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Annotations;
using NodaTime.Utility;
using System;

namespace NodaTime.Text
{
    /// <summary>
    /// Determines how a missing interval endpoint (an empty segment in interval exchange text) is resolved when
    /// parsing with <see cref="IntervalPattern"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A missing endpoint is deliberately distinct from an explicitly infinite endpoint (the
    /// "StartOfTime"/"EndOfTime" tokens). None of the built-in policies consult a clock: a missing endpoint is
    /// never silently replaced with "now". Callers that want the current time must pass an explicit policy whose
    /// resolver reads the clock themselves.
    /// </para>
    /// <para>
    /// Use the static factory properties and methods to obtain a policy: <see cref="Reject"/>,
    /// <see cref="TreatAsInfinite"/>, <see cref="Use(Instant)"/> or <see cref="Resolve(Func{bool, Instant?})"/>.
    /// Policies are immutable and thread-safe, and may be shared between patterns.
    /// </para>
    /// </remarks>
    /// <threadsafety>All static members of this type are thread-safe. Custom policies are as thread-safe as the
    /// resolver delegate supplied to <see cref="Resolve(Func{bool, Instant?})"/>.</threadsafety>
    /// <seealso cref="IntervalPattern"/>
    /// <seealso cref="IntervalEndpoints"/>
    [Immutable]
    public abstract class MissingEndpointPolicy
    {
        /// <summary>
        /// A policy which fails the parse when an endpoint is missing. This is the default policy of
        /// <see cref="IntervalPattern.ExtendedIso"/>: omitting an endpoint is a parse error rather than an
        /// implicit infinite endpoint or an implicit "now".
        /// </summary>
        public static MissingEndpointPolicy Reject { get; } = new RejectPolicy();

        /// <summary>
        /// A policy which treats a missing endpoint as if it were explicitly infinite ("StartOfTime" for a
        /// missing start, "EndOfTime" for a missing end), i.e. the resolved <see cref="Instant"/> is null.
        /// </summary>
        public static MissingEndpointPolicy TreatAsInfinite { get; } = new InfinitePolicy();

        /// <summary>
        /// Creates a policy which replaces a missing endpoint with the given fixed instant.
        /// </summary>
        /// <param name="instant">The instant to use for a missing endpoint.</param>
        /// <returns>The fixed-instant policy.</returns>
        public static MissingEndpointPolicy Use(Instant instant) => new FixedPolicy(instant);

        /// <summary>
        /// Creates a policy which resolves a missing endpoint using the given caller-supplied resolver.
        /// The resolver receives <c>true</c> when resolving a start endpoint and <c>false</c> when resolving an
        /// end endpoint, and returns the instant to use, or <c>null</c> to treat the endpoint as infinite.
        /// </summary>
        /// <remarks>
        /// The resolver is invoked at most once per missing endpoint. It is the caller's responsibility if it
        /// reads a clock; the library does not do so. If the resolver throws, the parse fails (the exception is
        /// surfaced through the <see cref="ParseResult{T}"/>).
        /// </remarks>
        /// <param name="resolver">The resolver delegate; never null.</param>
        /// <returns>The custom policy.</returns>
        public static MissingEndpointPolicy Resolve(Func<bool, Instant?> resolver) =>
            new DelegatePolicy(Preconditions.CheckNotNull(resolver, nameof(resolver)));

        /// <summary>
        /// Internal constructor; instances are created via the static factory members.
        /// </summary>
        private protected MissingEndpointPolicy()
        {
        }

        /// <summary>
        /// Resolves a missing endpoint.
        /// </summary>
        /// <param name="isStart"><c>true</c> for a start endpoint; <c>false</c> for an end endpoint.</param>
        /// <returns>The instant to use, or <c>null</c> to treat the endpoint as infinite.</returns>
        /// <remarks>Implementations may throw to indicate that the endpoint cannot be resolved; the parse then fails.</remarks>
        internal abstract Instant? Resolve(bool isStart);

        private sealed class RejectPolicy : MissingEndpointPolicy
        {
            internal override Instant? Resolve(bool isStart) => throw new MissingEndpointException(isStart);
        }

        private sealed class InfinitePolicy : MissingEndpointPolicy
        {
            internal override Instant? Resolve(bool isStart) => null;
        }

        private sealed class FixedPolicy : MissingEndpointPolicy
        {
            private readonly Instant instant;

            internal FixedPolicy(Instant instant)
            {
                this.instant = instant;
            }

            internal override Instant? Resolve(bool isStart) => instant;
        }

        private sealed class DelegatePolicy : MissingEndpointPolicy
        {
            private readonly Func<bool, Instant?> resolver;

            internal DelegatePolicy(Func<bool, Instant?> resolver)
            {
                this.resolver = resolver;
            }

            internal override Instant? Resolve(bool isStart) => resolver(isStart);
        }
    }
}
