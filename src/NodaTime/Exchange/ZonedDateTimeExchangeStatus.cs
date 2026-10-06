// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

namespace NodaTime.Exchange
{
    /// <summary>
    /// Outcome classification of decoding an exchanged <see cref="ZonedDateTime"/>.
    /// </summary>
    public enum ZonedDateTimeExchangeStatus
    {
        /// <summary>
        /// The provider version matched and the declared offset agrees with the decoded instant and zone;
        /// the value was reproduced exactly as encoded.
        /// </summary>
        Exact = 0,

        /// <summary>
        /// The provider version differed and the value was re-interpreted under the decoding provider's data.
        /// The instant is preserved exactly, but the offset (and therefore the local time) may have changed;
        /// inspect <see cref="ZonedDateTimeExchangeReceipt.DeclaredOffset"/> and
        /// <see cref="ZonedDateTimeExchangeReceipt.ActualOffset"/>.
        /// </summary>
        Reinterpreted = 1,
    }
}
