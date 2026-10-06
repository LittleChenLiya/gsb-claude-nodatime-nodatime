// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using JetBrains.Annotations;
using NodaTime.Annotations;
using NodaTime.Utility;
using System;

namespace NodaTime
{
    /// <summary>
    /// Represents the result of resolving a <see cref="Period"/> into a number of whole days and an
    /// exact time portion, via <see cref="Period.Resolve(LocalDate?, CalendarSystem?)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Months within a period vary in length, so they can only be converted to days when a start
    /// date is available: the actual end date (the start date plus the months, in the relevant
    /// calendar) determines the number of days. When no start date is available and the period
    /// contains months, the resolution is "pending" (<see cref="IsResolved"/> is <see langword="false"/>)
    /// and <see cref="Days"/> only reflects the week/day portion of the period.
    /// </para>
    /// <para>
    /// The time portion (hours, minutes, seconds and subsecond units) is always converted exactly,
    /// with no reference date required.
    /// </para>
    /// </remarks>
    /// <threadsafety>This type is an immutable value type. See the thread safety section of the user guide for more information.</threadsafety>
    [Immutable]
    public readonly struct PeriodResolution : IEquatable<PeriodResolution>
    {
        /// <summary>
        /// Indicates whether every part of the period was resolved into <see cref="Days"/> and
        /// <see cref="Time"/>. This is <see langword="false"/> when the period contains months but
        /// no start date was provided; in that case <see cref="Days"/> contains only the week/day
        /// portion of the period, and the months remain unresolved (pending).
        /// </summary>
        /// <value>Whether the period was fully resolved.</value>
        public bool IsResolved { get; }

        /// <summary>
        /// The total number of whole days in the resolved period: the weeks and days of the
        /// original period, plus the actual number of days between the start date and the end
        /// date obtained by adding the period's months to it (when a start date was provided).
        /// </summary>
        /// <value>The resolved number of whole days.</value>
        public long Days { get; }

        /// <summary>
        /// The exact time portion of the period (hours, minutes, seconds and subsecond units).
        /// This is always converted exactly, whether or not a start date was provided.
        /// </summary>
        /// <value>The exact time portion of the period.</value>
        public Duration Time { get; }

        internal PeriodResolution(bool isResolved, long days, Duration time)
        {
            IsResolved = isResolved;
            Days = days;
            Time = time;
        }

        /// <summary>
        /// Compares this resolution with another for equality, comparing each property.
        /// </summary>
        /// <param name="other">The resolution to compare this one with.</param>
        /// <returns>True if the two resolutions are equal; false otherwise.</returns>
        public bool Equals(PeriodResolution other) =>
            IsResolved == other.IsResolved && Days == other.Days && Time == other.Time;

        /// <summary>
        /// Compares this resolution with another for equality, comparing each property.
        /// </summary>
        /// <param name="obj">The object to compare this one with.</param>
        /// <returns>True if the other object is an equal resolution; false otherwise.</returns>
        public override bool Equals(object? obj) => obj is PeriodResolution other && Equals(other);

        /// <summary>
        /// Returns the hash code for this resolution, consistent with <see cref="Equals(PeriodResolution)"/>.
        /// </summary>
        /// <returns>The hash code for this resolution.</returns>
        public override int GetHashCode() =>
            HashCodeHelper.Initialize().Hash(IsResolved).Hash(Days).Hash(Time).Value;

        /// <summary>
        /// Compares two resolutions for equality.
        /// </summary>
        /// <param name="left">The first resolution to compare.</param>
        /// <param name="right">The second resolution to compare.</param>
        /// <returns>True if the two resolutions are equal; false otherwise.</returns>
        public static bool operator ==(PeriodResolution left, PeriodResolution right) => left.Equals(right);

        /// <summary>
        /// Compares two resolutions for inequality.
        /// </summary>
        /// <param name="left">The first resolution to compare.</param>
        /// <param name="right">The second resolution to compare.</param>
        /// <returns>True if the two resolutions are not equal; false otherwise.</returns>
        public static bool operator !=(PeriodResolution left, PeriodResolution right) => !left.Equals(right);

        /// <summary>
        /// Returns a short diagnostic string describing this resolution.
        /// </summary>
        /// <returns>A string representation of this resolution.</returns>
        public override string ToString() =>
            IsResolved ? $"{Days} days, {Time}" : $"pending (months unresolved); {Days} days, {Time}";
    }
}
