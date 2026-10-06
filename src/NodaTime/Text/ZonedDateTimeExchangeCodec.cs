// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Annotations;
using NodaTime.TimeZones;
using NodaTime.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace NodaTime.Text
{
    /// <summary>
    /// Encodes and decodes <see cref="ZonedDateTime"/> values as explicit, self-describing
    /// exchange payloads, preserving the exact <see cref="Instant"/>, the declared UTC offset,
    /// the time zone ID, the calendar ID and the ID of the time zone provider version in use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike the local-date/time text produced by <see cref="ZonedDateTimePattern"/>, the payload
    /// carries the instant itself as integer Unix seconds plus a nanosecond adjustment, so the
    /// instant is never recovered from a (potentially ambiguous or skipped) local time string and
    /// never passes through a floating-point representation. The local date/time is included only
    /// as a derived, human-readable cross-check which is validated on decoding; it is never treated
    /// as the identity of the value.
    /// </para>
    /// <para>
    /// The payload format is a single line of pipe-separated key/value fields:
    /// <c>NODAZDT/1|i=&lt;unix-seconds&gt;.&lt;nanosecond-of-second, 9 digits&gt;|o=&lt;offset-seconds&gt;|z=&lt;zone-id&gt;|c=&lt;calendar-id&gt;|v=&lt;provider-version-id&gt;|l=&lt;local ISO-8601 date/time&gt;</c>.
    /// Decoding requires the zone and calendar to be available (an unknown zone is an error; the
    /// codec never falls back to the local system zone), requires the declared offset to agree with
    /// the zone's actual offset at the instant, and handles a provider version change according to
    /// the supplied <see cref="ZonedDateTimeExchangeVersionPolicy"/> (reject, or reinterpret with
    /// the mismatch recorded on the result receipt).
    /// </para>
    /// </remarks>
    /// <threadsafety>This type is immutable and instances may be shared freely between threads.</threadsafety>
    [Immutable]
    public sealed class ZonedDateTimeExchangeCodec
    {
        /// <summary>
        /// The format identifier and version which every payload starts with.
        /// </summary>
        public const string FormatIdentifier = "NODAZDT/1";

        private const char FieldSeparator = '|';

        /// <summary>
        /// The provider used to stamp payloads with a version ID when encoding, and to resolve
        /// time zone IDs (and check versions) when decoding.
        /// </summary>
        public IDateTimeZoneProvider Provider { get; }

        /// <summary>
        /// Creates a codec which stamps and verifies payloads against the given provider.
        /// </summary>
        /// <param name="provider">The time zone provider to encode with and decode against.</param>
        public ZonedDateTimeExchangeCodec(IDateTimeZoneProvider provider) =>
            Provider = Preconditions.CheckNotNull(provider, nameof(provider));

        /// <summary>
        /// A codec backed by <see cref="DateTimeZoneProviders.Tzdb"/>.
        /// </summary>
        public static ZonedDateTimeExchangeCodec Tzdb => new ZonedDateTimeExchangeCodec(DateTimeZoneProviders.Tzdb);

        /// <summary>
        /// Encodes a zoned date/time as an exchange payload.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <returns>The payload text.</returns>
        /// <exception cref="ArgumentException">A field of the value (or the provider version ID)
        /// contains a character which cannot be represented in the payload format.</exception>
        public string Encode(ZonedDateTime value)
        {
            var (seconds, nanoseconds) = value.ToInstant().ToUnixTimeSecondsAndNanoseconds();
            string zoneId = CheckField(value.Zone.Id, "zone ID");
            string calendarId = CheckField(value.Calendar.Id, "calendar ID");
            string versionId = CheckField(Provider.VersionId, "provider version ID");
            string local = CheckField(LocalDateTimePattern.ExtendedIso.Format(value.LocalDateTime), "local date/time");
            return FormatIdentifier
                + "|i=" + seconds.ToString(CultureInfo.InvariantCulture) + "." + nanoseconds.ToString("D9", CultureInfo.InvariantCulture)
                + "|o=" + value.Offset.Seconds.ToString(CultureInfo.InvariantCulture)
                + "|z=" + zoneId
                + "|c=" + calendarId
                + "|v=" + versionId
                + "|l=" + local;
        }

        /// <summary>
        /// Decodes a payload, rejecting it if the recorded provider version differs from
        /// <see cref="Provider"/>'s version.
        /// </summary>
        /// <param name="text">The payload to decode.</param>
        /// <returns>The decode receipt; check <see cref="ZonedDateTimeExchangeDecodeResult.Success"/>.</returns>
        public ZonedDateTimeExchangeDecodeResult Decode(string text) =>
            Decode(text, ZonedDateTimeExchangeVersionPolicy.Reject);

        /// <summary>
        /// Decodes a payload according to the given provider version policy.
        /// </summary>
        /// <remarks>
        /// This method never throws for malformed or inconsistent input; all such conditions are
        /// reported via the returned receipt.
        /// </remarks>
        /// <param name="text">The payload to decode.</param>
        /// <param name="versionPolicy">How to handle a provider version difference.</param>
        /// <returns>The decode receipt; check <see cref="ZonedDateTimeExchangeDecodeResult.Success"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="versionPolicy"/> is not a defined value.</exception>
        public ZonedDateTimeExchangeDecodeResult Decode(string text, ZonedDateTimeExchangeVersionPolicy versionPolicy)
        {
            if (versionPolicy is not (ZonedDateTimeExchangeVersionPolicy.Reject or ZonedDateTimeExchangeVersionPolicy.ReinterpretWithReceipt))
            {
                throw new ArgumentOutOfRangeException(nameof(versionPolicy), versionPolicy, "Unknown version policy");
            }
            if (text is null)
            {
                return Failure("The payload text is null.");
            }
            if (!text.StartsWith(FormatIdentifier + FieldSeparator, StringComparison.Ordinal))
            {
                return Failure($"The payload does not start with the expected format identifier '{FormatIdentifier}'.");
            }

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string field in text.Substring(FormatIdentifier.Length + 1).Split(FieldSeparator))
            {
                int equals = field.IndexOf('=');
                if (equals <= 0)
                {
                    return Failure($"Malformed field '{field}': expected 'key=value'.");
                }
                string key = field.Substring(0, equals);
                if (key is not ("i" or "o" or "z" or "c" or "v" or "l"))
                {
                    return Failure($"Unknown field key '{key}'.");
                }
                if (fields.ContainsKey(key))
                {
                    return Failure($"Duplicate field key '{key}'.");
                }
                fields[key] = field.Substring(equals + 1);
            }
            foreach (string required in new[] { "i", "o", "z", "c", "v", "l" })
            {
                if (!fields.ContainsKey(required))
                {
                    return Failure($"Missing required field '{required}'.");
                }
            }

            // Instant: integer Unix seconds + 9-digit nanosecond-of-second. No floating point is
            // involved at any point, so the nanosecond units digit survives the round trip.
            string instantField = fields["i"];
            int dot = instantField.IndexOf('.');
            if (dot <= 0 || instantField.Length - dot - 1 != 9
                || !long.TryParse(instantField.Substring(0, dot), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long seconds)
                || !int.TryParse(instantField.Substring(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int nanoseconds))
            {
                return Failure($"Field 'i' ('{instantField}') is not '<integer seconds>.<9-digit nanoseconds>'.");
            }
            Instant instant;
            try
            {
                instant = Instant.FromUnixTimeSeconds(seconds).PlusNanoseconds(nanoseconds);
            }
            catch (Exception e) when (e is ArgumentOutOfRangeException || e is OverflowException)
            {
                return Failure($"Field 'i' ('{instantField}') denotes an instant outside the supported range.");
            }

            if (!int.TryParse(fields["o"], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int offsetSeconds))
            {
                return Failure($"Field 'o' ('{fields["o"]}') is not an integer number of seconds.");
            }
            Offset offset;
            try
            {
                offset = Offset.FromSeconds(offsetSeconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Failure($"Field 'o' ({offsetSeconds} seconds) is outside the valid offset range.");
            }

            // The zone must be available from the decoding provider; there is deliberately no
            // fallback to the local system zone.
            string zoneId = fields["z"];
            DateTimeZone? zone;
            try
            {
                zone = Provider.GetZoneOrNull(zoneId);
            }
            catch (DateTimeZoneNotFoundException)
            {
                zone = null;
            }
            if (zone is null)
            {
                return Failure($"Time zone ID '{zoneId}' is not available from provider version '{Provider.VersionId}'; no fallback to the local system zone is applied.");
            }

            string calendarId = fields["c"];
            CalendarSystem calendar;
            try
            {
                calendar = CalendarSystem.ForId(calendarId);
            }
            catch (Exception e) when (e is KeyNotFoundException || e is NotSupportedException)
            {
                return Failure($"Calendar ID '{calendarId}' is not available: {e.Message}");
            }

            // The declared offset must agree with the zone's actual offset at the instant; a
            // disagreement means the payload was produced against different zone data and is an
            // explicit conflict, never a silent re-interpretation.
            Offset actualOffset = zone.GetUtcOffset(instant);
            if (actualOffset != offset)
            {
                return Failure($"Declared offset {offset} conflicts with the actual offset {actualOffset} of zone '{zoneId}' at the encoded instant (provider version '{Provider.VersionId}').");
            }

            string encodedVersion = fields["v"];
            string decodingVersion = Provider.VersionId;
            bool versionMismatch = !string.Equals(encodedVersion, decodingVersion, StringComparison.Ordinal);
            if (versionMismatch && versionPolicy == ZonedDateTimeExchangeVersionPolicy.Reject)
            {
                return ZonedDateTimeExchangeDecodeResult.ForError(
                    $"Provider version mismatch: the payload was encoded with '{encodedVersion}' but the decoding provider is '{decodingVersion}'.",
                    providerVersionMismatch: true,
                    encodedProviderVersionId: encodedVersion,
                    decodingProviderVersionId: decodingVersion);
            }

            var value = new ZonedDateTime(instant, zone, calendar);

            // The local date/time is a derived cross-check only; it is never the source of the instant.
            string expectedLocal = LocalDateTimePattern.ExtendedIso.Format(value.LocalDateTime);
            if (!string.Equals(expectedLocal, fields["l"], StringComparison.Ordinal))
            {
                return Failure($"Field 'l' ('{fields["l"]}') is inconsistent with the encoded instant, offset and calendar; the recomputed local date/time is '{expectedLocal}'.");
            }

            IReadOnlyList<string>? warnings = versionMismatch
                ? new[] { $"Provider version mismatch: the payload was encoded with '{encodedVersion}' and was reinterpreted using '{decodingVersion}'." }
                : null;
            return ZonedDateTimeExchangeDecodeResult.ForValue(value, versionMismatch, encodedVersion, decodingVersion, warnings);
        }

        private static string CheckField(string field, string description)
        {
            if (field.IndexOfAny(new[] { FieldSeparator, '\r', '\n' }) >= 0)
            {
                throw new ArgumentException($"The {description} ('{field}') cannot be represented in a {FormatIdentifier} payload.", description);
            }
            return field;
        }

        private static ZonedDateTimeExchangeDecodeResult Failure(string error) =>
            ZonedDateTimeExchangeDecodeResult.ForError(error);
    }
}
