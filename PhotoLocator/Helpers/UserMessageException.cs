using System;
using System.Runtime.Serialization;

namespace PhotoLocator.Helpers
{
    [Serializable]
    public class UserMessageException : Exception
    {
        public UserMessageException()
        {
        }

        public UserMessageException(string? message) : base(message)
        {
        }

        public UserMessageException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected UserMessageException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}