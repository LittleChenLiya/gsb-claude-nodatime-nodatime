// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Annotations;
using NodaTime.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NodaTime.Text
{
    /// <summary>
    /// The receipt produced by <see cref="ZonedDateTimeExchangeCodec"/> when decoding a payload.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Decoding never throws for malformed or inconsistent input; instead this result reports
    /// either the decoded value or an explicit error. Any provider version difference between
    /// encoding and decoding is surfaced via <see cref="ProviderVersionMismatch"/>,
    /// <see cref="EncodedProviderVersionId"/>, <see cref="DecodingProviderVersionId"/> and
    /// (when the value was reinterpreted rather than rejected) <see cref="Warnings"/>.
    /// </para>
    /// </remarks>
    /// <threadsafety>This type is immutable and instances may be shared freely between threads.</threadsafety>
    [Immutable]
    public sealed class ZonedDateTimeExchangeDecodeResult
    {
        private static readonly IReadOnlyList<string> NoWarnings =
            new ReadOnlyCollection<string>(new string[0]);

        private readonly ZonedDateTime value;

        /// <summary>
        /// Whether decoding succeeded. When this is <see langword="false"/>, <see cref="Error"/>
        /// describes the failure and <see cref="Value"/> throws.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// The decoded value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Decoding failed.</exception>
        public ZonedDateTime Value => Success
            ? value
            : throw new InvalidOperationException($"Cannot obtain the value of a failed decode result. Error: {Error}");

        /// <summary>
        /// A description of why decoding failed, or <see langword="null"/> on success.
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// Whether the provider version recorded in the payload differs from the version of the
        /// provider used to decode it. This is reported whether the payload was rejected or
        /// reinterpreted; a version change is never silently ignored.
        /// </summary>
        public bool ProviderVersionMismatch { get; }

        /// <summary>
        /// The provider version ID recorded in the payload, if the payload was well-formed enough
        /// to contain one; <see langword="null"/> otherwise.
        /// </summary>
        public string? EncodedProviderVersionId { get; }

        /// <summary>
        /// The version ID of the provider used to decode the payload.
        /// </summary>
        public string? DecodingProviderVersionId { get; }

        /// <summary>
        /// Receipt entries describing non-fatal anomalies accepted during decoding (currently:
        /// reinterpretation across a provider version change). Empty when decoding was clean or
        /// when decoding failed.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }

        private ZonedDateTimeExchangeDecodeResult(ZonedDateTime value, string? error,
            bool providerVersionMismatch, string? encodedProviderVersionId, string? decodingProviderVersionId,
            IReadOnlyList<string> warnings)
        {
            this.value = value;
            Success = error is null;
            Error = error;
            ProviderVersionMismatch = providerVersionMismatch;
            EncodedProviderVersionId = encodedProviderVersionId;
            DecodingProviderVersionId = decodingProviderVersionId;
            Warnings = warnings;
        }

        internal static ZonedDateTimeExchangeDecodeResult ForValue(ZonedDateTime value,
            bool providerVersionMismatch, string encodedProviderVersionId, string decodingProviderVersionId,
            IReadOnlyList<string>? warnings) =>
            new ZonedDateTimeExchangeDecodeResult(value, null, providerVersionMismatch,
                encodedProviderVersionId, decodingProviderVersionId, warnings ?? NoWarnings);

        internal static ZonedDateTimeExchangeDecodeResult ForError(string error,
            bool providerVersionMismatch = false, string? encodedProviderVersionId = null,
            string? decodingProviderVersionId = null) =>
            new ZonedDateTimeExchangeDecodeResult(default, Preconditions.CheckNotNull(error, nameof(error)),
                providerVersionMismatch, encodedProviderVersionId, decodingProviderVersionId, NoWarnings);
    }
}
