// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

namespace NodaTime.Text
{
    /// <summary>
    /// Policy applied by <see cref="ZonedDateTimeExchangeCodec.Decode(string, ZonedDateTimeExchangeVersionPolicy)"/>
    /// when the provider version recorded in an encoded payload differs from the version of the
    /// provider used to decode it.
    /// </summary>
    public enum ZonedDateTimeExchangeVersionPolicy
    {
        /// <summary>
        /// Reject the payload: decoding fails and the mismatch is reported in
        /// <see cref="ZonedDateTimeExchangeDecodeResult.Error"/>. This is the default.
        /// </summary>
        Reject = 0,

        /// <summary>
        /// Reinterpret the payload using the decoding provider's data. The mismatch is recorded
        /// on the returned receipt (<see cref="ZonedDateTimeExchangeDecodeResult.Warnings"/> and
        /// <see cref="ZonedDateTimeExchangeDecodeResult.ProviderVersionMismatch"/>); it is never
        /// silently swallowed.
        /// </summary>
        ReinterpretWithReceipt = 1,
    }
}
