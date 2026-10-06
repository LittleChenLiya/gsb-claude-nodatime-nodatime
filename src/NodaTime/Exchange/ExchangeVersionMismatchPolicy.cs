// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

namespace NodaTime.Exchange
{
    /// <summary>
    /// Policy applied by <see cref="ZonedDateTimeExchangeCodec"/> when the time zone provider
    /// version declared in an encoded value differs from the version of the provider used to decode it.
    /// A version mismatch is never silently ignored.
    /// </summary>
    public enum ExchangeVersionMismatchPolicy
    {
        /// <summary>
        /// Reject the value by throwing a <see cref="ZonedDateTimeExchangeException"/>. This is the default.
        /// </summary>
        Reject = 0,

        /// <summary>
        /// Re-interpret the exact instant within the zone as currently known by the decoding provider,
        /// returning a <see cref="ZonedDateTimeExchangeReceipt"/> with a status of
        /// <see cref="ZonedDateTimeExchangeStatus.Reinterpreted"/> that records both versions and both offsets.
        /// </summary>
        Reinterpret = 1,
    }
}
