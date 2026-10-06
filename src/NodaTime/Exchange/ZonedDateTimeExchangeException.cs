// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using System;

namespace NodaTime.Exchange
{
    /// <summary>
    /// Thrown when an exchanged <see cref="ZonedDateTime"/> representation is malformed, internally
    /// inconsistent (for example the declared offset does not match the instant in the declared zone),
    /// refers to an unavailable calendar, or fails a provider version check.
    /// </summary>
    public sealed class ZonedDateTimeExchangeException : Exception
    {
        /// <summary>
        /// Creates an instance with the given message.
        /// </summary>
        /// <param name="message">The message for the exception.</param>
        public ZonedDateTimeExchangeException(string message) : base(message)
        {
        }

        /// <summary>
        /// Creates an instance with the given message and inner exception.
        /// </summary>
        /// <param name="message">The message for the exception.</param>
        /// <param name="innerException">The underlying cause.</param>
        public ZonedDateTimeExchangeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
