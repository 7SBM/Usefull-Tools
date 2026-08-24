using System;

namespace BisDll.Model.ODOL;

public class UnknownAnimTypeException : Exception
{
    public UnknownAnimTypeException(string message) : base(message) { }
}
