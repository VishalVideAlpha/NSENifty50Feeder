using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NSENifty50Feeder.Helper
{
    public interface IListener
    {
        Task ListenToFirestore(string collectionName, TaskCompletionSource<bool> tcs);
    }
}
