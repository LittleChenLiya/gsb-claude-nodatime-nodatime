// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Text;
using NUnit.Framework;
using System;
using System.Globalization;

namespace NodaTime.Test.Text
{
    /// <summary>
    /// Tests for the strict local-date/time-to-offset parse combinator:
    /// a text-declared offset must match the explicitly specified offset,
    /// a missing offset is assigned, and duplicate/conflicting declarations fail.
    /// </summary>
    public class StrictOffsetDateTimePatternTest
    {
        private const string LocalPatternText = "uuuu'-'MM'-'dd' 'HH':'mm";
        private const string OffsetPatternText = "+HH':'mm";
        private static readonly Offset ExpectedOffset = Offset.FromHoursAndMinutes(2, 30);
        private static readonly LocalDateTime SampleLocal = new LocalDateTime(2026, 10, 7, 13, 45);

        private static IPattern<OffsetDateTime> CreatePattern(Offset expectedOffset, CultureInfo? culture = null)
        {
            var localPattern = culture is null
                ? LocalDateTimePattern.CreateWithInvariantCulture(LocalPatternText)
                : LocalDateTimePattern.Create(LocalPatternText, culture, LocalDateTimePattern.DefaultTemplateValue);
            var offsetPattern = OffsetPattern.CreateWithInvariantCulture(OffsetPatternText);
            return StrictOffsetDateTimePattern.Create(localPattern, offsetPattern, expectedOffset);
        }

        [Test]
        public void NoOffset_AssignsSpecifiedOffset()
        {
            var pattern = CreatePattern(ExpectedOffset);
            var result = pattern.Parse("2026-10-07 13:45");
            Assert.IsTrue(result.Success, result.Success ? "" : result.Exception.Message);
            Assert.AreEqual(SampleLocal.WithOffset(ExpectedOffset), result.Value);
        }

        [Test]
        public void MatchingOffset_Accepted()
        {
            var pattern = CreatePattern(ExpectedOffset);
            var result = pattern.Parse("2026-10-07 13:45+02:30");
            Assert.IsTrue(result.Success, result.Success ? "" : result.Exception.Message);
            Assert.AreEqual(SampleLocal.WithOffset(ExpectedOffset), result.Value);
        }

        [Test]
        public void ConflictingOffset_RejectedWithPosition()
        {
            var pattern = CreatePattern(ExpectedOffset);
            const string text = "2026-10-07 13:45-08:00";
            var result = pattern.Parse(text);
            Assert.IsFalse(result.Success);
            var exception = (UnparsableValueException) result.Exception;
            Assert.AreEqual(text, exception.Value);
            // The error is reported at the start of the declared (conflicting) offset.
            Assert.AreEqual(text.IndexOf("-08:00", StringComparison.Ordinal), exception.Index);
        }

        [Test]
        public void TwoOffsets_SameValue_SecondRejectedAsTrailingText()
        {
            var pattern = CreatePattern(ExpectedOffset);
            const string text = "2026-10-07 13:45+02:30 +02:30";
            var result = pattern.Parse(text);
            Assert.IsFalse(result.Success);
            var exception = (UnparsableValueException) result.Exception;
            Assert.AreEqual(text, exception.Value);
            // The first, matching offset is consumed; the second (duplicate) one is trailing text.
            Assert.AreEqual(text.IndexOf(" +02:30", StringComparison.Ordinal), exception.Index);
        }

        [Test]
        public void TwoOffsets_Conflicting_FirstConflictReported()
        {
            var pattern = CreatePattern(ExpectedOffset);
            const string text = "2026-10-07 13:45-08:00 +02:30";
            var result = pattern.Parse(text);
            Assert.IsFalse(result.Success);
            var exception = (UnparsableValueException) result.Exception;
            // The first conflicting offset is reported before any second field is examined.
            Assert.AreEqual(text.IndexOf("-08:00", StringComparison.Ordinal), exception.Index);
        }

        [Test]
        public void TimeZoneName_IsNotAnOffset()
        {
            var pattern = CreatePattern(ExpectedOffset);
            // "Europe/Paris" is not an offset, so this fails to parse as an offset (rather than
            // being mapped to the zone's offset or the system local offset).
            var result = pattern.Parse("2026-10-07 13:45 Europe/Paris");
            Assert.IsFalse(result.Success);
            var exception = (UnparsableValueException) result.Exception;
            Assert.AreEqual("2026-10-07 13:45 Europe/Paris", exception.Value);
            // Failure points at the offset slot (the space where the '+' sign was expected),
            // i.e. immediately after the local date/time text.
            Assert.AreEqual("2026-10-07 13:45".Length, exception.Index);
        }

        [Test]
        public void LocalParseError_PreservesPositionFromLocalPattern()
        {
            var pattern = CreatePattern(ExpectedOffset);
            const string text = "2026-10-07 99:45";
            var result = pattern.Parse(text);
            Assert.IsFalse(result.Success);
            var exception = (UnparsableValueException) result.Exception;
            Assert.AreEqual(text, exception.Value);
            // The bad hour position is owned by the local date/time pattern.
            Assert.AreEqual(text.IndexOf("99", StringComparison.Ordinal), exception.Index);
        }

        [Test]
        public void FrenchCulture_OffsetSemanticsRemainExplicit()
        {
            // Same local shape, different culture for the date/time part: the offset is still
            // parsed with the explicit offset pattern/culture, and a conflict is still rejected.
            var culture = CultureInfo.ReadOnly(new CultureInfo("fr-FR"));
            var pattern = CreatePattern(ExpectedOffset, culture);

            var ok = pattern.Parse("2026-10-07 13:45+02:30");
            Assert.IsTrue(ok.Success, ok.Success ? "" : ok.Exception.Message);
            Assert.AreEqual(SampleLocal.WithOffset(ExpectedOffset), ok.Value);

            var missing = pattern.Parse("2026-10-07 13:45");
            Assert.IsTrue(missing.Success, missing.Success ? "" : missing.Exception.Message);
            Assert.AreEqual(SampleLocal.WithOffset(ExpectedOffset), missing.Value);

            const string conflictText = "2026-10-07 13:45+05:00";
            var conflict = pattern.Parse(conflictText);
            Assert.IsFalse(conflict.Success);
            var exception = (UnparsableValueException) conflict.Exception;
            Assert.AreEqual(conflictText.IndexOf("+05:00", StringComparison.Ordinal), exception.Index);
        }

        [Test]
        public void NullAndEmpty_FailLikeTheUnderlyingPatterns()
        {
            var pattern = CreatePattern(ExpectedOffset);
            var nullResult = pattern.Parse(null!);
            Assert.IsFalse(nullResult.Success);
            Assert.Throws<ArgumentNullException>(() => nullResult.GetValueOrThrow());

            var emptyResult = pattern.Parse("");
            Assert.IsFalse(emptyResult.Success);
            Assert.Throws<UnparsableValueException>(() => emptyResult.GetValueOrThrow());
        }

        [Test]
        public void Format_Roundtrips()
        {
            var pattern = CreatePattern(ExpectedOffset);
            var value = SampleLocal.WithOffset(ExpectedOffset);
            Assert.AreEqual("2026-10-07 13:45+02:30", pattern.Format(value));
        }
    }
}
