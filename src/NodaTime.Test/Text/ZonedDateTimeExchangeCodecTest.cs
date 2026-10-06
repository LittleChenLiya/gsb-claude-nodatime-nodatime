// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Testing.TimeZones;
using NodaTime.Text;
using NUnit.Framework;
using System;

namespace NodaTime.Test.Text
{
    /// <summary>
    /// Acceptance tests for <see cref="ZonedDateTimeExchangeCodec"/>.
    /// </summary>
    public class ZonedDateTimeExchangeCodecTest
    {
        private static readonly DateTimeZone London = DateTimeZoneProviders.Tzdb["Europe/London"];
        private static readonly DateTimeZone Paris = DateTimeZoneProviders.Tzdb["Europe/Paris"];

        [Test]
        public void RoundTrip_PreservesInstantZoneCalendarAndProviderVersion()
        {
            var instant = Instant.FromUnixTimeSeconds(1_760_000_000).PlusNanoseconds(123_456_789);
            var value = new ZonedDateTime(instant, Paris, CalendarSystem.HebrewCivil);
            var codec = ZonedDateTimeExchangeCodec.Tzdb;

            string encoded = codec.Encode(value);

            StringAssert.StartsWith("NODAZDT/1|", encoded);
            StringAssert.Contains("|i=1760000000.123456789|", encoded);
            StringAssert.Contains("|z=Europe/Paris|", encoded);
            StringAssert.Contains("|c=" + CalendarSystem.HebrewCivil.Id + "|", encoded);
            StringAssert.Contains("|v=" + DateTimeZoneProviders.Tzdb.VersionId + "|", encoded);

            var result = codec.Decode(encoded);
            Assert.IsTrue(result.Success, result.Error!);
            Assert.AreEqual(value, result.Value);
            Assert.AreEqual(CalendarSystem.HebrewCivil.Id, result.Value.Calendar.Id);
            Assert.IsFalse(result.ProviderVersionMismatch);
            Assert.IsEmpty(result.Warnings);
        }

        [Test]
        public void AmbiguousLocalTime_BothInstantsRoundTripSeparately()
        {
            // Europe/London falls back on 2025-10-26: 01:30 + nanos occurs twice.
            var local = new LocalDateTime(2025, 10, 26, 1, 30, 0).PlusNanoseconds(123_456_789);
            var mapping = London.MapLocal(local);
            Assert.AreEqual(2, mapping.Count);
            var earlier = mapping.First();
            var later = mapping.Last();
            Assert.AreEqual(local, earlier.LocalDateTime);
            Assert.AreEqual(local, later.LocalDateTime);
            Assert.AreNotEqual(earlier.ToInstant(), later.ToInstant());

            var codec = ZonedDateTimeExchangeCodec.Tzdb;
            string earlierEncoded = codec.Encode(earlier);
            string laterEncoded = codec.Encode(later);
            // The local string is identical; the payloads are not, because the instant is explicit.
            Assert.AreNotEqual(earlierEncoded, laterEncoded);

            var earlierResult = codec.Decode(earlierEncoded);
            var laterResult = codec.Decode(laterEncoded);
            Assert.IsTrue(earlierResult.Success, earlierResult.Error!);
            Assert.IsTrue(laterResult.Success, laterResult.Error!);
            Assert.AreEqual(earlier, earlierResult.Value);
            Assert.AreEqual(later, laterResult.Value);
            Assert.AreNotEqual(earlierResult.Value.ToInstant(), laterResult.Value.ToInstant());
        }

        [Test]
        public void Nanoseconds_UnitsDigitPreserved()
        {
            var codec = ZonedDateTimeExchangeCodec.Tzdb;
            foreach (long seconds in new[] { 1_700_000_000L, -100L })
            {
                foreach (long nanos in new[] { 1L, 7L, 123_456_787L, 999_999_999L })
                {
                    var instant = Instant.FromUnixTimeSeconds(seconds).PlusNanoseconds(nanos);
                    var value = new ZonedDateTime(instant, Paris, CalendarSystem.Iso);
                    string encoded = codec.Encode(value);
                    StringAssert.Contains("|i=" + seconds + "." + nanos.ToString("D9") + "|", encoded);
                    var result = codec.Decode(encoded);
                    Assert.IsTrue(result.Success, result.Error!);
                    Assert.AreEqual(nanos, result.Value.ToInstant().ToUnixTimeSecondsAndNanoseconds().nanoseconds);
                    Assert.AreEqual(value, result.Value);
                }
            }
        }

        [Test]
        public void OffsetConflict_WhenProviderChangesOffset_IsExplicitFailure()
        {
            var transition = Instant.FromUtc(2020, 1, 1, 0, 0);
            var zoneV1 = new SingleTransitionDateTimeZone(transition, Offset.FromHours(1), Offset.FromHours(2), "Fake/Zone");
            var zoneV2 = new SingleTransitionDateTimeZone(transition, Offset.FromHours(1), Offset.FromHours(3), "Fake/Zone");
            var builderV1 = new FakeDateTimeZoneSource.Builder { zoneV1 };
            builderV1.VersionId = "FakeTzdb: 1";
            var providerV1 = builderV1.Build().ToProvider();
            var builderV2 = new FakeDateTimeZoneSource.Builder { zoneV2 };
            builderV2.VersionId = "FakeTzdb: 1";
            var providerV2 = builderV2.Build().ToProvider();

            var instant = Instant.FromUtc(2025, 6, 1, 0, 0);
            var value = new ZonedDateTime(instant, zoneV1, CalendarSystem.Iso);
            string encoded = new ZonedDateTimeExchangeCodec(providerV1).Encode(value);

            var decodeCodec = new ZonedDateTimeExchangeCodec(providerV2);
            var rejected = decodeCodec.Decode(encoded);
            Assert.IsFalse(rejected.Success);
            StringAssert.Contains("conflict", rejected.Error!.ToLowerInvariant());
            Assert.Throws<InvalidOperationException>(() => { var _ = rejected.Value; });

            // An offset conflict is a data integrity failure, not a version skew: it is not reinterpretable.
            var reinterpreted = decodeCodec.Decode(encoded, ZonedDateTimeExchangeVersionPolicy.ReinterpretWithReceipt);
            Assert.IsFalse(reinterpreted.Success);
            StringAssert.Contains("conflict", reinterpreted.Error!.ToLowerInvariant());
        }

        [Test]
        public void ProviderVersionMismatch_RejectedByDefault()
        {
            var value = new ZonedDateTime(Instant.FromUnixTimeSeconds(1_760_000_000).PlusNanoseconds(42), London, CalendarSystem.Iso);
            string encoded = ZonedDateTimeExchangeCodec.Tzdb.Encode(value);
            var builder = new FakeDateTimeZoneSource.Builder { London };
            builder.VersionId = "TZDB: 2999z (fake)";
            var fakeProvider = builder.Build().ToProvider();

            var result = new ZonedDateTimeExchangeCodec(fakeProvider).Decode(encoded);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.ProviderVersionMismatch);
            Assert.AreEqual(DateTimeZoneProviders.Tzdb.VersionId, result.EncodedProviderVersionId);
            Assert.AreEqual("TZDB: 2999z (fake)", result.DecodingProviderVersionId);
            StringAssert.Contains("version", result.Error!.ToLowerInvariant());
        }

        [Test]
        public void ProviderVersionMismatch_ReinterpretedWithReceipt_NeverSilent()
        {
            var value = new ZonedDateTime(Instant.FromUnixTimeSeconds(1_760_000_000).PlusNanoseconds(42), London, CalendarSystem.Iso);
            string encoded = ZonedDateTimeExchangeCodec.Tzdb.Encode(value);
            var builder = new FakeDateTimeZoneSource.Builder { London };
            builder.VersionId = "TZDB: 2999z (fake)";
            var fakeProvider = builder.Build().ToProvider();

            var result = new ZonedDateTimeExchangeCodec(fakeProvider)
                .Decode(encoded, ZonedDateTimeExchangeVersionPolicy.ReinterpretWithReceipt);

            Assert.IsTrue(result.Success, result.Error!);
            Assert.AreEqual(value, result.Value);
            Assert.IsTrue(result.ProviderVersionMismatch);
            Assert.AreEqual(DateTimeZoneProviders.Tzdb.VersionId, result.EncodedProviderVersionId);
            Assert.AreEqual("TZDB: 2999z (fake)", result.DecodingProviderVersionId);
            Assert.AreEqual(1, result.Warnings.Count);
            StringAssert.Contains(DateTimeZoneProviders.Tzdb.VersionId, result.Warnings[0]);
            StringAssert.Contains("TZDB: 2999z (fake)", result.Warnings[0]);
        }

        [Test]
        public void UnknownZone_FailsWithoutLocalFallback()
        {
            var value = new ZonedDateTime(Instant.FromUnixTimeSeconds(1_760_000_000), London, CalendarSystem.Iso);
            string encoded = ZonedDateTimeExchangeCodec.Tzdb.Encode(value)
                .Replace("z=Europe/London", "z=Mars/Olympus_Mons");

            var result = ZonedDateTimeExchangeCodec.Tzdb.Decode(encoded);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Mars/Olympus_Mons", result.Error!);
            StringAssert.Contains("no fallback", result.Error!);
        }

        [Test]
        public void UnknownCalendar_Fails()
        {
            var value = new ZonedDateTime(Instant.FromUnixTimeSeconds(1_760_000_000), London, CalendarSystem.Iso);
            string encoded = ZonedDateTimeExchangeCodec.Tzdb.Encode(value).Replace("c=ISO", "c=Bogus");

            var result = ZonedDateTimeExchangeCodec.Tzdb.Decode(encoded);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Bogus", result.Error!);
        }

        [Test]
        public void TamperedLocalTimeField_Fails()
        {
            var value = new ZonedDateTime(Instant.FromUnixTimeSeconds(1_760_000_000), London, CalendarSystem.Iso);
            string encoded = ZonedDateTimeExchangeCodec.Tzdb.Encode(value).Replace("l=2025-", "l=2026-");

            var result = ZonedDateTimeExchangeCodec.Tzdb.Decode(encoded);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("inconsistent", result.Error!);
        }

        [Test]
        public void MalformedPayloads_Fail()
        {
            var codec = ZonedDateTimeExchangeCodec.Tzdb;
            Assert.IsFalse(codec.Decode(null!).Success);
            Assert.IsFalse(codec.Decode("not a payload").Success);
            Assert.IsFalse(codec.Decode("NODAZDT/1|i=1.000000000").Success); // missing fields
            Assert.IsFalse(codec.Decode("NODAZDT/1|i=1.5|o=0|z=UTC|c=ISO|v=x|l=y").Success); // bad instant field
            Assert.IsFalse(codec.Decode("NODAZDT/1|i=1.000000000|o=0|z=UTC|c=ISO|v=x|l=y|q=1").Success); // unknown key
            Assert.IsFalse(codec.Decode("NODAZDT/1|i=1.000000000|i=2.000000000|o=0|z=UTC|c=ISO|v=x|l=y").Success); // duplicate key
        }
    }
}
