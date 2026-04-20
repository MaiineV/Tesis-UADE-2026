using System;

namespace Rollgeon.Run
{
    public interface IRunController : IDisposable
    {
        bool IsRunActive { get; }
    }
}
