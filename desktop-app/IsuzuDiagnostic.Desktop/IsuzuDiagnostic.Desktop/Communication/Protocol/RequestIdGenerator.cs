using System.Threading;

namespace IsuzuDiagnostic.Desktop.Communication.Protocol
{
    public sealed class RequestIdGenerator
    {
        private int _currentRequestId;

        public int GetNext()
        {
            int nextRequestId = Interlocked.Increment(ref _currentRequestId ); 

            if (nextRequestId > 0)
            {
                return nextRequestId;
            }

            Interlocked.Exchange(ref _currentRequestId, 1 );

            return 1;
        }
    }
}