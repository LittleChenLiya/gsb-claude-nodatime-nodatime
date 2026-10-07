// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Text;
using NUnit.Framework;
using System;

namespace NodaTime.Test.Text
{
    /// <summary>
    /// Tests for <see cref="OffsetDateTimePattern.CreateStrict"/>: a strict combinator which parses
    /// local date/time fields and combines them with a specified offset, rejecting text which
    /// declares conflicting offsets.
    /// </summary>
    public class StrictOffsetDateTimePatternTest
    {
        // A distinctive offset which is vanishingly unlikely to be the system offset,
        // demonstrating that no system time zone is consulted.
        private static readonly Offset SpecifiedOffset = Offset.FromHoursAndMinutes(5, 30);
        private static readonly LocalDateTime SampleLocal = new LocalDateTime(2026, 10, 7, 12, 30, 45);

        private static IPattern<OffsetDateTime> CreateStrict(string patternText) =>
            OffsetDateTimePattern.CreateStrict(patternText, Cultures.Invariant, SpecifiedOffset);

        [Test]
        public void TwoOffsetFields_SameValue_Succeeds()
        {
            var pattern = CreateStrict("uuuu-MM-dd'T'HH:mm:ss o<+HH:mm> o<+HH:mm>");
            var result = pattern.Parse("2026-10-07T12:30:45 +05:30 +05:30");
            Assert.IsTrue(result.Success);
            Assert.AreEqual(SampleLocal.WithOffset(SpecifiedOffset), result.Value);
        }

        [Test]
        public void TwoOffsetFields_Conflicting_FailsAtSecondField()
        {
            var pattern = CreateStrict("uuuu-MM-dd'T'HH:mm:ss o<+HH:mm> o<+HH:mm>");
            string text = "2026-10-07T12:30:45 +05:30 +02:00";
            var result = pattern.Parse(text);
            Assert.IsFalse(result.Success);
            var exception = (UnparsableValueException) result.Exception;
            // The error is positioned at the second (conflicting) offset field.
            Assert.AreEqual(text.IndexOf("+02:00", StringComparison.Ordinal), exception.Index);
            Assert.AreEqual(27, exception.Index);
        }

        [Test]
        public void SingleOffsetField_MatchingSpecified_Succeeds()
        {
            var pattern = CreateStrict("uuuu-MM-dd'T'HH:mm:ss o<+HH:mm>");
            var result = pattern.Parse("2026-10-07T12:30:45 +05:30");
            Assert.IsTrue(result.Success);
            Assert.AreEqual(SampleLocal.WithOffset(SpecifiedOffset), result.Value);
        }

        [Test]
        public void SingleOffsetField_ConflictingWithSpecified_FailsAtField()
        {
            var pattern = CreateStrict("uuuu-MM-dd'T'HH:mm:ss o<+HH:mm>");
            string text = "2026-10-07T12:30:45 +02:00";
            var result = pattern.Parse(text);
            Assert.IsFalse(result.Success);
            var exception = (UnparsableValueException) result.Exception;
            Assert.AreEqual(text.IndexOf("+02:00", StringComparison.Ordinal), exception.Index);
            Assert.AreEqual(20, exception.Index);
        }

        [Test]
        public void NoOffsetField_ShortText_UsesSpecifiedOffset()
        {
            var pattern = CreateStrict("uuuu-MM-dd'T'HH:mm");
            var result = pattern.Parse("2026-10-07T12:30");
            Assert.IsTrue(result.Success);
            Assert.AreEqual(new LocalDateTime(2026, 10, 7, 12, 30, 0).WithOffset(SpecifiedOffset), result.Value);
            Assert.AreEqual(SpecifiedOffset, result.Value.Offset);
        }

        [Test]
        public void LocalFieldError_PositionPreserved()
        {
            string patternText = "uuuu-MM-dd'T'HH:mm o<+HH:mm>";
            string text = "2026-1X-07T12:30 +05:30";
            var result = CreateStrict(patternText).Parse(text);
            Assert.IsFalse(result.Success);
            // The failure is positioned at the mismatched month field, exactly as for
            // the equivalent non-strict pattern.
            var expected = OffsetDateTimePattern
                .Create(patternText, Cultures.Invariant, OffsetDateTimePattern.DefaultTemplateValue)
                .Parse(text);
            Assert.IsFalse(expected.Success);
            Assert.AreEqual(((UnparsableValueException) expected.Exception).Index,
                ((UnparsableValueException) result.Exception).Index);
            Assert.AreEqual(5, ((UnparsableValueException) result.Exception).Index);
        }

        [Test]
        public void ZoneNameInText_IsNotTreatedAsOffset()
        {
            var pattern = CreateStrict("uuuu-MM-dd'T'HH:mm o<+HH:mm>");
            var result = pattern.Parse("2026-10-07T12:30 Europe/London");
            Assert.IsFalse(result.Success);
            // The failure is at the start of the zone name, where an offset was expected.
            Assert.AreEqual(17, ((UnparsableValueException) result.Exception).Index);
        }

        [Test]
        public void ZonePatternCharacter_IsNotSupported()
        {
            Assert.Throws<InvalidPatternException>(() => CreateStrict("uuuu-MM-dd'T'HH:mm z o<+HH:mm>"));
        }

        [Test]
        public void ThirdOffsetField_IsPatternError() =>
            Assert.Throws<InvalidPatternException>(() => CreateStrict("uuuu-MM-dd o<+HH:mm> o<+HH:mm> o<+HH:mm>"));

        [Test]
        public void StandardPattern_IsNotSupported() =>
            Assert.Throws<InvalidPatternException>(() => CreateStrict("G"));

        [Test]
        public void NullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => OffsetDateTimePattern.CreateStrict(null!, Cultures.Invariant, SpecifiedOffset));
            Assert.Throws<ArgumentNullException>(() => OffsetDateTimePattern.CreateStrict("uuuu-MM-dd", null!, SpecifiedOffset));
        }

        [Test]
        public void Format_RoundTrip_WithTwoOffsetFields()
        {
            var pattern = CreateStrict("uuuu-MM-dd'T'HH:mm:ss o<+HH:mm> o<+HH:mm>");
            var value = SampleLocal.WithOffset(SpecifiedOffset);
            string text = pattern.Format(value);
            Assert.AreEqual("2026-10-07T12:30:45 +05:30 +05:30", text);
            Assert.AreEqual(value, pattern.Parse(text).Value);
        }

        [Test]
        public void Culture_IsUsedForLocalFields()
        {
            // The time separator comes from the culture (for both local fields and offset
            // fields, exactly as in non-strict patterns)...
            var dotCulturePattern = OffsetDateTimePattern.CreateStrict("HH:mm o<+HH:mm>", Cultures.DotTimeSeparator, SpecifiedOffset);
            var result = dotCulturePattern.Parse("12.30 +05.30");
            Assert.IsTrue(result.Success);
            Assert.AreEqual(SpecifiedOffset, result.Value.Offset);
            // ... so the same text does not parse in the invariant culture.
            var invariantPattern = CreateStrict("HH:mm o<+HH:mm>");
            Assert.IsFalse(invariantPattern.Parse("12.30 +05.30").Success);
        }

        [Test]
        public void DifferentCulture_OffsetSemanticsUnchanged()
        {
            // Switching culture changes local field handling, but the explicit offset
            // semantics (matching fields accepted, conflicting fields rejected at the
            // second field, specified offset applied when undeclared) are unchanged.
            var pattern = OffsetDateTimePattern.CreateStrict(
                "dd/MM/uuuu HH:mm o<+HH:mm> o<+HH:mm>", Cultures.FrFr, SpecifiedOffset);

            var same = pattern.Parse("07/10/2026 12:30 +05:30 +05:30");
            Assert.IsTrue(same.Success);
            Assert.AreEqual(SpecifiedOffset, same.Value.Offset);

            string conflictingText = "07/10/2026 12:30 +05:30 +02:00";
            var conflicting = pattern.Parse(conflictingText);
            Assert.IsFalse(conflicting.Success);
            Assert.AreEqual(conflictingText.IndexOf("+02:00", StringComparison.Ordinal),
                ((UnparsableValueException) conflicting.Exception).Index);

            var shortPattern = OffsetDateTimePattern.CreateStrict("dd/MM/uuuu HH:mm", Cultures.FrFr, SpecifiedOffset);
            var noOffset = shortPattern.Parse("07/10/2026 12:30");
            Assert.IsTrue(noOffset.Success);
            Assert.AreEqual(SpecifiedOffset, noOffset.Value.Offset);
        }
    }
}
