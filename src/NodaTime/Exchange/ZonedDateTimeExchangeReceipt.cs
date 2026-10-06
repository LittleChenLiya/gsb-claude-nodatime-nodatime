// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

namespace NodaTime.Exchange
{
    /// <summary>
    /// The result of decoding an exchanged <see cref="ZonedDateTime"/>: the decoded value plus an
    /// explicit record of how the declared provider version and offset were treated, so that a
    /// version change is surfaced as a receipt rather than silently swallowed.
    /// </summary>
    public sealed class ZonedDateTimeExchangeReceipt
    {
        /// <summary>
        /// The decoded value. The instant is always exactly the encoded instant.
        /// </summary>
        public ZonedDateTime Value { get; }

        /// <summary>
        /// Whether the value was reproduced exactly or re-interpreted under a different provider version.
        /// </summary>
        public ZonedDateTimeExchangeStatus Status { get; }

        /// <summary>
        /// The provider version ID declared in the encoded representation.
        /// </summary>
        public string DeclaredProviderVersion { get; }

        /// <summary>
        /// The version ID of the provider that performed the decode.
        /// </summary>
        public string ActualProviderVersion { get; }

        /// <summary>
        /// The UTC offset declared in the encoded representation.
        /// </summary>
        public Offset DeclaredOffset { get; }

        /// <summary>
        /// The UTC offset computed from the decoded instant and zone using the decoding provider's data.
        /// </summary>
        public Offset ActualOffset { get; }

        internal ZonedDateTimeExchangeReceipt(
            ZonedDateTime value,
            ZonedDateTimeExchangeStatus status,
            string declaredProviderVersion,
            string actualProviderVersion,
            Offset declaredOffset,
            Offset actualOffset)
        {
            Value = value;
            Status = status;
            DeclaredProviderVersion = declaredProviderVersion;
            ActualProviderVersion = actualProviderVersion;
            DeclaredOffset = declaredOffset;
            ActualOffset = actualOffset;
        }
    }
}
