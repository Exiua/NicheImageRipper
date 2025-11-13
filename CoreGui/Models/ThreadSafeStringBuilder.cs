using System.Text;
using System.Threading;

namespace CoreGui.Models;

public class ThreadSafeStringBuilder
{
    private readonly StringBuilder _core = new();
    private readonly ReaderWriterLockSlim _sync = new();

    public int Length
    {
        get
        {
            try
            {
                _sync.EnterReadLock();
                return _core.Length;
            }
            finally
            {
                _sync.ExitReadLock();
            }
        }
    }

    public char this[int index]
    {
        get
        {
            try
            {
                _sync.EnterReadLock();
                return _core[index];
            }
            finally
            {
                _sync.ExitReadLock();
            }
        }
        set
        {
            try
            {
                _sync.EnterWriteLock();
                _core[index] = value;
            }
            finally
            {
                _sync.ExitWriteLock();
            }
        }
    }

    public void Append(string str)
    {
        try
        {
            _sync.EnterWriteLock();
            _core.Append(str);
        }
        finally
        {
            _sync.ExitWriteLock();
        }
    }

    public ThreadSafeStringBuilder Remove(int startIndex, int length)
    {
        try
        {
            _sync.EnterWriteLock();
            _core.Remove(startIndex, length);
            return this;
        }
        finally
        {
            _sync.ExitWriteLock();
        }
    }

    public override string ToString()
    {
        try
        {
            _sync.EnterReadLock();
            return _core.ToString();
        }
        finally
        {
            _sync.ExitReadLock();
        }
    }
}