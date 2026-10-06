// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using JetBrains.Annotations;
using NodaTime.Annotations;

namespace NodaTime
{
    /// <summary>
    /// Represents the result of resolving a <see cref="Period"/> against a calendar system and an
    /// optional start date, via <see cref="Period.Resolve(CalendarSystem, LocalDate?)"/>.
    /// </summary>
    /// <remarks>
    /// A resolution is either <i>resolved</i>, in which case <see cref="Period"/> contains the resolved
    /// period (with months converted to days according to the actual end date, and the remaining units
    /// normalized), or <i>unresolved</i>, which happens when the original period contains months but no
    /// start date was provided to determine their actual length.
    /// </remarks>
    /// <threadsafety>This type is an immutable value type. See the thread safety section of the user guide for more information.</threadsafety>
    [Immutable]
    public readonly struct PeriodResolution
    {
        /// <summary>
        /// A resolution representing a period which could not be resolved due to a missing start date.
        /// </summary>
        /// <value>An unresolved result.</value>
        public static PeriodResolution Unresolved => default;

        /// <summary>
        /// Indicates whether the period was successfully resolved.
        /// </summary>
        /// <value><see langword="true"/> if the period was resolved and <see cref="Period"/> is
        /// non-null; <see langword="false"/> if the period contained months but no start date was
        /// provided.</value>
        public bool IsResolved => Period is not null;

        /// <summary>
        /// The resolved period, or <see langword="null"/> if the period could not be resolved.
        /// </summary>
        /// <remarks>
        /// When resolved, months and weeks are always zero: months are converted to days according to
        /// the actual end date obtained by adding them to the start date, weeks are converted to 7 days
        /// each, and all time properties are normalized to their natural ranges. Years are unchanged.
        /// </remarks>
        /// <value>The resolved period, or <see langword="null"/> for an unresolved result.</value>
        public Period? Period { get; }

        internal PeriodResolution(Period period) => Period = period;
    }
}
