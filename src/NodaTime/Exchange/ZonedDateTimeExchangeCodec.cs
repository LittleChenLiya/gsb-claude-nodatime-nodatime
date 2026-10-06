// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Text;
using NodaTime.TimeZones;
using NodaTime.Utility;
using System;
using System.Collections.Generic;
using static System.FormattableString;

namespace NodaTime.Exchange
{
    /// <summary>
    /// An explicit exchange codec for <see cref="ZonedDateTime"/> values, designed for data interchange
    /// between systems that may not share the same time zone data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The wire format is a single line of pipe-delimited, key-tagged fields with a versioned header:
    /// <c>NZDT/1|i=instant|o=offset|z=zone-id|c=calendar-id|v=provider-version</c>.
    /// The instant is encoded in extended ISO-8601 form with up to nanosecond resolution and is the
    /// authoritative moment in time; the local time is never used as the sole representation of an instant.
    /// The offset, zone ID, calendar ID and provider version ID travel alongside as explicit cross-checks.
    /// </para>
    /// <para>Decoding is strict:</para>
    /// <list type="bullet">
    /// <item><description>Unknown zone IDs are rejected; there is never a fallback to the system local time zone.</description></item>
    /// <item><description>Unknown calendar IDs are rejected.</description></item>
    /// <item><description>The declared offset must agree with the instant in the declared zone under the
    /// decoding provider's data; a disagreement is an explicit conflict, not a silent adjustment.</description></item>
    /// <item><description>A provider version mismatch is handled according to
    /// <see cref="VersionMismatchPolicy"/>: either rejected, or re-interpreted with a receipt recording
    /// both versions and both offsets. It is never silently ignored.</description></item>
    /// </list>
    /// <para>
    /// Nanosecond precision is preserved exactly: instants and offsets are formatted and parsed as
    /// integers and text patterns only, never via floating point.
    /// </para>
    /// <para>This type is immutable and thread-safe.</para>
    /// </remarks>
    public sealed class ZonedDateTimeExchangeCodec
    {
        /// <summary>
        /// The format header (including format version) that every encoded representation starts with.
        /// </summary>
        public const string FormatHeader = "NZDT/1";

        private const char FieldSeparator = '|';
        private const char KeyValueSeparator = '=';

        // Nanosecond-precision, culture-invariant patterns. Neither involves any floating-point conversion.
        private static readonly InstantPattern InstantPattern = InstantPattern.ExtendedIso;
        private static readonly OffsetPattern OffsetPattern = OffsetPattern.GeneralInvariant;

        /// <summary>
        /// The time zone provider used to resolve zone IDs when decoding, and whose
        /// <see cref="IDateTimeZoneProvider.VersionId"/> is stamped into encoded representations.
        /// </summary>
        public IDateTimeZoneProvider Provider { get; }

        /// <summary>
        /// The policy applied when an encoded representation declares a provider version that differs
        /// from the version of <see cref="Provider"/>.
        /// </summary>
        public ExchangeVersionMismatchPolicy VersionMismatchPolicy { get; }

        /// <summary>
        /// Creates a codec using the given time zone provider.
        /// </summary>
        /// <param name="provider">The provider used to resolve zone IDs and supply the provider version ID.
        /// Must not be null.</param>
        /// <param name="versionMismatchPolicy">The policy for provider version mismatches when decoding.
        /// The default is <see cref="ExchangeVersionMismatchPolicy.Reject"/>.</param>
        public ZonedDateTimeExchangeCodec(
            IDateTimeZoneProvider provider,
            ExchangeVersionMismatchPolicy versionMismatchPolicy = ExchangeVersionMismatchPolicy.Reject)
        {
            Provider = Preconditions.CheckNotNull(provider, nameof(provider));
            VersionMismatchPolicy = versionMismatchPolicy;
        }

        /// <summary>
        /// Encodes a <see cref="ZonedDateTime"/> into its exchange representation, preserving the exact
        /// instant, the zone ID, the calendar ID and the encoding provider's version ID.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <returns>The exchange representation.</returns>
        /// <exception cref="ArgumentException">A zone ID, calendar ID or provider version ID contains
        /// a character that cannot be represented in the exchange format.</exception>
        public string Encode(ZonedDateTime value)
        {
            string zoneId = value.Zone.Id;
            string calendarId = value.Calendar.Id;
            string versionId = Provider.VersionId;
            ValidateField(zoneId, nameof(value));
            ValidateField(calendarId, nameof(value));
            ValidateField(versionId, nameof(versionId));
            return string.Join(FieldSeparator.ToString(),
                FormatHeader,
                "i=" + InstantPattern.Format(value.ToInstant()),
                "o=" + OffsetPattern.Format(value.Offset),
                "z=" + zoneId,
                "c=" + calendarId,
                "v=" + versionId);
        }

        /// <summary>
        /// Decodes an exchange representation, validating the zone, calendar, offset consistency and
        /// provider version as described in the type remarks.
        /// </summary>
        /// <param name="text">The exchange representation to decode. Must not be null.</param>
        /// <returns>A receipt containing the decoded value and an explicit record of how the declared
        /// provider version and offset were treated.</returns>
        /// <exception cref="ZonedDateTimeExchangeException">The representation is malformed, the declared
        /// offset conflicts with the instant and zone, the calendar is unavailable, or the provider
        /// version differs and <see cref="VersionMismatchPolicy"/> is
        /// <see cref="ExchangeVersionMismatchPolicy.Reject"/>.</exception>
        /// <exception cref="DateTimeZoneNotFoundException">The declared zone ID is unknown to
        /// <see cref="Provider"/>. No fallback to the system local time zone is ever attempted.</exception>
        public ZonedDateTimeExchangeReceipt Decode(string text)
        {
            Preconditions.CheckNotNull(text, nameof(text));
            var fields = SplitFields(text);

            Instant instant = ParseField(fields, 'i', InstantPattern, "instant");
            Offset declaredOffset = ParseField(fields, 'o', OffsetPattern, "offset");
            string zoneId = fields['z'];
            string calendarId = fields['c'];
            string declaredVersion = fields['v'];

            // Unknown zone: fail explicitly. Never fall back to the system local zone.
            DateTimeZone zone = Provider.GetZoneOrNull(zoneId)
                ?? throw new DateTimeZoneNotFoundException(
                    Invariant($"Exchange representation declares zone ID '{zoneId}', which is unknown to provider version '{Provider.VersionId}'."));

            CalendarSystem calendar;
            try
            {
                calendar = CalendarSystem.ForId(calendarId);
            }
            catch (KeyNotFoundException e)
            {
                throw new ZonedDateTimeExchangeException(
                    Invariant($"Exchange representation declares calendar ID '{calendarId}', which is not available."), e);
            }

            bool versionMismatch = declaredVersion != Provider.VersionId;
            if (versionMismatch && VersionMismatchPolicy == ExchangeVersionMismatchPolicy.Reject)
            {
                throw new ZonedDateTimeExchangeException(
                    Invariant($"Exchange representation declares provider version '{declaredVersion}', but the decoding provider is version '{Provider.VersionId}'. ") +
                    "Decode with ExchangeVersionMismatchPolicy.Reinterpret to re-interpret the instant under the current data and receive a receipt.");
            }

            Offset actualOffset = zone.GetUtcOffset(instant);
            if (actualOffset != declaredOffset && !versionMismatch)
            {
                throw new ZonedDateTimeExchangeException(
                    Invariant($"Exchange representation declares offset {OffsetPattern.Format(declaredOffset)} for instant {InstantPattern.Format(instant)} in zone '{zoneId}', ") +
                    Invariant($"but provider version '{Provider.VersionId}' gives {OffsetPattern.Format(actualOffset)}. ") +
                    "The data is corrupt or was produced by a different provider version.");
            }

            var value = new ZonedDateTime(instant, zone, calendar);
            return new ZonedDateTimeExchangeReceipt(
                value,
                versionMismatch ? ZonedDateTimeExchangeStatus.Reinterpreted : ZonedDateTimeExchangeStatus.Exact,
                declaredVersion,
                Provider.VersionId,
                declaredOffset,
                actualOffset);
        }

        /// <summary>
        /// Decodes an exchange representation and returns just the value. The receipt is discarded;
        /// use <see cref="Decode"/> when the interpretation status matters.
        /// </summary>
        /// <param name="text">The exchange representation to decode. Must not be null.</param>
        /// <returns>The decoded value.</returns>
        public ZonedDateTime DecodeValue(string text) => Decode(text).Value;

        private static void ValidateField(string value, string paramName)
        {
            if (value.Length == 0 ||
                value.IndexOf(FieldSeparator) >= 0 ||
                value.IndexOf(KeyValueSeparator) >= 0 ||
                value.IndexOf('\r') >= 0 ||
                value.IndexOf('\n') >= 0)
            {
                throw new ArgumentException(
                    Invariant($"Value '{value}' cannot be represented in the {FormatHeader} exchange format: ") +
                    "fields must be non-empty and must not contain '|', '=' or line breaks.", paramName);
            }
        }

        private static Dictionary<char, string> SplitFields(string text)
        {
            string[] parts = text.Split(FieldSeparator);
            if (parts.Length != 6 || parts[0] != FormatHeader)
            {
                throw new ZonedDateTimeExchangeException(
                    Invariant($"Exchange representation must start with '{FormatHeader}' followed by exactly 5 fields (i, o, z, c, v)."));
            }
            var fields = new Dictionary<char, string>();
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length < 3 || part[1] != KeyValueSeparator || "iozcv".IndexOf(part[0]) < 0)
                {
                    throw new ZonedDateTimeExchangeException(
                        Invariant($"Malformed field '{part}' in exchange representation; expected one of i=, o=, z=, c=, v= with a non-empty value."));
                }
                if (fields.ContainsKey(part[0]))
                {
                    throw new ZonedDateTimeExchangeException(
                        Invariant($"Duplicate field '{part[0]}' in exchange representation."));
                }
                fields.Add(part[0], part.Substring(2));
            }
            return fields;
        }

        private static T ParseField<T>(Dictionary<char, string> fields, char key, IPattern<T> pattern, string description)
        {
            var result = pattern.Parse(fields[key]);
            if (!result.Success)
            {
                throw new ZonedDateTimeExchangeException(
                    Invariant($"Cannot parse {description} field '{fields[key]}' in exchange representation."), result.Exception);
            }
            return result.Value;
        }
    }
}
