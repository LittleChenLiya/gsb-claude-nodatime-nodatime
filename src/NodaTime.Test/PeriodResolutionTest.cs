// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NUnit.Framework;
using System;

namespace NodaTime.Test
{
    /// <summary>
    /// Tests for <see cref="Period.Resolve(LocalDate?, CalendarSystem?)"/> and <see cref="PeriodResolution"/>.
    /// </summary>
    public class PeriodResolutionTest
    {
        // Period example 1: one month from January 31st resolves to the actual
        // length of the month ending at the real end date (February 28th 2026).
        [Test]
        public void Example1_OneMonthFromJanuary31st_ResolvesToActualDays()
        {
            var resolution = Period.FromMonths(1).Resolve(new LocalDate(2026, 1, 31));
            Assert.IsTrue(resolution.IsResolved);
            Assert.AreEqual(28L, resolution.Days);
            Assert.AreEqual(Duration.Zero, resolution.Time);
        }

        // Period example 2: the same period from a different start date resolves
        // to a different, real day count (March 31st 2026 -> April 30th 2026).
        [Test]
        public void Example2_OneMonthFromMarch31st_ResolvesToActualDays()
        {
            var resolution = Period.FromMonths(1).Resolve(new LocalDate(2026, 3, 31));
            Assert.IsTrue(resolution.IsResolved);
            Assert.AreEqual(30L, resolution.Days);
            Assert.AreEqual(Duration.Zero, resolution.Time);
        }

        // Period example 3: without a start date, a month-based period is pending.
        [Test]
        public void Example3_OneMonthWithoutStart_IsUnresolved()
        {
            var resolution = Period.FromMonths(1).Resolve(null);
            Assert.IsFalse(resolution.IsResolved);
            Assert.AreEqual(0L, resolution.Days);
            Assert.AreEqual(Duration.Zero, resolution.Time);
        }

        [Test]
        public void HoursAndMinutes_ConvertExactly()
        {
            var period = Period.FromHours(36) + Period.FromMinutes(90);
            var resolution = period.Resolve(null);
            Assert.IsTrue(resolution.IsResolved);
            Assert.AreEqual(0L, resolution.Days);
            Assert.AreEqual(Duration.FromHours(36) + Duration.FromMinutes(90), resolution.Time);
        }

        [Test]
        public void HoursAndMinutes_ConvertExactly_WithStartAndExplicitIsoCalendar()
        {
            var period = Period.FromHours(25) + Period.FromMinutes(30);
            var resolution = period.Resolve(new LocalDate(2026, 1, 31), CalendarSystem.Iso);
            Assert.IsTrue(resolution.IsResolved);
            Assert.AreEqual(0L, resolution.Days);
            Assert.AreEqual(Duration.FromHours(25) + Duration.FromMinutes(30), resolution.Time);
        }

        [Test]
        public void SubsecondUnits_ConvertExactly()
        {
            var period = Period.FromSeconds(90) + Period.FromMilliseconds(1500) + Period.FromNanoseconds(7);
            var resolution = period.Resolve(null);
            Assert.AreEqual(Duration.FromSeconds(91) + Duration.FromMilliseconds(500) + Duration.FromNanoseconds(7),
                resolution.Time);
        }

        [Test]
        public void MonthsWeeksAndDays_CombineWithActualMonthLength()
        {
            var period = Period.FromMonths(1) + Period.FromWeeks(2) + Period.FromDays(3);
            var resolution = period.Resolve(new LocalDate(2026, 1, 31));
            Assert.IsTrue(resolution.IsResolved);
            Assert.AreEqual(28L + 14L + 3L, resolution.Days);
        }

        [Test]
        public void MonthsWithoutStart_WeeksAndDaysStillCounted()
        {
            var period = Period.FromMonths(2) + Period.FromWeeks(1) + Period.FromDays(2);
            var resolution = period.Resolve(null);
            Assert.IsFalse(resolution.IsResolved);
            Assert.AreEqual(9L, resolution.Days);
        }

        [Test]
        public void NoMonths_StartNotRequired()
        {
            var resolution = Period.FromDays(5).Resolve(null);
            Assert.IsTrue(resolution.IsResolved);
            Assert.AreEqual(5L, resolution.Days);
        }

        [Test]
        public void Years_NotSupported()
        {
            Assert.Throws<InvalidOperationException>(() => Period.FromYears(1).Resolve(new LocalDate(2026, 1, 1)));
        }

        [Test]
        public void NonIsoCalendar_NotSupported()
        {
            Assert.Throws<ArgumentException>(() => Period.FromDays(1).Resolve(new LocalDate(2026, 1, 1), CalendarSystem.Julian));
        }

        [Test]
        public void ResolutionEquality()
        {
            var resolution = Period.FromMonths(1).Resolve(new LocalDate(2026, 1, 31));
            var equal = Period.FromMonths(1).Resolve(new LocalDate(2026, 1, 31));
            var different = Period.FromMonths(1).Resolve(new LocalDate(2026, 3, 31));
            Assert.AreEqual(resolution, equal);
            Assert.IsTrue(resolution == equal);
            Assert.IsTrue(resolution != different);
            Assert.AreEqual(resolution.GetHashCode(), equal.GetHashCode());
        }

        // The original Normalize behavior is unchanged by the new entry point.
        [Test]
        public void Normalize_RemainsCompatible()
        {
            var original = new PeriodBuilder { Months = 12, Hours = 25, Days = 1 }.Build();
            var normalized = original.Normalize();
            var expected = new PeriodBuilder { Months = 12, Hours = 1, Days = 2 }.Build();
            Assert.AreEqual(expected, normalized);
        }
    }
}
