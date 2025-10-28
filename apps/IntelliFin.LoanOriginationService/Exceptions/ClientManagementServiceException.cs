using System;

namespace IntelliFin.LoanOriginationService.Exceptions;

/// <summary>
/// Infrastructure exception thrown when the Client Management Service is unreachable or returns errors.
/// </summary>
public class ClientManagementServiceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientManagementServiceException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ClientManagementServiceException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
