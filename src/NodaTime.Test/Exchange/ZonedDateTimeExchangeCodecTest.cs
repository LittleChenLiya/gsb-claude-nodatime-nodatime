// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Exchange;
using NodaTime.Testing.TimeZones;
using NodaTime.TimeZones;
using NUnit.Framework;
using System;

namespace NodaTime.Test.Exchange
{
    /// <summary>
    /// Tests for <see cref="ZonedDateTimeExchangeCodec"/>.
    /// </summary>
    public class ZonedDateTimeExchangeCodecTest
    {
        private static readonly Instant SampleInstant = Instant.FromUtc(2026, 1, 2, 3, 4, 5);

        private static DateTimeZone FixedZone(string id, int offsetHours) =>
            new SingleTransitionDateTimeZone(Instant.FromUtc(2000, 1, 1, 0, 0),
                Offset.FromHours(offsetHours), Offset.FromHours(offsetHours), id);

        private static IDateTimeZoneProvider FakeProvider(string versionId, params DateTimeZone[] zones)
        {
            var builder = new FakeDateTimeZoneSource.Builder { VersionId = versionId };
            foreach (var zone in zones)
            {
                builder.Add(zone);
            }
            return builder.Build().ToProvider();
        }

        [Test]
        public void Format_IsStableAndExplicit()
        {
            var provider = FakeProvider("v1", FixedZone("Fake/Zone", 2));
            var codec = new ZonedDateTimeExchangeCodec(provider);
            var value = new ZonedDateTime(SampleInstant.PlusNanoseconds(123456789), provider["Fake/Zone"], CalendarSystem.Iso);
            Assert.AreEqual(
                "NZDT/1|i=2026-01-02T03:04:05.123456789Z|o=+02|z=Fake/Zone|c=ISO|v=v1",
                codec.Encode(value));
        }

        [Test]
        public void AmbiguousLocalTime_BothInstantsRoundTripSeparately()
        {
            // Europe/London falls back on 2026-10-25: 01:30 occurs twice, at offsets +01 and +00.
            var zone = DateTimeZoneProviders.Tzdb["Europe/London"];
            var codec = new ZonedDateTimeExchangeCodec(DateTimeZoneProviders.Tzdb);
            var local = new LocalDateTime(2026, 10, 25, 1, 30);
            var early = new ZonedDateTime(local, zone, Offset.FromHours(1));
            var late = new ZonedDateTime(local, zone, Offset.FromHours(0));

            string earlyText = codec.Encode(early);
            string lateText = codec.Encode(late);
            Assert.AreNotEqual(earlyText, lateText);

            var earlyReceipt = codec.Decode(earlyText);
            var lateReceipt = codec.Decode(lateText);
            Assert.AreEqual(ZonedDateTimeExchangeStatus.Exact, earlyReceipt.Status);
            Assert.AreEqual(ZonedDateTimeExchangeStatus.Exact, lateReceipt.Status);
            Assert.AreEqual(early, earlyReceipt.Value);
            Assert.AreEqual(late, lateReceipt.Value);
            // The two occurrences of the same local time remain two distinct instants, one hour apart.
            Assert.AreEqual(early.ToInstant() + Duration.FromHours(1), late.ToInstant());
            Assert.AreEqual(early.ToInstant(), earlyReceipt.Value.ToInstant());
            Assert.AreEqual(late.ToInstant(), lateReceipt.Value.ToInstant());
        }

        [Test]
        public void NanosecondLastDigit_Preserved()
        {
            var provider = FakeProvider("v1", FixedZone("Fake/Zone", 0));
            var codec = new ZonedDateTimeExchangeCodec(provider);
            var instant = SampleInstant.PlusNanoseconds(123456789);
            var value = new ZonedDateTime(instant, provider["Fake/Zone"], CalendarSystem.Iso);

            string text = codec.Encode(value);
            StringAssert.Contains("123456789", text);
            var receipt = codec.Decode(text);
            Assert.AreEqual(instant, receipt.Value.ToInstant());
            Assert.AreEqual(123456789, receipt.Value.NanosecondOfSecond);
        }

        [Test]
        public void NonIsoCalendar_RoundTrips()
        {
            var provider = FakeProvider("v1", FixedZone("Fake/Zone", 0));
            var codec = new ZonedDateTimeExchangeCodec(provider);
            var value = new ZonedDateTime(SampleInstant, provider["Fake/Zone"], CalendarSystem.PersianSimple);

            string text = codec.Encode(value);
            StringAssert.Contains("c=Persian Simple", text);
            var receipt = codec.Decode(text);
            Assert.AreEqual("Persian Simple", receipt.Value.Calendar.Id);
            Assert.AreEqual(value, receipt.Value);
        }

        [Test]
        public void OffsetConflict_SameVersion_ThrowsExplicitly()
        {
            // Same zone ID and same provider version, but the fake provider now reports a different offset.
            var encodeProvider = FakeProvider("v1", FixedZone("Fake/Zone", 2));
            var decodeProvider = FakeProvider("v1", FixedZone("Fake/Zone", 3));
            var value = new ZonedDateTime(SampleInstant, encodeProvider["Fake/Zone"], CalendarSystem.Iso);
            string text = new ZonedDateTimeExchangeCodec(encodeProvider).Encode(value);

            var ex = Assert.Throws<ZonedDateTimeExchangeException>(
                () => new ZonedDateTimeExchangeCodec(decodeProvider).Decode(text));
            StringAssert.Contains("offset", ex!.Message);
            StringAssert.Contains("+02", ex.Message);
            StringAssert.Contains("+03", ex.Message);
        }

        [Test]
        public void VersionMismatch_RejectedByDefault_NotSwallowed()
        {
            var encodeProvider = FakeProvider("v1", FixedZone("Fake/Zone", 2));
            var decodeProvider = FakeProvider("v2", FixedZone("Fake/Zone", 2));
            var value = new ZonedDateTime(SampleInstant, encodeProvider["Fake/Zone"], CalendarSystem.Iso);
            string text = new ZonedDateTimeExchangeCodec(encodeProvider).Encode(value);

            var ex = Assert.Throws<ZonedDateTimeExchangeException>(
                () => new ZonedDateTimeExchangeCodec(decodeProvider).Decode(text));
            StringAssert.Contains("v1", ex!.Message);
            StringAssert.Contains("v2", ex.Message);
        }

        [Test]
        public void VersionMismatch_Reinterpret_ReturnsReceiptWithBothVersionsAndOffsets()
        {
            // The new provider version moved the offset: the instant is preserved, the local time shifts.
            var encodeProvider = FakeProvider("v1", FixedZone("Fake/Zone", 2));
            var decodeProvider = FakeProvider("v2", FixedZone("Fake/Zone", 3));
            var value = new ZonedDateTime(SampleInstant, encodeProvider["Fake/Zone"], CalendarSystem.Iso);
            string text = new ZonedDateTimeExchangeCodec(encodeProvider).Encode(value);

            var codec = new ZonedDateTimeExchangeCodec(decodeProvider, ExchangeVersionMismatchPolicy.Reinterpret);
            var receipt = codec.Decode(text);

            Assert.AreEqual(ZonedDateTimeExchangeStatus.Reinterpreted, receipt.Status);
            Assert.AreEqual("v1", receipt.DeclaredProviderVersion);
            Assert.AreEqual("v2", receipt.ActualProviderVersion);
            Assert.AreEqual(Offset.FromHours(2), receipt.DeclaredOffset);
            Assert.AreEqual(Offset.FromHours(3), receipt.ActualOffset);
            // The exact instant survives; the offset (and hence local time) is reinterpreted.
            Assert.AreEqual(SampleInstant, receipt.Value.ToInstant());
            Assert.AreEqual(Offset.FromHours(3), receipt.Value.Offset);
        }

        [Test]
        public void UnknownZone_Throws_NoLocalFallback()
        {
            var provider = FakeProvider("v1", FixedZone("Fake/Zone", 2));
            var codec = new ZonedDateTimeExchangeCodec(provider);
            var value = new ZonedDateTime(SampleInstant, provider["Fake/Zone"], CalendarSystem.Iso);
            string text = codec.Encode(value).Replace("z=Fake/Zone", "z=Bogus/Nowhere");

            Assert.Throws<DateTimeZoneNotFoundException>(() => codec.Decode(text));
        }

        [Test]
        public void UnknownCalendar_Throws()
        {
            var provider = FakeProvider("v1", FixedZone("Fake/Zone", 2));
            var codec = new ZonedDateTimeExchangeCodec(provider);
            var value = new ZonedDateTime(SampleInstant, provider["Fake/Zone"], CalendarSystem.Iso);
            string text = codec.Encode(value).Replace("c=ISO", "c=NO_SUCH_CALENDAR");

            Assert.Throws<ZonedDateTimeExchangeException>(() => codec.Decode(text));
        }

        [Test]
        public void MalformedRepresentation_Throws()
        {
            var provider = FakeProvider("v1", FixedZone("Fake/Zone", 2));
            var codec = new ZonedDateTimeExchangeCodec(provider);
            Assert.Throws<ZonedDateTimeExchangeException>(() => codec.Decode("not-an-exchange-value"));
            Assert.Throws<ZonedDateTimeExchangeException>(() => codec.Decode("NZDT/1|i=2026-01-02T03:04:05Z"));
        }
    }
}
